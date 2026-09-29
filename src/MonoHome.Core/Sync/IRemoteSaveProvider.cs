namespace MonoHome.Core.Sync;

public interface IRemoteSaveProvider
{
    Task<RepositoryBinding> BindRepositoryAsync(string owner, string repository, CancellationToken cancellationToken);
    Task<RemoteSaveVersion?> GetLatestAsync(string saveKey, string lineageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RemoteSaveVersion>> ListVersionsAsync(string saveKey, CancellationToken cancellationToken);
    Task<byte[]> DownloadAsync(RemoteSaveVersion version, CancellationToken cancellationToken);
    Task<RemoteSaveVersion> UploadAsync(
        string saveKey,
        string lineageId,
        byte[] content,
        string? expectedCommitSha,
        string message,
        CancellationToken cancellationToken);
    Task<string> CreateLineageAsync(string saveKey, string fromCommitSha, CancellationToken cancellationToken);
}
