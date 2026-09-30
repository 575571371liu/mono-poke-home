using System.Text.Json;

namespace MonoHome.Core.Sync.GitHub;

public sealed record GitHubDeviceCode(
    string DeviceCode,
    string UserCode,
    Uri VerificationUri,
    TimeSpan ExpiresIn,
    TimeSpan Interval)
{
    /// <summary>Redacts the device code so it cannot reach a log or an error dialog.</summary>
    public override string ToString() => $"GitHubDeviceCode {{ UserCode = {UserCode}, ExpiresIn = {ExpiresIn}, Interval = {Interval} }}";
}

public sealed record GitHubAccessToken(
    string AccessToken,
    string TokenType,
    DateTimeOffset? ExpiresAt,
    string? RefreshToken)
{
    /// <summary>Redacts both secrets so the token cannot reach a log or an error dialog.</summary>
    public override string ToString() => $"GitHubAccessToken {{ TokenType = {TokenType}, ExpiresAt = {ExpiresAt}, HasRefreshToken = {RefreshToken is not null} }}";
}

public sealed class GitHubOAuthException(string error, string? description = null) : InvalidOperationException(description is null ? error : $"{error}: {description}")
{
    public string Error { get; } = error;
}

public sealed class GitHubDeviceFlowClient
{
    const string DefaultBaseAddress = "https://github.com/";
    const int DefaultPollSeconds = 5;

    /// <summary>RFC 8628 requires a positive polling interval; GitHub may return 0.</summary>
    const int MinimumPollSeconds = 1;

    readonly HttpClient client;
    readonly string clientId;

    public GitHubDeviceFlowClient(HttpClient client, string clientId)
    {
        this.client = client;
        this.clientId = clientId;
        client.BaseAddress ??= new Uri(DefaultBaseAddress);
    }

    public async Task<GitHubDeviceCode> RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        EnsureClientId();
        using var response = await PostFormAsync(
            "login/device/code",
            new Dictionary<string, string> { ["client_id"] = clientId },
            cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        ThrowOAuthError(json);
        var root = json.RootElement;
        return new GitHubDeviceCode(
            root.GetProperty("device_code").GetString() ?? throw new InvalidDataException("GitHub device response has no device code."),
            root.GetProperty("user_code").GetString() ?? throw new InvalidDataException("GitHub device response has no user code."),
            new Uri(root.GetProperty("verification_uri").GetString() ?? throw new InvalidDataException("GitHub device response has no verification URI.")),
            TimeSpan.FromSeconds(root.GetProperty("expires_in").GetInt32()),
            TimeSpan.FromSeconds(root.TryGetProperty("interval", out var interval) ? Math.Max(interval.GetInt32(), MinimumPollSeconds) : DefaultPollSeconds));
    }

    public async Task<GitHubAccessToken> WaitForAccessTokenAsync(GitHubDeviceCode deviceCode, CancellationToken cancellationToken)
    {
        EnsureClientId();
        var deadline = DateTimeOffset.UtcNow + deviceCode.ExpiresIn;
        var interval = deviceCode.Interval;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(interval, cancellationToken);
            using var response = await PostFormAsync(
                "login/oauth/access_token",
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["device_code"] = deviceCode.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                },
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

            return ParseAccessToken(root);
        }
        throw new GitHubOAuthException("expired_token", "GitHub device authorization expired.");
    }

    public async Task<GitHubAccessToken> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        EnsureClientId();
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));

        using var response = await PostFormAsync(
            "login/oauth/access_token",
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
            },
            cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        ThrowOAuthError(json);
        var refreshed = ParseAccessToken(json.RootElement);
        return refreshed with { RefreshToken = refreshed.RefreshToken ?? refreshToken };
    }

    /// <summary>
    /// Posts a form body and asks for a JSON reply. GitHub serves these endpoints as
    /// <c>application/x-www-form-urlencoded</c> unless the request opts in with
    /// <c>Accept: application/json</c>, so the header is required for every call.
    /// </summary>
    async Task<HttpResponseMessage> PostFormAsync(string path, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Accept.ParseAdd("application/json");
        return await client.SendAsync(request, cancellationToken);
    }

    void EnsureClientId()
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("GitHub App client ID is not configured.");
    }

    static GitHubAccessToken ParseAccessToken(JsonElement root)
    {
        var token = root.GetProperty("access_token").GetString() ?? throw new InvalidDataException("GitHub token response has no access token.");
        var expiresIn = root.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 0;
        return new GitHubAccessToken(
            token,
            root.TryGetProperty("token_type", out var type) ? type.GetString() ?? "bearer" : "bearer",
            expiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(expiresIn) : null,
            root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null);
    }

    static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // A successful OAuth reply carries the access token, so the body must never be
            // embedded in a message: messages reach dialogs and logs. Only a short prefix of
            // a failed response is kept, and error fields are redacted.
            throw new HttpRequestException(
                $"GitHub device flow failed: {(int)response.StatusCode} {Summarize(body)}",
                null,
                response.StatusCode);
        }
        return JsonDocument.Parse(body);
    }

    static string Summarize(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "no response body";
        var flattened = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flattened.Length <= 200 ? Redact(flattened) : Redact(flattened[..200]) + "…";
    }

    static string Redact(string value)
    {
        var result = value;
        foreach (var key in (string[])["access_token", "refresh_token", "device_code"])
            result = RedactField(result, key);
        return result;
    }

    /// <summary>
    /// Replaces the value of <paramref name="key"/> in either the form-encoded
    /// (<c>key=value&amp;</c>) or JSON (<c>"key":"value"</c>) shape, so a body echoed into an
    /// exception message cannot carry a secret in either encoding.
    /// </summary>
    static string RedactField(string value, string key)
    {
        var index = value.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return value;

        var cursor = index + key.Length;
        while (cursor < value.Length && (value[cursor] == '"' || value[cursor] == ' '))
            cursor++;
        if (cursor >= value.Length)
            return value;

        string terminator;
        if (value[cursor] == '=')
        {
            terminator = "&";
            cursor++;
        }
        else if (value[cursor] == ':')
        {
            terminator = "\"";
            cursor++;
            while (cursor < value.Length && value[cursor] == ' ')
                cursor++;
            if (cursor < value.Length && value[cursor] == '"')
                cursor++;
        }
        else
        {
            return value;
        }

        var end = value.IndexOf(terminator, cursor, StringComparison.Ordinal);
        if (end < 0)
            end = value.Length;
        return string.Concat(value.AsSpan(0, cursor), "<redacted>", value.AsSpan(end));
    }

    static void ThrowOAuthError(JsonDocument json)
    {
        if (json.RootElement.TryGetProperty("error", out var error))
            throw new GitHubOAuthException(error.GetString() ?? "unknown_error", json.RootElement.TryGetProperty("error_description", out var description) ? description.GetString() : null);
    }
}
