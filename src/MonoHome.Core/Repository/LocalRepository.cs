using System.Text.Json;
using PKHeX.Core;

namespace MonoHome.Core.Repository;

public sealed record StoredPokemon(
    string Id,
    int Species,
    string OriginalPath,
    string WorkingPath,
    string ManifestPath,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string LegalityStatus,
    string? ParentId = null,
    long Revision = 1);

public sealed record WorkingEdit(
    string? Nickname,
    int? Level,
    int? HeldItem,
    int? StatusCondition,
    int? PokerusStrain,
    int? PokerusDays,
    IReadOnlyList<int>? Moves = null,
    IReadOnlyList<int>? IVs = null,
    IReadOnlyList<int>? EVs = null,
    int? Species = null,
    int? Nature = null,
    int? AbilityIndex = null,
    int? Gender = null,
    int? Form = null,
    bool? Shiny = null,
    bool? Egg = null);

public sealed record RepairOutcome(
    PKM Pokemon,
    bool Valid,
    string Template,
    IReadOnlyList<string> Changes,
    string? FailureReason = null);

public static class LocalRepository
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly HashSet<string> LegalityStates = ["valid", "invalid", "pending", "stale", "unsupported"];

    public static StoredPokemon Upload(PKM pokemon, string root)
    {
        var working = RepairBackground(pokemon);
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(root, id);
        Directory.CreateDirectory(directory);
        var now = DateTimeOffset.UtcNow;
        var stored = new StoredPokemon(
            id,
            pokemon.Species,
            Path.Combine(directory, "original.pkm"),
            Path.Combine(directory, "working.pkm"),
            Path.Combine(directory, "record.json"),
            now,
            now,
            LegalityStatus(working));
        WritePokemon(stored.OriginalPath, pokemon);
        WritePokemon(stored.WorkingPath, working);
        WriteRecord(stored);
        return stored;
    }

    public static PKM LoadWorking(StoredPokemon stored) =>
        EntityFormat.GetFromBytes(File.ReadAllBytes(stored.WorkingPath)) ?? throw new InvalidDataException("Stored Pokémon is unreadable.");

    public static PKM ApplyEdit(PKM source, WorkingEdit edit)
    {
        var pokemon = source.Clone();
        var originalShiny = pokemon.IsShiny;
        if (!string.IsNullOrWhiteSpace(edit.Nickname))
        {
            pokemon.Nickname = edit.Nickname;
            pokemon.IsNicknamed = true;
        }
        if (edit.Level is { } level)
        {
            if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(edit.Level));
            pokemon.CurrentLevel = (byte)level;
        }
        if (edit.Species is { } species)
        {
            if (species is < 1 || species > pokemon.MaxSpeciesID) throw new ArgumentOutOfRangeException(nameof(edit.Species));
            pokemon.Species = (ushort)species;
        }
        if (edit.Nature is { } nature)
        {
            if (nature is < 0 or > 24) throw new ArgumentOutOfRangeException(nameof(edit.Nature));
            pokemon.SetPIDNature((Nature)nature);
        }
        if (edit.AbilityIndex is { } ability)
        {
            if (ability < 0 || ability >= pokemon.PersonalInfo.AbilityCount) throw new ArgumentOutOfRangeException(nameof(edit.AbilityIndex));
            pokemon.RefreshAbility(ability);
        }
        if (edit.Gender is { } gender)
        {
            if (gender is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(edit.Gender));
            pokemon.SetPIDGender((byte)gender);
        }
        if (edit.Form is { } form)
        {
            if (form is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(edit.Form));
            pokemon.Form = (byte)form;
        }
        if (edit.Shiny is { } shiny)
        {
            if (shiny && !pokemon.IsShiny) pokemon.SetShiny();
            if (!shiny && pokemon.IsShiny) pokemon.SetShinySID(Shiny.Never);
        }
        else if ((edit.Nature is not null || edit.Gender is not null) && originalShiny && !pokemon.IsShiny)
            pokemon.SetShiny();
        if (edit.Egg is { } egg)
            pokemon.IsEgg = egg;
        if (edit.HeldItem is { } item)
        {
            if (item < 0) throw new ArgumentOutOfRangeException(nameof(edit.HeldItem));
            pokemon.HeldItem = item;
        }
        if (edit.StatusCondition is { } status)
        {
            if (status < 0) throw new ArgumentOutOfRangeException(nameof(edit.StatusCondition));
            pokemon.Status_Condition = status;
        }
        if (edit.PokerusStrain is { } strain)
        {
            if (strain is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(edit.PokerusStrain));
            pokemon.PokerusStrain = strain;
        }
        if (edit.PokerusDays is { } days)
        {
            if (days is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(edit.PokerusDays));
            pokemon.PokerusDays = days;
        }
        if (edit.Moves is { } moves)
        {
            if (moves.Count != 4 || moves.Any(move => move < 0 || move > pokemon.MaxMoveID)) throw new ArgumentOutOfRangeException(nameof(edit.Moves));
            pokemon.SetMoves(moves.Select(move => (ushort)move).ToArray());
        }
        if (edit.IVs is { } ivs)
        {
            if (ivs.Count != 6 || ivs.Any(iv => iv is < 0 or > 31)) throw new ArgumentOutOfRangeException(nameof(edit.IVs));
            pokemon.SetIVs(ivs.ToArray());
        }
        if (edit.EVs is { } evs)
        {
            if (evs.Count != 6 || evs.Any(ev => ev is < 0 or > 255) || evs.Sum() > 510) throw new ArgumentOutOfRangeException(nameof(edit.EVs));
            pokemon.SetEVs(evs.ToArray());
        }
        return pokemon;
    }

    public static StoredPokemon SaveWorking(StoredPokemon stored, PKM pokemon)
    {
        var working = RepairBackground(pokemon);
        WritePokemon(stored.WorkingPath, working);
        var status = LegalityStatus(working);
        var updated = stored with { UpdatedAt = DateTimeOffset.UtcNow, LegalityStatus = status, Revision = stored.Revision + 1 };
        WriteRecord(updated);
        return updated;
    }

    static string LegalityStatus(PKM pokemon)
    {
        try { return new LegalityAnalysis(pokemon).Valid ? "valid" : "invalid"; }
        catch { return "stale"; }
    }

    /// <summary>Repairs an entity in stages, never returning an unverified replacement.</summary>
    public static RepairOutcome RepairWithStrategy(PKM source)
    {
        var template = RepairTemplateName(source);
        LegalityAnalysis analysis;
        try
        {
            analysis = new LegalityAnalysis(source);
            if (analysis.Valid)
                return new(source, true, template, ["原始数据已通过合法性检查"]);
            var encounter = analysis.EncounterMatch as IEncounterConvertible;
            if (encounter is null or EncounterInvalid)
            {
                encounter = EncounterGenerator.GetEncounters(source, analysis.Info)
                    .Where(candidate => candidate is not EncounterInvalid && candidate.Species == source.Species && candidate.Form == source.Form)
                    .OfType<IEncounterConvertible>()
                    .FirstOrDefault();
            }
            if (encounter is null)
                return new(source, false, template, [], "找不到与当前种类、形态和来源世代匹配的合法相遇模板。请改用“编辑并另存为合法副本”，或选择受支持的来源世代。");

            var baseCandidate = BuildEncounterTemplate(source, encounter);
            if (IsLegal(baseCandidate))
                return new(baseCandidate, true, template, ["按世代背景模板重建"]);
            var preserved = PreserveUserFields(baseCandidate, source);
            if (TryRepairGen3Correlation(preserved, source, encounter) && IsLegal(preserved))
                return new(preserved, true, template, ["背景信息", "Gen 3 PID/IV 关联"]);
            if (IsLegal(preserved))
                return new(preserved, true, template, ["背景信息"]);

            var moveSafe = preserved.Clone();
            ApplyLegalMoves(moveSafe);
            if (TryRepairGen3Correlation(moveSafe, source, encounter) && IsLegal(moveSafe))
                return new(moveSafe, true, template, ["背景信息", "替换为该世代可学习招式", "Gen 3 PID/IV 关联"]);
            if (IsLegal(moveSafe))
                return new(moveSafe, true, template, ["背景信息", "替换为该世代可学习招式"]);

            var clean = BuildSafeTemplate(baseCandidate, source);
            if (TryRepairGen3Correlation(clean, source, encounter) && IsLegal(clean))
                return new(clean, true, template, ["按世代背景模板重建", "清理不兼容招式、道具和状态", "Gen 3 PID/IV 关联"]);
            if (IsLegal(clean))
                return new(clean, true, template, ["按世代背景模板重建", "清理不兼容招式、道具和状态"]);

            return new(source, false, template, ["保留背景字段", "替换合法招式", "按世代模板重建"],
                "所有候选仍未通过合法性检查。请查看编辑页中的招式、昵称、等级、形态和性别，或选择一个在当前来源世代真实存在的个体。");
        }
        catch (Exception ex)
        {
            return new(source, false, template, [], $"修复过程无法完成：{ex.Message}");
        }
    }

    public static PKM RepairBackground(PKM source) => RepairWithStrategy(source).Pokemon;

    static PKM BuildEncounterTemplate(PKM source, IEncounterConvertible encounter)
    {
        var trainer = new SimpleTrainerInfo(source.Version)
        {
            OT = source.OriginalTrainerName,
            TID16 = source.TID16,
            SID16 = source.SID16,
            Gender = source.OriginalTrainerGender,
            Language = source.Language,
        };
        var criteria = new EncounterCriteria
        {
            Gender = (Gender)source.Gender,
            Nature = (Nature)source.Nature,
            Shiny = source.IsShiny ? Shiny.Always : Shiny.Never,
        };
        try { return encounter.ConvertToPKM(trainer, criteria); }
        catch { return encounter.ConvertToPKM(trainer); }
    }

    static PKM PreserveUserFields(PKM destination, PKM source)
    {
        destination.Nickname = source.Nickname;
        destination.IsNicknamed = source.IsNicknamed;
        destination.CurrentLevel = (byte)Math.Clamp(Math.Max((int)destination.MetLevel, (int)source.CurrentLevel), (int)destination.MetLevel, 100);
        destination.SetMoves([source.Move1, source.Move2, source.Move3, source.Move4]);
        Span<int> ivs = stackalloc int[6];
        Span<int> evs = stackalloc int[6];
        source.GetIVs(ivs);
        source.GetEVs(evs);
        destination.SetIVs(ivs);
        destination.SetEVs(evs);
        destination.HeldItem = source.HeldItem;
        destination.Status_Condition = source.Status_Condition;
        destination.PokerusStrain = source.PokerusStrain;
        destination.PokerusDays = source.PokerusDays;
        destination.IsEgg = source.IsEgg;
        return destination;
    }

    static PKM BuildSafeTemplate(PKM destination, PKM source)
    {
        destination.Nickname = SpeciesName.GetSpeciesNameGeneration(destination.Species, source.Language, (byte)destination.Generation);
        destination.IsNicknamed = false;
        destination.CurrentLevel = (byte)Math.Clamp(Math.Max((int)destination.MetLevel, (int)source.CurrentLevel), (int)destination.MetLevel, 100);
        destination.SetMoveset();
        destination.SetIVs([0, 0, 0, 0, 0, 0]);
        destination.SetEVs([0, 0, 0, 0, 0, 0]);
        destination.HeldItem = 0;
        destination.Status_Condition = 0;
        destination.PokerusStrain = 0;
        destination.PokerusDays = 0;
        destination.IsEgg = false;
        if (destination.Generation >= 4)
            destination.SetRelearnMoves([0, 0, 0, 0]);
        return destination;
    }

    static void ApplyLegalMoves(PKM pokemon)
    {
        Span<ushort> moves = stackalloc ushort[4];
        var analysis = new LegalityAnalysis(pokemon);
        analysis.GetSuggestedCurrentMoves(moves, MoveSourceType.Encounter);
        if (moves[0] == 0)
            pokemon.SetMoveset();
        else
            pokemon.SetMoves(moves);
    }

    static bool TryRepairGen3Correlation(PKM pokemon, PKM source, IEncounterConvertible encounter)
    {
        if (pokemon is not PK3 pk3 || source is not PK3 source3 || encounter is not IEncounterSlot3 slot3)
            return true;
        var report = new LegalityAnalysis(pk3).Report();
        if (!report.Contains("PID+ correlation", StringComparison.Ordinal))
            return true;
        var currentLevel = pk3.CurrentLevel;
        slot3.SetRandom(pk3, PersonalTable.E[pk3.Species], new EncounterCriteria
        {
            Gender = (Gender)source.Gender,
            Nature = source.Nature,
            Shiny = source.IsShiny ? Shiny.Always : Shiny.Never,
        }, source3.PID ^ source3.IV32 ^ (uint)source.Species);
        pk3.CurrentLevel = Math.Max(pk3.MetLevel, currentLevel);
        return true;
    }

    static bool IsLegal(PKM pokemon)
    {
        try { return new LegalityAnalysis(pokemon).Valid; }
        catch { return false; }
    }

    static string RepairTemplateName(PKM pokemon) => pokemon.Generation switch
    {
        3 => "Gen 3 · Emerald 背景模板",
        4 => "Gen 4 · HGSS 背景模板",
        _ => $"Gen {pokemon.Generation} · 通用背景模板",
    };

    public static StoredPokemon CreateLegalCopy(StoredPokemon parent, PKM legalPokemon, string root)
    {
        var repaired = RepairBackground(legalPokemon);
        if (!new LegalityAnalysis(repaired).Valid)
            throw new InvalidOperationException("自动修复背景信息后仍未通过合法性检查，请调整可编辑字段。" );
        var copy = Upload(repaired, root);
        var linked = copy with { ParentId = parent.Id, Revision = 1, LegalityStatus = "valid" };
        WriteRecord(linked);
        return linked;
    }

    public static StoredPokemon DiscardEdits(StoredPokemon stored)
    {
        var original = EntityFormat.GetFromBytes(File.ReadAllBytes(stored.OriginalPath)) ?? throw new InvalidDataException("Original Pokémon is unreadable.");
        WritePokemon(stored.WorkingPath, original);
        var updated = stored with { UpdatedAt = DateTimeOffset.UtcNow, LegalityStatus = "stale" };
        WriteRecord(updated);
        return updated;
    }

    public static StoredPokemon SetLegality(StoredPokemon stored, string status)
    {
        if (!LegalityStates.Contains(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        var updated = stored with { UpdatedAt = DateTimeOffset.UtcNow, LegalityStatus = status };
        WriteRecord(updated);
        return updated;
    }

    public static void Remove(StoredPokemon stored)
    {
        var directory = Path.GetDirectoryName(stored.ManifestPath) ?? throw new InvalidDataException("Repository record directory is missing.");
        foreach (var path in new[] { stored.ManifestPath, stored.WorkingPath, stored.OriginalPath })
            if (File.Exists(path))
                File.Delete(path);
        if (Directory.Exists(directory))
            Directory.Delete(directory);
    }

    public static StoredPokemon? GetLatest(string root)
        => List(root).FirstOrDefault();

    public static IReadOnlyList<StoredPokemon> List(string root)
    {
        if (!Directory.Exists(root))
            return [];
        return Directory.EnumerateFiles(root, "record.json", SearchOption.AllDirectories)
            .Select(ReadRecord)
            .Where(record => record is not null && File.Exists(record.WorkingPath) && File.Exists(record.OriginalPath))
            .OrderByDescending(record => record!.UpdatedAt)
            .Cast<StoredPokemon>()
            .ToArray();
    }

    static StoredPokemon? ReadRecord(string path)
    {
        try { return JsonSerializer.Deserialize<StoredPokemon>(File.ReadAllText(path), Json); }
        catch (JsonException) { return null; }
    }

    static void WritePokemon(string path, PKM pokemon)
    {
        var data = new byte[pokemon.SIZE_STORED];
        pokemon.WriteEncryptedDataStored(data);
        WriteAtomic(path, data);
        if (EntityFormat.GetFromBytes(data) is null)
            throw new InvalidDataException("Working copy verification failed.");
    }

    static void WriteRecord(StoredPokemon stored) =>
        WriteAtomic(stored.ManifestPath, JsonSerializer.SerializeToUtf8Bytes(stored, Json));

    static void WriteAtomic(string path, byte[] data)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(temporary, data);
        File.Move(temporary, path, true);
    }
}
