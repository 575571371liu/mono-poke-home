using MonoHome.Core.Repository;
using PKHeX.Core;

namespace MonoHome.Core.Transfers;

public sealed record TransferChange(string Field, string From, string To);
public sealed record TransferReport(bool Succeeded, int Species, string ConversionResult, bool Legal, string OutputPath, string Message, IReadOnlyList<TransferChange> Changes, int Slot = -1);
public sealed record TransferBatchReport(bool Succeeded, string OutputPath, string Message, IReadOnlyList<TransferReport> Reports)
{
    public IReadOnlyList<TransferChange> Changes => Reports.SelectMany(report => report.Changes).ToArray();
}
public enum TransferMode { Conversion, Fidelity }

public static class EmeraldHgssTransfer
{
    public static TransferReport TransferStored(PKM pokemon, string heartGoldPath, string outputPath, TransferMode mode = TransferMode.Conversion, int destinationSlot = -1) =>
        Transfer(pokemon, heartGoldPath, outputPath, mode, destinationSlot);

    public static TransferBatchReport TransferStoredMany(IReadOnlyList<PKM> pokemon, string heartGoldPath, string outputPath, TransferMode mode = TransferMode.Conversion, int destinationSlot = -1)
    {
        if (pokemon.Count == 0)
            return new(false, outputPath, "未选择宝可梦。", []);
        var reports = new List<TransferReport>();
        var insertedSlots = new List<int>();
        try { File.Delete(outputPath); } catch { }
        var currentPath = Path.Combine(Path.GetTempPath(), $"mono-home-batch-{Guid.NewGuid():N}.sav");
        File.Copy(heartGoldPath, currentPath, true);
        var temporaryPaths = new List<string> { currentPath };
        try
        {
            foreach (var (entity, index) in pokemon.Select((entity, index) => (entity, index)))
            {
                var nextPath = Path.Combine(Path.GetTempPath(), $"mono-home-batch-{Guid.NewGuid():N}-{index}.sav");
                temporaryPaths.Add(nextPath);
                var report = Transfer(entity, currentPath, nextPath, mode, destinationSlot >= 0 ? destinationSlot + index : -1);
                reports.Add(report);
                if (!report.Succeeded)
                {
                    try { File.Delete(outputPath); } catch { }
                    return new(false, outputPath, $"第 {index + 1} 只宝可梦未通过合法性检查：{report.Message}", reports);
                }
                insertedSlots.Add(report.Slot);
                currentPath = nextPath;
            }
            File.Copy(currentPath, outputPath, true);
            var persisted = SaveUtil.GetSaveFile(outputPath) as SAV4HGSS;
            if (persisted is null || insertedSlots.Any(slot => !new LegalityAnalysis(persisted.GetBoxSlotAtIndex(slot)).Valid))
            {
                try { File.Delete(outputPath); } catch { }
                return new(false, outputPath, "批量输出重新读取后未通过合法性检查。", reports);
            }
            return new(true, outputPath, $"已处理 {reports.Count} 只宝可梦。", reports);
        }
        finally
        {
            foreach (var temporaryPath in temporaryPaths)
                try { File.Delete(temporaryPath); } catch { }
        }
    }

