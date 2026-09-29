namespace MonoHome.Core.Sync;

public sealed record SaveManifestEntry(string DisplayName, string Path, string Format);

public sealed record SaveRepositoryManifest(
    int Schema,
    string App,
    IReadOnlyDictionary<string, SaveManifestEntry> Saves)
{
    public static SaveRepositoryManifest CreateDefault() => new(
        1,
        "mono-home",
        new Dictionary<string, SaveManifestEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["emerald"] = new("绿宝石", "saves/emerald/emerald.srm", "srm"),
            ["heartgold"] = new("心金", "saves/heartgold/heartgold.sav", "sav"),
            ["soulsilver"] = new("魂银", "saves/soulsilver/soulsilver.sav", "sav"),
        });

    public void Validate()
    {
        if (Schema != 1 || !string.Equals(App, "mono-home", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported MONO / HOME repository manifest.");
        if (Saves is null || Saves.Count == 0 || Saves.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key) ||
                pair.Value is null ||
                string.IsNullOrWhiteSpace(pair.Value.DisplayName) ||
                string.IsNullOrWhiteSpace(pair.Value.Format) ||
                !IsSafeSavePath(pair.Value.Path)))
            throw new InvalidDataException("MONO / HOME repository manifest has no valid saves.");
    }

    static bool IsSafeSavePath(string path)
    {
        if (!path.StartsWith("saves/", StringComparison.Ordinal) || path.Contains('\\'))
            return false;
        return path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");
    }
}

public sealed record RepositoryBinding(
    string Provider,
    string Owner,
    string Repository,
    string DefaultBranch,
    DateTimeOffset BoundAt,
    int Schema);

public sealed record SaveRemoteBinding(
    string SaveKey,
    string LineageId,
    string? BaseCommitSha,
    string? BaseContentHash = null);

public sealed record RemoteSaveVersion(
    string SaveKey,
    string LineageId,
    string CommitSha,
    string? ContentHash,
    string? BlobSha,
    DateTimeOffset ModifiedAt,
    string Device,
    string Message,
    string? ParentCommitSha = null);

public sealed record SaveSyncState(
    string SaveKey,
    string LineageId,
    string LocalHash,
    string? BaseCommitSha,
    RemoteSaveVersion? RemoteLatest,
    SyncStatus Status);

public enum SyncStatus
{
    Unknown,
    Aligned,
    LocalNewer,
    RemoteNewer,
    Diverged,
}

public sealed record LocalSaveSnapshot(
    string SaveKey,
    byte[] Content,
    string ContentHash,
    DateTimeOffset ModifiedAt);

public sealed record SyncOperationResult(
    bool Succeeded,
    bool NoOp,
    SaveSyncState State,
    string Message,
    string? RecoveryPointPath = null);
