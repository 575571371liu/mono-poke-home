using Android.Content;
using MonoHome.Core.Sync;
using System.Text.Json;

namespace MonoHome.Android.Sync;

public sealed class AndroidRepositoryBindingStore(Context context)
{
    const string Preferences = "remote-save-binding";
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    readonly ISharedPreferences preferences = context.ApplicationContext!.GetSharedPreferences(Preferences, FileCreationMode.Private)!;

    public Task SaveRepositoryAsync(RepositoryBinding binding, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        preferences.Edit()!
            .PutString("provider", binding.Provider)!
            .PutString("owner", binding.Owner)!
            .PutString("repository", binding.Repository)!
            .PutString("defaultBranch", binding.DefaultBranch)!
            .PutString("boundAt", binding.BoundAt.ToString("O"))!
            .PutInt("schema", binding.Schema)!
            .Apply();
        return Task.CompletedTask;
    }

    public Task<RepositoryBinding?> LoadRepositoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var provider = preferences.GetString("provider", null);
        var owner = preferences.GetString("owner", null);
        var repository = preferences.GetString("repository", null);
        var branch = preferences.GetString("defaultBranch", null);
        var boundAt = preferences.GetString("boundAt", null);
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository) ||
            string.IsNullOrWhiteSpace(branch) || !DateTimeOffset.TryParse(boundAt, out var parsed))
            return Task.FromResult<RepositoryBinding?>(null);
        return Task.FromResult<RepositoryBinding?>(new RepositoryBinding(provider, owner, repository, branch, parsed, preferences.GetInt("schema", 1)));
    }

    public Task SaveSaveBindingAsync(SaveRemoteBinding binding, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        preferences.Edit()!
            .PutString($"save.{binding.SaveKey}", JsonSerializer.Serialize(binding, Json))!
            .Apply();
        return Task.CompletedTask;
    }

    public Task<SaveRemoteBinding?> LoadSaveBindingAsync(string saveKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var serialized = preferences.GetString($"save.{saveKey}", null);
        return Task.FromResult(string.IsNullOrWhiteSpace(serialized)
            ? null
            : JsonSerializer.Deserialize<SaveRemoteBinding>(serialized, Json));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        preferences.Edit()?.Clear()?.Apply();
        return Task.CompletedTask;
    }
}
