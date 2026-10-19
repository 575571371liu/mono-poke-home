using MonoHome.Core.Repository;
using MonoHome.Core.Saves;
using PKHeX.Core;
using System.Diagnostics;
using System.Text.Json;

namespace MonoHome.Verifier;

static class EditLegalityChecks
{
    public static void Run(Func<PKM, RepairOutcome> repair)
    {
        var rows = new List<object>();
        var failures = new List<string>();
        var root = TestEnvironment.NewScratchDirectory("edit-legality");
        var total = Stopwatch.StartNew();
        var checks = 0;
        var accepted = 0;
        void Check(PKM input, string label, bool expected = true)
        {
            var bytes = input.Data.ToArray();
            var timer = Stopwatch.StartNew();
            var result = repair(input);
            var valid = result.Valid && new LegalityAnalysis(result.Pokemon).Valid;
            if (result.Valid != valid) failures.Add(label + ": accepted illegal result");
            if (expected && !valid) failures.Add(label + ": " + result.FailureReason);
            if (valid && UserFields(input) != UserFields(result.Pokemon)) failures.Add(label + ": changed requested attributes");
            if (!bytes.SequenceEqual(input.Data) || !valid && !bytes.SequenceEqual(result.Pokemon.Data)) failures.Add(label + ": mutated input or rejected result");
            if (valid)
            {
                accepted++;
                var stored = LocalRepository.Upload(input, root);
                var original = LocalRepository.ReadOriginalBytes(stored);
                StoredPokemon saved;
                try { saved = LocalRepository.SaveEditedWorking(stored, result.Pokemon); }
                catch (Exception ex)
                {
                    File.WriteAllBytes(Path.Combine(root, "failed-result.pkm"), result.Pokemon.Data);
                    throw new InvalidOperationException(label + ": persistence failed; result " + result.Pokemon.PID.ToString("X8"), ex);
                }
                var reloaded = LocalRepository.LoadWorking(saved);
                if (!new LegalityAnalysis(reloaded).Valid || UserFields(input) != UserFields(reloaded)
                    || !original.SequenceEqual(LocalRepository.ReadOriginalBytes(saved))) failures.Add(label + ": persistence failed or backup changed");
            }
            rows.Add(new { label, expected, accepted = valid, result.Template, result.FailureReason, elapsedMs = timer.ElapsedMilliseconds });
            checks++;
            if (checks % 100 == 0) Console.WriteLine($"Editing matrix: {checks} checked, {accepted} legal, {failures.Count} failures, {total.Elapsed.TotalSeconds:F1}s");
        }
        // An encrypted Gen3 payload can coincidentally match its plaintext checksum.
        var checksumCollision = new PK3();
        Convert.FromBase64String("LnfTZdIELhbMu87Ou867/wAAAgLOv83O////AOZgAAATAAAA6AMAAAB4AAAhACcAAAAAACMeAAAAAAAAAAAAAAAAAAAAIIAh////PwAAAAAAAAAACgAAAAAAAAAAAAAAAAAAAA==").AsSpan().CopyTo(checksumCollision.Data);
        checksumCollision.RefreshChecksum();
        Require(new LegalityAnalysis(checksumCollision).Valid, "Checksum collision witness must be legal");
        Check(checksumCollision, "Gen3 encrypted checksum collision persistence");
        foreach (var species in new ushort[] { 19, 147 })
        {
            var source = BoxReader.Read(TestEnvironment.HeartGoldSave)
                .Select(slot => BoxReader.ReadPokemon(TestEnvironment.HeartGoldSave, slot)).First(p => p.Species == species);
            var baseline = LocalRepository.RepairPreservingFields(source);
            Require(baseline.Valid, "Reported fixture must be repairable before IV editing");
            var edited = baseline.Pokemon.Clone();
            if (species == 19) { edited.IV_HP = 31; edited.IV_ATK = 31; }
            else edited.SetIVs([31, 31, 31, 31, 31, 31]);
            var bytes = edited.Data.ToArray();
            var result = repair(edited);
            Require(result.Valid && new LegalityAnalysis(result.Pokemon).Valid,
                $"Reported #{species} must retain edited IVs and find a legal background: {result.FailureReason}");
            Require(UserFields(edited) == UserFields(result.Pokemon), "Repair changed requested attributes");
            Require(bytes.SequenceEqual(edited.Data), "Repair mutated its input");
            Console.WriteLine($"PASS: reported #{species} preserves requested IVs with legal {result.Template}; {string.Join("; ", result.Changes)}");
            Check(edited, $"reported {species}");
        }
        foreach (var version in new[] { GameVersion.R, GameVersion.S, GameVersion.E, GameVersion.FR, GameVersion.LG,
            GameVersion.D, GameVersion.P, GameVersion.Pt, GameVersion.HG, GameVersion.SS })
        foreach (var species in new ushort[] { 19, 147, 81, 113, 128 })
        foreach (var nature in species is 113 or 128 ? new[] { 0, 1, 10, 22, 24 } : Enumerable.Range(0, 25))
        foreach (var gender in species switch { 81 => new byte[] { 2 }, 113 => new byte[] { 1 }, 128 => new byte[] { 0 }, _ => new byte[] { 0, 1 } })
        foreach (var ability in new[] { AbilityPermission.OnlyFirst, AbilityPermission.OnlySecond })
        foreach (var shiny in new[] { false, true })
        {
            PersonalInfo personal = version.Generation == 3 ? PersonalTable.RS[species] : PersonalTable.HGSS[species];
            if (personal.GetAbilityAtIndex(ability == AbilityPermission.OnlyFirst ? 0 : 1) == 0) continue;
            IEncounterConvertible egg = version.Generation == 3 ? new EncounterEgg3(species, version) : new EncounterEgg4(species, version);
            var trainer = new SimpleTrainerInfo(version) { OT = "TEST", Language = 2, TID16 = 1234, SID16 = 5678 };
            var criteria = new EncounterCriteria
            {
                Nature = (Nature)nature, Gender = (Gender)gender, Ability = ability,
                Shiny = shiny ? Shiny.Always : Shiny.Never,
                IV_HP = 31, IV_ATK = 31, IV_DEF = 31, IV_SPE = 31, IV_SPA = 31, IV_SPD = 31,
            };
            var template = egg.ConvertToPKM(trainer, criteria);
            template.CurrentLevel = 10;
            for (var attempt = 0; attempt < 32 && !new LegalityAnalysis(template).Valid; attempt++)
            {
                Require(new LegalityAnalysis(template).Results.Any(r => !r.Valid && r.Identifier == CheckIdentifier.PID),
                    "Official witness failed outside random PID classification: " + new LegalityAnalysis(template).Report());
                template = egg.ConvertToPKM(trainer, criteria);
                template.CurrentLevel = 10;
            }
            Require(new LegalityAnalysis(template).Valid, $"Official breeding witness must be legal: {version} #{species} nature={nature} gender={gender} ability={ability} shiny={shiny} PID={template.PID:X8}; " + new LegalityAnalysis(template).Report());
            var edited = template.Clone();
            edited.EggLocation = 0;
            edited.MetLocation = (ushort)(version.Generation == 3 ? 101 : 194);
            edited.MetLevel = 2;
            Check(edited, $"breeding {version} #{species} nature={nature} gender={gender} ability={ability} shiny={shiny}");
        }
        var fixtures = new[] { TestEnvironment.EmeraldSave, TestEnvironment.HeartGoldSave }
            .SelectMany(path => BoxReader.Read(path).Select(slot => BoxReader.ReadPokemon(path, slot)))
            .Select(LocalRepository.RepairPreservingFields).Where(r => r.Valid).Select(r => r.Pokemon)
            .DistinctBy(p => (p.Version, p.Species, p.Form)).ToArray();
        foreach (var source in fixtures.Where(p => p.Species is 132 or 250))
        foreach (var nature in Enumerable.Range(0, 25))
        {
            var encounter = (IEncounterConvertible)new LegalityAnalysis(source).EncounterMatch;
            var template = encounter.ConvertToPKM(new SimpleTrainerInfo(source.Version) { OT = "TEST", Language = 2 },
                new EncounterCriteria { Nature = (Nature)nature, Gender = (Gender)source.Gender, Shiny = Shiny.Never });
            Require(new LegalityAnalysis(template).Valid, "Official non-breeding witness must be legal");
            var edited = template.Clone();
            for (uint offset = 1; offset <= 32; offset++)
            {
                var delta = offset * 12800;
                edited.PID = template.PID <= uint.MaxValue - delta ? template.PID + delta : template.PID - delta;
                if (edited.IsShiny == template.IsShiny && !new LegalityAnalysis(edited).Valid) break;
            }
            Require(UserFields(edited) == UserFields(template), "PID negative control must preserve all chosen attributes");
            Check(edited, $"non-breeding {source.Version} #{source.Species} nature={nature}");
        }
        foreach (var source in fixtures)
        foreach (var variation in new[] { "perfect", "zero", "two", "EV", "nature", "ability", "gender", "egg" })
        {
            var edit = source.Clone();
            switch (variation)
            {
                case "perfect": edit.SetIVs([31, 31, 31, 31, 31, 31]); break;
                case "zero": edit.SetIVs([0, 0, 0, 0, 0, 0]); break;
                case "two": edit.IV_HP = 31; edit.IV_ATK = 31; break;
                case "EV": edit.SetEVs([252, 252, 0, 6, 0, 0]); break;
                case "nature": edit.SetPIDNature((Nature)(((int)edit.Nature + 1) % 25)); break;
                case "ability": edit.RefreshAbility(1 - Math.Max(0, edit.PersonalInfo.GetIndexOfAbility(edit.Ability))); break;
                case "gender": if (edit.PersonalInfo.Gender is > 0 and < 254) edit.SetPIDGender((byte)(1 - edit.Gender)); break;
                case "egg": edit.IsEgg = true; break;
            }
            Check(edit, $"fixture {source.Version} #{source.Species} {variation}", false);
        }
        foreach (var version in new[] { GameVersion.E, GameVersion.HG })
        {
            IEncounterConvertible egg = version == GameVersion.E ? new EncounterEgg3(19, version) : new EncounterEgg4(19, version);
            var template = egg.ConvertToPKM(new SimpleTrainerInfo(version) { OT = "TEST", Language = 2 });
            template.IsEgg = true;
            template.MetLocation = template is PK3 ? template.MetLocation : (ushort)0;
            template.MetDate = null;
            template.CurrentLevel = EggStateLegality.GetEggLevel(template.Generation);
            template.Nickname = SpeciesName.GetEggName(template.Language, template.Generation);
            template.IsNicknamed = EggStateLegality.IsNicknameFlagSet((IEncounterTemplate)egg, template);
            template.OriginalTrainerFriendship = template.PersonalInfo.HatchCycles;
            Require(new LegalityAnalysis(template).Valid, "Unhatched egg witness must be legal: " + new LegalityAnalysis(template).Report());
            template.MetLocation = 194;
            template.CurrentLevel = 50;
            Check(template, $"unhatched egg metadata and EXP {version}");
        }
        foreach (var source in fixtures.Where(p => p is PK3).Take(8))
        {
            var migrated = ((PK3)source).ConvertToPK4();
            migrated.SetIVs([31, 31, 31, 31, 31, 31]);
            Check(migrated, $"migrated {source.Species} perfect");
        }
        var wrongTile = (PK4)fixtures.First(p => p is PK4 && p.Species == 19).Clone();
        wrongTile.GroundTile = (GroundTileType)255;
        Check(wrongTile, "invalid encounter ground tile without changing source");
        var shinyCollision = new EncounterEgg3(147, GameVersion.E).ConvertToPKM(new SimpleTrainerInfo(GameVersion.E)
            { OT = "TEST", Language = 2, TID16 = 1234, SID16 = 5678 });
        shinyCollision.PID = 0x1270008C;
        shinyCollision.Gender = EntityGender.GetFromPID(shinyCollision.Species, shinyCollision.PID);
        shinyCollision.RefreshAbility((int)(shinyCollision.PID & 1));
        shinyCollision.SetIVs([31, 31, 31, 31, 31, 31]);
        shinyCollision.CurrentLevel = 10;
        Require(shinyCollision.IsShiny && !new LegalityAnalysis(shinyCollision).Valid, "Rare shiny PID classification control");
        Check(shinyCollision, "shiny egg PID mistaken for gift correlation");
        foreach (var form in Enumerable.Range(0, 28))
        {
            var probe = new PK3 { Species = 201, Version = GameVersion.FR, Language = 2, CurrentLevel = 25, Form = (byte)form };
            var encounter = EncounterMovesetGenerator.GenerateEncounters(probe, Array.Empty<ushort>(), GameVersion.FR).OfType<EncounterSlot3>().First(e => e.Form == form);
            var template = encounter.ConvertToPKM(new SimpleTrainerInfo(GameVersion.FR) { OT = "TEST", Language = 2 });
            Require(template.Form == form && new LegalityAnalysis(template).Valid, "Native Unown form witness must be legal");
            var edited = template.Clone();
            var damaged = false;
            for (uint offset = 1; offset <= 64 && !damaged; offset++)
            foreach (var increase in new[] { true, false })
            {
                edited.PID = increase ? unchecked(template.PID + offset * 12800) : unchecked(template.PID - offset * 12800);
                if (UserFields(edited) != UserFields(template) || new LegalityAnalysis(edited).Valid) continue;
                damaged = true;
                break;
            }
            Require(damaged, "Unown PID corruption must preserve chosen form and nature");
            Check(edited, $"Gen3 Unown form={form} PID reversal");
        }
        var negative = fixtures.First(p => p.Species == 19 && p is PK4);
        try { LocalRepository.ApplyEdit(negative, new WorkingEdit(null, null, null, null, null, null, Gender: 2)); failures.Add("Mixed-gender species accepted genderless editing"); }
        catch (ArgumentOutOfRangeException) { }
        try { LocalRepository.ApplyEdit(negative, new WorkingEdit(null, null, null, null, null, null, Form: 255)); failures.Add("Out-of-range form accepted"); }
        catch (ArgumentOutOfRangeException) { }
        var unown = new PK3 { Species = 201 };
        try { LocalRepository.ApplyEdit(unown, new WorkingEdit(null, null, null, null, null, null, Form: 28)); failures.Add("Impossible Gen3 Unown form accepted"); }
        catch (ArgumentOutOfRangeException) { }
        foreach (var conflict in new[] { "gender", "ability", "EV", "egg EV", "move", "PP" })
        {
            var edited = negative.Clone();
            switch (conflict)
            {
                case "gender": edited.Gender = 2; break;
                case "ability": edited.Ability = ushort.MaxValue; break;
                case "EV": edited.SetEVs([255, 255, 1, 0, 0, 0]); break;
                case "egg EV": edited.IsEgg = true; edited.EV_HP = 1; break;
                case "move": edited.Move1 = ushort.MaxValue; break;
                case "PP": edited.Move1_PP = byte.MaxValue; break;
            }
            var bytes = edited.Data.ToArray();
            var result = repair(edited);
            Require(!result.Valid && result.Pokemon.Data.SequenceEqual(bytes) && edited.Data.SequenceEqual(bytes), "Invalid " + conflict + " must be rejected unchanged");
            rows.Add(new { label = "negative " + conflict, accepted = false, result.FailureReason });
            checks++;
        }
        File.WriteAllText(Path.Combine(root, "matrix.json"), JsonSerializer.Serialize(new { checks, accepted, failures, rows }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Editing matrix complete: {checks} checked, {accepted} legal, {failures.Count} failures, {total.Elapsed.TotalSeconds:F1}s; {root}");
        foreach (var failure in failures) Console.WriteLine("FAIL: " + failure);
        Require(failures.Count == 0, "Editing legality matrix failed");
    }

    static string UserFields(PKM p) => string.Join('|', p.Species, p.Form, p.Nature, p.Gender, p.IsShiny, p.Ability,
        p.IV_HP, p.IV_ATK, p.IV_DEF, p.IV_SPE, p.IV_SPA, p.IV_SPD,
        p.EV_HP, p.EV_ATK, p.EV_DEF, p.EV_SPE, p.EV_SPA, p.EV_SPD,
        p.Move1, p.Move2, p.Move3, p.Move4, p.Move1_PP, p.Move2_PP, p.Move3_PP, p.Move4_PP,
        p.Move1_PPUps, p.Move2_PPUps, p.Move3_PPUps, p.Move4_PPUps,
        p.Version, p.Language, p.ID32, p.OriginalTrainerName, p.OriginalTrainerGender,
        p.HeldItem, p.Status_Condition, p.PokerusStrain, p.PokerusDays, p.IsEgg);

    static void Require(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
    }
}
