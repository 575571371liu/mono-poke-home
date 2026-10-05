namespace MonoHome.Android;

using global::Android.App;
using global::Android.Graphics;
using global::Android.Graphics.Drawables;
using global::Android.Views;
using global::Android.Widget;
using MonoHome.Core.Repository;

public partial class MainActivity
{
    void ShowWarehouseRelease(StoredPokemon[] records)
    {
        if (records.Length == 0 || operationBusy || warehouseReleaseRunning || warehouseError is not null) return;
        var dialog = new Dialog(this);
        GradientDrawable Panel(string fill, string stroke = "#315249", int radius = 12)
        {
            var drawable = new GradientDrawable();
            drawable.SetColor(Color.ParseColor(fill)); drawable.SetCornerRadius(Dp(radius));
            drawable.SetStroke(Dp(1), Color.ParseColor(stroke)); return drawable;
        }
        TextView Text(string value, int size = 12, string color = "#E9F4EF")
        {
            var view = new TextView(this) { Text = value, TextSize = size };
            view.SetTextColor(Color.ParseColor(color)); view.SetIncludeFontPadding(false); view.SetLineSpacing(Dp(3), 1);
            return view;
        }
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(14), Dp(16), Dp(14), Dp(12)); panel.Background = Panel("#0F201C", radius: 16);
        var title = Text($"放生 {records.Length} 只宝可梦？", 18, "#FF9B91"); panel.AddView(title);
        var message = Text("将永久清除主仓库记录及原始备份，无法恢复。\n游戏存档不受影响。", 12, "#B4C9C0");
        panel.AddView(message, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10) });
        var scroll = new ScrollView(this);
        var list = new LinearLayout(this) { Orientation = Orientation.Vertical };
        list.SetPadding(Dp(10), Dp(8), Dp(10), Dp(8)); list.Background = Panel("#12201F");
        foreach (var record in records)
        {
            var label = ChineseSpeciesName(record.Species);
            try
            {
                var pokemon = LocalRepository.LoadWorking(record);
                label += $" · Lv.{pokemon.CurrentLevel}" + (pokemon.IsShiny ? " · 闪光" : "") + (pokemon.IsEgg ? " · 蛋" : "");
            }
            catch { label += " · 等级无法读取"; }
            list.AddView(Text(label, 11), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(5), BottomMargin = Dp(5) });
        }
        scroll.AddView(list);
        panel.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1) { TopMargin = Dp(12) });
        var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var cancel = new Button(this) { Text = "取消" };
        cancel.SetAllCaps(false); cancel.SetTextColor(Color.ParseColor("#8DE4D1")); cancel.Background = Panel("#132A25", radius: 8);
        var confirm = new Button(this) { Text = "确认放生" };
        confirm.SetAllCaps(false); confirm.SetTextColor(Color.ParseColor("#FF9B91")); confirm.Background = Panel("#291D1D", "#80504A", 8);
        actions.AddView(cancel, new LinearLayout.LayoutParams(0, Dp(48), 1));
        actions.AddView(confirm, new LinearLayout.LayoutParams(0, Dp(48), 1) { LeftMargin = Dp(8) });
        panel.AddView(actions, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
        cancel.Click += (_, _) => dialog.Dismiss();
        confirm.Click += async (_, _) =>
        {
            if (warehouseReleaseRunning || operationBusy) return;
            warehouseReleaseRunning = true;
            confirm.Enabled = cancel.Enabled = false;
            dialog.SetCancelable(false); dialog.SetCanceledOnTouchOutside(false);
            title.Text = "正在放生…";
            try
            {
                await Task.Run(() => LocalRepository.Release(WarehousePath, records));
            }
            catch (Exception ex)
            {
                warehouseReleaseRunning = false;
                if (!CanTouchUi) return;
                title.Text = "放生未完成";
                message.Text = $"本次未删除任何宝可梦。\n{ex.Message}";
                cancel.Text = "关闭"; cancel.Enabled = true; dialog.SetCancelable(true);
                return;
            }
            warehouseReleaseRunning = false;
            if (!CanTouchUi) return;
            dialog.Dismiss();
            selectedWarehouseIds.ExceptWith(records.Select(r => r.Id));
            if (storedPokemon is not null && records.Any(r => r.Id == storedPokemon.Id))
            {
                storedPokemon = null; selectedTransferTarget = null; selectedTransferSlot = -1;
            }
            RefreshWarehouse(storedPokemon?.Id, preserveView: true);
            var result = warehouseError is null ? $"已放生 {records.Length} 只宝可梦。" : "放生已完成，但清单读取失败，请刷新重试。";
            if (status is not null) status.Text = result;
            Toast.MakeText(this, result, ToastLength.Short)!.Show();
        };
        dialog.SetContentView(panel); ShowSafely(dialog);
        if (dialog.Window is { } window)
        {
            window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent)); window.SetDimAmount(.72f);
            window.AddFlags(WindowManagerFlags.DimBehind);
            window.SetLayout((int)(ScreenMetrics.WidthPixels * .94f), Math.Min(DetailDialogHeight(), Dp(230 + Math.Min(records.Length, 8) * 30)));
        }
    }
}
