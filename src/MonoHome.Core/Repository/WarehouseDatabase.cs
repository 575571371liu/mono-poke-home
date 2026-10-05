using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PKHeX.Core;

namespace MonoHome.Core.Repository;

/// <summary>One SQLite row owns the metadata, original and working entity; no entity file cache.</summary>
internal static class WarehouseDatabase
{
    const string FileName = "warehouse.sqlite3";
    const string Columns = "id, species, created_at, updated_at, legality, parent_id, revision";

    public static string DatabasePath(string root) => Path.Combine(Path.GetFullPath(root), FileName);

    public static SqliteConnection Open(string root)
    {
        root = Path.GetFullPath(root);
        Directory.CreateDirectory(root);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath(root), Pooling = false, DefaultTimeout = 5,
        }.ToString());
        try
        {
            connection.Open();
            Execute(connection, "PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; PRAGMA secure_delete=ON; PRAGMA auto_vacuum=FULL;");
            var version = Convert.ToInt32(Scalar(connection, "PRAGMA user_version"));
            if (version > 1) throw new InvalidDataException("仓库数据库来自较新版本，无法安全打开。");
            using (var transaction = connection.BeginTransaction())
            {
                Execute(connection, """
                    CREATE TABLE IF NOT EXISTS pokemon (
                        id TEXT PRIMARY KEY NOT NULL CHECK(length(id) > 0),
                        species INTEGER NOT NULL CHECK(species > 0),
                        created_at TEXT NOT NULL, updated_at TEXT NOT NULL,
                        legality TEXT NOT NULL, parent_id TEXT,
                        revision INTEGER NOT NULL CHECK(revision > 0),
                        original BLOB NOT NULL CHECK(length(original) > 0),
                        working BLOB NOT NULL CHECK(length(working) > 0));
                    CREATE TABLE IF NOT EXISTS warehouse_meta (key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL);
                    CREATE TABLE IF NOT EXISTS legacy_cleanup (path TEXT PRIMARY KEY NOT NULL, hash TEXT NOT NULL);
                    """, transaction);
                if (version == 0) Execute(connection, "PRAGMA user_version=1", transaction);
                if (Scalar(connection, "SELECT value FROM warehouse_meta WHERE key='migrated'", transaction) is null)
                    Migrate(connection, transaction, root);
                transaction.Commit();
            }
            CleanupLegacy(connection, root);
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    public static SqliteConnection Open(StoredPokemon record)
    {
        var root = Path.GetDirectoryName(record.DatabasePath) ?? throw new InvalidDataException("仓库路径缺失。");
        if (!SamePath(record.DatabasePath, DatabasePath(root))) throw new InvalidDataException("仓库数据库路径无效。");
        return Open(root);
    }

    public static IReadOnlyList<StoredPokemon> List(string root)
    {
        using var connection = Open(root);
        using var command = Command(connection, $"SELECT {Columns} FROM pokemon ORDER BY updated_at DESC, id");
        using var reader = command.ExecuteReader();
        var records = new List<StoredPokemon>();
        while (reader.Read()) records.Add(ReadRecord(reader, DatabasePath(root)));
        return records;
    }

    public static byte[] ReadPayload(StoredPokemon record, bool original)
    {
        using var connection = Open(record);
        using var command = Command(connection, $"SELECT {(original ? "original" : "working")} FROM pokemon WHERE id=$id");
        command.Parameters.AddWithValue("$id", record.Id);
        return command.ExecuteScalar() as byte[] ?? throw new InvalidDataException("宝可梦已不存在，请刷新仓库。");
    }

    public static StoredPokemon Insert(PKM pokemon, byte[] data, string root, string legality, string? parentId = null)
    {
        using var connection = Open(root);
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow;
        var record = new StoredPokemon(Guid.NewGuid().ToString("N"), pokemon.Species, DatabasePath(root), now, now, legality, parentId);
        Insert(connection, transaction, record, data, data);
        transaction.Commit();
        return record;
    }

    public static StoredPokemon Update(StoredPokemon record, byte[]? working, int species, string legality, bool restoreOriginal = false)
    {
        using var connection = Open(record);
        using var transaction = connection.BeginTransaction();
        var updated = record with { Species = species, UpdatedAt = DateTimeOffset.UtcNow, LegalityStatus = legality, Revision = record.Revision + 1 };
        var payload = restoreOriginal ? "working=original," : working is null ? "" : "working=$working,";
        using var command = Command(connection, $"UPDATE pokemon SET {payload} species=$species, updated_at=$updated, legality=$legality, revision=$next WHERE id=$id AND revision=$expected", transaction);
        command.Parameters.AddWithValue("$species", species);
        command.Parameters.AddWithValue("$updated", Timestamp(updated.UpdatedAt));
        command.Parameters.AddWithValue("$legality", legality);
        command.Parameters.AddWithValue("$next", updated.Revision);
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$expected", record.Revision);
        if (working is not null && !restoreOriginal) command.Parameters.AddWithValue("$working", working);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("宝可梦已变化或不存在，请刷新后重试。");
        transaction.Commit();
        return updated;
    }

    /// <summary>Revisions bind the transaction to the exact records shown in the confirmation.</summary>
    public static void Release(string root, IReadOnlyList<StoredPokemon> records)
    {
        if (records.Count == 0 || records.Select(r => r.Id).Distinct().Count() != records.Count)
            throw new ArgumentException("放生名单为空或包含重复个体。", nameof(records));
        if (records.Any(r => !SamePath(r.DatabasePath, DatabasePath(root))))
            throw new InvalidDataException("放生名单包含其他仓库的个体。");
        using var connection = Open(root);
        using var transaction = connection.BeginTransaction();
        foreach (var record in records)
        {
            using var command = Command(connection, "DELETE FROM pokemon WHERE id=$id AND revision=$revision", transaction);
            command.Parameters.AddWithValue("$id", record.Id);
            command.Parameters.AddWithValue("$revision", record.Revision);
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("放生名单中的个体已变化或不存在；本次放生已回滚。");
        }
        transaction.Commit();
    }

    static void Insert(SqliteConnection connection, SqliteTransaction transaction, StoredPokemon record, byte[] original, byte[] working)
    {
        using var command = Command(connection, $"INSERT INTO pokemon ({Columns}, original, working) VALUES ($id,$species,$created,$updated,$legality,$parent,$revision,$original,$working)", transaction);
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$species", record.Species);
        command.Parameters.AddWithValue("$created", Timestamp(record.CreatedAt));
        command.Parameters.AddWithValue("$updated", Timestamp(record.UpdatedAt));
        command.Parameters.AddWithValue("$legality", record.LegalityStatus);
        command.Parameters.AddWithValue("$parent", (object?)record.ParentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$revision", record.Revision);
        command.Parameters.AddWithValue("$original", original);
        command.Parameters.AddWithValue("$working", working);
        command.ExecuteNonQuery();
    }

    // These are the old JSON fields, used only while importing. Never persist old absolute paths in SQLite.
    sealed record LegacyRecord(string Id, int Species, string OriginalPath, string WorkingPath, string ManifestPath,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string LegalityStatus, string? ParentId = null, long Revision = 1);

    static void Migrate(SqliteConnection connection, SqliteTransaction transaction, string root)
    {
        var manifests = Directory.EnumerateFiles(root, "record.json", SearchOption.AllDirectories).Order().ToArray();
        foreach (var path in manifests)
        {
            EnsureInsideRoot(root, path);
            var json = File.ReadAllBytes(path);
            var legacy = JsonSerializer.Deserialize<LegacyRecord>(json) ?? throw new InvalidDataException("旧仓库记录无法读取。");
            if (string.IsNullOrEmpty(legacy.Id) || legacy.Species <= 0 || legacy.Revision <= 0 || string.IsNullOrEmpty(legacy.LegalityStatus)
                || !SamePath(path, legacy.ManifestPath)) throw new InvalidDataException("旧仓库记录不完整或路径不一致。");
            var directory = Path.GetDirectoryName(path)!;
            foreach (var payloadPath in new[] { legacy.OriginalPath, legacy.WorkingPath })
            {
                EnsureInsideRoot(root, payloadPath);
                if (!SamePath(Path.GetDirectoryName(payloadPath)!, directory)) throw new InvalidDataException("旧仓库实体不在记录目录内。");
            }
            var original = File.ReadAllBytes(legacy.OriginalPath);
            var working = File.ReadAllBytes(legacy.WorkingPath);
            if (EntityFormat.GetFromBytes(original.ToArray()) is null || EntityFormat.GetFromBytes(working.ToArray()) is null)
                throw new InvalidDataException("旧仓库实体损坏；旧数据尚未删除。");
            var record = new StoredPokemon(legacy.Id, legacy.Species, DatabasePath(root), legacy.CreatedAt, legacy.UpdatedAt,
                legacy.LegalityStatus, legacy.ParentId, legacy.Revision);
            Insert(connection, transaction, record, original, working);
            // Verify actual database bytes and metadata before any source file can be deleted.
            using (var command = Command(connection, $"SELECT {Columns},original,working FROM pokemon WHERE id=$id", transaction))
            {
                command.Parameters.AddWithValue("$id", record.Id);
                using var reader = command.ExecuteReader();
                if (!reader.Read() || ReadRecord(reader, record.DatabasePath) != record
                    || !original.SequenceEqual((byte[])reader[7]) || !working.SequenceEqual((byte[])reader[8]))
                    throw new InvalidDataException("仓库迁移核验失败；旧数据尚未删除。");
            }
            foreach (var (file, bytes) in new[] { (path, json), (legacy.OriginalPath, original), (legacy.WorkingPath, working) })
                QueueCleanup(connection, transaction, root, file, bytes);
            foreach (var temporary in Directory.EnumerateFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(temporary);
                if (new[] { "record.json.", "original.pkm.", "working.pkm." }.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)
                    && Guid.TryParseExact(name[prefix.Length..^4], "N", out _)))
                    QueueCleanup(connection, transaction, root, temporary, File.ReadAllBytes(temporary));
            }
        }
        if (Convert.ToInt64(Scalar(connection, "SELECT count(*) FROM pokemon", transaction)) != manifests.Length)
            throw new InvalidDataException("仓库迁移数量不一致；旧数据尚未删除。");
        Execute(connection, "INSERT INTO warehouse_meta(key,value) VALUES('migrated','1')", transaction);
    }

    static void QueueCleanup(SqliteConnection connection, SqliteTransaction transaction, string root, string path, byte[] bytes)
    {
        EnsureInsideRoot(root, path);
        using var command = Command(connection, "INSERT INTO legacy_cleanup(path,hash) VALUES($path,$hash)", transaction);
        command.Parameters.AddWithValue("$path", Path.GetRelativePath(root, path));
        command.Parameters.AddWithValue("$hash", Convert.ToHexString(SHA256.HashData(bytes)));
        command.ExecuteNonQuery();
    }

    static void CleanupLegacy(SqliteConnection connection, string root)
    {
        // A durable cleanup list avoids reimport after an interrupted cleanup or later release.
        using var transaction = connection.BeginTransaction();
        var pending = new List<(string Relative, string Hash)>();
        using (var command = Command(connection, "SELECT path,hash FROM legacy_cleanup", transaction))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) pending.Add((reader.GetString(0), reader.GetString(1)));
        foreach (var (relative, hash) in pending)
        {
            var path = Path.GetFullPath(Path.Combine(root, relative));
            EnsureInsideRoot(root, path);
            if (File.Exists(path))
            {
                if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != hash)
                    throw new InvalidDataException("旧仓库文件在迁移后发生变化，已暂停清理和放生。");
                File.Delete(path);
            }
            using var command = Command(connection, "DELETE FROM legacy_cleanup WHERE path=$path", transaction);
            command.Parameters.AddWithValue("$path", relative);
            command.ExecuteNonQuery();
            var directory = Path.GetDirectoryName(path)!;
            if (!SamePath(directory, root) && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        transaction.Commit();
    }

    static void EnsureInsideRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, Path.GetFullPath(path));
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative == ".")
            throw new InvalidDataException("旧仓库路径超出仓库范围，已拒绝迁移或清理。");
        var cursor = Path.GetFullPath(path);
        while (!SamePath(cursor, root))
        {
            if ((File.Exists(cursor) || Directory.Exists(cursor)) && File.GetAttributes(cursor).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("旧仓库路径包含符号链接，已拒绝迁移或清理。");
            cursor = Path.GetDirectoryName(cursor)!;
        }
    }

    static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    static string Timestamp(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    static StoredPokemon ReadRecord(SqliteDataReader reader, string databasePath) => new(reader.GetString(0), reader.GetInt32(1), databasePath,
        DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture), DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
        reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt64(6));

    static SqliteCommand Command(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand(); command.CommandText = sql; command.Transaction = transaction; return command;
    }
    static void Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = Command(connection, sql, transaction); command.ExecuteNonQuery();
    }
    static object? Scalar(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = Command(connection, sql, transaction); return command.ExecuteScalar();
    }
}
