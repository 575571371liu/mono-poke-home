using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MonoHome.Core.Sync.GitHub;

public sealed record GitHubRepositoryInfo(
    string Owner,
    string Name,
    bool IsPrivate,
    string DefaultBranch,
    bool CanPush);

public sealed record GitHubFileContent(string Path, string BlobSha, byte[] Content);

public sealed record GitHubCommitInfo(
    string Sha,
    DateTimeOffset ModifiedAt,
    string Message,
    string? ParentSha);

public sealed record GitHubPutContentResult(
    string FileSha,
    string CommitSha,
    DateTimeOffset ModifiedAt);

public sealed record GitHubReference(string Ref, string Sha);

public sealed class GitHubApiException(HttpStatusCode statusCode, string message, TimeSpan? retryAfter = null)
    : HttpRequestException(message, null, statusCode)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public string UserMessage => StatusCode switch
    {
        HttpStatusCode.Unauthorized => "GitHub 授权已失效，请重新连接账号。",
        HttpStatusCode.Forbidden => "GitHub 拒绝了仓库访问，请检查仓库权限或请求频率。",
        HttpStatusCode.NotFound => "GitHub 仓库或存档文件不存在。",
        HttpStatusCode.Conflict => "远端存档已被其他设备修改，请先刷新版本。",
        (HttpStatusCode)429 => "GitHub 请求过于频繁，请稍后重试。",
        _ => $"GitHub 请求失败（{(int?)StatusCode}）。",
    };
}

public sealed class GitHubApiClient
{
    const string ApiVersion = "2022-11-28";
    readonly HttpClient client;
    readonly Func<CancellationToken, Task<string>> accessTokenProvider;

    public GitHubApiClient(HttpClient client, Func<CancellationToken, Task<string>> accessTokenProvider)
    {
        this.client = client;
        this.accessTokenProvider = accessTokenProvider;
        client.BaseAddress ??= new Uri("https://api.github.com/");
    }

