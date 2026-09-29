using System.Text.Json;

namespace MonoHome.Core.Sync.GitHub;

public sealed record GitHubDeviceCode(
    string DeviceCode,
    string UserCode,
    Uri VerificationUri,
    TimeSpan ExpiresIn,
    TimeSpan Interval);

public sealed record GitHubAccessToken(
    string AccessToken,
    string TokenType,
    DateTimeOffset? ExpiresAt,
    string? RefreshToken);

public sealed class GitHubOAuthException(string error, string? description = null) : InvalidOperationException(description is null ? error : $"{error}: {description}")
{
    public string Error { get; } = error;
}

public sealed class GitHubDeviceFlowClient
{
    readonly HttpClient client;
    readonly string clientId;

    public GitHubDeviceFlowClient(HttpClient client, string clientId)
    {
        this.client = client;
        this.clientId = clientId;
        client.BaseAddress ??= new Uri("https://github.com/");
    }

    public async Task<GitHubDeviceCode> RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        EnsureClientId();
        using var response = await client.PostAsync(
            "login/device/code",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = clientId }),
            cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        ThrowOAuthError(json);
        var root = json.RootElement;
        return new GitHubDeviceCode(
            root.GetProperty("device_code").GetString() ?? throw new InvalidDataException("GitHub device response has no device code."),
            root.GetProperty("user_code").GetString() ?? throw new InvalidDataException("GitHub device response has no user code."),
            new Uri(root.GetProperty("verification_uri").GetString() ?? throw new InvalidDataException("GitHub device response has no verification URI.")),
            TimeSpan.FromSeconds(root.GetProperty("expires_in").GetInt32()),
            TimeSpan.FromSeconds(root.TryGetProperty("interval", out var interval) ? interval.GetInt32() : 5));
    }

    public async Task<GitHubAccessToken> WaitForAccessTokenAsync(GitHubDeviceCode deviceCode, CancellationToken cancellationToken)
    {
        EnsureClientId();
        var deadline = DateTimeOffset.UtcNow + deviceCode.ExpiresIn;
        var interval = deviceCode.Interval;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(interval, cancellationToken);
            using var response = await client.PostAsync(
                "login/oauth/access_token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["device_code"] = deviceCode.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                }),
                cancellationToken);
            using var json = await ReadJsonAsync(response, cancellationToken);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out var errorElement))
            {
                var error = errorElement.GetString() ?? "unknown_error";
                if (error == "authorization_pending")
                    continue;
                if (error == "slow_down")
                {
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                }
                throw new GitHubOAuthException(error, root.TryGetProperty("error_description", out var description) ? description.GetString() : null);
            }

            var token = root.GetProperty("access_token").GetString() ?? throw new InvalidDataException("GitHub token response has no access token.");
            var expiresIn = root.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 0;
            return new GitHubAccessToken(
                token,
                root.TryGetProperty("token_type", out var type) ? type.GetString() ?? "bearer" : "bearer",
                expiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(expiresIn) : null,
                root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null);
        }
        throw new GitHubOAuthException("expired_token", "GitHub device authorization expired.");
    }

    void EnsureClientId()
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("GitHub App client ID is not configured.");
    }

    static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub device flow failed: {(int)response.StatusCode} {body}", null, response.StatusCode);
        return JsonDocument.Parse(body);
    }

    static void ThrowOAuthError(JsonDocument json)
    {
        if (json.RootElement.TryGetProperty("error", out var error))
            throw new GitHubOAuthException(error.GetString() ?? "unknown_error", json.RootElement.TryGetProperty("error_description", out var description) ? description.GetString() : null);
    }
}
