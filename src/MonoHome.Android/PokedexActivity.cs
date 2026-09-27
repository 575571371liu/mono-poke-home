namespace MonoHome.Android;

using global::Android.App;
using global::Android.OS;
using global::Android.Widget;
using global::Android.Graphics;
using PKHeX.Core;

[Activity(Label = "宝可梦图鉴", Theme = "@style/AppTheme")]
public sealed class PokedexActivity : Activity
{
    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        var species = Intent?.GetIntExtra("species", 1) ?? 1;
        var name = SpeciesName.GetSpeciesNameGeneration((ushort)species, (int)LanguageID.ChineseS, 4);
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.SetPadding(36, 48, 36, 36);
        var title = new TextView(this) { Text = name, TextSize = 28 };
        title.SetTextColor(Color.Rgb(233, 244, 239));
        content.AddView(title);
        var info = new TextView(this) { Text = $"全国图鉴 #{species}\n\n这是本地 PKHeX 图鉴条目。\n可在仓库长按任意宝可梦查看对应资料。\n\n当前发布传送路线：绿宝石 → 心金 / 魂银。", TextSize = 16 };
        info.SetTextColor(Color.Rgb(145, 170, 161));
        content.AddView(info);
        var close = new Button(this) { Text = "返回仓库" };
        close.Click += (_, _) => Finish();
        content.AddView(close);
        SetContentView(content);
    }
}
