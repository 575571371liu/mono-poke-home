using System.Security.Cryptography;

namespace MonoHome.Core.Sync.GitHub;

public sealed record GitHubAuthOptions(string ClientId, Uri RedirectUri);

public sealed record GitHubAuthorizationRequest(
    Uri AuthorizationUri,
    string State,
    string CodeVerifier);

public sealed class GitHubAuthClient(GitHubAuthOptions options)
{
    public GitHubAuthorizationRequest CreateAuthorizationRequest()
    {
        if (string.IsNullOrWhiteSpace(options.ClientId))
            throw new InvalidOperationException("GitHub App client ID is not configured.");

        var state = RandomBase64Url(32);
        var verifier = RandomBase64Url(32);
        var challenge = Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        var query = string.Join('&', new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri.ToString(),
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new GitHubAuthorizationRequest(
            new Uri($"https://github.com/login/oauth/authorize?{query}"),
            state,
            verifier);
    }

    public static string ValidateCallback(string expectedState, string? actualState, string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(actualState) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(expectedState),
                System.Text.Encoding.UTF8.GetBytes(actualState)))
            throw new InvalidOperationException("GitHub authorization callback is invalid.");
        return code;
    }

    static string RandomBase64Url(int bytes)
    {
        var value = RandomNumberGenerator.GetBytes(bytes);
        return Base64Url(value);
    }

    static string Base64Url(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace("+", "-", StringComparison.Ordinal)
        .Replace("/", "_", StringComparison.Ordinal);
}
