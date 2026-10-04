using System.Security.Cryptography;
using System.Text.Json;
using MonoHome.Core.Repository;
using MonoHome.Core.Saves;
using PKHeX.Core;

namespace MonoHome.Verifier;

static class FixedFieldChecks
{
    public static void CheckEdited(string path, string root)
    {
        var source = EntityFormat.GetFromBytes(File.ReadAllBytes(path))!;
        var input = source.Data.ToArray();
        var outcome = LocalRepository.RepairPreservingFields(source);
        if (!outcome.Valid || !new LegalityAnalysis(outcome.Pokemon).Valid)
            throw new InvalidOperationException(outcome.FailureReason);
        // Gen4 ability slot is derived from PID, while the actual ability ID must remain identical.
        var differences = Differences(Snapshot(source), Snapshot(outcome.Pokemon)).Where(key => key != "AbilitySlot").ToArray();
        var restored = outcome.Pokemon.Clone();
        restored.PID = source.PID;
        restored.EXP = source.EXP;
        if (differences.Length != 0 || !restored.Data.SequenceEqual(input) || !source.Data.SequenceEqual(input))
            throw new InvalidOperationException("Edited repair changed fields outside PID/EXP or mutated input: " + string.Join(",", differences));
        var stored = LocalRepository.Upload(source, root);
        var originalHash = SHA256.HashData(File.ReadAllBytes(stored.OriginalPath));
        var reloaded = LocalRepository.LoadWorking(LocalRepository.SaveWorking(stored, outcome.Pokemon));
        if (!reloaded.Data.SequenceEqual(outcome.Pokemon.Data) || !new LegalityAnalysis(reloaded).Valid
            || !originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(stored.OriginalPath))))
            throw new InvalidOperationException("Edited repair failed persistence or changed backup.");
        var impossible = source.Clone();
        impossible.Move1 = (ushort)(impossible.MaxMoveID + 1);
        var rejected = LocalRepository.RepairPreservingFields(impossible);
        if (rejected.Valid || !rejected.Pokemon.Data.SequenceEqual(impossible.Data))
            throw new InvalidOperationException("Impossible move was accepted or rejection changed data.");
        Console.WriteLine($"PASS: reported edited entity legal; PID {source.PID:X8}→{outcome.Pokemon.PID:X8}, EXP {source.EXP}→{outcome.Pokemon.EXP}; all user fields and bytes outside PID/EXP preserved; persisted legal; backup unchanged; impossible edit rejected unchanged.");
    }

    public static void CompareSaved(string beforePath, string afterPath, string evidencePath)
    {
        var before = EntityFormat.GetFromBytes(File.ReadAllBytes(beforePath))!;
        var after = EntityFormat.GetFromBytes(File.ReadAllBytes(afterPath))!;
        var differences = Differences(Snapshot(before), Snapshot(after));
        var unchangedOutsideNickname = before.Data[..8].SequenceEqual(after.Data[..8]) && before.Data[18..].SequenceEqual(after.Data[18..]);
        var legal = new LegalityAnalysis(after).Valid;
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(new { differences, unchangedOutsideNickname, legal, before = Snapshot(before), after = Snapshot(after) }, new JsonSerializerOptions { WriteIndented = true }));
        if (differences.Length != 0 || !unchangedOutsideNickname || !legal)
            throw new InvalidOperationException("Emulator result did not preserve fixed fields and legality.");
        Console.WriteLine("PASS: emulator update preserves all locked fields; only nickname buffer changed; persisted result legal.");
    }

    public static void Run(Func<PKM, RepairOutcome> repair, string root, params string[] saves)
    {
        Directory.CreateDirectory(root);
        var rows = new List<object>();
        var errors = new List<string>();
        foreach (var path in saves)
        {
            var fileHash = SHA256.HashData(File.ReadAllBytes(path));
            foreach (var slot in BoxReader.Read(path))
            {
                var source = BoxReader.ReadPokemon(path, slot);
                var label = $"{Path.GetFileName(path)} {slot.Location} {slot.Box + 1}-{slot.Slot + 1} #{source.Species}";
                var input = source.Data.ToArray();
                var unchangedEdit = LocalRepository.ApplyEdit(source, new WorkingEdit(source.Nickname, source.CurrentLevel, null, null, source.PokerusStrain, source.PokerusDays,
                    [source.Move1, source.Move2, source.Move3, source.Move4],
                    [source.IV_HP, source.IV_ATK, source.IV_DEF, source.IV_SPE, source.IV_SPA, source.IV_SPD],
                    [source.EV_HP, source.EV_ATK, source.EV_DEF, source.EV_SPE, source.EV_SPA, source.EV_SPD]));
                if (!source.Data.SequenceEqual(unchangedEdit.Data)) errors.Add(label + ": unchanged editor submission modified entity bytes");
                var outcome = repair(source);
                var before = Snapshot(source);
                var after = Snapshot(outcome.Pokemon);
                var differences = Differences(before, after);
                var npc = new LegalityAnalysis(source).EncounterMatch as EncounterTrade4PID;
                var npcNameException = npc is { IsLanguageSwap: false } && source.Version is GameVersion.HG or GameVersion.SS
                    && source.ID32 == npc.ID32 && source.PID == npc.PID;
                var lockedDifferences = differences.Where(key => !npcNameException || key is not ("TrainerName" or "TrainerBytes")).ToArray();
                if (!source.Data.SequenceEqual(input)) errors.Add(label + ": mutated input");
                if (lockedDifferences.Length != 0) errors.Add(label + ": changed locked fields " + string.Join(", ", lockedDifferences));
                var accepted = outcome.Valid && new LegalityAnalysis(outcome.Pokemon).Valid;
                if (npcNameException && !accepted) errors.Add(label + ": NPC name exception must repair known fixture");
                if (new LegalityAnalysis(source).Valid && !accepted) errors.Add(label + ": rejected an already legal entity");
                if (outcome.Valid && !accepted) errors.Add(label + ": accepted illegal result");
                if (!outcome.Valid && !outcome.Pokemon.Data.SequenceEqual(input)) errors.Add(label + ": changed data on failure");
                if (source is PK3 && accepted && (!input.AsSpan(0, 8).SequenceEqual(outcome.Pokemon.Data[..8]) || !input.AsSpan(18).SequenceEqual(outcome.Pokemon.Data[18..])))
                    errors.Add(label + ": changed Gen3 data outside nickname buffer");
                if (source is PK4 && accepted)
                {
                    var restored = outcome.Pokemon.Clone();
                    source.NicknameTrash.CopyTo(restored.NicknameTrash);
                    restored.IsNicknamed = source.IsNicknamed;
                    if (npcNameException) source.OriginalTrainerTrash.CopyTo(restored.OriginalTrainerTrash);
                    if (!source.Data.SequenceEqual(restored.Data)) errors.Add(label + ": changed Gen4 data outside nickname and flag");
                    var expectedNickname = npc is null ? SpeciesName.GetSpeciesNameGeneration(source.Species, source.Language, source.Generation) : npc.GetNickname(source.Language);
                    if (!new LegalityAnalysis(source).Valid && outcome.Pokemon.Nickname != expectedNickname)
                        errors.Add(label + ": incorrect localized species name");
                    if (npcNameException && !npc!.IsTrainerMatch(outcome.Pokemon, outcome.Pokemon.OriginalTrainerName, source.Language))
                        errors.Add(label + ": incorrect NPC trainer name");
                }
                var stored = LocalRepository.Upload(source, Path.Combine(root, "warehouse"));
                var originalHash = SHA256.HashData(File.ReadAllBytes(stored.OriginalPath));
                var saved = LocalRepository.SaveWorking(stored, outcome.Pokemon);
                var reloaded = LocalRepository.LoadWorking(saved);
                var persistedDifferences = Differences(after, Snapshot(reloaded));
                if (persistedDifferences.Length != 0) errors.Add(label + ": persistence changed " + string.Join(", ", persistedDifferences));
                if (outcome.Valid && !new LegalityAnalysis(reloaded).Valid) errors.Add(label + ": persisted result illegal");
                if (!originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(stored.OriginalPath)))) errors.Add(label + ": rewrote original backup");
                rows.Add(new { label, originalLegal = new LegalityAnalysis(source).Valid, accepted, differences, persistedDifferences, before, after, persisted = Snapshot(reloaded), outcome.Changes, outcome.FailureReason });
            }
            if (!fileHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)))) errors.Add(path + ": source save modified");
        }
        var legalSource = BoxReader.Read(saves[0]).Select(slot => BoxReader.ReadPokemon(saves[0], slot)).First(p => new LegalityAnalysis(p).Valid);
        // The exact reported Cascoon is a known invalid-but-repairable positive control.
        var repairable = BoxReader.ReadPokemon(saves[0], BoxReader.Read(saves[0]).First(slot => slot.Location == "Box" && slot.Box == 0 && slot.Slot == 0));
        var repairableBytes = repairable.Data.ToArray();
        var unchangedLevel = LocalRepository.ApplyEdit(repairable, new WorkingEdit(null, repairable.CurrentLevel, null, null, null, null));
        if (!unchangedLevel.Data.SequenceEqual(repairableBytes)) errors.Add("unchanged editor level: modified entity bytes");
        rows.Add(new { label = "unchanged editor level", bytesUnchanged = unchangedLevel.Data.SequenceEqual(repairableBytes) });
        var repaired = repair(repairable);
        if (new LegalityAnalysis(repairable).Valid || !repaired.Valid || !new LegalityAnalysis(repaired.Pokemon).Valid)
            errors.Add("nickname positive control: known repairable data must become legal");
        if (!repairable.Data.SequenceEqual(repairableBytes) || Differences(Snapshot(repairable), Snapshot(repaired.Pokemon)).Length != 0)
            errors.Add("nickname positive control: protected data changed");
        rows.Add(new { label = "nickname positive control", beforeReport = new LegalityAnalysis(repairable).Report(), afterReport = new LegalityAnalysis(repaired.Pokemon).Report(), repaired.Valid, inputUnchanged = repairable.Data.SequenceEqual(repairableBytes), differences = Differences(Snapshot(repairable), Snapshot(repaired.Pokemon)) });
        foreach (var test in new[] { "impossible-move", "impossible-PP" })
        {
            var conflict = legalSource.Clone();
            if (test == "impossible-move") conflict.Move1 = ushort.MaxValue;
            else conflict.Move1_PP = byte.MaxValue;
            var original = conflict.Data.ToArray();
            if (new LegalityAnalysis(conflict).Valid) errors.Add(test + ": invalid negative control");
            var outcome = repair(conflict);
            if (outcome.Valid) errors.Add(test + ": accepted a conflicting locked field");
            if (!original.AsSpan().SequenceEqual(conflict.Data) || !original.AsSpan().SequenceEqual(outcome.Pokemon.Data)) errors.Add(test + ": changed rejected data");
            rows.Add(new { label = test, outcome.Valid, inputUnchanged = original.AsSpan().SequenceEqual(conflict.Data), outputUnchanged = original.AsSpan().SequenceEqual(outcome.Pokemon.Data), outcome.FailureReason });
        }
        // Party-only status must survive the actual warehouse writer, not just a memory clone.
        var withStatus = legalSource.Clone();
        withStatus.Status_Condition = 8;
        withStatus.PokerusStrain = 3;
        withStatus.PokerusDays = 2;
        var statusRecord = LocalRepository.Upload(withStatus, Path.Combine(root, "warehouse"));
        var statusReloaded = LocalRepository.LoadWorking(LocalRepository.SaveWorking(statusRecord, withStatus));
        var statusDiff = Differences(Snapshot(withStatus), Snapshot(statusReloaded));
        if (statusDiff.Length != 0) errors.Add("nonzero-status persistence: " + string.Join(", ", statusDiff));
        rows.Add(new { label = "nonzero-status persistence", before = Snapshot(withStatus), after = Snapshot(statusReloaded), differences = statusDiff });
        var npcSource = BoxReader.Read(saves[1]).Select(slot => BoxReader.ReadPokemon(saves[1], slot))
            .First(p => new LegalityAnalysis(p).EncounterMatch is EncounterTrade4PID);
        foreach (var conflict in new[] { "PID", "TrainerID", "IV" })
        {
            var candidate = npcSource.Clone();
            if (conflict == "PID") candidate.PID ^= 1;
            else if (conflict == "TrainerID") candidate.TID16 ^= 1;
            else candidate.IV_HP = (candidate.IV_HP + 1) % 32;
            var input = candidate.Data.ToArray();
            var outcome = repair(candidate);
            if (outcome.Valid || !input.AsSpan().SequenceEqual(candidate.Data) || !input.AsSpan().SequenceEqual(outcome.Pokemon.Data))
                errors.Add($"NPC mismatched {conflict}: must reject without changing data");
            rows.Add(new { label = $"NPC mismatched {conflict}", outcome.Valid, unchanged = input.AsSpan().SequenceEqual(outcome.Pokemon.Data) });
        }
        foreach (var language in new[] { 1, 2 })
        {
            var localized = BoxReader.Read(saves[1]).Select(slot => BoxReader.ReadPokemon(saves[1], slot))
                .First(p => p is PK4 && !p.IsEgg && new LegalityAnalysis(p).EncounterMatch is not EncounterTrade4PID);
            localized.Language = language;
            localized.Nickname = SpeciesName.GetSpeciesNameGeneration(localized.Species, language, 4);
            localized.IsNicknamed = false;
            if (!new LegalityAnalysis(localized).Valid) throw new InvalidOperationException("Localized positive control is not legal.");
            localized.Nickname = "ABC";
            var outcome = repair(localized);
            var expected = SpeciesName.GetSpeciesNameGeneration(localized.Species, language, 4);
            if (!outcome.Valid || outcome.Pokemon.Nickname != expected || Differences(Snapshot(localized), Snapshot(outcome.Pokemon)).Length != 0)
                errors.Add($"language {language}: standard name repair failed");
            rows.Add(new { label = $"standard-name language {language}", expected, actual = outcome.Pokemon.Nickname, outcome.Valid });
        }
        var resultPath = Path.Combine(root, "fixed-field-results.json");
        File.WriteAllText(resultPath, JsonSerializer.Serialize(new { errors, rows }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Fixed-field checks: {rows.Count} cases; {errors.Count} failures; evidence: {resultPath}");
        foreach (var error in errors) Console.WriteLine("FAIL: " + error);
        if (errors.Count != 0) throw new InvalidOperationException("Fixed-field preservation failed.");
    }

    static string[] Differences(Dictionary<string, string> before, Dictionary<string, string> after) => before.Keys.Where(key => before[key] != after[key]).ToArray();

    static Dictionary<string, string> Snapshot(PKM p) => new()
    {
        ["Species"] = p.Species.ToString(), ["Form"] = p.Form.ToString(), ["Level"] = p.CurrentLevel.ToString(),
        ["Nature"] = p.Nature.ToString(), ["StatAlignment"] = p.StatAlignment.ToString(), ["Gender"] = p.Gender.ToString(), ["Shiny"] = p.IsShiny.ToString(),
        ["IVs"] = $"{p.IV_HP},{p.IV_ATK},{p.IV_DEF},{p.IV_SPE},{p.IV_SPA},{p.IV_SPD}",
        ["EVs"] = $"{p.EV_HP},{p.EV_ATK},{p.EV_DEF},{p.EV_SPE},{p.EV_SPA},{p.EV_SPD}",
        ["Stats"] = string.Join(',', p.GetStats(p.PersonalInfo).ToArray()),
        ["Moves"] = $"{p.Move1},{p.Move2},{p.Move3},{p.Move4}", ["PP"] = $"{p.Move1_PP},{p.Move2_PP},{p.Move3_PP},{p.Move4_PP}",
        ["PPUps"] = $"{p.Move1_PPUps},{p.Move2_PPUps},{p.Move3_PPUps},{p.Move4_PPUps}",
        ["HeldItem"] = p.HeldItem.ToString(), ["Status"] = p.Status_Condition.ToString(), ["Pokerus"] = $"{p.PokerusStrain}/{p.PokerusDays}", ["Egg"] = p.IsEgg.ToString(),
        ["TrainerName"] = p.OriginalTrainerName, ["TrainerID"] = $"{p.TID16}/{p.SID16}", ["TrainerGender"] = p.OriginalTrainerGender.ToString(),
        ["TrainerBytes"] = Convert.ToHexString(p.OriginalTrainerTrash), ["Language"] = p.Language.ToString(),
        ["OriginGame"] = p.Version.ToString(), ["Ball"] = p.Ball.ToString(), ["Ability"] = p.Ability.ToString(), ["AbilitySlot"] = p.AbilityNumber.ToString(),
    };
}
