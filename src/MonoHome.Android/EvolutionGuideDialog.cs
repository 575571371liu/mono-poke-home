namespace MonoHome.Android;

using global::Android.App;
using global::Android.Graphics;
using global::Android.Graphics.Drawables;
using global::Android.Views;
using global::Android.Widget;
using MonoHome.Core.Pokemon;
using PKHeX.Core;

/// <summary>A reference view; the original individual remains untouched.</summary>
static class EvolutionGuideDialog
{
    public static Dialog Create(Activity activity, PKM pokemon, GameVersion game, bool currentSave,
        Action<GameVersion, EvolutionNodeKey>? evolve = null)
    {
        var originalGame = game;
        int Dp(float n) => (int)(n * activity.Resources!.DisplayMetrics!.Density + .5f);
        GradientDrawable Panel(string fill = "#12201F", string stroke = "#315249", int radius = 12)
        {
            var shape = new GradientDrawable();
            shape.SetColor(Color.ParseColor(fill)); shape.SetCornerRadius(Dp(radius));
            shape.SetStroke(Dp(1), Color.ParseColor(stroke)); return shape;
        }
        TextView Text(string text, int size = 12, string color = "#E9F4EF")
        {
            var view = new TextView(activity) { Text = text, TextSize = size };
            view.SetTextColor(Color.ParseColor(color)); view.SetIncludeFontPadding(false);
            view.SetLineSpacing(Dp(3), 1); return view;
        }
        var dialog = new Dialog(activity);
        var root = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(12), Dp(14), Dp(12), Dp(8)); root.Background = Panel("#0F201C", radius: 16);
        root.AddView(Text("进化与形态", 19, "#D6FF63"));
        var rule = Text("", 12, "#B4C9C0");
        root.AddView(rule, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(6) });
        var games = EvolutionGuide.Games.OrderBy(g => g.Context.Generation).ToArray();
        var picker = new Spinner(activity);
        picker.Adapter = new CompactSpinnerAdapter(activity, games.Select(g => $"第 {g.Context.Generation} 世代 · {EvolutionGuide.GameName(g)}").ToArray());
        picker.SetSelection(Array.IndexOf(games, game));
        picker.SetPopupBackgroundDrawable(Panel("#132A25"));
        root.AddView(picker, new LinearLayout.LayoutParams(-1, Dp(48)));
        var scroll = new ScrollView(activity) { VerticalScrollBarEnabled = false };
        var body = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        body.SetPadding(0, Dp(4), 0, Dp(8)); scroll.AddView(body);
        root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        var close = new Button(activity) { Text = "关闭" };
        close.SetAllCaps(false); close.SetTextColor(Color.ParseColor("#8DE4D1")); close.Background = Panel("#132A25", radius: 8);
        close.Click += (_, _) => dialog.Dismiss();
        root.AddView(close, new LinearLayout.LayoutParams(-1, Dp(48)) { TopMargin = Dp(8) });
        var current = new EvolutionNodeKey(pokemon.Species, pokemon.Form);
        var selected = current;
        var family = EvolutionGuide.Family(pokemon.Species);
        View? selectedDetails = null;
        var renderRevision = 0;
        dialog.DismissEvent += (_, _) => renderRevision++;
        var evaluations = new Dictionary<GameVersion, TradeEvolutionEvaluation>();

        async void TradeActions()
        {
            if (evolve is null || selected != current) return;
            var revision = renderRevision;
            var ruleGame = game;
            var card = (LinearLayout)Section("当前个体 · 按通讯条件进化", "正在检查…");
            var message = (TextView)card.GetChildAt(1)!;
            try
            {
                if (!evaluations.TryGetValue(ruleGame, out var evaluation))
                {
                    evaluation = await Task.Run(() => TradeEvolution.Evaluate(pokemon, ruleGame));
                    evaluations[ruleGame] = evaluation;
                }
                if (revision != renderRevision || activity.IsFinishing || activity.IsDestroyed) return;
                message.Text = $"按《{EvolutionGuide.GameName(ruleGame)}》规则\n按个体条件处理，不检查游戏剧情进度。\n" + evaluation.Reason;
                foreach (var branch in evaluation.Branches)
                {
                    card.AddView(Text(EvolutionGuide.NodeName(branch.Edge.Target, pokemon.Context) + " · " + branch.Reason, 12, "#B4C9C0"),
                        new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10) });
                    var button = new Button(activity) { Text = "按通讯条件进化", Enabled = branch.CanEvolve, TextSize = 12 };
                    button.SetAllCaps(false);
                    button.SetTextColor(Color.ParseColor(branch.CanEvolve ? "#142019" : "#91AAA1"));
                    button.SetBackgroundResource(branch.CanEvolve ? Resource.Drawable.primary_button : Resource.Drawable.card);
                    button.ContentDescription = "按通讯条件进化为" + EvolutionGuide.NodeName(branch.Edge.Target, pokemon.Context) + "，" + branch.Reason;
                    button.Click += (_, _) => evolve(ruleGame, branch.Edge.Target);
                    card.AddView(button, new LinearLayout.LayoutParams(-1, Dp(48)) { TopMargin = Dp(6) });
                }
            }
            catch (Exception ex)
            {
                if (revision == renderRevision && !activity.IsFinishing && !activity.IsDestroyed)
                    message.Text = "无法检查进化条件：" + ex.Message;
            }
        }

        View Section(string title, string text, bool collapsed = false)
        {
            var card = new LinearLayout(activity) { Orientation = Orientation.Vertical };
            card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10)); card.Background = Panel();
            var heading = Text(title + (collapsed ? "  ›" : ""), 13, "#D6FF63");
            card.AddView(heading);
            var content = Text(text, 12, "#E9F4EF");
            content.Visibility = collapsed ? ViewStates.Gone : ViewStates.Visible;
            card.AddView(content, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10) });
            if (collapsed)
            {
                card.SetMinimumHeight(Dp(56));
                card.Click += (_, _) =>
                {
                    var open = content.Visibility == ViewStates.Gone;
                    content.Visibility = open ? ViewStates.Visible : ViewStates.Gone;
                    heading.Text = title + (open ? "  ⌄" : "  ›");
                };
            }
            body.AddView(card, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10) });
            return card;
        }
        void Node(EvolutionNodeKey node, EntityContext context, LinearLayout? chain = null, GuideEdge? incoming = null)
        {
            var row = new LinearLayout(activity) { Orientation = chain is null ? Orientation.Horizontal : Orientation.Vertical };
            row.SetGravity(chain is null ? GravityFlags.CenterVertical : GravityFlags.Top | GravityFlags.CenterHorizontal); row.SetPadding(Dp(8), Dp(5), Dp(8), Dp(5));
            row.Background = Panel(node == current ? "#17312B" : "#12201F", node == current ? "#D6FF63" : "#315249", 8);
            row.SetMinimumHeight(Dp(48));
            var icon = new ImageView(activity);
            if (node.Form == 0)
            {
                var id = PokemonSprite.ResourceId(activity, node.Species, pokemon.IsShiny, false);
                if (id != 0) icon.SetImageResource(id);
            }
            icon.SetScaleType(ImageView.ScaleType.FitCenter);
            row.AddView(icon, new LinearLayout.LayoutParams(Dp(44), Dp(44)) { RightMargin = chain is null ? Dp(8) : 0 });
            var name = EvolutionGuide.NodeName(node, context);
            var suffix = node == current ? " · 当前个体" : "";
            if (node == selected && node != current) suffix += " · 正在查看";
            if (chain is not null) suffix = suffix.Replace(" · ", "\n");
            var label = Text(name + suffix, chain is null ? 12 : 10, node == current ? "#D6FF63" : "#E9F4EF");
            label.Gravity = chain is null ? GravityFlags.Left : GravityFlags.Center;
            row.AddView(label);
            if (incoming is not null)
            {
                var unavailable = EvolutionGuide.Availability(incoming, game);
                var alternatives = family.Where(e => e.Source == incoming.Source && e.Target == incoming.Target && e.Context == incoming.Context).DistinctBy(e => e.Method).Count();
                var brief = unavailable.Length != 0 ? "本作不可用" : alternatives > 1 ? $"{alternatives} 种进化方式" : EvolutionGuide.Condition(incoming.Method, game);
                var condition = Text(brief + " ›", 10, "#8DE4D1");
                condition.Gravity = GravityFlags.Center;
                condition.SetPadding(0, Dp(8), 0, Dp(4));
                condition.SetMinimumHeight(Dp(48));
                View? detail = null;
                condition.Click += (_, _) =>
                {
                    if (detail is not null) { body.RemoveView(detail); detail = null; condition.Text = brief + " ›"; return; }
                    detail = Section(EvolutionGuide.NodeName(incoming.Source, context) + " → " + name,
                        EvolutionGuide.DetailedConnectionCondition(incoming, game, pokemon));
                    body.RemoveView(detail);
                    body.AddView(detail, body.IndexOfChild(chain) + 1);
                    condition.Text = brief + " ⌄";
                };
                row.AddView(condition);
            }
            row.ContentDescription = name + suffix + "，点击查看条件与形态";
            if (node == selected) selectedDetails = row;
            row.Click += (_, _) =>
            {
                selected = node; Render();
                scroll.Post(() => scroll.SmoothScrollTo(0, selectedDetails?.Top ?? 0));
            };
            (chain ?? body).AddView(row, new LinearLayout.LayoutParams(chain is null ? -1 : 0, -2, chain is null ? 0 : 1) { TopMargin = Dp(8), BottomMargin = chain is null ? 0 : Dp(8) });
        }
        void Render()
        {
            renderRevision++;
            body.RemoveAllViews();
            rule.Text = currentSave && game == originalGame ? "按当前存档查看进化条件" : "选择作品，查看对应进化条件";
            body.AddView(Text("完整进化家族", 13, "#D6FF63"));
            var context = game.Context;
            var visible = family.GroupBy(e => (e.Source, e.Target)).Select(group => group.FirstOrDefault(e => e.Context == context) ?? group.OrderByDescending(e => (byte)e.Context).First()).ToArray();
            var horizontal = visible.Length is 1 or 2 && visible.Select(e => e.Source).Distinct().Count() == visible.Length
                && visible.Select(e => e.Target).Distinct().Count() == visible.Length
                && (visible.Length == 1 || visible.Any(e => visible.Any(other => other.Source == e.Target)))
                && activity.Resources!.Configuration!.FontScale <= 1.2f
                && activity.Resources.DisplayMetrics!.WidthPixels / activity.Resources.DisplayMetrics.Density >= 340;
            if (horizontal)
            {
                var first = visible.First(e => !visible.Any(other => other.Target == e.Source));
                var chain = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
                chain.SetGravity(GravityFlags.Top);
                chain.LayoutChange += (_, _) =>
                {
                    var height = 0;
                    for (var i = 0; i < chain.ChildCount; i++)
                        if (chain.GetChildAt(i) is LinearLayout node) height = Math.Max(height, node.Height);
                    for (var i = 0; i < chain.ChildCount; i++)
                        if (chain.GetChildAt(i) is LinearLayout node && node.MinimumHeight != height) node.SetMinimumHeight(height);
                };
                body.AddView(chain, new LinearLayout.LayoutParams(-1, -2));
                Node(first.Source, first.Context, chain);
                var edge = first;
                for (var i = 0; i < visible.Length; i++)
                {
                    chain.AddView(Text("→", 12, "#8DE4D1"), new LinearLayout.LayoutParams(Dp(18), -2) { Gravity = GravityFlags.CenterVertical });
                    Node(edge.Target, edge.Context, chain, edge);
                    var nextEdge = visible.FirstOrDefault(e => e.Source == edge.Target);
                    if (nextEdge is null) break;
                    edge = nextEdge;
                }
            }
            if (visible.Length == 0)
            {
                Node(current, pokemon.Context);
                body.AddView(Text("无普通进化。", 11, "#91AAA1"));
            }
            foreach (var edge in visible)
            {
                if (horizontal) continue;
                if (!horizontal) Node(edge.Source, edge.Context);
                var inContext = family.Any(e => e.Source == edge.Source && e.Target == edge.Target && e.Context == context);
                var available = inContext && EvolutionGuide.IsInGame(edge.Source, game) && EvolutionGuide.IsInGame(edge.Target, game);
                var unavailable = EvolutionGuide.Availability(edge, game);
                var brief = available ? EvolutionGuide.Condition(edge.Method, game) : "本作不可用 · 点击查看参考条件";
                if (unavailable.Length != 0) brief = unavailable;
                var prefix = horizontal ? EvolutionGuide.NodeName(edge.Source, edge.Context) + " → " + EvolutionGuide.NodeName(edge.Target, edge.Context) + "\n" : "↓  ";
                var condition = Text(prefix + brief + "  ›", 12, available && unavailable.Length == 0 ? "#8DE4D1" : "#B4C9C0");
                condition.SetPadding(Dp(14), Dp(10), Dp(8), Dp(10)); condition.SetMinimumHeight(Dp(48));
                var expanded = false;
                condition.Click += (_, _) =>
                {
                    expanded = !expanded;
                    condition.Text = expanded ? prefix + EvolutionGuide.DetailedConnectionCondition(edge, game, pokemon) : prefix + brief;
                };
                body.AddView(condition);
                if (!horizontal) Node(edge.Target, edge.Context);
            }
            TradeActions();
            body.AddView(Text("特殊变化与其他形态", 13, "#D6FF63"), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(20) });
            var forms = EvolutionGuide.SpecialForms(selected.Species, selected.Form).GroupBy(f => (f.Form, f.Name)).Select(g => g.FirstOrDefault(f => f.Context == context) ?? g.OrderByDescending(f => (byte)f.Context).First()).ToArray();
            foreach (var form in forms)
                Section(form.Name, (form.Context == context && EvolutionGuide.IsInGame(new(selected.Species, form.Form), game) ? "本作形态参考" : $"本作不可用 · {EvolutionGuide.GameName(EvolutionGuide.ReferenceGame(form.Context))}参考") + "\n专属形态图片尚未收录\n" + form.Details, true);
            if (forms.Length == 0) Section("Mega／其他形态", "本地资料未收录该种类的其他形态。可点击家族其他阶段查看。", true);
            var dynamax = game is GameVersion.SW or GameVersion.SH;
            var gmax = Gigantamax.CanToggle(selected.Species, selected.Form) || selected.Species == 809;
            var forbidden = selected.Species is 888 or 889 or 890 || !EvolutionGuide.IsInGame(selected, GameVersion.SW);
            Section("极巨化／超极巨化", (dynamax && !forbidden ? "在允许极巨化的战斗中使用极巨腕带，持续 3 回合；每场战斗通常只能使用一次。" : "本作或该种类不可用；以下为剑／盾参考。") + "\n" + (gmax ? selected.Species == 809 ? "美录梅塔的超极巨化资格来自特殊赠送个体，不能通过极巨汤获得。" : "该种类支持超极巨化；需个体具有标记，可通过对应极巨团体战或铠之孤岛极巨汤获得，武道熊师另需极巨蜜汁。" : "该种类没有已收录的超极巨化形态。") + "\n" + (selected != current ? "正在浏览种类资料。" : pokemon is IGigantamaxReadOnly flag ? $"当前个体超极巨化标记：{(flag.CanGigantamax ? "有" : "无")}" : "当前格式无超极巨化标记数据。"), true);
            var tera = game is GameVersion.SL or GameVersion.VL;
            var teraType = selected == current && pokemon is ITeraTypeReadOnly t ? $"当前个体太晶属性：{EvolutionGuide.TeraName(t.TeraType)}" : "当前格式无太晶属性数据，或正在浏览其他种类。";
            Section("太晶化", (tera && EvolutionGuide.IsInGame(selected, game) ? "获得太晶珠并充能后，每场战斗可触发一次；通常防守属性变为太晶属性，原属性招式仍保留相应加成。星晶属性使用特殊规则。" : "本作或该种类不可用；以下为朱／紫参考。") + "\n" + teraType + (selected.Species is 1017 or 1024 ? "\n该种类不能通过普通太晶碎块自由更改太晶属性。" : "\n在锦汇镇使用对应太晶碎块可更改属性；星晶需要 DLC 对应进度。"), true);
            var source = Text("资料来源与覆盖说明  ⌄", 11, "#8DE4D1"); source.SetMinimumHeight(Dp(48));
            source.Click += (_, _) => source.Text = "基础关系与形态：本地 PKHeX.Core。具体作品的特殊条件仍在核实；标记未核实的条件不能作为确定操作指南。\n缺失图片使用文字说明，不以普通图片冒充。";
            body.AddView(source, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
        }
        picker.ItemSelected += (_, args) => { game = games[args.Position]; Render(); };
        Render();
        dialog.SetContentView(root);
        return dialog;
    }
}
