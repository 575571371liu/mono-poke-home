using MonoHome.Core.Repository;
using MonoHome.Core.Saves;
using MonoHome.Core.Transfers;
using Microsoft.Data.Sqlite;
using PKHeX.Core;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace MonoHome.Verifier;

static class WarehouseReleaseChecks
{
    public static void AndroidFixture(string root, string deviceRoot)
    {
        root = Path.GetFullPath(root);
        Require(!Directory.Exists(root), "fixture destination must be new");
        for (var i = 1; i <= 34; i++)
        {
            var pokemon = Pokemon(i + 4);
            pokemon.Species = (ushort)(i % 2 == 0 ? 25 : 41);
            var manifest = Legacy(root, i.ToString("D32"), pokemon, pokemon);
            var json = File.ReadAllText(manifest);
            var escapedRoot = JsonSerializer.Serialize(Path.GetFullPath(root))[1..^1];
            json = json.Replace(escapedRoot, deviceRoot).Replace("\\\\", "/");
            File.WriteAllText(manifest, json);
        }
        Console.WriteLine("Created 34 synthetic legacy records for isolated Android acceptance.");
    }

    public static void Run()
    {
        CheckStorageAndEditing();
        CheckMigration();
        CheckMigrationFailures();
        CheckRelease();
        CheckSqlFailure();
        CheckCrashRecovery();
        CheckTransferRetry();
        Console.WriteLine("Warehouse release checks passed: storage, migration, rollback, stale confirmation, real process termination.");
    }

