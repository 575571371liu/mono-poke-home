using MonoHome.Core.Repository;
using MonoHome.Core.Pokemon;
using MonoHome.Core.Transfers;
using MonoHome.Core.Saves;
using PKHeX.Core;
using Microsoft.Data.Sqlite;

namespace MonoHome.Verifier;

static class TradeEvolutionChecks
{
    public static void Run()
    {
        var kadabra3 = Sample(64, GameVersion.E);
        var kadabra4 = Sample(64, GameVersion.HG);
        CheckEvolution(kadabra3, GameVersion.E, 65);
        CheckEvolution(kadabra4, GameVersion.HG, 65);
        var japanese = Sample(64, GameVersion.E, 1);
        var japaneseResult = CheckEvolution(japanese, GameVersion.E, 65);
        Require(japaneseResult.Nickname == SpeciesName.GetSpeciesNameGeneration(65, 1, 3), "default nickname follows individual language");
        foreach (var game in new[] { GameVersion.R, GameVersion.S, GameVersion.E, GameVersion.FR, GameVersion.LG })
            CheckEvolution(kadabra3, game, 65);
        foreach (var game in new[] { GameVersion.D, GameVersion.P, GameVersion.Pt, GameVersion.HG, GameVersion.SS })
            CheckEvolution(kadabra4, game, 65);
        var shiny = kadabra3.Clone(); shiny.SID16 = (ushort)(shiny.TID16 ^ (shiny.PID >> 16) ^ (shiny.PID & 0xffff));
        Require(shiny.IsShiny && new LegalityAnalysis(shiny).Valid, "legal shiny fixture");
        CheckEvolution(shiny, GameVersion.E, 65);
        var injured = kadabra4.Clone(); injured.ResetPartyStats(); injured.Stat_HPCurrent = injured.Stat_HPMax - 10; injured.Status_Condition = 8;
        var injuredResult = CheckEvolution(injured, GameVersion.HG, 65);
        Require(injuredResult.Stat_HPMax - injuredResult.Stat_HPCurrent == 10 && injuredResult.Status_Condition == 8, "HP deficit and status survive stat recomputation");
        foreach (var species in new ushort[] { 67, 75, 93, 61, 79, 95, 123, 117, 137, 112, 125, 126, 233, 356 })
        {
            var pokemon = Sample(species, GameVersion.HG);
            var branches = TradeEvolution.Evaluate(pokemon, GameVersion.HG).Branches;
            Require(branches.Count > 0, "HG fixture has a trade branch");
            foreach (var branch in branches)
            {
                var source = pokemon.Clone();
                if (branch.Edge.Method.Method == EvolutionType.TradeHeldItem) source.HeldItem = branch.Edge.Method.Argument;
                CheckEvolution(source, GameVersion.HG, branch.Edge.Target.Species);
            }
        }
        foreach (var species in new ushort[] { 67, 75, 93, 61, 79, 95, 123, 117, 137 })
        {
            var origin = species switch { 93 or 61 or 95 or 123 or 137 => GameVersion.FR, 79 => GameVersion.LG, _ => GameVersion.E };
            var pokemon = Sample(species, origin);
            var branches = TradeEvolution.Evaluate(pokemon, GameVersion.E).Branches;
            Require(branches.Count > 0, "Gen3 fixture has a trade branch");
            foreach (var branch in branches)
            {
                var source = pokemon.Clone();
                if (branch.Edge.Method.Method == EvolutionType.TradeHeldItem) source.HeldItem = branch.Edge.Method.Argument;
                CheckEvolution(source, GameVersion.E, branch.Edge.Target.Species);
            }
        }
        var held3 = kadabra3.Clone(); held3.HeldItem = 195;
        Fails(() => TradeEvolution.Preview(held3, GameVersion.E, new(65, 0)), "Gen3 Everstone blocks Kadabra");
        var held4 = kadabra4.Clone(); held4.HeldItem = 229;
        Require(TradeEvolution.Preview(held4, GameVersion.HG, new(65, 0)).HeldItem == 229, "Gen4 Kadabra retains Everstone");
        var haunter = Sample(93, GameVersion.HG); haunter.HeldItem = 229;
        Fails(() => TradeEvolution.Preview(haunter, GameVersion.HG, new(94, 0)), "Everstone blocks Haunter");
        foreach (var game in new[] { GameVersion.E, GameVersion.HG })
        {
            var clamperl = Sample(366, game);
            var options = TradeEvolution.Evaluate(clamperl, game).Branches;
            Require(options.Count == 2 && options.All(b => !b.CanEvolve), "Clamperl needs one of two items");
            foreach (var branch in options)
            {
                var source = clamperl.Clone(); source.HeldItem = branch.Edge.Method.Argument;
                var evolved = CheckEvolution(source, game, branch.Edge.Target.Species);
                Require(evolved.HeldItem == 0, "evolution item consumed");
                var other = options.Single(b => b.Edge.Target != branch.Edge.Target);
                Fails(() => TradeEvolution.Preview(source, game, other.Edge.Target), "wrong branch rejected");
            }
        }
        var egg = kadabra3.Clone(); egg.IsEgg = true;
        Fails(() => TradeEvolution.Preview(egg, GameVersion.E, new(65, 0)), "egg rejected");
        var invalid = kadabra3.Clone(); invalid.Move1 = 999;
        Fails(() => TradeEvolution.Preview(invalid, GameVersion.E, new(65, 0)), "invalid source rejected");
        Fails(() => TradeEvolution.Preview(kadabra3, GameVersion.HG, new(65, 0)), "cross-context rejected");
        Fails(() => TradeEvolution.Preview(kadabra3, GameVersion.E, new(6, 0)), "arbitrary target rejected");
        Require(!TradeEvolution.Evaluate(new PK5 { Species = 64 }, GameVersion.B).CanUse, "unsupported format rejected");
        var named = kadabra3.Clone(); named.SetNickname("MONO"); named.Status_Condition = 8;
        named.ResetPartyStats(); named.Status_Condition = 8; named.Stat_HPCurrent = 0;
        var namedResult = CheckEvolution(named, GameVersion.E, 65);
        Require(namedResult.Nickname == "MONO" && namedResult.Stat_HPCurrent == 0 && namedResult.Status_Condition == 8, "nickname, fainting and status retained");
        var root = TestEnvironment.NewScratchDirectory("trade-evolution");
        var record = LocalRepository.Upload(kadabra3, root);
        var before = LocalRepository.ReadWorkingBytes(record);
        var original = LocalRepository.ReadOriginalBytes(record);
        var targetSave = SaveRegistry.Register(File.ReadAllBytes(TestEnvironment.HeartGoldSave), "heartgold.sav", TestEnvironment.NewScratchDirectory("evolution-target"));
        var cache = TestEnvironment.NewScratchDirectory("evolution-prepared");
        var beforePreparation = TargetPreparationService.Prepare(record, targetSave, TestEnvironment.HeartGoldSave, cache);
        Require(beforePreparation.IsCurrentFor(record, targetSave), "before-evolution preparation ready");
        _ = TradeEvolution.Preview(LocalRepository.LoadWorking(record), GameVersion.E, new(65, 0));
        Require(before.SequenceEqual(LocalRepository.ReadWorkingBytes(record)), "preview/cancel does not write");
        var updated = LocalRepository.ApplyTradeEvolution(record, GameVersion.E, new(65, 0));
        Require(!beforePreparation.IsCurrentFor(updated, targetSave), "evolution invalidates prior transfer preparation");
        var afterPreparation = TargetPreparationService.Prepare(updated, targetSave, TestEnvironment.HeartGoldSave, cache);
        Require(afterPreparation.IsCurrentFor(updated, targetSave) && afterPreparation.PreparedSavePath != beforePreparation.PreparedSavePath,
            "evolution creates a fresh target preparation");
        var preparedSave = SaveUtil.GetSaveFile(afterPreparation.PreparedSavePath!)!;
        Require(preparedSave.GetBoxSlotAtIndex(afterPreparation.DestinationSlot).Species == 65, "prepared target contains evolved species");
        Require(updated.Id == record.Id && updated.Revision == record.Revision + 1 && updated.LegalityStatus == "valid", "same record revision updated");
        Require(LocalRepository.List(root).Count == 1 && original.SequenceEqual(LocalRepository.ReadOriginalBytes(updated)), "count and original unchanged");
        var after = LocalRepository.ReadWorkingBytes(updated);
        Fails(() => LocalRepository.ApplyTradeEvolution(record, GameVersion.E, new(65, 0)), "stale revision rejected");
        Require(after.SequenceEqual(LocalRepository.ReadWorkingBytes(updated)), "rejected request leaves payload unchanged");
        var stale = LocalRepository.Upload(kadabra3, root);
        var edited = LocalRepository.SaveWorking(stale, kadabra3);
        var editedBytes = LocalRepository.ReadWorkingBytes(edited);
        Fails(() => LocalRepository.ApplyTradeEvolution(stale, GameVersion.E, new(65, 0)), "stale still-evolvable record rejected by revision");
        Require(editedBytes.SequenceEqual(LocalRepository.ReadWorkingBytes(edited)) && LocalRepository.List(root).Single(r => r.Id == stale.Id).Revision == edited.Revision, "stale request changes neither payload nor revision");
        using (var connection = new SqliteConnection($"Data Source={record.DatabasePath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER fail_evolution BEFORE UPDATE ON pokemon BEGIN SELECT RAISE(ABORT, 'injected update failure'); END;";
            command.ExecuteNonQuery();
            Fails(() => LocalRepository.ApplyTradeEvolution(edited, GameVersion.E, new(65, 0)), "SQL failure rejected");
            Require(editedBytes.SequenceEqual(LocalRepository.ReadWorkingBytes(edited)) && LocalRepository.List(root).Single(r => r.Id == stale.Id).Revision == edited.Revision, "SQL failure leaves payload and revision unchanged");
            command.CommandText = "DROP TRIGGER fail_evolution"; command.ExecuteNonQuery();
        }
        var output = TestEnvironment.ScratchFile("evolved-heartgold.sav");
        var report = EmeraldHgssTransfer.TransferStored(LocalRepository.LoadWorking(updated), TestEnvironment.HeartGoldSave, output);
        Require(report.Succeeded && report.Species == 65, "evolved record transfers using existing engine: " + report.Message);
        var deleted = LocalRepository.Upload(kadabra4, root); LocalRepository.Remove(deleted);
        Fails(() => LocalRepository.ApplyTradeEvolution(deleted, GameVersion.HG, new(65, 0)), "deleted record cannot return");
        var migrated = EntityConverter.ConvertToType(kadabra3, typeof(PK4), out _) ?? throw new InvalidOperationException("migration fixture failed");
        Require(TradeEvolution.DefaultGame(migrated).Context == migrated.Context, "origin version cannot select another format's rules");
        CheckEvolution(migrated, GameVersion.Pt, 65);
        Console.WriteLine("Trade evolution checks passed: rules, branches, identity, legality, payloads, revisions and transfer.");
    }

    public static void AndroidFixture(string root)
    {
        if (Directory.Exists(root)) throw new InvalidOperationException("Fixture root must be new.");
        foreach (var (species, game, item) in new (ushort, GameVersion, int)[]
        { (64, GameVersion.E, 0), (64, GameVersion.HG, 229), (93, GameVersion.HG, 229), (366, GameVersion.E, 0), (366, GameVersion.E, 192) })
        {
            var pokemon = Sample(species, game); pokemon.HeldItem = item;
            LocalRepository.Upload(pokemon, root);
        }
        Console.WriteLine("Created isolated legal trade evolution warehouse: " + root);
    }

    public static void CompareAndroid(string beforePath, string afterPath, int expectedChanges = 1)
    {
        static Dictionary<string, (long Revision, byte[] Original, byte[] Working)> Read(string path)
        {
            using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(path)};Mode=ReadOnly;Pooling=False");
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "SELECT id,revision,original,working FROM pokemon";
            using var reader = command.ExecuteReader();
            var rows = new Dictionary<string, (long, byte[], byte[])>();
            while (reader.Read()) rows.Add(reader.GetString(0), (reader.GetInt64(1), (byte[])reader[2], (byte[])reader[3]));
            return rows;
        }
        var before = Read(beforePath); var after = Read(afterPath);
        Require(before.Keys.Order().SequenceEqual(after.Keys.Order()), "Android evolution preserves IDs and count");
        var changes = 0;
        foreach (var (id, row) in before)
        {
            var next = after[id];
            Require(row.Original.SequenceEqual(next.Original), "Android original payload preserved");
            if (row.Working.SequenceEqual(next.Working)) { Require(row.Revision == next.Revision, "untouched revision"); continue; }
            changes++;
            Require(next.Revision == row.Revision + 1, "one Android commit only");
            var source = EntityFormat.GetFromBytes(row.Working)!; var result = EntityFormat.GetFromBytes(next.Working)!;
            TradeEvolution.VerifyPreserved(source, result);
            Require(new LegalityAnalysis(result).Valid, "Android persisted result legal");
            Console.WriteLine($"Android evolved #{source.Species} → #{result.Species}, revision {row.Revision} → {next.Revision}, item {source.HeldItem} → {result.HeldItem}.");
        }
        Require(changes == expectedChanges, "expected Android changed-record count");
        if (expectedChanges == 0)
        {
            static string Metadata(string path)
            {
                using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(path)};Mode=ReadOnly;Pooling=False");
                connection.Open(); using var command = connection.CreateCommand();
                command.CommandText = "SELECT id,species,created_at,updated_at,legality,parent_id,revision FROM pokemon ORDER BY id";
                using var reader = command.ExecuteReader(); var rows = new List<string>();
                while (reader.Read()) { var values = new object[7]; reader.GetValues(values); rows.Add(System.Text.Json.JsonSerializer.Serialize(values)); }
                return string.Join("\n", rows);
            }
            Require(Metadata(beforePath) == Metadata(afterPath), "failed Android transaction preserves every metadata field");
            Console.WriteLine("Android failed transaction preserved all records, revisions, metadata and original/working payloads.");
        }
    }

    internal static PKM Sample(ushort species, GameVersion game, int language = 2)
    {
        PKM seed = game.Context == EntityContext.Gen3 ? new PK3() : new PK4();
        seed.Species = species; seed.Version = game;
        var trainer = new SimpleTrainerInfo(game) { OT = language == 1 ? "テスト" : "MONO", TID16 = 12345, SID16 = 54321, Language = language };
        var baseSpecies = species switch { 64 => (ushort)63, 67 => (ushort)66, 75 => (ushort)74, 61 => (ushort)60,
            93 => (ushort)92, 117 => (ushort)116, 112 => (ushort)111, 233 => (ushort)137, 356 => (ushort)355, _ => species };
        var chain = new[] { new EvoCriteria { Species = species, LevelMin = 1, LevelMax = 100 }, new EvoCriteria { Species = baseSpecies, LevelMin = 1, LevelMax = 100 } };
        var encounters = EncounterGenerator.GetGenerator(game, game.Generation).GetPossible(seed, chain, game, EncounterTypeGroup.Slot | EncounterTypeGroup.Static);
        var failure = "No matching encounter.";
        foreach (var encounter in encounters.OfType<IEncounterConvertible>())
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var pokemon = encounter.ConvertToPKM(trainer);
                if (pokemon.Species != species)
                {
                    pokemon.CurrentLevel = 55; pokemon.Species = species;
                    pokemon.RefreshAbility(pokemon.AbilityNumber == 2 ? 1 : 0); pokemon.ClearNickname(); pokemon.RefreshChecksum();
                }
                var analysis = new LegalityAnalysis(pokemon);
                if (pokemon.Species == species && analysis.Valid) return pokemon;
                failure = analysis.Report();
            }
        throw new InvalidOperationException($"No legal fixture for {species} / {game}, language {language}: {failure}");
    }

    static PKM CheckEvolution(PKM source, GameVersion game, ushort target)
    {
        var before = source.Data.ToArray();
        var next = TradeEvolution.Preview(source, game, new(target, 0));
        Require(next.Species == target && new LegalityAnalysis(next).Valid, "legal evolved species");
        Require(before.SequenceEqual(source.Data), "source not mutated");
        Require(next.PID == source.PID && next.ID32 == source.ID32 && next.EXP == source.EXP && next.IsShiny == source.IsShiny
            && next.Nature == source.Nature && next.Gender == source.Gender && next.CurrentLevel == source.CurrentLevel
            && next.OriginalTrainerName == source.OriginalTrainerName && next.Version == source.Version
            && next.MetLocation == source.MetLocation && next.OriginalTrainerFriendship == source.OriginalTrainerFriendship
            && next.Move1 == source.Move1 && next.Move2 == source.Move2 && next.Move3 == source.Move3 && next.Move4 == source.Move4,
            "identity, origin, experience and moves retained");
        return next;
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Fails(Action action, string message)
    {
        try { action(); } catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or SqliteException) { return; }
        throw new InvalidOperationException(message);
    }
}