    public async Task<GitHubRepositoryInfo> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken)
    {
        using var json = await SendAsync(HttpMethod.Get, $"repos/{Segment(owner)}/{Segment(repository)}", null, cancellationToken);
        var root = json.RootElement;
        var permissions = root.TryGetProperty("permissions", out var permissionElement) ? permissionElement : default;
        var canPush = permissions.ValueKind == JsonValueKind.Object &&
            ((permissions.TryGetProperty("push", out var push) && push.GetBoolean()) ||
             (permissions.TryGetProperty("admin", out var admin) && admin.GetBoolean()) ||
             (permissions.TryGetProperty("maintain", out var maintain) && maintain.GetBoolean()));
        return new GitHubRepositoryInfo(
            owner,
            repository,
            root.GetProperty("private").GetBoolean(),
            root.GetProperty("default_branch").GetString() ?? "main",
            canPush);
    }

    public async Task<GitHubFileContent> GetFileAsync(
        string owner,
        string repository,
        string path,
        string? reference,
        CancellationToken cancellationToken)
    {
        var endpoint = $"repos/{Segment(owner)}/{Segment(repository)}/contents/{Path(path)}";
        if (!string.IsNullOrWhiteSpace(reference))
            endpoint += $"?ref={Uri.EscapeDataString(reference)}";

        using var json = await SendAsync(HttpMethod.Get, endpoint, null, cancellationToken);
        var root = json.RootElement;
        var encoding = root.TryGetProperty("encoding", out var encodingElement) ? encodingElement.GetString() : null;
        var content = root.TryGetProperty("content", out var contentElement) ? contentElement.GetString() : null;
        if (!string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase) || content is null)
            throw new InvalidDataException("GitHub contents response did not include base64 file content.");
        return new GitHubFileContent(
            root.GetProperty("path").GetString() ?? path,
            root.GetProperty("sha").GetString() ?? throw new InvalidDataException("GitHub contents response did not include a blob SHA."),
            Convert.FromBase64String(content.Replace("\n", string.Empty, StringComparison.Ordinal)));
    }

    public async Task<GitHubCommitInfo?> GetLatestCommitAsync(
        string owner,
        string repository,
        string path,
        string branch,
        CancellationToken cancellationToken)
    {
        var endpoint = $"repos/{Segment(owner)}/{Segment(repository)}/commits?path={Uri.EscapeDataString(path)}&sha={Uri.EscapeDataString(branch)}&per_page=1";
        using var json = await SendAsync(HttpMethod.Get, endpoint, null, cancellationToken);
        var item = json.RootElement.EnumerateArray().FirstOrDefault();
        return item.ValueKind == JsonValueKind.Undefined ? null : ParseCommit(item);
    }

    public async Task<IReadOnlyList<GitHubCommitInfo>> ListCommitsAsync(
        string owner,
        string repository,
        string path,
        string branch,
        CancellationToken cancellationToken)
    {
        var endpoint = $"repos/{Segment(owner)}/{Segment(repository)}/commits?path={Uri.EscapeDataString(path)}&sha={Uri.EscapeDataString(branch)}&per_page=100";
        using var json = await SendAsync(HttpMethod.Get, endpoint, null, cancellationToken);
        return json.RootElement.EnumerateArray().Select(ParseCommit).ToArray();
    }

    public async Task<GitHubPutContentResult> PutFileAsync(
        string owner,
        string repository,
        string path,
        string branch,
        byte[] content,
        string message,
        string? expectedBlobSha,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["message"] = message,
            ["content"] = Convert.ToBase64String(content),
            ["branch"] = branch,
        };
        if (expectedBlobSha is not null)
            payload["sha"] = expectedBlobSha;

        using var json = await SendAsync(
            HttpMethod.Put,
            $"repos/{Segment(owner)}/{Segment(repository)}/contents/{Path(path)}",
            JsonContent.Create(payload),
            cancellationToken);
        var root = json.RootElement;
        return new GitHubPutContentResult(
            root.GetProperty("content").GetProperty("sha").GetString() ?? throw new InvalidDataException("GitHub update response did not include a file SHA."),
            root.GetProperty("commit").GetProperty("sha").GetString() ?? throw new InvalidDataException("GitHub update response did not include a commit SHA."),
            DateTimeOffset.UtcNow);
    }

    public async Task<GitHubReference> CreateReferenceAsync(
        string owner,
        string repository,
        string branch,
        string commitSha,
        CancellationToken cancellationToken)
    {
        using var json = await SendAsync(
            HttpMethod.Post,
            $"repos/{Segment(owner)}/{Segment(repository)}/git/refs",
            JsonContent.Create(new { @ref = $"refs/heads/{branch}", sha = commitSha }),
            cancellationToken);
        var root = json.RootElement;
        return new GitHubReference(
            root.GetProperty("ref").GetString() ?? throw new InvalidDataException("GitHub ref response did not include a ref."),
            root.GetProperty("object").GetProperty("sha").GetString() ?? throw new InvalidDataException("GitHub ref response did not include a commit SHA."));
    }

    async Task<JsonDocument> SendAsync(HttpMethod method, string endpoint, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, endpoint) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        request.Headers.UserAgent.ParseAdd("MonoHome/1.1");
        var token = await accessTokenProvider(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("GitHub access token is missing.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = TryGetErrorMessage(body) ?? response.ReasonPhrase ?? "GitHub request failed.";
            var retryAfter = response.Headers.RetryAfter?.Delta;
            throw new GitHubApiException(response.StatusCode, message, retryAfter);
        }
        return JsonDocument.Parse(body);
    }

    static GitHubCommitInfo ParseCommit(JsonElement element)
    {
        var commit = element.GetProperty("commit");
        var committer = commit.TryGetProperty("committer", out var committerElement) ? committerElement : commit.GetProperty("author");
        var date = DateTimeOffset.Parse(committer.GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var parent = element.TryGetProperty("parents", out var parents) && parents.GetArrayLength() > 0
            ? parents[0].GetProperty("sha").GetString()
            : null;
        return new GitHubCommitInfo(
            element.GetProperty("sha").GetString() ?? throw new InvalidDataException("GitHub commit response did not include a SHA."),
            date,
            commit.GetProperty("message").GetString() ?? string.Empty,
            parent);
    }

    static string? TryGetErrorMessage(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string Segment(string value) => Uri.EscapeDataString(value);

    static string Path(string value) => string.Join('/', value.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Segment));
}
