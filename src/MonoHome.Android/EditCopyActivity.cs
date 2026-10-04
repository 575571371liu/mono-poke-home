namespace MonoHome.Android;

using global::Android.App;
using global::Android.OS;
using global::Android.Text;
using global::Android.Widget;
using MonoHome.Core.Repository;
using PKHeX.Core;

[Activity(Label = "编辑宝可梦", Theme = "@style/AppTheme")]
public sealed class EditCopyActivity : Activity
{
    readonly Dictionary<string, EditText> fields = new();
    readonly Dictionary<string, int> selections = new();
    readonly Dictionary<string, Button> choiceButtons = new();
    readonly Dictionary<string, Switch> flags = new();
    StoredPokemon? record;
    PKM current = null!;
    Button save = null!;
    Button legalize = null!;
    GameStrings names = GameInfo.GetStrings("zh-Hans");
    TextView identity = null!;
    ImageView sprite = null!;
    bool standardNickname;

    int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + .5f);
    global::Android.Graphics.Drawables.GradientDrawable Panel(string fill = "#12201F", string stroke = "#315249")
    {
        var background = new global::Android.Graphics.Drawables.GradientDrawable();
        background.SetColor(global::Android.Graphics.Color.ParseColor(fill));
        background.SetCornerRadius(Dp(12));
        background.SetStroke(Dp(1), global::Android.Graphics.Color.ParseColor(stroke));
        return background;
    }
    TextView Text(string text, float size = 13, string color = "#E9F4EF")
    {
        var view = new TextView(this) { Text = text, TextSize = size, Gravity = global::Android.Views.GravityFlags.CenterVertical };
        view.SetTextColor(global::Android.Graphics.Color.ParseColor(color));
        return view;
    }
    Button Action(string label, bool primary = false)
    {
        var button = new Button(this) { Text = label, TextSize = 13 };
        button.SetAllCaps(false);
        button.SetTextColor(global::Android.Graphics.Color.ParseColor(primary ? "#0B1414" : "#8DE4D1"));
        button.Background = Panel(primary ? "#D6FF63" : "#132A25");
        return button;
    }
    LinearLayout Card(LinearLayout parent, string title)
    {
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.SetPadding(Dp(14), Dp(14), Dp(14), Dp(14));
        card.Background = Panel();
        card.AddView(Text(title, 16, "#D6FF63"), new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(10) });
        parent.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(12) });
        return card;
    }
    LinearLayout Row(LinearLayout parent)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.SetGravity(global::Android.Views.GravityFlags.CenterVertical);
        parent.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(8) });
        return row;
    }
    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        var id = Intent?.GetStringExtra("repository_id");
        record = LocalRepository.List(Path.Combine(FilesDir!.AbsolutePath, "warehouse")).FirstOrDefault(x => x.Id == id);
        if (record is null) { Finish(); return; }
        current = LocalRepository.LoadWorking(record);
        Window!.SetSoftInputMode(global::Android.Views.SoftInput.AdjustResize);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#0B1414"));
        root.SetPadding(Dp(16), Dp(10), Dp(16), Dp(12));
        var toolbar = Row(root);
        var back = Action("‹ 返回");
        back.Click += (_, _) => Finish();
        toolbar.AddView(back, new LinearLayout.LayoutParams(Dp(76), Dp(42)));
        var title = Text("编辑宝可梦", 20);
        title.SetPadding(Dp(12), 0, 0, 0);
        toolbar.AddView(title);
        var scroll = new ScrollView(this) { FillViewport = true, VerticalScrollBarEnabled = false };
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        scroll.AddView(content);
        root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        var header = Card(content, "当前工作副本");
        var headerRow = Row(header);
        sprite = new ImageView(this);
        sprite.SetScaleType(ImageView.ScaleType.FitCenter);
        headerRow.AddView(sprite, new LinearLayout.LayoutParams(Dp(72), Dp(72)));
        identity = Text("", 20);
        identity.SetPadding(Dp(14), 0, 0, 0);
        headerRow.AddView(identity);
        UpdateIdentity(current.Species);
        header.AddView(Text(new LegalityAnalysis(current).Valid ? "● 合法性检查通过" : "● 有待检查项目", 12, "#8DE4D1"));
        header.AddView(Text("未修改的属性保持原值，原始备份始终保留。", 11, "#91AAA1"));

        var basic = Card(content, "基本信息");
        var speciesRow = Row(basic);
        speciesRow.AddView(Text("种类", 12, "#91AAA1"), new LinearLayout.LayoutParams(Dp(84), -2));
        speciesRow.AddView(Text(names.Species[current.Species], 14, "#8DE4D1"), new LinearLayout.LayoutParams(0, Dp(46), 1));
        var nicknameRow = Row(basic);
        nicknameRow.AddView(Text("昵称", 12, "#91AAA1"), new LinearLayout.LayoutParams(Dp(84), -2));
        nicknameRow.AddView(Editor("Nickname", "昵称", current.Nickname, false), new LinearLayout.LayoutParams(0, Dp(46), 1));
        var normalize = Action("正式名称");
        normalize.ContentDescription = "按存档语言恢复正式名称";
        normalize.Click += (_, _) =>
        {
            fields["Nickname"].Text = SpeciesName.GetSpeciesNameGeneration(current.Species, current.Language, current.Generation);
            standardNickname = true;
        };
        nicknameRow.AddView(normalize, new LinearLayout.LayoutParams(Dp(86), Dp(46)) { LeftMargin = Dp(8) });
        Input(basic, "Level", "等级", current.CurrentLevel);
        Choice(basic, "Nature", "性格", (int)current.Nature, () => names.Natures.Select((name, id) => (id, name)).ToArray());
        Choice(basic, "Gender", "性别", current.Gender, () => [(0, "雄性 ♂"), (1, "雌性 ♀"), (2, "无性别")]);
        Choice(basic, "Form", "形态", current.Form, () => FormChoices(), () =>
        {
            selections["Ability"] = 0;
            choiceButtons["Ability"].Text = AbilityChoices()[0].Name;
        });
        Choice(basic, "Ability", "特性", current.AbilityNumber >> 1, () => AbilityChoices());
        Choice(basic, "Item", "携带道具", current.HeldItem, () => names.GetItemStrings(current.Context, current.Version).Take(current.MaxItemID + 1).Select((name, id) => (id, id == 0 ? "无" : name)).ToArray());
        Toggle(basic, "Shiny", "闪光", current.IsShiny);
        Toggle(basic, "Egg", "蛋", current.IsEgg);

        var stats = Card(content, "六维属性");
        stats.AddView(Text("能力值按当前工作副本展示；个体值 0–31，努力值总计 ≤510", 11, "#91AAA1"));
        var columns = Row(stats);
        foreach (var label in new[] { "属性", "能力", "个体值", "努力值" }) columns.AddView(Text(label, 11, "#91AAA1"), new LinearLayout.LayoutParams(0, Dp(32), 1));
        var values = current.GetStats(current.PersonalInfo).ToArray();
        var iv = new[] { current.IV_HP, current.IV_ATK, current.IV_DEF, current.IV_SPE, current.IV_SPA, current.IV_SPD };
        var ev = new[] { current.EV_HP, current.EV_ATK, current.EV_DEF, current.EV_SPE, current.EV_SPA, current.EV_SPD };
        var statNames = new[] { "HP", "攻击", "防御", "速度", "特攻", "特防" };
        foreach (var i in new[] { 0, 1, 2, 4, 5, 3 })
        {
            var row = Row(stats);
            row.AddView(Text(statNames[i]), new LinearLayout.LayoutParams(0, Dp(44), 1));
            row.AddView(Text(values[i].ToString(), 16, "#8DE4D1"), new LinearLayout.LayoutParams(0, Dp(44), 1));
            row.AddView(Editor($"IV{i}", statNames[i] + "个体值", iv[i]), new LinearLayout.LayoutParams(0, Dp(44), 1) { RightMargin = Dp(6) });
            row.AddView(Editor($"EV{i}", statNames[i] + "努力值", ev[i]), new LinearLayout.LayoutParams(0, Dp(44), 1));
        }
        var moves = Card(content, "招式");
        var moveIds = new[] { current.Move1, current.Move2, current.Move3, current.Move4 };
        var pp = new[] { current.Move1_PP, current.Move2_PP, current.Move3_PP, current.Move4_PP };
        for (var i = 0; i < 4; i++)
        {
            Choice(moves, $"Move{i}", $"招式 {i + 1}", moveIds[i], () => names.Move.Take(current.MaxMoveID + 1).Select((name, id) => (id, id == 0 ? "无" : name)).ToArray());
            moves.AddView(Text($"当前 PP {pp[i]} / {current.GetMovePP(moveIds[i], i switch { 0 => current.Move1_PPUps, 1 => current.Move2_PPUps, 2 => current.Move3_PPUps, _ => current.Move4_PPUps })}", 11, "#91AAA1"), new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(10) });
        }
        var status = Card(content, "其他状态");
        Choice(status, "Status", "异常状态", current.Status_Condition, () => [(0, "无"), (1, "睡眠 · 1 回合"), (2, "睡眠 · 2 回合"), (3, "睡眠 · 3 回合"), (4, "睡眠 · 4 回合"), (5, "睡眠 · 5 回合"), (6, "睡眠 · 6 回合"), (7, "睡眠 · 7 回合"), (8, "中毒"), (16, "灼伤"), (32, "冰冻"), (64, "麻痹"), (128, "剧毒")]);
        var more = Action("宝可病毒设置  ▾");
        var virus = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = global::Android.Views.ViewStates.Gone };
        Input(virus, "Strain", "病毒株 · 0–15", current.PokerusStrain);
        Input(virus, "Days", "剩余天数 · 0–15", current.PokerusDays);
        more.Click += (_, _) => virus.Visibility = virus.Visibility == global::Android.Views.ViewStates.Gone ? global::Android.Views.ViewStates.Visible : global::Android.Views.ViewStates.Gone;
        status.AddView(more, new LinearLayout.LayoutParams(-1, Dp(44)));
        status.AddView(virus);
        var legality = Card(content, "合法性检查");
        var analysis = new LegalityAnalysis(current);
        legality.AddView(Text(analysis.Valid ? "已通过合法性检查" : "存在待检查项目，可预览合法化更新", 13, analysis.Valid ? "#8DE4D1" : "#D6FF63"));
        var details = Text(analysis.Report(), 12, "#91AAA1");
        details.Visibility = global::Android.Views.ViewStates.Gone;
        var expand = Action("查看详细报告  ▾");
        expand.Click += (_, _) => details.Visibility = details.Visibility == global::Android.Views.ViewStates.Gone ? global::Android.Views.ViewStates.Visible : global::Android.Views.ViewStates.Gone;
        legality.AddView(expand, new LinearLayout.LayoutParams(-1, Dp(44)) { TopMargin = Dp(8) });
        legality.AddView(details);
        root.AddView(Text("保存仅更新编辑内容；合法化需预览并确认。", 11, "#91AAA1"), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(8), BottomMargin = Dp(8) });
        var actions = Row(root);
        legalize = Action("合法化预览");
        save = Action("保存编辑", true);
        legalize.Click += async (_, _) => await PreviewLegalization();
        save.Click += (_, _) => SaveEdit();
        actions.AddView(legalize, new LinearLayout.LayoutParams(0, Dp(48), 1) { RightMargin = Dp(10) });
        actions.AddView(save, new LinearLayout.LayoutParams(0, Dp(48), 1));
        root.FocusableInTouchMode = true;
        root.RequestFocus();
        SetContentView(root);
    }
    void UpdateIdentity(int species)
    {
        identity.Text = $"{names.Species[species]}\nLv.{current.CurrentLevel} · 工作副本";
        var icon = PokemonSprite.ResourceId(this, species, flags.TryGetValue("Shiny", out var shiny) ? shiny.Checked : current.IsShiny,
            flags.TryGetValue("Egg", out var egg) ? egg.Checked : current.IsEgg);
        if (icon != 0) sprite.SetImageResource(icon);
    }
    (int Id, string Name)[] FormChoices()
    {
        var forms = FormConverter.GetFormList(current.Species, names.Types, names.forms, current.Context);
        return forms.Select((name, id) => (id, string.IsNullOrWhiteSpace(name) ? "普通形态" : name)).ToArray();
    }
    (int Id, string Name)[] AbilityChoices()
    {
        var p = current.Clone();
        p.Form = (byte)selections["Form"];
        return Enumerable.Range(0, p.PersonalInfo.AbilityCount).Select(i => (i, names.Ability[p.PersonalInfo.GetAbilityAtIndex(i)])).ToArray();
    }
    void Choice(LinearLayout parent, string key, string label, int value, Func<(int Id, string Name)[]> options, Action? changed = null)
    {
        selections[key] = value;
        var row = Row(parent);
        row.AddView(Text(label, 12, "#91AAA1"), new LinearLayout.LayoutParams(Dp(84), -2));
        var button = Action(options().FirstOrDefault(x => x.Id == value).Name ?? "保持原值");
        button.ContentDescription = label + "选择";
        choiceButtons[key] = button;
        row.AddView(button, new LinearLayout.LayoutParams(0, Dp(46), 1));
        button.Click += (_, _) =>
        {
            var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
            panel.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
            panel.Background = Panel("#0F201C");
            panel.AddView(Text("选择" + label, 19, "#D6FF63"));
            var search = new EditText(this) { Hint = "搜索" + label, TextSize = 15, InputType = InputTypes.ClassText };
            search.SetTextColor(global::Android.Graphics.Color.ParseColor("#E9F4EF"));
            search.SetHintTextColor(global::Android.Graphics.Color.ParseColor("#91AAA1"));
            search.Background = Panel("#0B1917");
            search.SetPadding(Dp(12), 0, Dp(12), 0);
            panel.AddView(search, new LinearLayout.LayoutParams(-1, Dp(46)) { TopMargin = Dp(12), BottomMargin = Dp(8) });
            var list = new ListView(this);
            panel.AddView(list, new LinearLayout.LayoutParams(-1, Dp(320)));
            var all = options();
            var filtered = all;
            void Refresh()
            {
                filtered = all.Where(x => x.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
                list.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleListItemSingleChoice, filtered.Select(x => x.Name).ToArray());
                list.ChoiceMode = ChoiceMode.Single;
                var selected = Array.FindIndex(filtered, x => x.Id == selections[key]);
                if (selected >= 0) { list.SetItemChecked(selected, true); list.SetSelection(selected); }
            }
            Refresh();
            search.TextChanged += (_, _) => Refresh();
            var dialog = new Dialog(this);
            var cancel = Action("取消");
            cancel.Click += (_, _) => dialog.Dismiss();
            panel.AddView(cancel, new LinearLayout.LayoutParams(-1, Dp(44)) { TopMargin = Dp(8) });
            dialog.SetContentView(panel);
            list.ItemClick += (_, e) =>
            {
                var previous = selections[key];
                selections[key] = filtered[e.Position].Id;
                button.Text = filtered[e.Position].Name;
                if (previous != selections[key]) changed?.Invoke();
                dialog.Dismiss();
            };
            dialog.Show();
            dialog.Window!.SetBackgroundDrawable(new global::Android.Graphics.Drawables.ColorDrawable(global::Android.Graphics.Color.Transparent));
            dialog.Window.SetLayout((int)(Resources!.DisplayMetrics!.WidthPixels * .9f), -2);
            dialog.Window!.SetSoftInputMode(global::Android.Views.SoftInput.StateAlwaysHidden);
        };
    }
    EditText Editor(string key, string label, object value, bool numeric = true)
    {
        var input = new EditText(this) { Text = value.ToString(), TextSize = 14, ContentDescription = label, InputType = numeric ? InputTypes.ClassNumber : InputTypes.ClassText };
        input.SetSingleLine(true);
        input.SetTextColor(global::Android.Graphics.Color.ParseColor("#E9F4EF"));
        input.SetPadding(Dp(10), 0, Dp(10), 0);
        input.Background = Panel("#0B1917");
        fields[key] = input;
        return input;
    }
    void Input(LinearLayout parent, string key, string label, object value, bool numeric = true)
    {
        var row = Row(parent);
        row.AddView(Text(label, 12, "#91AAA1"), new LinearLayout.LayoutParams(Dp(84), -2));
        row.AddView(Editor(key, label, value, numeric), new LinearLayout.LayoutParams(0, Dp(46), 1));
    }
    void Toggle(LinearLayout parent, string key, string label, bool value)
    {
        var toggle = new Switch(this) { Text = label, Checked = value, TextSize = 13, ContentDescription = label };
        toggle.SetTextColor(global::Android.Graphics.Color.ParseColor("#E9F4EF"));
        flags[key] = toggle;
        toggle.CheckedChange += (_, _) => UpdateIdentity(current.Species);
        parent.AddView(toggle, new LinearLayout.LayoutParams(-1, Dp(44)));
    }
    int Number(string key) => int.TryParse(fields[key].Text?.Trim(), out var value) ? value : throw new FormatException($"请填写有效的{fields[key].ContentDescription}。");
    int? Changed(string key, int original) => selections[key] == original ? null : selections[key];
    PKM ReadEdit()
    {
        // Pass only changed identity fields so opening and saving cannot normalize original data.
        var edited = LocalRepository.ApplyEdit(current, new WorkingEdit(
            fields["Nickname"].Text, Number("Level"), Changed("Item", current.HeldItem), Changed("Status", current.Status_Condition), Number("Strain"), Number("Days"),
            Enumerable.Range(0, 4).Select(i => selections[$"Move{i}"]).ToArray(),
            Enumerable.Range(0, 6).Select(i => Number($"IV{i}")).ToArray(), Enumerable.Range(0, 6).Select(i => Number($"EV{i}")).ToArray(),
            Nature: Changed("Nature", (int)current.Nature), AbilityIndex: Changed("Ability", current.AbilityNumber >> 1),
            Gender: Changed("Gender", current.Gender), Form: Changed("Form", current.Form),
            Shiny: flags["Shiny"].Checked == current.IsShiny ? null : flags["Shiny"].Checked, Egg: flags["Egg"].Checked == current.IsEgg ? null : flags["Egg"].Checked));
        if (standardNickname && edited.Nickname == SpeciesName.GetSpeciesNameGeneration(edited.Species, edited.Language, edited.Generation))
            edited.IsNicknamed = false;
        return edited;
    }
    void SaveEdit()
    {
        try { Save(ReadEdit()); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    void Save(PKM pokemon)
    {
        if (IsFinishing || IsDestroyed) return;
        record = LocalRepository.SaveWorking(record!, pokemon);
        SetResult(Result.Ok, new global::Android.Content.Intent().PutExtra("repository_id", record.Id));
        Finish();
    }

    async Task PreviewLegalization()
    {
        save.Enabled = legalize.Enabled = false;
        try
        {
            var edited = ReadEdit();
            var outcome = await Task.Run(() => LocalRepository.RepairPreservingFields(edited));
            if (IsFinishing || IsDestroyed) return;
            if (!outcome.Valid) { ShowError(outcome.FailureReason ?? "无法生成合法结果；当前工作副本未修改。"); return; }
            var sourceFields = Describe(current);
            var targetFields = Describe(outcome.Pokemon);
            var changes = sourceFields.Zip(targetFields).Where(pair => pair.First.Value != pair.Second.Value)
                .Select(pair => $"{pair.First.Name}：{pair.First.Value} → {pair.Second.Value}").ToArray();
            var message = (changes.Length == 0 ? "列出的属性无变化。" : string.Join("\n", changes))
                + $"\n\n处理：{string.Join("、", outcome.Changes)}\n确认后更新当前工作副本，原始备份保留。";
            var dialog = new Dialog(this);
            var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
            panel.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
            panel.Background = Panel("#0F201C");
            panel.AddView(Text("确认合法化更新", 19, "#D6FF63"));
            var preview = Text(message, 14);
            preview.SetPadding(0, Dp(12), 0, Dp(12));
            var scroll = new ScrollView(this);
            scroll.AddView(preview);
            preview.Measure(global::Android.Views.View.MeasureSpec.MakeMeasureSpec((int)(Resources!.DisplayMetrics!.WidthPixels * .9f) - Dp(32), global::Android.Views.MeasureSpecMode.Exactly),
                global::Android.Views.View.MeasureSpec.MakeMeasureSpec(0, global::Android.Views.MeasureSpecMode.Unspecified));
            panel.AddView(scroll, new LinearLayout.LayoutParams(-1, Math.Min(Dp(360), preview.MeasuredHeight)));
            var actions = Row(panel);
            var cancel = Action("取消");
            cancel.Click += (_, _) => dialog.Dismiss();
            var confirm = Action("确认更新", true);
            confirm.Click += (_, _) =>
            {
                try { Save(outcome.Pokemon); dialog.Dismiss(); }
                catch (Exception ex) { ShowError(ex.Message); }
            };
            actions.AddView(cancel, new LinearLayout.LayoutParams(0, Dp(46), 1) { RightMargin = Dp(8) });
            actions.AddView(confirm, new LinearLayout.LayoutParams(0, Dp(46), 1));
            dialog.SetContentView(panel);
            dialog.Show();
            dialog.Window!.SetBackgroundDrawable(new global::Android.Graphics.Drawables.ColorDrawable(global::Android.Graphics.Color.Transparent));
            dialog.Window.SetLayout((int)(Resources!.DisplayMetrics!.WidthPixels * .9f), -2);
        }
        catch (Exception ex) { if (!IsFinishing && !IsDestroyed) ShowError(ex.Message); }
        finally { if (!IsFinishing && !IsDestroyed) save.Enabled = legalize.Enabled = true; }
    }

    static (string Name, string Value)[] Describe(PKM p) =>
    [
        ("种类 / 形态", $"{p.Species} / {p.Form}"), ("昵称", p.Nickname), ("等级", $"{p.CurrentLevel}"),
        ("性格", $"{p.Nature}"), ("特性", $"{p.Ability}"), ("性别", $"{p.Gender}"),
        ("闪光", $"{p.IsShiny}"), ("蛋", $"{p.IsEgg}"), ("PID", $"{p.PID:X8}"),
        ("个体值 HP/攻/防/速/特攻/特防", $"{p.IV_HP}/{p.IV_ATK}/{p.IV_DEF}/{p.IV_SPE}/{p.IV_SPA}/{p.IV_SPD}"),
        ("努力值 HP/攻/防/速/特攻/特防", $"{p.EV_HP}/{p.EV_ATK}/{p.EV_DEF}/{p.EV_SPE}/{p.EV_SPA}/{p.EV_SPD}"),
        ("六维能力 HP/攻/防/速/特攻/特防", string.Join("/", p.GetStats(p.PersonalInfo).ToArray())),
        ("招式", $"{p.Move1}/{p.Move2}/{p.Move3}/{p.Move4}"),
        ("招式 PP", $"{p.Move1_PP}/{p.Move2_PP}/{p.Move3_PP}/{p.Move4_PP}"),
        ("道具 / 状态", $"{p.HeldItem} / {p.Status_Condition}"), ("宝可病毒", $"{p.PokerusStrain}/{p.PokerusDays}"),
        ("来源游戏", $"{p.Version}"), ("训练家", $"{p.OriginalTrainerName} / {p.TID16} / {p.SID16} / {p.OriginalTrainerGender}"),
        ("相遇地点 / 等级", $"{p.MetLocation} / {p.MetLevel}"), ("蛋地点", $"{p.EggLocation}"),
        ("相遇日期", $"{p.MetDate}"), ("蛋日期", $"{p.EggMetDate}"),
        ("精灵球 / 语言", $"{p.Ball} / {p.Language}"), ("亲密度", $"{p.CurrentFriendship}"),
    ];

    void ShowError(string message)
    {
        if (!IsFinishing && !IsDestroyed)
            new AlertDialog.Builder(this)!.SetTitle("未保存")!.SetMessage(message)!.SetPositiveButton("知道了", (_, _) => { })!.Show();
    }
}
