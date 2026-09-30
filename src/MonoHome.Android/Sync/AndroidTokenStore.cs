using Android.Content;
using Android.Security;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using MonoHome.Core.Sync;
using MonoHome.Core.Sync.GitHub;
using System.Text;
using System.Text.Json;

namespace MonoHome.Android.Sync;

public sealed class AndroidTokenStore(Context context)
{
    const string Provider = "AndroidKeyStore";
    const string Alias = "mono_home_github_token";
    const string Preferences = "remote-save-secret";
    const string TokenKey = "github_token";
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    readonly Context appContext = context.ApplicationContext ?? context;

    public Task SaveTokenAsync(string token, CancellationToken cancellationToken = default) =>
        SaveAccessTokenAsync(new GitHubAccessToken(token, "bearer", null, null), cancellationToken);

    public Task SaveAccessTokenAsync(GitHubAccessToken token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new ArgumentException("Token is required.", nameof(token));

        var cipher = Cipher.GetInstance("AES/GCM/NoPadding") ?? throw new InvalidOperationException("Android cipher is unavailable.");
        cipher.Init(CipherMode.EncryptMode, GetOrCreateKey());
        var serialized = JsonSerializer.SerializeToUtf8Bytes(token, Json);
        var encrypted = cipher.DoFinal(serialized) ?? throw new InvalidOperationException("Android encryption failed.");
        var iv = cipher.GetIV() ?? throw new InvalidOperationException("Android cipher did not return an IV.");
        var payload = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, payload, iv.Length, encrypted.Length);
        appContext.GetSharedPreferences(Preferences, FileCreationMode.Private)!
            .Edit()!
            .PutString(TokenKey, Convert.ToBase64String(payload))!
            .Apply();
        return Task.CompletedTask;
    }

    public async Task<string?> LoadTokenAsync(CancellationToken cancellationToken = default) =>
        (await LoadAccessTokenAsync(cancellationToken))?.AccessToken;

    public Task<GitHubAccessToken?> LoadAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var encoded = appContext.GetSharedPreferences(Preferences, FileCreationMode.Private)?.GetString(TokenKey, null);
        if (string.IsNullOrWhiteSpace(encoded))
            return Task.FromResult<GitHubAccessToken?>(null);

        GitHubAccessToken? token;
        try
        {
            token = Decrypt(encoded);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or Java.Lang.Exception)
        {
            // The ciphertext outlives the Keystore key across a restore, a "clear
            // credentials" reset, or an interrupted write. The stored token is then
            // unrecoverable by design, so drop it and report "not connected" rather than
            // throwing out of a dialog callback and taking the process down.
            // Cleanup must never turn a recoverable read into a throw, so it is best-effort
            // and does not take the caller's cancellation token.
            try { ClearTokenAsync(CancellationToken.None).GetAwaiter().GetResult(); }
            catch (Exception cleanup) when (cleanup is Java.Lang.Exception or IOException or UnauthorizedAccessException or InvalidOperationException) { }
            return Task.FromResult<GitHubAccessToken?>(null);
        }
        return Task.FromResult(token);
    }

    static GitHubAccessToken? Decrypt(string encoded)
    {
        var payload = Convert.FromBase64String(encoded);
        const int ivLength = 12;
        if (payload.Length <= ivLength)
            throw new InvalidDataException("Stored GitHub token payload is invalid.");
        var iv = payload[..ivLength];
        var ciphertext = payload[ivLength..];
        var cipher = Cipher.GetInstance("AES/GCM/NoPadding") ?? throw new InvalidOperationException("Android cipher is unavailable.");
        cipher.Init(CipherMode.DecryptMode, GetOrCreateKey(), new GCMParameterSpec(128, iv));
        var plaintext = cipher.DoFinal(ciphertext) ?? throw new InvalidDataException("Stored GitHub token could not be decrypted.");
        var value = Encoding.UTF8.GetString(plaintext);
        try
        {
            var token = JsonSerializer.Deserialize<GitHubAccessToken>(value, Json);
            if (token is not null && !string.IsNullOrWhiteSpace(token.AccessToken))
                return token;
        }
        catch (JsonException)
        {
            // Migrate the previous encrypted plain-token format on read.
        }

        // A JSON-looking payload that failed to yield a token is corrupt, not a legacy
        // token: returning it verbatim would send the JSON text as the bearer token.
        return value.TrimStart().StartsWith('{') ? null : new GitHubAccessToken(value, "bearer", null, null);
    }

    public Task ClearTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Commit, not Apply, so "已清除" is durable before the caller reports success.
        appContext.GetSharedPreferences(Preferences, FileCreationMode.Private)?.Edit()?.Remove(TokenKey)?.Commit();
        DeleteKey();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes the Keystore key so no retained ciphertext copy stays decryptable after
    /// the user revokes their PAT.
    /// </summary>
    static void DeleteKey()
    {
        try
        {
            var keyStore = KeyStore.GetInstance(Provider);
            keyStore?.Load(null);
            if (keyStore?.ContainsAlias(Alias) == true)
                keyStore.DeleteEntry(Alias);
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException)
        {
            // A missing Keystore entry is the desired end state anyway.
        }
    }

    static Java.Security.IKey GetOrCreateKey()
    {
        var keyStore = KeyStore.GetInstance(Provider) ?? throw new InvalidOperationException("Android Keystore is unavailable.");
        keyStore.Load(null);
        if (!keyStore.ContainsAlias(Alias))
        {
            var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, Provider) ?? throw new InvalidOperationException("Android AES key generator is unavailable.");
            generator.Init(new KeyGenParameterSpec.Builder(
                    Alias,
                    KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
                .SetKeySize(256)
                .Build());
            generator.GenerateKey();
        }
        return keyStore.GetKey(Alias, null) ?? throw new InvalidOperationException("Android Keystore key is unavailable.");
    }
}
