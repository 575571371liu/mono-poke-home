namespace MonoHome.Core.Sync;

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
