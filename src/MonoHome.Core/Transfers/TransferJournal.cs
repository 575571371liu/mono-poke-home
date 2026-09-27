using System.Text.Json;

namespace MonoHome.Core.Transfers;

public sealed record TransferRecord(
    string Id,
    string RepositoryId,
    string SourceGame,
    string TargetGame,
    string Status,
    IReadOnlyList<TransferChange> Changes,
    DateTimeOffset CreatedAt,
    string? OutputName,
    string? BackupPath,
    DateTimeOffset? CompletedAt,
    string? Mode = null);

public static class TransferJournal
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static TransferRecord Append(string root, string repositoryId, string sourceGame, string targetGame, TransferReport report, string? backupPath = null, TransferMode mode = TransferMode.Conversion)
    {
        Directory.CreateDirectory(root);
        var record = new TransferRecord(
            Guid.NewGuid().ToString("N"),
            repositoryId,
            sourceGame,
            targetGame,
            report.Succeeded ? "prepared" : "failed",
            report.Changes,
            DateTimeOffset.UtcNow,
            null,
            backupPath,
            null,
            mode.ToString().ToLowerInvariant());
        Write(root, record);
        return record;
    }

    public static TransferRecord MarkExported(string root, string id, string outputName)
    {
        var record = Read(Path.Combine(root, $"{id}.json")) ?? throw new InvalidDataException("Transfer record is missing.");
        if (record.Status != "prepared")
            throw new InvalidOperationException("Only a prepared transfer can be exported.");
        var completed = record with { Status = "succeeded", OutputName = outputName, CompletedAt = DateTimeOffset.UtcNow };
        Write(root, completed);
        return completed;
    }

    public static IReadOnlyList<TransferRecord> List(string root)
    {
        if (!Directory.Exists(root))
            return [];
        return Directory.EnumerateFiles(root, "*.json")
            .Select(Read)
            .Where(record => record is not null)
            .Cast<TransferRecord>()
            .OrderByDescending(record => record.CreatedAt)
            .ToArray();
    }

    static TransferRecord? Read(string path)
    {
        try { return JsonSerializer.Deserialize<TransferRecord>(File.ReadAllText(path), Json); }
        catch (JsonException) { return null; }
    }

    static void Write(string root, TransferRecord record)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{record.Id}.json");
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(record, Json));
        File.Move(temporary, path, true);
    }
}
