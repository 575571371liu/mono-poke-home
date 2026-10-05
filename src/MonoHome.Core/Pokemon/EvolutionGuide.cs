using PKHeX.Core;
using static PKHeX.Core.EvolutionType;

namespace MonoHome.Core.Pokemon;

public readonly record struct EvolutionNodeKey(ushort Species, byte Form);
public sealed record GuideEdge(EvolutionNodeKey Source, EvolutionNodeKey Target, EvolutionMethod Method, EntityContext Context);
public sealed record GuideForm(byte Form, string Name, string Details, EntityContext Context);

/// <summary>Read-only reference data; never uses legality history as current evolution eligibility.</summary>
public static class EvolutionGuide
{
    static readonly (EntityContext Context, IPersonalTable Table)[] Tables =
    [
        (EntityContext.Gen1, PersonalTable.Y), (EntityContext.Gen2, PersonalTable.C),
        (EntityContext.Gen3, PersonalTable.RS), (EntityContext.Gen4, PersonalTable.HGSS),
        (EntityContext.Gen5, PersonalTable.B2W2), (EntityContext.Gen6, PersonalTable.AO),
        (EntityContext.Gen7, PersonalTable.USUM), (EntityContext.Gen7b, PersonalTable.GG),
        (EntityContext.Gen8, PersonalTable.SWSH), (EntityContext.Gen8a, PersonalTable.LA),
        (EntityContext.Gen8b, PersonalTable.BDSP), (EntityContext.Gen9, PersonalTable.SV),
        (EntityContext.Gen9a, PersonalTable.ZA),
    ];
    static readonly Lazy<GuideEdge[]> Edges = new(BuildEdges);
    public static readonly GameVersion[] Games = [GameVersion.RD, GameVersion.BU, GameVersion.YW, GameVersion.GD, GameVersion.SI, GameVersion.C, GameVersion.R, GameVersion.S, GameVersion.E, GameVersion.FR, GameVersion.LG, GameVersion.D, GameVersion.P, GameVersion.Pt, GameVersion.HG, GameVersion.SS, GameVersion.B, GameVersion.W, GameVersion.B2, GameVersion.W2, GameVersion.X, GameVersion.Y, GameVersion.OR, GameVersion.AS, GameVersion.SN, GameVersion.MN, GameVersion.US, GameVersion.UM, GameVersion.GP, GameVersion.GE, GameVersion.SW, GameVersion.SH, GameVersion.BD, GameVersion.SP, GameVersion.PLA, GameVersion.SL, GameVersion.VL, GameVersion.ZA];

    public static string GameName(GameVersion game) => game switch
    {
        GameVersion.E => "绿宝石", GameVersion.HG => "心金", GameVersion.SS => "魂银",
        GameVersion.R => "红宝石", GameVersion.S => "蓝宝石", GameVersion.FR => "火红", GameVersion.LG => "叶绿",
        GameVersion.B => "黑", GameVersion.W => "白", GameVersion.B2 => "黑 2", GameVersion.W2 => "白 2",
        GameVersion.SN => "太阳", GameVersion.MN => "月亮", GameVersion.GP => "Let's Go 皮卡丘", GameVersion.GE => "Let's Go 伊布",
        GameVersion.RD => "红", GameVersion.BU => "蓝", GameVersion.YW => "黄", GameVersion.GD => "金", GameVersion.SI => "银", GameVersion.C => "水晶",
        GameVersion.D => "钻石", GameVersion.P => "珍珠", GameVersion.Pt => "白金",
        GameVersion.X => "X", GameVersion.Y => "Y", GameVersion.OR => "欧米伽红宝石", GameVersion.AS => "阿尔法蓝宝石",
        GameVersion.US => "究极之日", GameVersion.UM => "究极之月", GameVersion.SW => "剑", GameVersion.SH => "盾",
        GameVersion.PLA => "传说 阿尔宙斯", GameVersion.BD => "晶灿钻石", GameVersion.SP => "明亮珍珠",
        GameVersion.SL => "朱", GameVersion.VL => "紫", GameVersion.ZA => "传说 Z-A", _ => game.ToString(),
    };

