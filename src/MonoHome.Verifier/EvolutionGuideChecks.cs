using MonoHome.Core.Pokemon;
using PKHeX.Core;

namespace MonoHome.Verifier;

static class EvolutionGuideChecks
{
    public static void Run()
    {
        Require(EvolutionGuide.Games.Select(g => g.Context.Generation).SequenceEqual(EvolutionGuide.Games.Select(g => g.Context.Generation).Order()), "games follow generation order");
        var family = EvolutionGuide.Family(135);
        Require(family.Any(e => e.Source.Species == 133 && e.Target.Species == 134), "evolved Eevee includes siblings");
        Require(family.Any(e => e.Target.Species == 700), "future branches remain visible");
        var pk = new PK3 { Species = 4, Version = GameVersion.E, CurrentLevel = 15 };
        var bytes = pk.Data.ToArray();
        var edge = EvolutionGuide.Family(4).First(e => e.Source.Species == 4 && e.Target.Species == 5 && e.Context == EntityContext.Gen3);
        Require(EvolutionGuide.Condition(edge.Method, GameVersion.E).Contains("16"), "level is translated");
        Require(EvolutionGuide.Progress(pk, edge.Method, GameVersion.E).Contains("尚未满足"), "insufficient level");
        pk.CurrentLevel = 16;
        Require(EvolutionGuide.Progress(pk, edge.Method, GameVersion.E).Contains("升级"), "level still requires trigger");
        pk.CurrentLevel = 15;
        Require(bytes.SequenceEqual(pk.Data), "guide does not mutate Pokemon");
        var magnetic = new EvolutionMethod(462, 0, 0, EvolutionType.LevelUpElectric, 0, 1);
        Require(EvolutionGuide.Availability(magnetic, GameVersion.HG).Contains("本作不可用"), "HGSS lacks magnetic evolution location");
        Require(!EvolutionGuide.Availability(magnetic, GameVersion.Pt).Contains("本作不可用"), "Platinum has magnetic location");
        var moss = EvolutionGuide.Family(133).First(e => e.Source.Species == 133 && e.Target.Species == 470 && e.Context == EntityContext.Gen4);
        Require(EvolutionGuide.Availability(moss, GameVersion.HG).Contains("本作不可用"), "shared edge availability blocks HGSS moss evolution");
        Require(EvolutionGuide.DetailedCondition(moss, GameVersion.HG).Contains("本作不可用") && EvolutionGuide.DetailedCondition(moss, GameVersion.HG).Contains("白金参考"), "expanded unavailable condition retains warning and reference game");
        Require(EvolutionGuide.DefaultGame(new PB7 { Version = GameVersion.GO }) == GameVersion.GP, "GO origin uses explicit supported format reference");
        var feebas = EvolutionGuide.Family(349).First(e => e.Source.Species == 349 && e.Target.Species == 350 && e.Context == EntityContext.Gen6);
        var alternatives = EvolutionGuide.DetailedConnectionCondition(feebas, GameVersion.OR);
        Require(alternatives.Contains("交换") && alternatives.Contains("美丽") && alternatives.Contains("或"), "connection details retain every evolution method");
        Require(EvolutionGuide.SpecialForms(6, 0).Any(f => f.Name.Contains("超级")), "Charizard Mega forms");
        Require(EvolutionGuide.SpecialForms(382, 0).Any(f => f.Name.Contains("原始")), "Kyogre primal form");
        Require(EvolutionGuide.TeraName((MoveType)99) == "星晶", "Stellar uses its display name, not an array index");
        foreach (var game in EvolutionGuide.Games)
        {
            _ = EvolutionGuide.IsInGame(new(133, 0), game);
            foreach (var e in family.Where(e => e.Context == game.Context))
                Require(EvolutionGuide.Condition(e.Method, game).Length > 0, "game-specific conditions are readable");
        }
        var gift = new PK8 { Species = 133, Version = GameVersion.SW, CanGigantamax = true };
        Require(EvolutionGuide.Progress(gift, new(134, 84, 0, EvolutionType.UseItem, 0, 0), GameVersion.SW).Contains("不能进化"), "Gigantamax Eevee cannot evolve");
        Console.WriteLine("Evolution guide checks passed.");
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
