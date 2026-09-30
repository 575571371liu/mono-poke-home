using MonoHome.Core.Storage;
using System.Security.Cryptography;
using System.Text.Json;

namespace MonoHome.Core.Saves;

public sealed record RegisteredSave(
    string Id,
    string DisplayName,
    string Game,
    int Generation,
    long FileSize,
    string Hash,
    string SnapshotPath,
    string ManifestPath,
    DateTimeOffset ImportedAt,
    string? SourceUri,
    int SourceFlags = 0);

public static class SaveRegistry
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static RegisteredSave Register(byte[] bytes, string displayName, string root, string? sourceUri = null, int sourceFlags = 0)
    {
        var inspection = SaveInspector.Inspect(bytes, displayName);
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(root, id);
        Directory.CreateDirectory(directory);
        var record = new RegisteredSave(
            id,
            displayName,
            inspection.Game,
            inspection.Generation,
            bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes)),
            Path.Combine(directory, "source.snapshot"),
            Path.Combine(directory, "record.json"),
            DateTimeOffset.UtcNow,
            sourceUri,
            sourceFlags);
        AtomicFile.Write(record.SnapshotPath, bytes);
        AtomicFile.WriteJson(record.ManifestPath, record, Json);
        return record;
    }

    public static RegisteredSave? GetLatest(string root)
    {
        if (!Directory.Exists(root))
            return null;
        return Directory.EnumerateFiles(root, "record.json", SearchOption.AllDirectories)
            .Select(ReadRecord)
            .Where(record => record is not null && File.Exists(record.SnapshotPath))
            .OrderByDescending(record => record!.ImportedAt)
            .FirstOrDefault();
    }

    public static RegisteredSave? Get(string root, string id) =>
        ReadRecord(Path.Combine(root, id, "record.json"));

    public static RegisteredSave UpdateSnapshot(RegisteredSave current, byte[] bytes)
    {
        var updated = current with
        {
            FileSize = bytes.Length,
            Hash = Convert.ToHexString(SHA256.HashData(bytes)),
            ImportedAt = DateTimeOffset.UtcNow,
        };
        AtomicFile.Write(updated.SnapshotPath, bytes);
        AtomicFile.WriteJson(updated.ManifestPath, updated, Json);
        return updated;
    }

    static RegisteredSave? ReadRecord(string path) => AtomicFile.TryReadJson<RegisteredSave>(path, Json);
}