    public static GameVersion ReferenceGame(EntityContext context) => context switch
    {
        EntityContext.Gen1 => GameVersion.YW, EntityContext.Gen2 => GameVersion.C,
        EntityContext.Gen3 => GameVersion.E, EntityContext.Gen4 => GameVersion.Pt,
        EntityContext.Gen5 => GameVersion.B2, EntityContext.Gen6 => GameVersion.OR,
        EntityContext.Gen7 => GameVersion.US, EntityContext.Gen7b => GameVersion.GP,
        EntityContext.Gen8 => GameVersion.SW, EntityContext.Gen8a => GameVersion.PLA,
        EntityContext.Gen8b => GameVersion.BD, EntityContext.Gen9 => GameVersion.SL,
        EntityContext.Gen9a => GameVersion.ZA, _ => throw new ArgumentOutOfRangeException(nameof(context)),
    };

    public static GameVersion DefaultGame(PKM pokemon) => Games.Contains(pokemon.Version) ? pokemon.Version : ReferenceGame(pokemon.Context);

    public static string Availability(GuideEdge edge, GameVersion game)
    {
        if (!Games.Contains(game)) return "资料未核实：该作品尚未收录具体进化规则。";
        if (edge.Context != game.Context || !IsInGame(edge.Source, game) || !IsInGame(edge.Target, game))
            return "本作不可用：该进化分支不在本作资料中。";
        return Availability(edge.Method, game);
    }

    public static string DetailedCondition(GuideEdge edge, GameVersion game)
    {
        var unavailable = Availability(edge, game);
        var reference = unavailable.Length == 0 ? game : ReferenceGame(edge.Context);
        return (unavailable.Length == 0 ? "" : unavailable + "\n" + GameName(reference) + "参考：") + Condition(edge.Method, reference);
    }

    public static string DetailedConnectionCondition(GuideEdge edge, GameVersion game, PKM? individual = null) =>
        string.Join("\n\n或\n\n", Family(edge.Source.Species)
            .Where(e => e.Source == edge.Source && e.Target == edge.Target && e.Context == edge.Context)
            .DistinctBy(e => e.Method)
            .Select(e => DetailedCondition(e, game) + (individual is not null && individual.Species == e.Source.Species && individual.Form == e.Source.Form && Availability(e, game).Length == 0
                ? "\n" + Progress(individual, e.Method, game) : "")));

    public static GuideEdge[] Family(ushort species)
    {
        var relatives = new HashSet<ushort> { species };
        // ponytail: a small cached graph scan; use adjacency lists if the family catalog grows materially.
        bool changed;
        do
        {
            changed = false;
            foreach (var edge in Edges.Value)
                if (relatives.Contains(edge.Source.Species) || relatives.Contains(edge.Target.Species))
                {
                    changed |= relatives.Add(edge.Source.Species);
                    changed |= relatives.Add(edge.Target.Species);
                }
        } while (changed);
        return Edges.Value.Where(e => relatives.Contains(e.Source.Species)).ToArray();
    }

    static GuideEdge[] BuildEdges()
    {
        var result = new List<GuideEdge>();
        foreach (var (context, table) in Tables)
        {
            var tree = EvolutionTree.GetEvolutionTree(context);
            for (ushort species = 1; species <= table.MaxSpeciesID; species++)
                for (byte form = 0; form < Math.Max(1, (int)table[species].FormCount); form++)
                {
                    if (!table.IsPresentInGame(species, form) || FormInfo.IsBattleOnlyForm(species, form, context.Generation)) continue;
                    foreach (var method in tree.Forward.GetForward(species, form).Span)
                        if (method.Species != 0)
                            result.Add(new(new(species, form), new(method.Species, method.GetDestinationForm(form)), method, context));
                }
        }
        return result.Distinct().ToArray();
    }

