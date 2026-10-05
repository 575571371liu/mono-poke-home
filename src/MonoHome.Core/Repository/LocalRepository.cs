using PKHeX.Core;

namespace MonoHome.Core.Repository;

public sealed record StoredPokemon(
    string Id,
    int Species,
    string DatabasePath,
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
    static readonly HashSet<string> LegalityStates = ["valid", "invalid", "pending", "stale", "unsupported"];

    public static StoredPokemon Upload(PKM pokemon, string root)
    {
        return WarehouseDatabase.Insert(pokemon, EntityBytes(pokemon), root, LegalityStatus(pokemon));
    }

    public static PKM LoadWorking(StoredPokemon stored) =>
        EntityFormat.GetFromBytes(ReadWorkingBytes(stored)) ?? throw new InvalidDataException("Stored Pokémon is unreadable.");

    public static byte[] ReadWorkingBytes(StoredPokemon stored) => WarehouseDatabase.ReadPayload(stored, false);
    public static byte[] ReadOriginalBytes(StoredPokemon stored) => WarehouseDatabase.ReadPayload(stored, true);

    public static PKM ApplyEdit(PKM source, WorkingEdit edit)
    {
        var pokemon = source.Clone();
        var originalShiny = pokemon.IsShiny;
        if (!string.IsNullOrWhiteSpace(edit.Nickname) && edit.Nickname != pokemon.Nickname)
        {
            pokemon.Nickname = edit.Nickname;
            pokemon.IsNicknamed = true;
        }
        if (edit.Level is { } level)
        {
            if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(edit.Level));
            if (pokemon.CurrentLevel != level)
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
            if (pokemon.Nature != (Nature)nature)
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
            if (pokemon.Gender != gender)
                pokemon.SetPIDGender((byte)gender);
        }
        if (edit.Form is { } form)
        {
            if (form is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(edit.Form));
            pokemon.Form = (byte)form;
        }
        if (edit.Shiny is { } shiny)
        {
            if (shiny)
                EnsureShiny(pokemon);
            else
                EnsureNotShiny(pokemon);
        }
        else if ((edit.Nature is not null || edit.Gender is not null) && originalShiny)
        {
            // Rerolling the PID for a new nature or gender can drop shininess, so re-apply
            // the state the entity started with.
            EnsureShiny(pokemon);
        }
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
            if (!moves.SequenceEqual(new[] { (int)pokemon.Move1, pokemon.Move2, pokemon.Move3, pokemon.Move4 }))
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

    /// <summary>Makes an entity shiny without disturbing its nature or gender.</summary>
    static void EnsureShiny(PKM pokemon)
    {
        if (!pokemon.IsShiny)
            pokemon.SetShiny();
    }

    /// <summary>
    /// Makes an entity not shiny.
    ///
    /// This must reroll the PID rather than nudge the trainer SID: <c>PKM.SetShinySID</c>
    /// targets a shiny XOR within [0,7], which is shiny by definition, so it can never
    /// clear shininess — it only corrupts the original trainer identity.
    /// </summary>
    static void EnsureNotShiny(PKM pokemon)
    {
        // Bounded so a fixed-gender species whose PID space offers no non-shiny value
        // cannot spin forever; leaving the entity shiny is better than hanging.
        for (var attempt = 0; attempt < 64 && pokemon.IsShiny; attempt++)
            pokemon.SetPIDGender(pokemon.Gender);
    }

    public static StoredPokemon SaveWorking(StoredPokemon stored, PKM pokemon)
    {
        return WarehouseDatabase.Update(stored, EntityBytes(pokemon), pokemon.Species, LegalityStatus(pokemon));
    }

    public static StoredPokemon ApplyTradeEvolution(StoredPokemon stored, GameVersion game, MonoHome.Core.Pokemon.EvolutionNodeKey target)
    {
        var source = LoadWorking(stored);
        var evolved = MonoHome.Core.Pokemon.TradeEvolution.Preview(source, game, target);
        var bytes = EntityBytes(evolved);
        var persisted = EntityFormat.GetFromBytes(bytes) ?? throw new InvalidDataException("进化结果无法回读，未保存。");
        MonoHome.Core.Pokemon.TradeEvolution.VerifyPreserved(source, persisted);
        if (!new LegalityAnalysis(persisted).Valid)
            throw new InvalidDataException("进化结果回读后未通过合法性检查，未保存。");
        return WarehouseDatabase.Update(stored, bytes, persisted.Species, "valid");
    }

    static string LegalityStatus(PKM pokemon)
    {
        try { return new LegalityAnalysis(pokemon).Valid ? "valid" : "invalid"; }
        catch { return "stale"; }
    }

    /// <summary>Preserves fixed fields, except explicitly authorized fixed NPC names.</summary>
    public static RepairOutcome RepairPreservingFields(PKM source)
    {
        try
        {
            var analysis = new LegalityAnalysis(source);
            if (analysis.Valid)
                return new(source.Clone(), true, "固定属性", ["原始数据已通过合法性检查"]);
            if (source is PK3 { IsEgg: false })
            {
                var candidate = source.Clone();
                candidate.PrepareNickname();
                candidate.Nickname = source.Nickname;
                if (candidate.Nickname == source.Nickname && new LegalityAnalysis(candidate).Valid)
                    return new(candidate, true, "固定属性", ["规范昵称尾部填充字节，昵称文字及其他数据不变"]);
            }
            if (source is PK4 { IsEgg: false, Language: 1 or 2 })
            {
                var candidate = source.Clone();
                candidate.PrepareNickname();
                candidate.Nickname = analysis.EncounterMatch is EncounterTrade4PID trade
                    ? trade.GetNickname(source.Language)
                    : SpeciesName.GetSpeciesNameGeneration(source.Species, source.Language, source.Generation);
                candidate.IsNicknamed = analysis.EncounterMatch is EncounterTrade4PID;
                // ponytail: HG/SS ordinary-language fixed trades only; language-swapped trades need separate locale handling.
                if (analysis.EncounterMatch is EncounterTrade4PID { IsLanguageSwap: false } npc
                    && source.Version is GameVersion.HG or GameVersion.SS
                    && source.ID32 == npc.ID32 && source.PID == npc.PID)
                {
                    var template = npc.ConvertToPKM(new SimpleTrainerInfo(source.Version) { Language = source.Language });
                    candidate.OriginalTrainerName = template.OriginalTrainerName;
                    if (new LegalityAnalysis(candidate).Valid)
                        return new(candidate, true, "NPC 固定名称特例",
                            [$"恢复 NPC 固定昵称：{source.Nickname} → {candidate.Nickname}",
                             $"恢复 NPC 固定训练家名：{source.OriginalTrainerName} → {candidate.OriginalTrainerName}；训练家 ID 及其他固定属性不变"]);
                }
                if (new LegalityAnalysis(candidate).Valid)
                    return new(candidate, true, "固定属性", [$"按来源语言恢复名称：{source.Nickname} → {candidate.Nickname}；训练家及固定属性不变"]);
            }
            if (source is PK4 { IsEgg: false, Generation: 4 } && analysis.EncounterMatch is IEncounterSlot4)
            {
                var repaired = RepairWildGen4Background(source, analysis);
                if (repaired is not null)
                    return repaired;
            }
            return new(source.Clone(), false, "固定属性", [],
                $"未找到保留全部固定信息的合法结果；当前数据未修改。\n{new LegalityAnalysis(source).Report()}");
        }
        catch (Exception ex)
        {
            return new(source.Clone(), false, "固定属性", [], $"无法完成检查；当前数据未修改：{ex.Message}");
        }
    }

    static RepairOutcome? RepairWildGen4Background(PKM source, LegalityAnalysis analysis)
    {
        var candidate = source.Clone();
        var changes = new List<string>();
        if (analysis.Results.Any(r => !r.Valid && r.Identifier == CheckIdentifier.Nickname) && source.Language is 1 or 2)
        {
            candidate.PrepareNickname();
            candidate.Nickname = SpeciesName.GetSpeciesNameGeneration(source.Species, source.Language, source.Generation);
            candidate.IsNicknamed = false;
            changes.Add($"按来源语言恢复名称：{source.Nickname} → {candidate.Nickname}");
        }
        if (analysis.Results.Any(r => r.Result is LegalityCheckResultCode.EffortUntrainedCap_0 or LegalityCheckResultCode.EffortUntrainedMoreEXP_0))
        {
            var metLevel = Math.Max(source.MetLevel, analysis.EncounterMatch.LevelMin);
            var growth = PersonalTable.HGSS[analysis.EncounterMatch.Species].EXPGrowth;
            var baseEXP = Experience.GetEXP((byte)metLevel, growth);
            Span<int> evs = stackalloc int[6];
            source.GetEVs(evs);
            var required = baseEXP + (uint)Math.Max(1, EffortExpLegality.GetRequiredEffortEXP(evs, 0, source.IsPokerusInfected, source.Generation, source.Format));
            candidate.EXP = Math.Max(source.EXP, required);
            if (candidate.CurrentLevel != source.CurrentLevel)
                return new(source.Clone(), false, "固定属性", [], "这些努力值需要更多经验值，补足经验会改变等级；当前数据未修改。请调整努力值或等级后重新预览。");
            changes.Add($"补足努力值训练所需经验，等级不变：{source.EXP} → {candidate.EXP}");
        }
        if (IsLegal(candidate))
            return new(candidate, true, "固定属性", changes);

        // ponytail: ordinary Gen 4 wild Method 1 and Cute Charm only; other encounter methods remain rejected.
        Span<uint> seeds = stackalloc uint[LCRNG.MaxCountSeedsIV];
        var count = LCRNGReversal.GetSeedsIVs(seeds, (uint)source.IV_HP, (uint)source.IV_ATK, (uint)source.IV_DEF,
            (uint)source.IV_SPA, (uint)source.IV_SPD, (uint)source.IV_SPE);
        var pids = new List<uint>(count + 1);
        foreach (var seed in seeds[..count])
            pids.Add((seed & 0xffff0000) | (LCRNG.Prev(seed) >> 16));
        if (source.Gender is 0 or 1)
            pids.Add(source.Gender == 1 ? CuteCharm4.GetPIDFemale((uint)source.Nature)
                : CuteCharm4.GetPIDMale(source.PersonalInfo.Gender, (uint)source.Nature));
        foreach (var pid in pids.Distinct())
        {
            candidate.PID = pid;
            if (candidate.Nature != source.Nature || candidate.IsShiny != source.IsShiny
                || EntityGender.GetFromPID(source.Species, pid) != source.Gender
                || source.PersonalInfo.GetAbilityAtIndex((int)(pid & 1)) != source.Ability)
                continue;
            if (!IsLegal(candidate))
                continue;
            changes.Add($"恢复 PID 与个体值、性格、性别、闪光和特性的关联：{source.PID:X8} → {pid:X8}；上述属性不变");
            return new(candidate, true, "固定属性", changes);
        }
        return null;
    }

    /// <summary>Rebuilds an entity for an explicitly requested conversion or new legal copy.</summary>
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
        return WarehouseDatabase.Insert(repaired, EntityBytes(repaired), root, "valid", parent.Id);
    }

    public static StoredPokemon DiscardEdits(StoredPokemon stored)
    {
        var original = EntityFormat.GetFromBytes(ReadOriginalBytes(stored)) ?? throw new InvalidDataException("Original Pokémon is unreadable.");
        return WarehouseDatabase.Update(stored, null, original.Species, "stale", restoreOriginal: true);
    }

    public static StoredPokemon SetLegality(StoredPokemon stored, string status)
    {
        if (!LegalityStates.Contains(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        return WarehouseDatabase.Update(stored, null, stored.Species, status);
    }

    public static void Remove(StoredPokemon stored)
    {
        Release(Path.GetDirectoryName(stored.DatabasePath)!, [stored]);
    }

    /// <summary>Permanently removes the exact confirmed revisions in one all-or-nothing transaction.</summary>
    public static void Release(string root, IReadOnlyList<StoredPokemon> records) => WarehouseDatabase.Release(root, records);

    public static StoredPokemon? GetLatest(string root)
        => List(root).FirstOrDefault();

    public static IReadOnlyList<StoredPokemon> List(string root)
    {
        return WarehouseDatabase.List(root);
    }

    static byte[] EntityBytes(PKM pokemon)
    {
        // Status is party-only data; the stored/box format would silently discard it.
        var data = new byte[pokemon.SIZE_PARTY];
        pokemon.Clone().WriteEncryptedDataParty(data);
        // Verify before committing: moving an unreadable payload into place would destroy
        // the previous working copy with nothing left to fall back to.
        if (EntityFormat.GetFromBytes(data.ToArray()) is null)
            throw new InvalidDataException("Working copy verification failed.");
        return data;
    }

}