    static TransferReport Transfer(PKM pokemon, string heartGoldPath, string outputPath, TransferMode mode, int destinationSlot = -1)
    {
        var target = SaveUtil.GetSaveFile(heartGoldPath) as SAV4HGSS ?? throw new InvalidDataException("Invalid HeartGold save.");
        var changes = new List<TransferChange>();
        if (mode == TransferMode.Conversion)
        {
            changes.Add(new("OriginVersion", pokemon.Version.ToString(), target.Version.ToString()));
            changes.Add(new("OriginalTrainer", pokemon.OriginalTrainerName, target.OT));
            changes.Add(new("TrainerID", $"{pokemon.TID16}/{pokemon.SID16}", $"{target.TID16}/{target.SID16}"));
        }
        var conversionInput = pokemon;
        if (mode == TransferMode.Conversion)
        {
            var repaired = LocalRepository.RepairBackground(pokemon);
            if (!ReferenceEquals(repaired, pokemon))
                changes.Add(new("Background", "原始背景", "已按 PKHeX 合法相遇模板修复"));
            conversionInput = NormalizeGen3Correlation(repaired, changes);
        }

        var previousCompatibility = EntityConverter.AllowIncompatibleConversion;
        EntityConverter.AllowIncompatibleConversion = EntityCompatibilitySetting.AllowIncompatibleSane;
        PKM? converted;
        EntityConverterResult conversionResult;
        try
        {
            converted = EntityConverter.ConvertToType(conversionInput, target.BlankPKM.GetType(), out conversionResult);
        }
        finally
        {
            EntityConverter.AllowIncompatibleConversion = previousCompatibility;
        }
        if (converted is null)
            return new(false, pokemon.Species, conversionResult.ToString(), false, outputPath, "No target entity could be generated.", []);

        if (mode == TransferMode.Conversion)
        {
            converted.Language = target.Language;
            converted.OriginalTrainerName = target.OT;
            converted.TID16 = target.TID16;
            converted.SID16 = target.SID16;
            converted.OriginalTrainerGender = target.Gender;
        }
        if (converted.Nickname.Length > target.MaxStringLengthNickname)
        {
            changes.Add(new("Nickname", converted.Nickname, converted.Nickname[..target.MaxStringLengthNickname]));
            converted.Nickname = converted.Nickname[..target.MaxStringLengthNickname];
        }
        changes.Add(new("HeldItem", pokemon.HeldItem.ToString(), converted.HeldItem.ToString()));
        changes.Add(new("Shiny", pokemon.IsShiny.ToString(), converted.IsShiny.ToString()));
        changes.Add(new("Status", pokemon.Status_Condition.ToString(), converted.Status_Condition.ToString()));
        changes.Add(new("Pokerus", $"{pokemon.PokerusStrain}/{pokemon.PokerusDays}", $"{converted.PokerusStrain}/{converted.PokerusDays}"));
        changes.Add(new("Egg", pokemon.IsEgg.ToString(), converted.IsEgg.ToString()));
        changes.Add(new("Form", pokemon.Form.ToString(), converted.Form.ToString()));
        changes.Add(new("Ribbons", RibbonCount(pokemon).ToString(), RibbonCount(converted).ToString()));
        if (mode == TransferMode.Conversion)
            RestoreEmptyMoves(converted, changes);

        var slot = destinationSlot >= 0
            ? destinationSlot
            : Enumerable.Range(0, target.SlotCount).FirstOrDefault(i => target.GetBoxSlotAtIndex(i).Species == 0, -1);
        if (slot < 0 || slot >= target.SlotCount)
            return new(false, converted.Species, conversionResult.ToString(), false, outputPath, "Target save has no empty slot.", changes);

        target.SetBoxSlotAtIndex(converted, slot);
        var legality = new LegalityAnalysis(target.GetBoxSlotAtIndex(slot));
        var legalityReport = legality.Report();
        if (!legality.Valid && legalityReport.Contains("Nickname", StringComparison.Ordinal))
        {
            var defaultNickname = SpeciesName.GetSpeciesNameGeneration(converted.Species, target.Language, (byte)target.Generation);
            changes.Add(new("Nickname", converted.Nickname, defaultNickname));
            converted.Nickname = defaultNickname;
            converted.IsNicknamed = false;
            target.SetBoxSlotAtIndex(converted, slot);
            legality = new LegalityAnalysis(target.GetBoxSlotAtIndex(slot));
        }
        if (!legality.Valid)
            return new(false, converted.Species, conversionResult.ToString(), false, outputPath, legality.Report(), changes);

        File.WriteAllBytes(outputPath, target.Write().ToArray());
        var persisted = SaveUtil.GetSaveFile(outputPath) as SAV4HGSS;
        var persistedSlot = persisted?.GetBoxSlotAtIndex(slot);
        var persistedLegality = persistedSlot is null ? null : new LegalityAnalysis(persistedSlot);
        if (persistedLegality is { Valid: false } && persistedLegality.Report().Contains("Nickname", StringComparison.Ordinal))
        {
            var defaultNickname = SpeciesName.GetSpeciesNameGeneration(converted.Species, target.Language, (byte)target.Generation);
            changes.Add(new("Nickname", converted.Nickname, defaultNickname));
            converted.Nickname = defaultNickname;
            converted.IsNicknamed = false;
            target.SetBoxSlotAtIndex(converted, slot);
            File.WriteAllBytes(outputPath, target.Write().ToArray());
            persisted = SaveUtil.GetSaveFile(outputPath) as SAV4HGSS;
            persistedSlot = persisted?.GetBoxSlotAtIndex(slot);
            persistedLegality = persistedSlot is null ? null : new LegalityAnalysis(persistedSlot);
        }
        if (persisted is null || persistedSlot is null || persistedLegality is null || !persistedLegality.Valid)
        {
            try { File.Delete(outputPath); } catch { }
            return new(false, converted.Species, conversionResult.ToString(), false, outputPath, persistedLegality?.Report() ?? "写出后无法重新读取目标存档。", changes);
        }
        return new(true, converted.Species, conversionResult.ToString(), true, outputPath, $"Written to box slot {slot}.", changes, slot);
    }

    static PKM NormalizeGen3Correlation(PKM pokemon, List<TransferChange> changes)
    {
        if (pokemon is not PK3 source)
            return pokemon;
        var analysis = new LegalityAnalysis(source);
        if (!analysis.Report().Contains("PID+ correlation", StringComparison.Ordinal) || analysis.EncounterMatch is not IEncounterSlot3 encounter)
            return pokemon;
        var repaired = (PK3)source.Clone();
        encounter.SetRandom(repaired, PersonalTable.E[repaired.Species], new EncounterCriteria
        {
            Gender = (Gender)source.Gender,
            Nature = source.Nature,
            Shiny = source.IsShiny ? Shiny.Always : Shiny.Never,
        }, source.PID ^ source.IV32 ^ (uint)source.Species);
        changes.Add(new("PID/IV", $"{source.PID:X8}/{source.IV32:X8}", $"{repaired.PID:X8}/{repaired.IV32:X8}"));
        return repaired;
    }

    static void RestoreEmptyMoves(PKM pokemon, List<TransferChange> changes)
    {
        if (pokemon.Move1 != 0)
            return;
        Span<ushort> moves = stackalloc ushort[4];
        new LegalityAnalysis(pokemon).GetSuggestedCurrentMoves(moves);
        if (moves[0] == 0)
            return;
        pokemon.SetMoves(moves);
        changes.Add(new("Moves", "empty", string.Join(", ", moves.ToArray())));
    }

    static int RibbonCount(PKM pokemon) => pokemon is IRibbonSetRibbons ribbons ? ribbons.RibbonCount : 0;
}