    public static string NodeName(EvolutionNodeKey node, EntityContext context)
    {
        var strings = GameInfo.GetStrings("zh-Hans");
        var name = At(strings.Species, node.Species, $"宝可梦 #{node.Species}");
        if (node.Form == 0) return name;
        var forms = FormConverter.GetFormList(node.Species, strings.Types, strings.forms, context);
        return $"{name} · {At(forms, node.Form, $"形态 {node.Form}")}";
    }

    public static bool IsInGame(EvolutionNodeKey node, GameVersion game) => Table(game).IsPresentInGame(node.Species, node.Form);
    public static string TeraName(MoveType type)
    {
        if ((byte)type == TeraTypeUtil.Stellar) return "星晶";
        return At(GameInfo.GetStrings("zh-Hans").Types, (int)type, "未知太晶属性");
    }
    static IPersonalTable Table(GameVersion game) => game switch
    {
        GameVersion.E => PersonalTable.E, GameVersion.HG or GameVersion.SS => PersonalTable.HGSS,
        GameVersion.D or GameVersion.P => PersonalTable.DP, GameVersion.Pt => PersonalTable.Pt,
        GameVersion.X or GameVersion.Y => PersonalTable.XY, GameVersion.OR or GameVersion.AS => PersonalTable.AO,
        GameVersion.SN or GameVersion.MN => PersonalTable.SM,
        GameVersion.R or GameVersion.S => PersonalTable.RS, GameVersion.FR => PersonalTable.FR, GameVersion.LG => PersonalTable.LG,
        GameVersion.B or GameVersion.W => PersonalTable.BW,
        _ => Tables.First(t => t.Context == game.Context).Table,
    };

    public static string Availability(EvolutionMethod method, GameVersion game)
    {
        if (!Games.Contains(game)) return "资料未核实：该作品尚未收录具体进化规则。";
        if (game is GameVersion.HG or GameVersion.SS && method.Method is LevelUpElectric or LevelUpForest or LevelUpCold)
            return "本作不可用：没有对应进化地点，需在钻石／珍珠／白金中进化。";
        if (game is GameVersion.FR or GameVersion.LG && method.Method is LevelUpFriendshipMorning or LevelUpFriendshipNight)
            return "本作不可用：没有对应的时间进化机制。";
        return "";
    }