    static PK3 Pokemon(int level = 5) => new() { Species = 41, Version = GameVersion.E, CurrentLevel = (byte)level, OriginalTrainerName = "TEST", Nickname = "ZUBAT", PID = 12345, IV_HP = 31, EV_HP = 20 };
    static string Root(string label) => TestEnvironment.NewScratchDirectory("release-" + label);
    static string Database(string root) => Path.Combine(root, "warehouse.sqlite3");
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    static void Fails(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or SqliteException or JsonException or ArgumentException) { return; }
        throw new InvalidOperationException(message);
    }
    static string BytesHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static string Snapshot(string root) => string.Join("\n", LocalRepository.List(root).OrderBy(r => r.Id)
        .Select(r => JsonSerializer.Serialize(r) + BytesHash(LocalRepository.ReadOriginalBytes(r)) + BytesHash(LocalRepository.ReadWorkingBytes(r))));
    static SqliteConnection Connection(string root)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database(root), Pooling = false }.ToString());
        connection.Open(); return connection;
    }
    static void Sql(string root, string sql)
    {
        using var connection = Connection(root);
        using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    static long Count(string root, string table = "pokemon")
    {
        using var connection = Connection(root);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$name";
        command.Parameters.AddWithValue("$name", table);
        if ((long)command.ExecuteScalar()! == 0) return 0;
        command.CommandText = "SELECT count(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }

    static void CheckStorageAndEditing()
    {
        var root = Root("storage");
        var source = Pokemon();
        source.RefreshChecksum();
        var before = source.Data.ToArray();
        var record = LocalRepository.Upload(source, root);
        Require(File.Exists(Database(root)), "metadata and both payloads must live in SQLite");
        Require(!Directory.EnumerateFiles(root, "*.pkm", SearchOption.AllDirectories).Any(), "upload creates no extra entity files");
        Require(LocalRepository.ReadOriginalBytes(record).SequenceEqual(LocalRepository.ReadWorkingBytes(record)), "upload original and working bytes identical");
        Require(source.Data.SequenceEqual(before), "upload does not mutate source");
        Require(LocalRepository.LoadWorking(record).Data.SequenceEqual(source.Data), "party data round-trip byte identity");
        var originalHash = BytesHash(LocalRepository.ReadOriginalBytes(record));
        var edit = LocalRepository.ApplyEdit(source, new WorkingEdit("EDIT", 12, null, null, null, null));
        var saved = LocalRepository.SaveWorking(record, edit);
        Require(saved.Revision == record.Revision + 1 && LocalRepository.LoadWorking(saved).Nickname == "EDIT", "edit and revision committed together");
        var snapshot = Snapshot(root);
        Fails(() => LocalRepository.SaveWorking(record, source), "stale edit must fail");
        Require(Snapshot(root) == snapshot, "stale edit leaves all bytes and metadata unchanged");
        var discarded = LocalRepository.DiscardEdits(saved);
        Require(LocalRepository.ReadWorkingBytes(discarded).SequenceEqual(LocalRepository.ReadOriginalBytes(discarded)), "discard restores exact original bytes");
        Require(discarded.Revision == saved.Revision + 1, "discard invalidates old confirmations");
        var checkedRecord = LocalRepository.SetLegality(discarded, "invalid");
        Require(checkedRecord.Revision == discarded.Revision + 1, "legality changes invalidate confirmation");
        Require(BytesHash(LocalRepository.ReadOriginalBytes(checkedRecord)) == originalHash, "all edits preserve original");
        Require(Count(root) == 1, "one row contains metadata and both payloads");
        Console.WriteLine("PASS: SQLite payload identity, original immutable, edits atomic, stale revisions rejected.");
    }

    static string Legacy(string root, string id, PKM original, PKM working, string? parent = null, long revision = 7)
    {
        var directory = Path.Combine(root, id); Directory.CreateDirectory(directory);
        var originalPath = Path.Combine(directory, "original.pkm");
        var workingPath = Path.Combine(directory, "working.pkm");
        var manifest = Path.Combine(directory, "record.json");
        File.WriteAllBytes(originalPath, Serialize(original)); File.WriteAllBytes(workingPath, Serialize(working));
        File.WriteAllText(manifest, JsonSerializer.Serialize(new
        {
            Id = id, Species = (int)working.Species, OriginalPath = originalPath, WorkingPath = workingPath, ManifestPath = manifest,
            CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00+08:00"), UpdatedAt = DateTimeOffset.Parse("2026-10-04T00:00:00+08:00"),
            LegalityStatus = "invalid", ParentId = parent, Revision = revision,
        }));
        return manifest;
    }
    static byte[] Serialize(PKM entity)
    {
        var bytes = new byte[entity.SIZE_PARTY]; entity.WriteEncryptedDataParty(bytes); return bytes;
    }

    static void CheckMigration()
    {
        var root = Root("migration");
        var source = Pokemon(); var edited = Pokemon(42); edited.Nickname = "EDIT";
        var firstId = Guid.NewGuid().ToString("N"); var secondId = Guid.NewGuid().ToString("N");
        var manifest = Legacy(root, firstId, source, edited);
        Legacy(root, secondId, source, source, firstId, 3);
        var original = File.ReadAllBytes(Path.Combine(root, firstId, "original.pkm"));
        var working = File.ReadAllBytes(Path.Combine(root, firstId, "working.pkm"));
        File.WriteAllBytes(Path.Combine(root, firstId, "working.pkm." + Guid.NewGuid().ToString("N") + ".tmp"), working);
        File.WriteAllText(Path.Combine(root, "unrelated.txt"), "KEEP");
        var records = LocalRepository.List(root);
        var first = records.Single(r => r.Id == firstId); var second = records.Single(r => r.Id == secondId);
        Require(records.Count == 2 && first.Revision == 7 && second.ParentId == firstId, "migration preserves count, ID, revision and lineage");
        Require(first.CreatedAt == DateTimeOffset.Parse("2026-01-01T00:00:00+08:00") && first.LegalityStatus == "invalid", "migration preserves dates and legality");
        Require(LocalRepository.ReadOriginalBytes(first).SequenceEqual(original) && LocalRepository.ReadWorkingBytes(first).SequenceEqual(working), "migration preserves both payloads byte-for-byte");
        Require(!File.Exists(manifest) && !Directory.Exists(Path.Combine(root, firstId)), "verified legacy payloads and interrupted-write leftovers cleared");
        Require(File.ReadAllText(Path.Combine(root, "unrelated.txt")) == "KEEP", "migration leaves unrelated files alone");
        Require(Count(root, "legacy_cleanup") == 0 && LocalRepository.List(root).Count == 2, "repeat opening never duplicates migration");
        LocalRepository.Release(root, records);
        Require(LocalRepository.List(root).Count == 0 && Count(root) == 0, "released migrated records never resurrect");
        Require(!Directory.EnumerateFiles(root, "*.pkm", SearchOption.AllDirectories).Any(), "no backup entities survive release");
        Console.WriteLine("PASS: legacy migration count/metadata/two payloads exact; old copies cleared; no reimport after release.");
    }

    static void CheckMigrationFailures()
    {
        var root = Root("bad-migration");
        var first = Legacy(root, "first", Pokemon(), Pokemon());
        var second = Legacy(root, "second", Pokemon(), Pokemon());
        var workingPath = Path.Combine(root, "second", "working.pkm");
        var correct = File.ReadAllBytes(workingPath); File.WriteAllBytes(workingPath, [1, 2, 3]);
        Fails(() => LocalRepository.List(root), "corrupt migration must fail");
        Require(File.Exists(first) && File.Exists(second) && Count(root) == 0, "migration failure keeps every source and rolls back imported rows");
        File.WriteAllBytes(workingPath, correct);
        Require(LocalRepository.List(root).Count == 2, "migration can retry after fixing source");

        var escaped = Root("escaped"); var manifest = Legacy(escaped, "one", Pokemon(), Pokemon());
        var outside = Path.Combine(Root("outside"), "sentinel.pkm"); File.WriteAllBytes(outside, Serialize(Pokemon()));
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(manifest))!;
        json["OriginalPath"] = JsonSerializer.SerializeToElement(outside); File.WriteAllText(manifest, JsonSerializer.Serialize(json));
        var outsideHash = BytesHash(File.ReadAllBytes(outside));
        Fails(() => LocalRepository.List(escaped), "outside path migration must fail");
        Require(File.Exists(manifest) && BytesHash(File.ReadAllBytes(outside)) == outsideHash && Count(escaped) == 0, "outside file untouched and all migration rolled back");

        if (OperatingSystem.IsWindows())
        {
            var locked = Root("cleanup-retry"); var id = "locked"; var legacy = Legacy(locked, id, Pokemon(), Pokemon());
            var originalPath = Path.Combine(locked, id, "original.pkm");
            // Reads are allowed for migration but deletion is blocked: a real filesystem fault.
            using (var handle = new FileStream(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Fails(() => LocalRepository.List(locked), "cleanup lock must prevent warehouse access");
                Require(Count(locked) == 1 && Count(locked, "legacy_cleanup") > 0, "verified data committed, resumable cleanup retained");
                var record = new StoredPokemon(id, 41, Database(locked), DateTimeOffset.MinValue, DateTimeOffset.MinValue, "invalid", Revision: 7);
                Fails(() => LocalRepository.Release(locked, [record]), "release blocked until old copy cleared");
                Require(Count(locked) == 1 && File.Exists(originalPath), "failed cleanup cannot release data while backup remains");
            }
            Require(LocalRepository.List(locked).Count == 1 && Count(locked, "legacy_cleanup") == 0 && !File.Exists(originalPath), "cleanup resumes after partial cleanup failure");
        }
        Console.WriteLine("PASS: corrupt/outside legacy sources rejected atomically; filesystem cleanup failure blocks release and resumes safely.");
    }

    static void CheckRelease()
    {
        var root = Root("atomic");
        var first = LocalRepository.Upload(Pokemon(), root); var second = LocalRepository.Upload(Pokemon(), root); var third = LocalRepository.Upload(Pokemon(), root);
        var baseline = Snapshot(root);
        var edited = LocalRepository.SaveWorking(second, Pokemon(10)); var afterEdit = Snapshot(root);
        Fails(() => LocalRepository.Release(root, [first, second]), "stale confirmation must fail whole batch");
        Require(Snapshot(root) == afterEdit && Count(root) == 3, "failure on second item rolls back first DELETE too");
        var missing = second with { Id = "missing" };
        Fails(() => LocalRepository.Release(root, [first, missing]), "missing confirmed ID must fail whole batch");
        Require(Snapshot(root) == afterEdit, "missing ID rollback retains every payload and metadata");
        Fails(() => LocalRepository.Release(root, [first, first]), "duplicate IDs must be rejected");
        Fails(() => LocalRepository.Release(root, []), "empty batch must be rejected");
        var foreign = LocalRepository.Upload(Pokemon(), Root("foreign"));
        Fails(() => LocalRepository.Release(root, [first, foreign]), "cross-warehouse release must fail");
        Require(Snapshot(root) == afterEdit, "invalid batches do not change warehouse");
        LocalRepository.Release(root, [first]);
        Require(LocalRepository.List(root).Select(r => r.Id).ToHashSet().SetEquals([second.Id, third.Id]), "single release uses ID and preserves same-species siblings");
        Fails(() => LocalRepository.ReadOriginalBytes(first), "released original payload cannot be read");
        Fails(() => LocalRepository.ReadWorkingBytes(first), "released working payload cannot be read");
        LocalRepository.Release(root, [edited, third]);
        Require(LocalRepository.List(root).Count == 0 && Count(root) == 0, "bulk release clears metadata and both payloads together");
        Require(!File.Exists(Database(root) + "-journal") && !Directory.EnumerateFiles(root, "*.pkm", SearchOption.AllDirectories).Any(), "successful release retains no entity files or rollback journal");
        Require(baseline.Length > 0, "baseline captured before mutation");
        Console.WriteLine("PASS: single/bulk/empty warehouse; exact ID targeting; stale/missing/duplicate/foreign selection rolls back whole batch.");
    }

    static void CheckSqlFailure()
    {
        var root = Root("sql-failure"); var first = LocalRepository.Upload(Pokemon(), root); var second = LocalRepository.Upload(Pokemon(), root);
        var baseline = Snapshot(root);
        Sql(root, $"CREATE TRIGGER fail_release BEFORE DELETE ON pokemon WHEN old.id='{second.Id}' BEGIN SELECT RAISE(ABORT,'forced delete failure'); END;");
        Fails(() => LocalRepository.Release(root, [first, second]), "real database trigger failure must abort batch");
        Require(Snapshot(root) == baseline && Count(root) == 2, "SQL failure after first deletion rolls back all metadata and payload bytes");
        Sql(root, "DROP TRIGGER fail_release");
        LocalRepository.Release(root, [first, second]);
        Require(LocalRepository.List(root).Count == 0, "release retries successfully after transient database failure");
        Console.WriteLine("PASS: real SQLite failure on second DELETE rolls back first; retry succeeds.");
    }

    static void CheckTransferRetry()
    {
        var root = Root("transfer-retry");
        var source = TestEnvironment.EmeraldSave;
        var target = TestEnvironment.HeartGoldSave;
        var targetHash = BytesHash(File.ReadAllBytes(target));
        var records = BoxReader.Read(source).Take(2)
            .Select(slot => LocalRepository.Upload(BoxReader.ReadPokemon(source, slot), root)).ToArray();
        var output = Path.Combine(root, "retry.sav");
        var failed = EmeraldHgssTransfer.TransferStoredMany(records.Select(LocalRepository.LoadWorking).ToArray(), target, output, destinationSlot: int.MaxValue);
        Require(!failed.Succeeded, "invalid destination must fail before retry");
        foreach (var record in records) LocalRepository.SetLegality(record, "invalid");
        Fails(() => LocalRepository.Remove(records[0]), "failed-transfer metadata update invalidates stale records");
        var selectedIds = records.Select(r => r.Id).ToHashSet();
        var refreshed = LocalRepository.List(root).Where(r => selectedIds.Contains(r.Id)).ToArray();
        Require(refreshed.Length == 2 && refreshed.All(r => r.Revision == 2), "refresh preserves selected IDs and adopts new revisions");
        var retry = EmeraldHgssTransfer.TransferStoredMany(refreshed.Select(LocalRepository.LoadWorking).ToArray(), target, output);
        Require(retry.Succeeded, "real failed transfer retries successfully after refresh: " + retry.Message);
        foreach (var record in refreshed) LocalRepository.Remove(record);
        Require(LocalRepository.List(root).Count == 0, "successful retry removes warehouse records without stale revision failure");
        Require(BytesHash(File.ReadAllBytes(target)) == targetHash, "staged retry leaves source target fixture untouched");
        Console.WriteLine("PASS: real failed batch download, legality revision update, refresh and successful retry removal.");
    }

    public static void Child(string root, string mode)
    {
        if (mode == "before")
        {
            var records = LocalRepository.List(root).OrderBy(r => r.Id).ToArray();
            Sql(root, $"CREATE TRIGGER hold_release AFTER DELETE ON pokemon WHEN old.id='{records[0].Id}' BEGIN SELECT sum(n) FROM (WITH RECURSIVE counter(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM counter WHERE n<1000000000) SELECT n FROM counter); END;");
            File.WriteAllText(Path.Combine(root, "child.ready"), "release");
            LocalRepository.Release(root, records);
        }
        else if (mode == "after")
        {
            LocalRepository.Release(root, LocalRepository.List(root));
            File.WriteAllText(Path.Combine(root, "child.ready"), "committed");
            Thread.Sleep(Timeout.Infinite);
        }
        else if (mode == "migration-before")
        {
            File.WriteAllText(Path.Combine(root, "child.ready"), "migration");
            LocalRepository.List(root);
        }
        else throw new ArgumentException("Unknown child mode.");
    }

    static Process StartChild(string root, string mode)
    {
        var executable = Environment.ProcessPath!;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (var value in new[] { "--warehouse-crash-child", root, mode }) start.ArgumentList.Add(value);
        return Process.Start(start)!;
    }
    static void KillAtCheckpoint(string root, string mode, bool journal)
    {
        using var process = StartChild(root, mode);
        try
        {
            var timer = Stopwatch.StartNew();
            var marker = Path.Combine(root, "child.ready"); var journalPath = Database(root) + "-journal";
            while (!File.Exists(marker) || journal && (!File.Exists(journalPath) || new FileInfo(journalPath).Length <= 512))
            {
                if (process.HasExited) throw new InvalidOperationException("Crash child exited early: " + process.StandardError.ReadToEnd());
                if (timer.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Crash checkpoint not reached.");
                Thread.Sleep(20);
            }
            Thread.Sleep(250);
            Require(!process.HasExited, "child must still be inside uncommitted transaction or post-commit hold");
            if (journal) Require(File.Exists(journalPath), "hot journal must exist at forced termination");
            process.Kill(entireProcessTree: true);
            Require(process.WaitForExit(10000), "child terminated without graceful transaction disposal");
        }
        finally { if (!process.HasExited) { process.Kill(true); process.WaitForExit(); } }
    }

    static void CheckCrashRecovery()
    {
        var before = Root("kill-before"); LocalRepository.Upload(Pokemon(), before); LocalRepository.Upload(Pokemon(8), before);
        var baseline = Snapshot(before);
        KillAtCheckpoint(before, "before", journal: true);
        Require(Snapshot(before) == baseline && Count(before) == 2, "real process death before COMMIT restores every byte and metadata");
        Sql(before, "DROP TRIGGER hold_release");
        var after = Root("kill-after"); LocalRepository.Upload(Pokemon(), after); LocalRepository.Upload(Pokemon(8), after);
        KillAtCheckpoint(after, "after", journal: false);
        Require(LocalRepository.List(after).Count == 0 && Count(after) == 0, "real process death after COMMIT keeps all releases durable");

        var migration = Root("kill-migration"); var manifest = Legacy(migration, "legacy", Pokemon(), Pokemon(9));
        // Set up the actual schema, but not the migration marker, to block the first INSERT after it writes a journal.
        Sql(migration, """
            CREATE TABLE pokemon(id TEXT PRIMARY KEY, species INTEGER, created_at TEXT, updated_at TEXT, legality TEXT, parent_id TEXT, revision INTEGER, original BLOB, working BLOB);
            CREATE TRIGGER hold_migration AFTER INSERT ON pokemon BEGIN SELECT sum(n) FROM (WITH RECURSIVE counter(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM counter WHERE n<1000000000) SELECT n FROM counter); END;
            """);
        KillAtCheckpoint(migration, "migration-before", journal: true);
        Require(File.Exists(manifest) && Count(migration) == 0, "migration death before commit retains original source and rolls back import");
        Sql(migration, "DROP TRIGGER hold_migration");
        Require(LocalRepository.List(migration).Count == 1 && !File.Exists(manifest), "migration restarts and finishes after interrupted import");
        Console.WriteLine("PASS: forcibly killed real processes before/after release COMMIT and during migration; restart proves atomic recovery.");
    }
}
