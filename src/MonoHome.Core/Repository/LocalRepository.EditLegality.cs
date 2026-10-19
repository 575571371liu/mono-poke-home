using PKHeX.Core;

namespace MonoHome.Core.Repository;

public static partial class LocalRepository
{
    /// <summary>Repairs edited attributes, preferring the original encounter before rebuilding its background.</summary>
    public static RepairOutcome RepairForEdit(PKM source)
    {
        var original = RepairPreservingFields(source);
        if (original.Valid || source is not (PK3 or PK4) || source.Generation is not (3 or 4)) return original;
        if (source.EVTotal > 510 || (source.IsEgg && source.EVTotal != 0))
            return new(source.Clone(), false, "编辑合法化", [], "努力值总计不能超过 510，蛋的努力值必须为 0；当前数据未修改。");
        if (source.IsEgg && (source.HeldItem != 0 || source.Generation != source.Format))
            return new(source.Clone(), false, "编辑合法化", [], "蛋不能携带道具，也不能跨世代传送；请调整蛋状态或携带道具。当前数据未修改。");
        var ability = source.PersonalInfo.GetIndexOfAbility(source.Ability);
        if (!IsValidGender(source, source.Gender) || ability < 0)
            return new(source.Clone(), false, "编辑合法化", [], "所选性别或实际特性不属于当前种类／形态，无法仅调整背景解决；当前数据未修改。");

        var trainer = new SimpleTrainerInfo(source.Version)
        {
            OT = source.OriginalTrainerName, Language = source.Language,
            TID16 = source.TID16, SID16 = source.SID16, Gender = source.OriginalTrainerGender,
        };
        var criteria = new EncounterCriteria
        {
            Nature = source.Nature, Gender = (Gender)source.Gender,
            Shiny = source.IsShiny ? Shiny.Always : Shiny.Never,
            Ability = ability == 1 ? AbilityPermission.OnlySecond : AbilityPermission.OnlyFirst,
            IV_HP = (sbyte)source.IV_HP, IV_ATK = (sbyte)source.IV_ATK, IV_DEF = (sbyte)source.IV_DEF,
            IV_SPE = (sbyte)source.IV_SPE, IV_SPA = (sbyte)source.IV_SPA, IV_SPD = (sbyte)source.IV_SPD,
        };
        string? lastReport = null;
        var attempted = 0;
        try
        {
            var encounters = EncounterMovesetGenerator.GenerateEncounters(source.Clone(),
                new ushort[] { source.Move1, source.Move2, source.Move3, source.Move4 }, source.Version);
            foreach (var encounter in encounters)
            {
                if (encounter is not IEncounterConvertible convertible || source.IsEgg && !encounter.IsEgg) continue;
                if (source.IsEgg && encounter.Species != source.Species) continue;
                // Wild RNG generators can loop forever for impossible exact IVs; reverse those via the existing repair path.
                PKM? template;
                try { template = convertible.ConvertToPKM(trainer, encounter is IEncounterEgg ? criteria : EncounterCriteria.Unrestricted); }
                catch (Exception ex) { lastReport = ex.Message; continue; }
                if (template.GetType() != source.GetType())
                {
                    template = EntityConverter.ConvertToType(template, source.GetType(), out _);
                    if (template is null) continue;
                }
                attempted++;
                var metLevels = !source.IsEgg && source.Generation == source.Format && encounter is IEncounterSlot3 or IEncounterSlot4
                    ? Enumerable.Range(encounter.LevelMin, encounter.LevelMax - encounter.LevelMin + 1)
                    : new[] { (int)template.MetLevel };
                foreach (var metLevel in metLevels)
                {
                    var candidate = source.Clone();
                    candidate.PID = encounter is IEncounterEgg ? template.PID : source.PID;
                    if (encounter is IEncounterEgg)
                    {
                        // shortcut: 32 independent egg PID attempts; use direct constrained generation if a valid PID is ever missed.
                        for (var attempt = 0; attempt < 32 && !IsEggPIDCompatible(candidate, encounter); attempt++)
                            candidate.PID = convertible.ConvertToPKM(trainer, criteria).PID;
                        if (!IsEggPIDCompatible(candidate, encounter)) continue;
                    }
                    candidate.MetLocation = template.MetLocation;
                    candidate.MetLevel = (byte)metLevel;
                    candidate.MetDate = template.MetDate;
                    candidate.EggLocation = template.EggLocation;
                    candidate.EggMetDate = template.EggMetDate;
                    candidate.Ball = template.Ball;
                    candidate.FatefulEncounter = template.FatefulEncounter;
                    if (candidate is PK4 pk4 && !GroundTileVerifier.IsGroundTileValid(encounter, pk4))
                        pk4.GroundTile = encounter is IGroundTypeTile tile ? tile.GroundTile.GetIndex() : GroundTileType.None;
                    if (source.IsEgg)
                    {
                        candidate.MetLocation = candidate is PK3 ? template.MetLocation : (ushort)0;
                        candidate.MetLevel = 0;
                        candidate.MetDate = null;
                        candidate.EXP = Experience.GetEXP(EggStateLegality.GetEggLevel(source.Generation), source.PersonalInfo.EXPGrowth);
                        candidate.OriginalTrainerFriendship = source.PersonalInfo.HatchCycles;
                        candidate.PrepareNickname();
                        candidate.Nickname = SpeciesName.GetEggName(source.Language, source.Generation);
                        candidate.IsNicknamed = EggStateLegality.IsNicknameFlagSet(encounter, candidate);
                    }
                    else
                    {
                        candidate.EXP = Math.Max(source.EXP, Experience.GetEXP((byte)Math.Max(metLevel, encounter.LevelMin), source.PersonalInfo.EXPGrowth));
                    }
                    if (candidate.Nature != source.Nature || candidate.IsShiny != source.IsShiny
                        || EntityGender.GetFromPID(source.Species, candidate.PID) != source.Gender) continue;
                    for (var level = (int)candidate.CurrentLevel; level <= 100; level++)
                    {
                        var result = RepairPreservingFields(candidate);
                        if (result.Valid)
                            return result with
                            {
                                Template = "相遇背景重建",
                                Changes = [$"原来源无法满足编辑值，改用同游戏合法相遇：{encounter.LongName}；种类、个体值、努力值、性格、性别、闪光、实际特性、招式和训练家保留",
                                    $"相遇地点／等级：{source.MetLocation}/{source.MetLevel} → {result.Pokemon.MetLocation}/{result.Pokemon.MetLevel}；蛋地点：{source.EggLocation} → {result.Pokemon.EggLocation}",
                                    $"精灵球：{source.Ball} → {result.Pokemon.Ball}；命运相遇标记：{source.FatefulEncounter} → {result.Pokemon.FatefulEncounter}；PID：{source.PID:X8} → {result.Pokemon.PID:X8}",
                                    $"经验：{source.EXP} → {result.Pokemon.EXP}；等级：{source.CurrentLevel} → {result.Pokemon.CurrentLevel}；不触发进化或学习招式", .. result.Changes],
                            };
                        var analysis = new LegalityAnalysis(candidate);
                        lastReport = analysis.Report();
                        if (source.IsEgg || level == 100 || encounter.Species == source.Species
                            && !analysis.Results.Any(r => !r.Valid && r.Identifier is CheckIdentifier.Evolution or CheckIdentifier.Level or CheckIdentifier.EVs)
                            && analysis.Info.Moves.All(m => m.Valid)) break;
                        candidate.CurrentLevel = (byte)(level + 1);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lastReport = "相遇候选生成或检查失败：" + ex.Message;
        }
        return new(source.Clone(), false, "编辑合法化", [],
            $"保留编辑属性尝试了 {attempted} 个同游戏相遇背景，仍未找到完整合法结果；当前数据未修改。\n{lastReport ?? original.FailureReason}");
    }

    static bool IsEggPIDCompatible(PKM candidate, IEncounterTemplate encounter) =>
        (!WurmpleUtil.IsWurmpleEvo(candidate.Species) || WurmpleUtil.IsWurmpleEvoValid(candidate))
        && (encounter is not IRandomCorrelation correlation
            || correlation.IsCompatible(MethodFinder.Analyze(candidate).Type, candidate) != RandomCorrelationRating.Mismatch);
}