    public static string Condition(EvolutionMethod method, GameVersion game)
    {
        var strings = GameInfo.GetStrings("zh-Hans");
        var items = strings.GetItemStrings(game.Context, game);
        var item = At(items, method.Argument, $"道具 #{method.Argument}");
        var move = At(strings.Move, method.Argument, $"招式 #{method.Argument}");
        var condition = method.Method switch
        {
            LevelUp => "升级",
            LevelUpFriendship => "亲密度达到要求后升级",
            LevelUpFriendshipMorning => "亲密度达到要求，在白天升级",
            LevelUpFriendshipNight => "亲密度达到要求，在夜晚升级",
            Trade => game == GameVersion.PLA ? "使用联系绳" : "连接交换",
            TradeHeldItem => game == GameVersion.PLA ? $"使用{item}" : $"携带{item}进行连接交换",
            TradeShelmetKarrablast => "小嘴蜗与盖盖虫互相连接交换",
            UseItem => $"使用{item}", UseItemMale => $"雄性，使用{item}", UseItemFemale => $"雌性，使用{item}",
            LevelUpATK => "升级时攻击大于防御", LevelUpAeqD => "升级时攻击等于防御", LevelUpDEF => "升级时攻击小于防御",
            LevelUpECl5 => $"升级；个体固定分支：{(game.Generation >= 6 ? "加密常数" : "性格值")}高 16 位除以 10 的余数小于 5",
            LevelUpECgeq5 => $"升级；个体固定分支：{(game.Generation >= 6 ? "加密常数" : "性格值")}高 16 位除以 10 的余数至少 5",
            LevelUpNinjask => "升级", LevelUpShedinja => "土居忍士进化时队伍有空位；第三世代后还需背包有精灵球",
            LevelUpBeauty => $"美丽度至少 {method.Argument} 后升级",
            LevelUpHeldItemDay => $"携带{item}，白天升级", LevelUpHeldItemNight => $"携带{item}，夜晚升级",
            LevelUpKnowMove => $"学会{move}后升级", LevelUpWithTeammate => $"队伍中有{At(strings.Species, method.Argument, "指定宝可梦")}时升级",
            LevelUpMale => "雄性升级", LevelUpFemale or LevelUpFormFemale1 => "雌性升级",
            LevelUpElectric => game is GameVersion.D or GameVersion.P or GameVersion.Pt ? "在天冠山升级" : "在特殊磁场附近升级",
            LevelUpForest => game is GameVersion.D or GameVersion.P or GameVersion.Pt ? "在百代森林的苔岩附近升级" : "在苔岩附近升级",
            LevelUpCold => game is GameVersion.D or GameVersion.P or GameVersion.Pt ? "在 217 号道路的冰岩附近升级" : "在冰岩附近升级",
            LevelUpInverted => "升级时倒置主机",
            LevelUpAffection50MoveType => "友好度达到要求，学会妖精属性招式后升级（后续作品使用亲密度规则）",
            LevelUpMoveType => $"学会{At(strings.Types, method.Argument, "指定属性")}属性招式后升级",
            LevelUpWeather => "在游戏支持的雨天条件下升级", LevelUpMorning => "白天升级", LevelUpNight => "夜晚升级",
            LevelUpVersion => "在指定版本升级（版本资料未核实）",
            LevelUpVersionDay => "在指定版本白天升级（版本资料未核实）",
            LevelUpVersionNight => "在指定版本夜晚升级（版本资料未核实）",
            LevelUpSummit => "在拉纳基拉山升级", LevelUpDusk => "黄昏时升级，需对应特性／形态",
            LevelUpWormhole => "在究极空间升级", UseItemWormhole => $"在究极空间使用{item}",
            CriticalHitsInBattle => "同一场战斗中命中要害 3 次",
            HitPointsLostInBattle => "受到至少 49 点伤害后前往指定地点（地点资料未核实）",
            Spin => "携带草莓等糖饰旋转；旋转方式与时间决定形态",
            LevelUpNatureAmped => "升级，性格决定高调形态", LevelUpNatureLowKey => "升级，性格决定低调形态",
            TowerOfDarkness => "完成恶之塔试炼", TowerOfWaters => "完成水之塔试炼",
            LevelUpWalkStepsWith => "使用 Let's Go 模式同行行走 1000 步，再升级",
            LevelUpUnionCircle => "在集友圈中升级", LevelUpInBattleEC100 or LevelUpInBattleECElse => "战斗中升级，个体固定分支决定形态",
            LevelUpCollect999 => "收集 999 枚索财灵的硬币后升级",
            LevelUpDefeatEquals => "携带头领凭证，击败 3 只携带该道具的劈斩司令后升级",
            LevelUpUseMoveSpecial => "使用愤怒之拳 20 次后升级",
            LevelUpKnowMoveECElse or LevelUpKnowMoveEC100 => $"学会{move}后升级，个体固定分支决定形态",
            LevelUpRecoilDamageMale => "雄性，累积至少 294 点反作用力伤害后升级",
            LevelUpRecoilDamageFemale => "雌性，累积至少 294 点反作用力伤害后升级",
            UseItemFullMoon => "满月时使用泥炭块", UseMoveAgileStyle => "使用迅疾屏障猛攻 20 次",
            UseMoveStrongStyle => "使用刚猛毒千针 20 次", UseMoveBarbBarrage => "使用毒千针达到要求（次数资料未核实）",
            _ => "特殊进化条件：资料未核实",
        };
        return (method.Level == 0 ? "" : $"Lv.{method.Level} 起 · ") + condition;
    }

