namespace MonoHome.Android;

using global::Android.App;
using global::Android.OS;
using global::Android.Widget;
using MonoHome.Core.Repository;
using PKHeX.Core;

[Activity(Label = "编辑合法副本", Theme = "@style/AppTheme")]
public sealed class EditCopyActivity : Activity
{
    EditText? nickname;
    EditText? level;
    StoredPokemon? parent;

    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        var id = Intent?.GetStringExtra("repository_id");
        parent = string.IsNullOrWhiteSpace(id) ? null : LocalRepository.List(Path.Combine(FilesDir!.AbsolutePath, "warehouse")).FirstOrDefault(x => x.Id == id);
        if (parent is null) { Finish(); return; }
        var current = LocalRepository.LoadWorking(parent);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(36, 48, 36, 36);
        root.AddView(new TextView(this) { Text = "编辑并另存为合法副本", TextSize = 24 });
        nickname = new EditText(this) { Hint = "昵称", Text = current.Nickname };
        level = new EditText(this) { Hint = "等级 1–100", Text = current.CurrentLevel.ToString(), InputType = global::Android.Text.InputTypes.ClassNumber };
        root.AddView(nickname);
        root.AddView(level);
        var save = new Button(this) { Text = "生成新的合法副本并保存至仓库" };
        save.Click += (_, _) => SaveCopy();
        root.AddView(save);
        SetContentView(root);
    }

    void SaveCopy()
    {
        try
        {
            var source = LocalRepository.LoadWorking(parent!);
            var candidate = LocalRepository.ApplyEdit(source, new WorkingEdit(nickname?.Text?.Trim(), int.Parse(level?.Text ?? "0"), null, null, null, null));
            var copy = LocalRepository.CreateLegalCopy(parent!, candidate, Path.Combine(FilesDir!.AbsolutePath, "warehouse"));
            SetResult(Result.Ok, new global::Android.Content.Intent().PutExtra("repository_id", copy.Id));
            Finish();
        }
        catch (Exception ex)
        {
            Toast.MakeText(this, ex.Message, ToastLength.Long)!.Show();
        }
    }
}
