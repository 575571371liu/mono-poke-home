using PKHeX.Core;

namespace MonoHome.Core.Pokemon;

public sealed record TradeEvolutionBranch(GuideEdge Edge, bool CanEvolve, string Reason);
public sealed record TradeEvolutionEvaluation(bool CanUse, string Reason, IReadOnlyList<TradeEvolutionBranch> Branches);

public static class TradeEvolution
{
    static readonly GameVersion[] Games = [GameVersion.R, GameVersion.S, GameVersion.E, GameVersion.FR, GameVersion.LG,
        GameVersion.D, GameVersion.P, GameVersion.Pt, GameVersion.HG, GameVersion.SS];

    public static GameVersion DefaultGame(PKM pokemon) => pokemon.Version.Context == pokemon.Context && Games.Contains(pokemon.Version)
        ? pokemon.Version : EvolutionGuide.ReferenceGame(pokemon.Context);

    public static TradeEvolutionEvaluation Evaluate(PKM source, GameVersion game)
    {
        if (source is not (PK3 or PK4)) return new(false, "该格式的通讯条件进化尚未支持。", []);
        if (!Games.Contains(game) || game.Context != source.Context)
            return new(false, "该作品与个体格式不匹配，仅可查看资料。", []);
        if (source.IsEgg) return new(false, "蛋不能进化，请先孵化。", []);
        if (!new LegalityAnalysis(source).Valid)
            return new(false, "当前个体尚未通过合法性检查，请先使用编辑／合法化更新。", []);
        var current = new EvolutionNodeKey(source.Species, source.Form);
        var branches = EvolutionGuide.Family(source.Species)
            .Where(e => e.Source == current && e.Context == source.Context && e.Method.Method is EvolutionType.Trade or EvolutionType.TradeHeldItem)
            .DistinctBy(e => (e.Target, e.Method)).Select(e =>
            {
                var reason = EvolutionGuide.Availability(e, game);
                var everstone = source.Context == EntityContext.Gen3 ? 195 : 229;
                if (reason.Length == 0 && source.HeldItem == everstone && !(source is PK4 && source.Species == 64))
                    reason = "携带不变之石，不能进化。";
                if (reason.Length == 0 && e.Method.Method == EvolutionType.TradeHeldItem && source.HeldItem != e.Method.Argument)
                {
                    var items = GameInfo.GetStrings("zh-Hans").GetItemStrings(game.Context, game);
                    reason = $"需要携带{items[e.Method.Argument]}；当前携带{items[source.HeldItem]}。";
                }
                return new TradeEvolutionBranch(e, reason.Length == 0, reason.Length == 0 ? EvolutionGuide.Condition(e.Method, game) : reason);
            }).ToArray();
        return new(true, branches.Length == 0 ? "当前个体没有可执行的通讯进化分支。" : "", branches);
    }

    public static PKM Preview(PKM source, GameVersion game, EvolutionNodeKey target)
    {
        var evaluation = Evaluate(source, game);
        if (!evaluation.CanUse) throw new InvalidOperationException(evaluation.Reason);
        var branch = evaluation.Branches.FirstOrDefault(b => b.Edge.Target == target && b.CanEvolve);
        if (branch is null)
            throw new InvalidOperationException(evaluation.Branches.FirstOrDefault(b => b.Edge.Target == target)?.Reason ?? "目标不是当前个体可执行的直接通讯进化分支。");
        var next = source.Clone();
        var abilitySlot = source.AbilityNumber == 2 ? 1 : 0;
        next.Species = target.Species; next.Form = target.Form;
        next.RefreshAbility(abilitySlot);
        if (!source.IsNicknamed) next.ClearNickname();
        if (branch.Edge.Method.Method == EvolutionType.TradeHeldItem) next.HeldItem = 0;
        if (next.CurrentLevel != source.CurrentLevel) throw new InvalidOperationException("进化后的经验与等级不一致，未修改个体。");
        next.SetStats(next.GetStats(next.PersonalInfo));
        next.Stat_Level = next.CurrentLevel;
        if (source.PartyStatsPresent)
            next.Stat_HPCurrent = source.Stat_HPCurrent == 0 ? 0 : Math.Clamp(source.Stat_HPCurrent + next.Stat_HPMax - source.Stat_HPMax, 1, next.Stat_HPMax);
        next.RefreshChecksum();
        VerifyPreserved(source, next);
        if (!new LegalityAnalysis(next).Valid)
            throw new InvalidOperationException("进化结果未通过合法性检查，未修改个体。\n" + new LegalityAnalysis(next).Report());
        return next;
    }

    // Compare all stored fields after undoing only explicitly allowed changes.
    public static void VerifyPreserved(PKM source, PKM result)
    {
        var restored = result.Clone();
        restored.Species = source.Species; restored.Form = source.Form;
        restored.AbilityNumber = source.AbilityNumber; restored.Ability = source.Ability;
        restored.HeldItem = source.HeldItem; restored.IsNicknamed = source.IsNicknamed;
        source.NicknameTrash.CopyTo(restored.NicknameTrash);
        restored.Stat_Level = source.Stat_Level;
        restored.Stat_HPCurrent = source.Stat_HPCurrent; restored.Stat_HPMax = source.Stat_HPMax;
        restored.Stat_ATK = source.Stat_ATK; restored.Stat_DEF = source.Stat_DEF; restored.Stat_SPE = source.Stat_SPE;
        restored.Stat_SPA = source.Stat_SPA; restored.Stat_SPD = source.Stat_SPD;
        restored.RefreshChecksum();
        var original = source.Clone(); original.RefreshChecksum();
        if (!original.Data.SequenceEqual(restored.Data))
            throw new InvalidOperationException("进化改变了应保留的个体字段，未保存。");
    }
}
