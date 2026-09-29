using Android.Content;
using Android.Security;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using MonoHome.Core.Sync;
using System.Text;

namespace MonoHome.Android.Sync;

public sealed class AndroidTokenStore(Context context)
{
    const string Provider = "AndroidKeyStore";
    const string Alias = "mono_home_github_token";
    const string Preferences = "remote-save-secret";
    const string TokenKey = "github_token";
    readonly Context appContext = context.ApplicationContext ?? context;

    public Task SaveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.", nameof(token));

        var cipher = Cipher.GetInstance("AES/GCM/NoPadding") ?? throw new InvalidOperationException("Android cipher is unavailable.");
        cipher.Init(CipherMode.EncryptMode, GetOrCreateKey());
        var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(token)) ?? throw new InvalidOperationException("Android encryption failed.");
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

    public Task<string?> LoadTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var encoded = appContext.GetSharedPreferences(Preferences, FileCreationMode.Private)?.GetString(TokenKey, null);
        if (string.IsNullOrWhiteSpace(encoded))
            return Task.FromResult<string?>(null);

        var payload = Convert.FromBase64String(encoded);
        const int ivLength = 12;
        if (payload.Length <= ivLength)
            throw new InvalidDataException("Stored GitHub token payload is invalid.");
        var iv = payload[..ivLength];
        var ciphertext = payload[ivLength..];
        var cipher = Cipher.GetInstance("AES/GCM/NoPadding") ?? throw new InvalidOperationException("Android cipher is unavailable.");
        cipher.Init(CipherMode.DecryptMode, GetOrCreateKey(), new GCMParameterSpec(128, iv));
        var plaintext = cipher.DoFinal(ciphertext) ?? throw new InvalidDataException("Stored GitHub token could not be decrypted.");
        return Task.FromResult<string?>(Encoding.UTF8.GetString(plaintext));
    }

    public Task ClearTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        appContext.GetSharedPreferences(Preferences, FileCreationMode.Private)?.Edit()?.Remove(TokenKey)?.Apply();
        return Task.CompletedTask;
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
