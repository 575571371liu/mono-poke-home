using System.Security.Cryptography;
using System.Text.Json;

namespace MonoHome.Core.Sync.GitHub;

public sealed class GitHubRemoteSaveProvider : IRemoteSaveProvider
{
    const string ManifestPath = ".mono-home/manifest.json";
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    static readonly IReadOnlyDictionary<string, string> DefaultPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["emerald"] = "saves/emerald/emerald.srm",
        ["heartgold"] = "saves/heartgold/heartgold.sav",
        ["soulsilver"] = "saves/soulsilver/soulsilver.sav",
    };

    readonly GitHubApiClient api;
    readonly RepositoryBinding binding;
    readonly Dictionary<string, string> paths;

    public GitHubRemoteSaveProvider(
        GitHubApiClient api,
        RepositoryBinding binding,
        IReadOnlyDictionary<string, string>? paths = null)
    {
        this.api = api;
        this.binding = binding;
        this.paths = new Dictionary<string, string>(paths ?? DefaultPaths, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<RepositoryBinding> BindRepositoryAsync(string owner, string repository, CancellationToken cancellationToken)
    {
        var info = await api.GetRepositoryAsync(owner, repository, cancellationToken);
        if (!info.IsPrivate)
            throw new InvalidOperationException("存档同步只允许绑定私有 GitHub 仓库。");
        if (!info.CanPush)
            throw new InvalidOperationException("当前 GitHub 账号没有该仓库的 Contents 写权限。");
        return new RepositoryBinding("github", info.Owner, info.Name, info.DefaultBranch, DateTimeOffset.UtcNow, 1);
    }

    public async Task<SaveRepositoryManifest> GetManifestAsync(
        string lineageId,
        bool initializeIfMissing,
        CancellationToken cancellationToken)
    {
        try
        {
            var file = await api.GetFileAsync(binding.Owner, binding.Repository, ManifestPath, lineageId, cancellationToken);
            var manifest = JsonSerializer.Deserialize<SaveRepositoryManifest>(file.Content, Json)
                ?? throw new InvalidDataException("MONO / HOME repository manifest is empty.");
            manifest.Validate();
            ApplyManifest(manifest);
            return manifest;
        }
        catch (GitHubApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound && initializeIfMissing)
        {
            var manifest = SaveRepositoryManifest.CreateDefault();
            await api.PutFileAsync(
                binding.Owner,
                binding.Repository,
                ManifestPath,
                lineageId,
                JsonSerializer.SerializeToUtf8Bytes(manifest, Json),
                "Initialize MONO / HOME save manifest",
                null,
                cancellationToken);
            ApplyManifest(manifest);
            return manifest;
        }
    }

    public async Task<RemoteSaveVersion?> GetLatestAsync(string saveKey, string lineageId, CancellationToken cancellationToken)
    {
        var path = GetPath(saveKey);
        GitHubFileContent file;
        try
        {
            file = await api.GetFileAsync(binding.Owner, binding.Repository, path, lineageId, cancellationToken);
        }
        catch (GitHubApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var commit = await api.GetLatestCommitAsync(binding.Owner, binding.Repository, path, lineageId, cancellationToken)
            ?? throw new InvalidDataException("GitHub 存档文件存在，但找不到对应 commit。");
        return ToVersion(saveKey, lineageId, file, commit);
    }

    public async Task<IReadOnlyList<RemoteSaveVersion>> ListVersionsAsync(string saveKey, CancellationToken cancellationToken)
        => await ListVersionsAsync(saveKey, binding.DefaultBranch, cancellationToken);

    public async Task<IReadOnlyList<RemoteSaveVersion>> ListVersionsAsync(
        string saveKey,
        string lineageId,
        CancellationToken cancellationToken)
    {
        var commits = await api.ListCommitsAsync(binding.Owner, binding.Repository, GetPath(saveKey), lineageId, cancellationToken);
        return commits.Select(commit => new RemoteSaveVersion(
            saveKey,
            lineageId,
            commit.Sha,
            null,
            null,
            commit.ModifiedAt,
            "github",
            commit.Message,
            commit.ParentSha)).ToArray();
    }

    public async Task<IReadOnlyList<RemoteSaveVersion>> ListAllVersionsAsync(
        string saveKey,
        string activeLineageId,
        CancellationToken cancellationToken)
    {
        var branches = await api.ListBranchesAsync(binding.Owner, binding.Repository, cancellationToken);
        var prefix = $"save/{saveKey}/";
        var lineages = branches
            .Select(branch => branch.Name)
            .Where(name => string.Equals(name, binding.DefaultBranch, StringComparison.Ordinal) ||
                string.Equals(name, activeLineageId, StringComparison.Ordinal) ||
                name.StartsWith(prefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var versions = new List<RemoteSaveVersion>();
        foreach (var lineageId in lineages)
            versions.AddRange(await ListVersionsAsync(saveKey, lineageId, cancellationToken));
        return versions
            .GroupBy(version => $"{version.LineageId}:{version.CommitSha}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(version => version.ModifiedAt)
            .ToArray();
    }

    public async Task<byte[]> DownloadAsync(RemoteSaveVersion version, CancellationToken cancellationToken)
    {
        var file = await api.GetFileAsync(binding.Owner, binding.Repository, GetPath(version.SaveKey), version.CommitSha, cancellationToken);
        return file.Content;
    }

    public async Task<RemoteSaveVersion> UploadAsync(
        string saveKey,
        string lineageId,
        byte[] content,
        string? expectedCommitSha,
        string message,
        CancellationToken cancellationToken)
    {
        var path = GetPath(saveKey);
        GitHubFileContent? currentFile = null;
        GitHubCommitInfo? currentCommit = null;
        try
        {
            currentFile = await api.GetFileAsync(binding.Owner, binding.Repository, path, lineageId, cancellationToken);
            currentCommit = await api.GetLatestCommitAsync(binding.Owner, binding.Repository, path, lineageId, cancellationToken);
        }
        catch (GitHubApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound && expectedCommitSha is null)
        {
            // The first upload creates the save file.
        }

        if (!string.Equals(currentCommit?.Sha, expectedCommitSha, StringComparison.Ordinal))
            throw new GitHubApiException(System.Net.HttpStatusCode.Conflict, "The remote save head changed.");

        var result = await api.PutFileAsync(
            binding.Owner,
            binding.Repository,
            path,
            lineageId,
            content,
            message,
            currentFile?.BlobSha,
            cancellationToken);
        return new RemoteSaveVersion(
            saveKey,
            lineageId,
            result.CommitSha,
            Convert.ToHexString(SHA256.HashData(content)),
            result.FileSha,
            result.ModifiedAt,
            "github",
            message,
            expectedCommitSha);
    }

    public Task<string> CreateLineageAsync(string saveKey, string fromCommitSha, CancellationToken cancellationToken)
    {
        var lineageId = $"save/{saveKey}/{Guid.NewGuid():N}";
        return CreateLineageCoreAsync(saveKey, lineageId, fromCommitSha, cancellationToken);
    }

    async Task<string> CreateLineageCoreAsync(string saveKey, string lineageId, string fromCommitSha, CancellationToken cancellationToken)
    {
        await api.CreateReferenceAsync(binding.Owner, binding.Repository, lineageId, fromCommitSha, cancellationToken);
        return lineageId;
    }

    void ApplyManifest(SaveRepositoryManifest manifest)
    {
        foreach (var (saveKey, entry) in manifest.Saves)
            paths[saveKey] = entry.Path;
    }

    string GetPath(string saveKey) => paths.TryGetValue(saveKey, out var path)
        ? path
        : throw new ArgumentException($"Unsupported save key: {saveKey}", nameof(saveKey));

    static RemoteSaveVersion ToVersion(string saveKey, string lineageId, GitHubFileContent file, GitHubCommitInfo commit)
        => new(
            saveKey,
            lineageId,
            commit.Sha,
            Convert.ToHexString(SHA256.HashData(file.Content)),
            file.BlobSha,
            commit.ModifiedAt,
            "github",
            commit.Message,
            commit.ParentSha);
}