    public static string Progress(PKM pk, EvolutionMethod method, GameVersion game)
    {
        var unavailable = Availability(method, game);
        if (unavailable.Length != 0) return unavailable;
        if (pk.IsEgg) return "需在游戏中确认：先孵化，再满足进化条件。";
        if (game is GameVersion.SW or GameVersion.SH && pk.Species is 25 or 52 or 133 && pk is IGigantamaxReadOnly { CanGigantamax: true })
            return "尚未满足：该个体拥有超极巨化标记，不能进化；需先在铠之孤岛使用极巨汤移除此标记。";
        var lines = new List<string>();
        if (method.Level > 0)
            lines.Add($"{(pk.CurrentLevel >= method.Level ? "已满足" : "尚未满足")}：等级 {pk.CurrentLevel}／{method.Level}");
        if (method.Method is LevelUpFriendship or LevelUpFriendshipMorning or LevelUpFriendshipNight)
        {
            var threshold = game.Generation >= 8 ? 160 : 220;
            lines.Add($"{(pk.CurrentFriendship >= threshold ? "已满足" : "尚未满足")}：亲密度 {pk.CurrentFriendship}／{threshold}");
        }
        if (method.Method is TradeHeldItem or LevelUpHeldItemDay or LevelUpHeldItemNight && game != GameVersion.PLA)
            lines.Add($"{(pk.HeldItem == method.Argument ? "已满足" : "尚未满足")}：携带指定道具");
        if (method.Method is LevelUpMale or UseItemMale or LevelUpFemale or UseItemFemale)
        {
            var gender = method.Method is LevelUpMale or UseItemMale ? 0 : 1;
            lines.Add($"{(pk.Gender == gender ? "已满足" : "尚未满足")}：{(gender == 0 ? "雄性" : "雌性")}");
        }
        if (method.Method == LevelUpKnowMove)
            lines.Add($"{(new[] { pk.Move1, pk.Move2, pk.Move3, pk.Move4 }.Contains(method.Argument) ? "已满足" : "尚未满足")}：已学会指定招式");
        if (method.Method == LevelUpBeauty && pk is IContestStatsReadOnly contest)
            lines.Add($"{(contest.ContestBeauty >= method.Argument ? "已满足" : "尚未满足")}：美丽度 {contest.ContestBeauty}／{method.Argument}");
        if (method.LevelUp != 0 && pk.CurrentLevel == 100)
            lines.Add("需在游戏中确认：已达 Lv.100，升级触发的替代方法取决于具体作品。");
        lines.Add(method.Method is LevelUp or LevelUpNinjask ? "需在游戏中确认：满足等级要求后升级触发。" : method.LevelUp != 0 ? "需在游戏中确认：满足上方全部条件后升级触发。" : "需在游戏中确认：完成上方指定动作。");
        return string.Join("\n", lines);
    }

    public static GuideForm[] SpecialForms(ushort species, byte sourceForm)
    {
        var strings = GameInfo.GetStrings("zh-Hans");
        var result = new List<GuideForm>();
        // Preserve forms from every supported context, including forms absent from newer games.
        foreach (var (context, table) in Tables)
        {
            if (species > table.MaxSpeciesID) continue;
            var names = FormConverter.GetFormList(species, strings.Types, strings.forms, context);
            for (byte form = 1; form < table[species].FormCount && form < names.Length; form++)
            {
                var info = table.GetFormEntry(species, form);
                if (info.HP == 0) continue;
                var mega = FormInfo.IsMegaForm(species, form);
                var primal = FormInfo.IsPrimalForm(species, form);
                var ability = context == EntityContext.Gen9a ? "本作没有特性机制" : "特性：" + At(strings.Ability, info.GetAbilityAtIndex(0), "未收录");
                var details = $"{At(strings.Types, info.Type1, "")}／{At(strings.Types, info.Type2, "")} · {ability}\n基础能力：HP {info.HP} · 攻击 {info.ATK} · 防御 {info.DEF}\n特攻 {info.SPA} · 特防 {info.SPD} · 速度 {info.SPE}";
                var itemId = ItemStorage9ZA.GetExpectedMegaStoneOrPrimalOrb(species, form);
                var item = At(strings.GetItemStrings(EntityContext.Gen9a, GameVersion.ZA), itemId, "对应进化石／宝珠");
                var requirement = primal ? $"原始回归：携带{item}进入战斗。" : mega ? context == EntityContext.Gen9a ? $"Mega 进化：携带{item}，获得超级环并在战斗中蓄满超级能量后触发；部分形态需 DLC。" : species == 384 ? "Mega 进化：学会画龙点睛，获得钥石后在战斗中触发；第七世代不能携带 Z 纯晶。" : $"Mega 进化：携带{item}，训练家获得钥石后在战斗中触发。" : OtherFormCondition(species, form);
                if (mega && context == EntityContext.Gen9a && itemId == 0)
                    requirement = "Mega 进化：该作品的具体触发道具／条件资料未核实。";
                result.Add(new(form, (primal ? "原始回归 · " : mega ? "超级进化 · " : "") + names[form], requirement + "\n" + details, context));
            }
        }
        return result.DistinctBy(f => (f.Form, f.Name, f.Context)).ToArray();
    }

