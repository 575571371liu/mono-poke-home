namespace MonoHome.Android;

using global::Android.App;
using global::Android.Graphics;
using global::Android.Graphics.Drawables;
using global::Android.Views;
using global::Android.Widget;
using MonoHome.Core.Pokemon;
using MonoHome.Core.Repository;
using PKHeX.Core;

public partial class MainActivity
{
    // A database commit survives Activity recreation; the next Activity waits before reloading.
    static Task<StoredPokemon>? pendingTradeEvolution;

    protected override async void OnResume()
    {
        base.OnResume();
        var pending = pendingTradeEvolution;
        if (pending is null || operationBusy) return;
        operationBusy = true;
        try
        {
            await pending;
        }
        catch (Exception ex)
        {
            if (CanTouchUi) Toast.MakeText(this, "进化未完成：" + ex.Message, ToastLength.Long)!.Show();
        }
        finally
        {
            operationBusy = false;
            if (CanTouchUi)
            {
                if (ReferenceEquals(pendingTradeEvolution, pending)) pendingTradeEvolution = null;
                selectedTransferTarget = null; selectedTransferSlot = -1;
                RefreshWarehouse(storedPokemon?.Id, preserveView: true);
                UpdateButtons();
            }
        }
    }

    async void ShowTradeEvolution(StoredPokemon record, GameVersion game, EvolutionNodeKey target, Action closeParents)
    {
        if (operationBusy || warehouseReleaseRunning || warehouseError is not null || !CanTouchUi) return;
        operationBusy = true; UpdateButtons();
        PKM source;
        PKM preview;
        try
        {
            (source, preview) = await Task.Run(() =>
            {
                var current = LocalRepository.List(WarehousePath).SingleOrDefault(r => r.Id == record.Id);
                if (current?.Revision != record.Revision) throw new InvalidOperationException("宝可梦已变化或不存在，请刷新后重试。");
                var original = LocalRepository.LoadWorking(record);
                return (original, TradeEvolution.Preview(original, game, target));
            });
        }
        catch (Exception ex)
        {
            if (CanTouchUi) Toast.MakeText(this, "进化预览未完成：" + ex.Message, ToastLength.Long)!.Show();
            return;
        }
        finally
        {
            operationBusy = false;
            if (CanTouchUi) UpdateButtons();
        }
        if (!CanTouchUi) return;

        TextView Text(string value, int size = 12, string color = "#E9F4EF")
        {
            var view = new TextView(this) { Text = value, TextSize = size };
            view.SetTextColor(Color.ParseColor(color)); view.SetIncludeFontPadding(false);
            view.SetLineSpacing(Dp(3), 1); return view;
        }
        var dialog = new Dialog(this);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(14), Dp(16), Dp(14), Dp(12));
        var shape = new GradientDrawable(); shape.SetColor(Color.ParseColor("#0F201C"));
        shape.SetCornerRadius(Dp(16)); shape.SetStroke(Dp(1), Color.ParseColor("#315249")); root.Background = shape;
        root.AddView(Text("按通讯条件进化", 19, "#D6FF63"));
        var scroll = new ScrollView(this) { VerticalScrollBarEnabled = false };
        var body = new LinearLayout(this) { Orientation = Orientation.Vertical };
        body.SetPadding(0, Dp(12), 0, Dp(12)); scroll.AddView(body);
        root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        var horizontal = ScreenMetrics.WidthPixels / ScreenMetrics.Density >= 340 && Resources!.Configuration!.FontScale <= 1.2f;
        var comparison = new LinearLayout(this) { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
        comparison.SetGravity(GravityFlags.Center);
        void Individual(PKM pokemon)
        {
            var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
            panel.SetGravity(GravityFlags.Center);
            var name = EvolutionGuide.NodeName(new(pokemon.Species, pokemon.Form), pokemon.Context);
            var icon = new ImageView(this); icon.SetScaleType(ImageView.ScaleType.FitCenter);
            var resource = PokemonSprite.ResourceId(this, pokemon.Species, pokemon.IsShiny, false);
            if (resource != 0) icon.SetImageResource(resource);
            icon.ContentDescription = name;
            panel.AddView(icon, new LinearLayout.LayoutParams(Dp(72), Dp(72)));
            var label = Text(name, 14, "#D6FF63"); label.Gravity = GravityFlags.Center; panel.AddView(label);
            comparison.AddView(panel, new LinearLayout.LayoutParams(horizontal ? 0 : -1, -2, horizontal ? 1 : 0));
        }
        Individual(source);
        comparison.AddView(Text(horizontal ? "→" : "↓", 18, "#8DE4D1"));
        Individual(preview); body.AddView(comparison);
        body.AddView(Text($"按《{EvolutionGuide.GameName(game)}》规则\n按个体条件处理，不检查游戏剧情进度。", 12, "#B4C9C0"), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(14) });
        var strings = GameInfo.GetStrings("zh-Hans");
        var changes = new List<string>();
        if (source.Ability != preview.Ability) changes.Add($"特性：{strings.Ability[source.Ability]} → {strings.Ability[preview.Ability]}");
        if (source.Nickname != preview.Nickname) changes.Add($"默认昵称：{source.Nickname} → {preview.Nickname}");
        if (source.HeldItem != preview.HeldItem) changes.Add($"消耗道具：{strings.GetItemStrings(game.Context, game)[source.HeldItem]}");
        if (!source.PartyStatsPresent) changes.Add("当前 HP 按进化后的能力重新计算。");
        if (changes.Count != 0) body.AddView(Text(string.Join("\n", changes)), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
        body.AddView(Text("等级、个体值、性格和闪光等个体信息保留。\n保留现有招式，进化后可在详情中调整。\n仅更新主仓库当前个体；不执行游戏通讯。", 12, "#91AAA1"), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(14) });
        var result = Text("进化结果已通过合法性检查。", 12, "#8DE4D1");
        body.AddView(result, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
        var buttons = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var cancel = new Button(this) { Text = "取消", TextSize = 12 };
        cancel.SetAllCaps(false); cancel.SetTextColor(Color.ParseColor("#8DE4D1")); cancel.SetBackgroundResource(Resource.Drawable.card);
        cancel.Click += (_, _) => dialog.Dismiss();
        var confirm = new Button(this) { Text = "确认进化", TextSize = 12 };
        confirm.SetAllCaps(false); confirm.SetTextColor(Color.ParseColor("#142019")); confirm.SetBackgroundResource(Resource.Drawable.primary_button);
        buttons.AddView(cancel, new LinearLayout.LayoutParams(0, Dp(48), 1) { RightMargin = Dp(8) });
        buttons.AddView(confirm, new LinearLayout.LayoutParams(0, Dp(48), 1)); root.AddView(buttons);
        confirm.Click += async (_, _) =>
        {
            if (operationBusy || warehouseReleaseRunning || warehouseError is not null || !CanTouchUi) return;
            operationBusy = true; UpdateButtons();
            confirm.Enabled = cancel.Enabled = false; confirm.Text = "正在进化…";
            dialog.SetCancelable(false); dialog.SetCanceledOnTouchOutside(false);
            StoredPokemon? updated = null;
            try
            {
                pendingTradeEvolution = Task.Run(() => LocalRepository.ApplyTradeEvolution(record, game, target));
                updated = await pendingTradeEvolution;
                if (!CanTouchUi) return;
                selectedTransferTarget = null; selectedTransferSlot = -1;
                RefreshWarehouse(updated.Id, preserveView: true);
                closeParents(); dialog.Dismiss();
                Toast.MakeText(this, warehouseError is null ? $"已进化为{ChineseSpeciesName(updated.Species)}。" : "进化已保存，但清单读取失败，请刷新重试。", ToastLength.Long)!.Show();
                if (warehouseError is null) ShowWarehouseDetail(updated);
            }
            catch (Exception ex)
            {
                if (!CanTouchUi) return;
                if (updated is not null)
                {
                    closeParents(); dialog.Dismiss();
                    Toast.MakeText(this, "进化已保存，但界面刷新失败，请刷新重试。", ToastLength.Long)!.Show();
                }
                else
                {
                    result.Text = "进化未完成：" + ex.Message;
                    confirm.Text = "请关闭后重新检查";
                    cancel.Text = "关闭"; cancel.Enabled = true;
                    dialog.SetCancelable(true);
                }
            }
            finally
            {
                operationBusy = false;
                if (CanTouchUi)
                {
                    if (pendingTradeEvolution?.IsCompleted == true) pendingTradeEvolution = null;
                    UpdateButtons();
                }
            }
        };
        dialog.SetContentView(root); ShowSafely(dialog);
        if (dialog.Window is { } window)
        {
            window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent)); window.SetDimAmount(.72f);
            window.AddFlags(WindowManagerFlags.DimBehind);
            window.SetLayout((int)(ScreenMetrics.WidthPixels * .9f), DetailDialogHeight());
        }
    }
}
