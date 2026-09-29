using System.Security.Cryptography;
using MonoHome.Core.Sync;

namespace MonoHome.Verifier;

public sealed class SyncFakeRemoteSaveProvider : IRemoteSaveProvider
{
    readonly Dictionary<(string SaveKey, string LineageId), List<(RemoteSaveVersion Version, byte[] Content)>> versions = [];
    int commitNumber;

    public int UploadCount { get; private set; }

    public void Seed(string saveKey, string lineageId, string commitSha, byte[] content, string? parentCommitSha = null)
    {
        var version = new RemoteSaveVersion(
            saveKey,
            lineageId,
            commitSha,
            Hash(content),
            $"blob-{commitSha}",
            DateTimeOffset.UtcNow,
            "fake-device",
            "seed",
            parentCommitSha);
        GetVersions(saveKey, lineageId).Add((version, content.ToArray()));
    }

    public Task<RepositoryBinding> BindRepositoryAsync(string owner, string repository, CancellationToken cancellationToken)
        => Task.FromResult(new RepositoryBinding("fake", owner, repository, "main", DateTimeOffset.UtcNow, 1));

    public Task<RemoteSaveVersion?> GetLatestAsync(string saveKey, string lineageId, CancellationToken cancellationToken)
        => Task.FromResult<RemoteSaveVersion?>(GetVersions(saveKey, lineageId).LastOrDefault().Version);

    public Task<IReadOnlyList<RemoteSaveVersion>> ListVersionsAsync(string saveKey, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RemoteSaveVersion>>(versions
            .Where(pair => pair.Key.SaveKey == saveKey)
            .SelectMany(pair => pair.Value)
            .Select(item => item.Version)
            .OrderByDescending(version => version.ModifiedAt)
            .ToArray());

    public Task<byte[]> DownloadAsync(RemoteSaveVersion version, CancellationToken cancellationToken)
    {
        var match = GetVersions(version.SaveKey, version.LineageId).FirstOrDefault(item => item.Version.CommitSha == version.CommitSha);
        return Task.FromResult(match.Content?.ToArray() ?? throw new InvalidDataException("Fake remote version is missing."));
    }

    public Task<RemoteSaveVersion> UploadAsync(
        string saveKey,
        string lineageId,
        byte[] content,
        string? expectedCommitSha,
        string message,
        CancellationToken cancellationToken)
    {
        var latest = GetVersions(saveKey, lineageId).LastOrDefault().Version;
        if (!string.Equals(latest?.CommitSha, expectedCommitSha, StringComparison.Ordinal))
            throw new InvalidOperationException("Fake remote head changed.");

        UploadCount++;
        var commitSha = $"fake-{++commitNumber}";
        var version = new RemoteSaveVersion(
            saveKey,
            lineageId,
            commitSha,
            Hash(content),
            $"blob-{commitSha}",
            DateTimeOffset.UtcNow,
            "fake-device",
            message,
            expectedCommitSha);
        GetVersions(saveKey, lineageId).Add((version, content.ToArray()));
        return Task.FromResult(version);
    }

    public Task<string> CreateLineageAsync(string saveKey, string fromCommitSha, CancellationToken cancellationToken)
    {
        var source = versions
            .Where(pair => pair.Key.SaveKey == saveKey)
            .SelectMany(pair => pair.Value)
            .FirstOrDefault(item => item.Version.CommitSha == fromCommitSha);
        if (source.Version is null)
            throw new InvalidDataException("Fake source commit is missing.");

        var lineageId = $"lineage-{versions.Keys.Count + 1}";
        Seed(saveKey, lineageId, fromCommitSha, source.Content, fromCommitSha);
        return Task.FromResult(lineageId);
    }

    List<(RemoteSaveVersion Version, byte[] Content)> GetVersions(string saveKey, string lineageId)
    {
        var key = (saveKey, lineageId);
        if (!versions.TryGetValue(key, out var result))
            versions[key] = result = [];
        return result;
    }

    static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
}