    static string OtherFormCondition(ushort species, byte form) => species switch
    {
        386 => "代欧奇希斯：与作品中指定陨石互动切换形态；地点因作品而异。",
        479 => "洛托姆：与对应家电互动切换形态，并学习对应招式；具体地点因作品而异。",
        487 => "骑拉帝纳：毁坏的世界／携带白金宝珠可改变形态；后续作品道具规则不同。",
        492 => "谢米：白天使用葛拉西蒂亚花变为天空形态；夜晚或冰冻时恢复陆上形态。",
        493 => "阿尔宙斯：石板决定属性；传说 阿尔宙斯等作品的操作方式不同。",
        351 => "漂浮泡泡：战斗中由阴晴不定特性随天气改变形态，战斗外恢复。",
        421 => "樱花儿：战斗中由花之礼特性在晴天改变形态，战斗外恢复。",
        201 => "未知图腾：不同字母形态由遇见的个体决定，不能通过普通进化互换。",
        412 or 413 => "结草儿：最后战斗的场地决定蓑衣；雌性进化为结草贵妇后蓑衣固定。",
        422 or 423 => "无壳海兔／海兔兽：东西海形态由遇见的个体决定，不能自由切换。",
        646 => "酋雷姆：使用基因之楔与对应的莱希拉姆／捷克罗姆合体。",
        800 => "奈克洛兹玛：对应形态需要与索尔迦雷欧／露奈雅拉合体或进行战斗变化。",
        898 => "蕾冠王：使用牵绊缰绳与雪暴马／灵幽马合体。",
        648 => "美洛耶塔：战斗中使用古老之歌改变形态，战斗外恢复。",
        681 => "坚盾剑怪：战斗中由战斗切换特性，攻击招式／王者盾牌触发形态变化。",
        774 => "小陨星：战斗中由界限盾壳特性随 HP 改变形态；核心颜色由个体决定。",
        778 => "谜拟丘：画皮特性被攻击打破时改变外观，战斗外恢复。",
        875 => "冰砌鹅：结冻头特性被物理攻击打破时改变形态，天气可以恢复冰脸。",
        877 => "莫鲁贝可：战斗中由饱了又饿特性在回合结束时交替形态。",
        964 => "海豚侠：全能变身特性，在战斗中退出并再次上场后变为全能形态。",
        1017 => "厄诡椪：携带面具决定形态及太晶属性；太晶化时还会触发对应战斗形态。",
        1024 => "太乐巴戈斯：战斗中由太晶变形特性变化，太晶化进一步改变形态。",
        _ => "形态的获取／变化条件资料未核实；不能将所有形态视为可以自由切换。",
    };

    static string At(IReadOnlyList<string> values, int index, string fallback) => (uint)index < values.Count && !string.IsNullOrWhiteSpace(values[index]) ? values[index] : fallback;
}
