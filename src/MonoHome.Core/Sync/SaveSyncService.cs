using System.Security.Cryptography;
using MonoHome.Core.Saves;

namespace MonoHome.Core.Sync;

public sealed class SaveSyncService(IRemoteSaveProvider provider)
{
    public SaveSyncState Compare(
        string saveKey,
        ReadOnlyMemory<byte> localContent,
        string? baseCommitSha,
        RemoteSaveVersion? remoteLatest)
        => Compare(saveKey, localContent, new SaveRemoteBinding(saveKey, remoteLatest?.LineageId ?? "default", baseCommitSha), remoteLatest);

    public SaveSyncState Compare(
        string saveKey,
        ReadOnlyMemory<byte> localContent,
        SaveRemoteBinding binding,
        RemoteSaveVersion? remoteLatest)
    {
        var localHash = ComputeHash(localContent.Span);
        var status = GetStatus(localHash, binding.BaseCommitSha, binding.BaseContentHash, remoteLatest);
        return new SaveSyncState(saveKey, binding.LineageId, localHash, binding.BaseCommitSha, remoteLatest, status);
    }

    public async Task<SyncOperationResult> UploadAsync(
        LocalSaveSnapshot localSnapshot,
        RepositoryBinding binding,
        SaveRemoteBinding saveBinding,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var remoteLatest = await provider.GetLatestAsync(saveBinding.SaveKey, saveBinding.LineageId, cancellationToken);
        var state = Compare(saveBinding.SaveKey, localSnapshot.Content, saveBinding, remoteLatest);

        if (state.Status == SyncStatus.Aligned)
            return new SyncOperationResult(true, true, state, "本地与远端内容一致，无需创建新版本。");

        if (remoteLatest is not null && state.Status != SyncStatus.LocalNewer)
            return new SyncOperationResult(false, false, state, "远端状态无法安全覆盖，请先选择远端版本或创建新存档线。");

        var uploaded = await provider.UploadAsync(
            saveBinding.SaveKey,
            saveBinding.LineageId,
            localSnapshot.Content,
            remoteLatest?.CommitSha,
            $"Sync {localSnapshot.SaveKey}",
            cancellationToken);

        if (!string.Equals(uploaded.ContentHash, localSnapshot.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            var failed = state with { RemoteLatest = uploaded, Status = SyncStatus.Unknown };
            return new SyncOperationResult(false, false, failed, "远端返回的内容 hash 与本地不一致，已停止同步。");
        }

        var aligned = new SaveSyncState(
            saveBinding.SaveKey,
            uploaded.LineageId,
            localSnapshot.ContentHash,
            uploaded.CommitSha,
            uploaded,
            SyncStatus.Aligned);
        return new SyncOperationResult(true, false, aligned, "本地存档已上传并与远端对齐。");
    }

    public async Task<SyncOperationResult> PullAsync(
        RegisteredSave localSave,
        RemoteSaveVersion version,
        RepositoryBinding binding,
        SaveRemoteBinding saveBinding,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(version.SaveKey, saveBinding.SaveKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Remote version does not belong to the selected save.");

        var original = File.ReadAllBytes(localSave.SnapshotPath);
        var recoveryPath = $"{localSave.SnapshotPath}.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.recovery";
        try
        {
            var downloaded = await provider.DownloadAsync(version, cancellationToken);
            SaveInspector.Inspect(downloaded, localSave.DisplayName);
            if (version.ContentHash is not null && !string.Equals(ComputeHash(downloaded), version.ContentHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Downloaded save hash does not match the selected remote version.");

            File.Copy(localSave.SnapshotPath, recoveryPath, overwrite: false);
            SaveRegistry.UpdateSnapshot(localSave, downloaded);
            var state = new SaveSyncState(
                saveBinding.SaveKey,
                version.LineageId,
                ComputeHash(downloaded),
                version.CommitSha,
                version,
                SyncStatus.Aligned);
            return new SyncOperationResult(true, false, state, "远端存档已校验并写入本地快照。", recoveryPath);
        }
        catch
        {
            if (!File.Exists(localSave.SnapshotPath) || !original.SequenceEqual(File.ReadAllBytes(localSave.SnapshotPath)))
                File.WriteAllBytes(localSave.SnapshotPath, original);
            throw;
        }
    }

    public static string ComputeHash(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content));

    static SyncStatus GetStatus(
        string localHash,
        string? baseCommitSha,
        string? baseContentHash,
        RemoteSaveVersion? remoteLatest)
    {
        if (remoteLatest is null || remoteLatest.ContentHash is null || baseCommitSha is null)
            return SyncStatus.Unknown;

        if (string.Equals(localHash, remoteLatest.ContentHash, StringComparison.OrdinalIgnoreCase))
            return SyncStatus.Aligned;

        if (string.Equals(remoteLatest.CommitSha, baseCommitSha, StringComparison.Ordinal))
            return SyncStatus.LocalNewer;

        if (baseContentHash is not null && string.Equals(localHash, baseContentHash, StringComparison.OrdinalIgnoreCase))
            return SyncStatus.RemoteNewer;

        return SyncStatus.Diverged;
    }
}
