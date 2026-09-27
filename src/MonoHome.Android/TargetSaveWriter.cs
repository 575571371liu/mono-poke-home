namespace MonoHome.Android;

using global::Android.Content;
using MonoHome.Core.Saves;
using System.Security.Cryptography;

public sealed record TargetWriteResult(bool Succeeded, byte[]? WrittenBytes, string? BackupPath, string Message);

public sealed class TargetSaveWriter(ContentResolver resolver, string savesRoot)
{
    const int ReadWriteFlags = (int)(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

    public async Task<TargetWriteResult> WriteAsync(RegisteredSave target, byte[] preparedBytes, CancellationToken cancellationToken = default)
    {
        if ((target.SourceFlags & ReadWriteFlags) != ReadWriteFlags)
            return new(false, null, null, "目标存档没有持久读写权限，请重新授权。");
        if (string.IsNullOrWhiteSpace(target.SourceUri))
            return new(false, null, null, "目标存档地址不存在，请重新授权。");
        try
        {
            var uri = global::Android.Net.Uri.Parse(target.SourceUri) ?? throw new IOException("目标存档地址无效。");
            byte[] original;
            using (var input = resolver.OpenInputStream(uri) ?? throw new IOException("无法读取目标存档。"))
            using (var memory = new MemoryStream())
            {
                await input.CopyToAsync(memory, cancellationToken);
                original = memory.ToArray();
            }
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(original), Convert.FromHexString(target.Hash)))
                return new(false, null, null, "目标存档已被外部修改，请重新导入。");

            var backupPath = Path.Combine(savesRoot, target.Id, "backups", $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.sav");
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.WriteAllBytes(backupPath, original);
            using (var output = resolver.OpenOutputStream(uri, "wt") ?? throw new IOException("目标存档不支持写入。"))
                await output.WriteAsync(preparedBytes, cancellationToken);
            using var verifyInput = resolver.OpenInputStream(uri) ?? throw new IOException("写入后无法重新读取目标存档。");
            using var verified = new MemoryStream();
            await verifyInput.CopyToAsync(verified, cancellationToken);
            var written = verified.ToArray();
            if (!CryptographicOperations.FixedTimeEquals(preparedBytes, written))
                return new(false, null, backupPath, "目标存档写入校验不一致，恢复点已保留。");
            SaveInspector.Inspect(written, target.DisplayName);
            return new(true, written, backupPath, "目标存档已写入并校验。");
        }
        catch (Exception ex)
        {
            return new(false, null, null, ex.Message);
        }
    }
}
