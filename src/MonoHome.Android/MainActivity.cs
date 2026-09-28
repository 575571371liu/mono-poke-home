namespace MonoHome.Android;

using global::Android.App;
using global::Android.Content;
using global::Android.OS;
using global::Android.Widget;
using global::Android.Animation;
using global::Android.Graphics;
using global::Android.Graphics.Drawables;
using global::Android.Views;
using System.Security.Cryptography;
using PKHeX.Core;
using MonoHome.Core.Repository;
using MonoHome.Core.Saves;
using MonoHome.Core.Transfers;

[Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/AppTheme")]
public class MainActivity : Activity
{
    const int EmeraldRequest = 10;
    const int HeartGoldRequest = 11;
    const string EmeraldSaveKey = "emerald-save-id";
    const string HeartGoldSaveKey = "heartgold-save-id";
    TextView? status;
    ProgressBar? progress;
    Button? transferButton;
    Button? exportBackupButton;
    Button? topImportButton;
    Button? settingsButton;
    Button? navHome;
    Button? navSaves;
    Button? navEmerald;
    Button? navHeartGold;
    Button? navHistory;
    Button? uploadButton;
    Button? sourcePreviousBox;
    Button? sourceNextBox;
    TextView? sourceBoxTitle;
    GridLayout? sourceBoxGrid;
    Button? saveNicknameButton;
    Button? discardEditsButton;
    Spinner? sourcePicker;
    Spinner? warehousePicker;
    Spinner? targetPicker;
    TransferMode transferMode = TransferMode.Conversion;
    ImageView? pokemonIcon;
    TextView? selectedPokemon;
    TextView? emeraldState;
    TextView? heartGoldState;
    ImageView? emeraldSaveIcon;
    ImageView? heartGoldSaveIcon;
    View? emeraldSourceCard;
    View? heartGoldSourceCard;
    TextView? warehouseState;
    TextView? historyState;
    LinearLayout? warehouseGrid;
    LinearLayout? warehouseActionbar;
    TextView? warehouseSelectedCount;
    Button? warehouseBatchDownload;
    readonly HashSet<string> selectedWarehouseIds = [];
    TextView? warehouseCount;
    TextView? saveCount;
    LinearLayout? transferVisual;
    TextView? transferCaption;
    View? transferPacket;
    ObjectAnimator? transferAnimator;
    ScrollView? mainScroll;
    View? connectedSavesSection;
    View? warehouseSection;
    View? centralWarehouseContent;
    View? sourceArchiveContent;
    TextView? sourceArchiveTitle;
    TextView? sourceArchiveName;
    TextView? sourceArchiveSubtitle;
    ImageView? sourceArchiveIcon;
    TextView? mainPageTitle;
    View? mainDashboardHeader;
    View? mainDashboardStats;
    View? mainDashboardRoute;
    View? historySection;
    EditText? nicknameInput;
    EditText? speciesInput;
    EditText? levelInput;
    EditText? natureInput;
    EditText? abilityInput;
    EditText? genderInput;
    EditText? formInput;
    EditText? shinyInput;
    EditText? eggInput;
    EditText? itemInput;
    EditText? statusInput;
    EditText? pokerusInput;
    EditText? movesInput;
    EditText? ivsInput;
    EditText? evsInput;
    byte[]? emeraldBytes;
    byte[]? heartGoldBytes;
    List<PokemonSlot> emeraldSlots = [];
    List<StoragePage> emeraldPages = [];
    List<PokemonSlot> heartGoldSlots = [];
    List<StoragePage> heartGoldPages = [];
    int activeSourceRequest = EmeraldRequest;
    int sourcePageIndex;
    int warehousePageIndex;
    readonly HashSet<PokemonSlot> selectedSourceSlots = [];
    List<StoredPokemon> warehouse = [];
    StoredPokemon? storedPokemon;
    RegisteredSave? emeraldSave;
    RegisteredSave? heartGoldSave;
    RegisteredSave? selectedTransferTarget;
    string? pendingTransferId;
    readonly List<string> pendingTransferIds = [];
    string? lastBackupPath;
    string emeraldExternalState = "尚未导入";
    string heartGoldExternalState = "尚未导入";

    string WarehousePath => global::System.IO.Path.Combine(FilesDir!.AbsolutePath, "warehouse");
    string SavesPath => global::System.IO.Path.Combine(FilesDir!.AbsolutePath, "saves");
    string TransfersPath => global::System.IO.Path.Combine(FilesDir!.AbsolutePath, "transfers");
    int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);
        status = FindViewById<TextView>(Resource.Id.status_text);
        progress = FindViewById<ProgressBar>(Resource.Id.transfer_progress);
        transferButton = FindViewById<Button>(Resource.Id.transfer_button);
        exportBackupButton = FindViewById<Button>(Resource.Id.export_backup_button);
        topImportButton = FindViewById<Button>(Resource.Id.top_import_button);
        settingsButton = FindViewById<Button>(Resource.Id.settings_button);
        navHome = FindViewById<Button>(Resource.Id.nav_home);
        navSaves = FindViewById<Button>(Resource.Id.nav_saves);
        navEmerald = FindViewById<Button>(Resource.Id.nav_emerald);
        navHeartGold = FindViewById<Button>(Resource.Id.nav_heartgold);
        navHistory = FindViewById<Button>(Resource.Id.nav_history);
        mainScroll = FindViewById<ScrollView>(Resource.Id.main_scroll);
        connectedSavesSection = FindViewById(Resource.Id.connected_saves_section);
        warehouseSection = FindViewById(Resource.Id.warehouse_section);
        centralWarehouseContent = FindViewById(Resource.Id.central_warehouse_content);
        sourceArchiveContent = FindViewById(Resource.Id.source_archive_content);
        sourceArchiveTitle = FindViewById<TextView>(Resource.Id.source_archive_title);
        sourceArchiveName = FindViewById<TextView>(Resource.Id.source_archive_name);
        sourceArchiveSubtitle = FindViewById<TextView>(Resource.Id.source_archive_subtitle);
        sourceArchiveIcon = FindViewById<ImageView>(Resource.Id.source_archive_icon);
        mainPageTitle = FindViewById<TextView>(Resource.Id.main_page_title);
        mainDashboardHeader = FindViewById(Resource.Id.main_dashboard_header);
        mainDashboardStats = FindViewById(Resource.Id.main_dashboard_stats);
        mainDashboardRoute = FindViewById(Resource.Id.main_dashboard_route);
        historySection = FindViewById(Resource.Id.history_section);
        uploadButton = FindViewById<Button>(Resource.Id.upload_button);
        sourcePreviousBox = FindViewById<Button>(Resource.Id.source_previous_box);
        sourceNextBox = FindViewById<Button>(Resource.Id.source_next_box);
        sourceBoxTitle = FindViewById<TextView>(Resource.Id.source_box_title);
        sourceBoxGrid = FindViewById<GridLayout>(Resource.Id.source_box_grid);
        sourcePicker = FindViewById<Spinner>(Resource.Id.source_picker);
        warehousePicker = FindViewById<Spinner>(Resource.Id.warehouse_picker);
        targetPicker = FindViewById<Spinner>(Resource.Id.target_picker);
        targetPicker!.ItemSelected += (_, args) => transferMode = args.Position == 1 ? TransferMode.Fidelity : TransferMode.Conversion;
        pokemonIcon = FindViewById<ImageView>(Resource.Id.pokemon_icon);
        selectedPokemon = FindViewById<TextView>(Resource.Id.selected_pokemon);
        emeraldState = FindViewById<TextView>(Resource.Id.emerald_state);
        heartGoldState = FindViewById<TextView>(Resource.Id.heartgold_state);
        emeraldSaveIcon = FindViewById<ImageView>(Resource.Id.emerald_save_icon);
        heartGoldSaveIcon = FindViewById<ImageView>(Resource.Id.heartgold_save_icon);
        emeraldSourceCard = FindViewById(Resource.Id.emerald_source_card);
        heartGoldSourceCard = FindViewById(Resource.Id.heartgold_source_card);
        warehouseState = FindViewById<TextView>(Resource.Id.warehouse_state);
        historyState = FindViewById<TextView>(Resource.Id.history_state);
        warehouseGrid = FindViewById<LinearLayout>(Resource.Id.warehouse_grid);
        warehouseActionbar = FindViewById<LinearLayout>(Resource.Id.warehouse_actionbar);
        warehouseSelectedCount = FindViewById<TextView>(Resource.Id.warehouse_selected_count);
        warehouseBatchDownload = FindViewById<Button>(Resource.Id.warehouse_batch_download);
        warehouseCount = FindViewById<TextView>(Resource.Id.warehouse_count);
        saveCount = FindViewById<TextView>(Resource.Id.save_count);
        transferVisual = FindViewById<LinearLayout>(Resource.Id.transfer_visual);
        transferCaption = FindViewById<TextView>(Resource.Id.transfer_caption);
        transferPacket = FindViewById<View>(Resource.Id.transfer_packet);
        nicknameInput = FindViewById<EditText>(Resource.Id.nickname_input);
        speciesInput = FindViewById<EditText>(Resource.Id.species_input);
        levelInput = FindViewById<EditText>(Resource.Id.level_input);
        natureInput = FindViewById<EditText>(Resource.Id.nature_input);
        abilityInput = FindViewById<EditText>(Resource.Id.ability_input);
        genderInput = FindViewById<EditText>(Resource.Id.gender_input);
        formInput = FindViewById<EditText>(Resource.Id.form_input);
        shinyInput = FindViewById<EditText>(Resource.Id.shiny_input);
        eggInput = FindViewById<EditText>(Resource.Id.egg_input);
        itemInput = FindViewById<EditText>(Resource.Id.item_input);
        statusInput = FindViewById<EditText>(Resource.Id.status_input);
        pokerusInput = FindViewById<EditText>(Resource.Id.pokerus_input);
        movesInput = FindViewById<EditText>(Resource.Id.moves_input);
        ivsInput = FindViewById<EditText>(Resource.Id.ivs_input);
        evsInput = FindViewById<EditText>(Resource.Id.evs_input);
        saveNicknameButton = FindViewById<Button>(Resource.Id.save_nickname);
        discardEditsButton = FindViewById<Button>(Resource.Id.discard_edits);
        warehousePicker!.ItemSelected += (_, _) => SelectWarehousePokemon();
        FindViewById<Button>(Resource.Id.import_emerald)!.Click += (_, _) => PickSave(EmeraldRequest);
        FindViewById<Button>(Resource.Id.import_heartgold)!.Click += (_, _) => PickSave(HeartGoldRequest);
        emeraldSourceCard!.Click += (_, _) => SelectSourceSave(EmeraldRequest);
        heartGoldSourceCard!.Click += (_, _) => SelectSourceSave(HeartGoldRequest);
        uploadButton!.Click += async (_, _) => await UploadSelectedAsync();
        sourcePreviousBox!.Click += (_, _) => CycleSourceBox(-1);
        sourceNextBox!.Click += (_, _) => CycleSourceBox(1);
        warehouseBatchDownload!.Click += (_, _) => ShowWarehouseTargetChooser();
        saveNicknameButton!.Click += (_, _) => SaveNickname();
        discardEditsButton!.Click += (_, _) => DiscardEdits();
        transferButton!.Click += async (_, _) =>
        {
            if (selectedTransferTarget is null)
            {
                ShowTransferTargetChooser();
                return;
            }
            await GenerateTransferAsync();
        };
        exportBackupButton!.Click += (_, _) => BeginBackupExport();
        topImportButton!.Click += (_, _) => ShowImportChooser();
        settingsButton!.Click += (_, _) => status!.Text = "当前版本：本地仓库模式 · 所有数据仅在设备内处理。";
        navHome!.Click += (_, _) => SwitchPage("warehouse", navHome);
        navEmerald!.Click += (_, _) => SwitchPage("emerald", navEmerald);
        navHeartGold!.Click += (_, _) => SwitchPage("heartgold", navHeartGold);
        navSaves!.Click += (_, _) => SwitchPage("warehouse", navHome);
        navHistory!.Click += (_, _) => ScrollToSection(historySection, navHistory);
        RestoreImportedSaves();
        SwitchPage("warehouse", navHome);
    }

    void ShowImportChooser()
    {
        if (status is null)
            return;
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("导入存档");
        dialog.SetItems(["绿宝石 / SAV", "心金或魂银 / SAV"], (_, args) => PickSave(args?.Which == 0 ? EmeraldRequest : HeartGoldRequest));
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.Show();
    }

    void ScrollToSection(View? section, Button? active)
    {
        if (section is null || mainScroll is null)
            return;
        mainScroll.Post(() => mainScroll.SmoothScrollTo(0, Math.Max(0, section.Top - 12)));
        foreach (var button in new[] { navHome, navSaves, navHistory })
        {
            if (button is not null)
                button.SetTextColor(button == active ? Color.Rgb(214, 255, 99) : Color.Rgb(145, 170, 161));
        }
    }

    void SwitchPage(string page, Button? active)
    {
        var archive = page != "warehouse";
        var mainOnly = archive ? global::Android.Views.ViewStates.Gone : global::Android.Views.ViewStates.Visible;
        if (mainDashboardHeader is not null)
            mainDashboardHeader.Visibility = mainOnly;
        if (mainDashboardStats is not null)
            mainDashboardStats.Visibility = mainOnly;
        if (mainDashboardRoute is not null)
            mainDashboardRoute.Visibility = mainOnly;
        if (mainPageTitle is not null)
            mainPageTitle.Text = archive ? $"{ActiveSourceName()}，\n上传或下载。" : "主仓库";
        if (centralWarehouseContent is not null)
            centralWarehouseContent.Visibility = archive ? global::Android.Views.ViewStates.Gone : global::Android.Views.ViewStates.Visible;
        if (sourceArchiveContent is not null)
            sourceArchiveContent.Visibility = archive ? global::Android.Views.ViewStates.Visible : global::Android.Views.ViewStates.Gone;
        foreach (var button in new[] { navHome, navEmerald, navHeartGold })
        {
            if (button is not null)
                button.SetTextColor(button == active ? Color.Rgb(214, 255, 99) : Color.Rgb(145, 170, 161));
        }
        if (archive)
        {
            var requestCode = page == "emerald" ? EmeraldRequest : HeartGoldRequest;
            SelectSourceSave(requestCode, false);
            UpdateSourceArchiveHeader();
        }
        mainScroll?.Post(() => mainScroll.SmoothScrollTo(0, 0));
    }

    void UpdateSourceArchiveHeader()
    {
        if (sourceArchiveIcon is not null)
        {
            sourceArchiveIcon.SetImageResource(activeSourceRequest == EmeraldRequest ? Resource.Drawable.a_384 : Resource.Drawable.a_250);
            sourceArchiveIcon.ContentDescription = activeSourceRequest == EmeraldRequest ? "绿宝石代表宝可梦 烈空坐" : "心金代表宝可梦 凤王";
        }
        if (sourceArchiveTitle is not null)
            sourceArchiveTitle.Text = ActiveSourceName();
        if (sourceArchiveName is not null)
            sourceArchiveName.Text = ActiveSourceSave()?.DisplayName ?? "未导入存档";
        if (sourceArchiveSubtitle is not null)
            sourceArchiveSubtitle.Text = $"{ActiveSourceName()} · 仅管理此存档的队伍与盒子";
    }

    void UpdateArchiveTabs()
    {
        if (navEmerald is not null)
            navEmerald.Visibility = emeraldSave is null ? global::Android.Views.ViewStates.Gone : global::Android.Views.ViewStates.Visible;
        if (navHeartGold is not null)
            navHeartGold.Visibility = heartGoldSave is null ? global::Android.Views.ViewStates.Gone : global::Android.Views.ViewStates.Visible;
    }

    void PickSave(int requestCode)
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/octet-stream");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);
        StartActivityForResult(intent, requestCode);
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == 30)
        {
            if (resultCode == Result.Ok)
                RefreshWarehouse(data?.GetStringExtra("repository_id"));
            return;
        }
        var uri = data?.Data;
        if (resultCode != Result.Ok || uri is null || status is null)
            return;
        SetBusy(true);
        try
        {
            var resolver = ContentResolver ?? throw new InvalidOperationException("Android content resolver unavailable.");
            if (requestCode == 12)
            {
                var transferPath = global::System.IO.Path.Combine(CacheDir!.AbsolutePath, "heartgold-transfer.sav");
                var expected = File.ReadAllBytes(transferPath);
                status.Text = "正在写入导出副本…";
                using var source = File.OpenRead(transferPath);
                using var destination = resolver.OpenOutputStream(uri) ?? throw new IOException("无法写入导出文件。");
                await source.CopyToAsync(destination);
                destination.Close();
                status.Text = "正在验证导出副本…";
                using var written = resolver.OpenInputStream(uri) ?? throw new IOException("无法重新读取导出文件。");
                using var verified = new MemoryStream();
                await written.CopyToAsync(verified);
                if (!CryptographicOperations.FixedTimeEquals(expected, verified.ToArray()))
                    throw new IOException("导出校验不一致；原始存档未被修改。");
                foreach (var transferId in pendingTransferIds)
                    TransferJournal.MarkExported(TransfersPath, transferId, uri.LastPathSegment ?? "heartgold-transfer.sav");
                if (pendingTransferIds.Count == 0 && !string.IsNullOrWhiteSpace(pendingTransferId))
                    TransferJournal.MarkExported(TransfersPath, pendingTransferId, uri.LastPathSegment ?? "heartgold-transfer.sav");
                pendingTransferIds.Clear();
                pendingTransferId = null;
                status.Text = "传送副本已导出；原始两份存档未修改。";
                return;
            }
            if (requestCode == 13)
            {
                if (string.IsNullOrWhiteSpace(lastBackupPath) || !File.Exists(lastBackupPath))
                    throw new IOException("最近备份不存在。");
                var expected = File.ReadAllBytes(lastBackupPath);
                status.Text = "正在导出恢复备份…";
                using var source = File.OpenRead(lastBackupPath);
                using var destination = resolver.OpenOutputStream(uri) ?? throw new IOException("无法写入恢复备份。");
                await source.CopyToAsync(destination);
                destination.Close();
                using var written = resolver.OpenInputStream(uri) ?? throw new IOException("无法重新读取恢复备份。");
                using var verified = new MemoryStream();
                await written.CopyToAsync(verified);
                if (!CryptographicOperations.FixedTimeEquals(expected, verified.ToArray()))
                    throw new IOException("恢复备份校验不一致。");
                status.Text = "最近备份已导出，可用于恢复原存档。";
                return;
            }
            var grantedFlags = (data?.Flags ?? 0) & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            try { resolver.TakePersistableUriPermission(uri, grantedFlags); }
            catch (global::Java.Lang.SecurityException) { } // We retain our own local immutable copy below.
            status.Text = "正在读取存档…";
            using var stream = resolver.OpenInputStream(uri) ?? throw new IOException("无法读取存档。");
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bytes = memory.ToArray();
            var name = uri.LastPathSegment ?? "save";
            status.Text = "正在识别存档格式…";
            var info = SaveInspector.Inspect(bytes, name);
            if (requestCode == EmeraldRequest && info.Game != "Emerald")
                throw new InvalidDataException("请选择绿宝石存档。");
            if (requestCode == HeartGoldRequest && info.Game is not ("HeartGold" or "SoulSilver"))
                throw new InvalidDataException("请选择心金或魂银存档。");
            var slots = BoxReader.Read(bytes, name);
            var label = requestCode == EmeraldRequest ? "绿宝石" : "心金";
            status.Text = "正在登记本地快照…";
            RegisterSave(requestCode, bytes, slots, uri.ToString(), (int)grantedFlags);
            status.Text = $"{label}：{info.Game} / Gen {info.Generation}\n可读取宝可梦：{slots.Count}\n已登记，本地处理。";
        }
        catch (Exception ex)
        {
            status.Text = $"导入失败：{ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task GenerateTransferAsync()
    {
        if (heartGoldBytes is null || storedPokemon is null || status is null || selectedTransferTarget is null)
            return;
        if (heartGoldExternalState != "已同步")
        {
            status.Text = "目标存档已变更或无法验证；请重新导入后再生成副本。";
            return;
        }
        SetBusy(true);
        try
        {
            if (heartGoldSave is null || selectedTransferTarget.Id != heartGoldSave.Id)
                throw new IOException("尚未登记目标存档。");
            status.Text = "正在适配目标存档规则…";
            var cachePath = CacheDir?.AbsolutePath ?? throw new IOException("无法取得应用缓存目录。");
            var heartGoldPath = global::System.IO.Path.Combine(cachePath, "heartgold-input.sav");
            File.WriteAllBytes(heartGoldPath, heartGoldBytes);
            var current = storedPokemon;
            var preparation = await Task.Run(() => TargetPreparationService.Prepare(current, heartGoldSave, heartGoldPath, global::System.IO.Path.Combine(cachePath, "prepared")));
            if (!preparation.IsCurrentFor(current, heartGoldSave) || string.IsNullOrWhiteSpace(preparation.PreparedSavePath))
            {
                storedPokemon = LocalRepository.SetLegality(current, "invalid");
                RefreshWarehouse(current.Id);
                status.Text = $"无法传送：{preparation.Message}";
                return;
            }
            status.Text = "正在安全写入目标存档…";
            var prepared = File.ReadAllBytes(preparation.PreparedSavePath);
            var write = await new TargetSaveWriter(ContentResolver!, SavesPath).WriteAsync(heartGoldSave, prepared);
            if (!write.Succeeded)
            {
                status.Text = $"传送未完成：{write.Message}";
                return;
            }
            heartGoldSave = SaveRegistry.UpdateSnapshot(heartGoldSave, write.WrittenBytes!);
            selectedTransferTarget = heartGoldSave;
            heartGoldBytes = write.WrittenBytes;
            storedPokemon = LocalRepository.SetLegality(current, "valid");
            RefreshWarehouse(current.Id);
            TransferJournal.Append(TransfersPath, current.Id, emeraldSave?.Game ?? "Unknown", heartGoldSave.Game,
                new(true, current.Species, "prepared", true, preparation.PreparedSavePath, write.Message, preparation.Changes), write.BackupPath);
            status.Text = $"{ChineseSpeciesName(current.Species)} 已传送至 {heartGoldSave.Game} 存档。";
        }
        catch (Exception ex)
        {
            status.Text = $"传送失败：{ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task GenerateBatchTransferAsync()
    {
        var records = warehouse.Where(record => selectedWarehouseIds.Contains(record.Id)).ToArray();
        if (records.Length == 0)
            return;
        if (heartGoldBytes is null || status is null || heartGoldExternalState != "已同步")
        {
            status!.Text = "目标存档已变更或无法验证；请重新授权后再传送。";
            return;
        }
        SetBusy(true);
        try
        {
            if (heartGoldSave is null)
                throw new IOException("尚未登记目标存档。");
            status.Text = $"正在适配 {records.Length} 只宝可梦…";
            var cachePath = CacheDir?.AbsolutePath ?? throw new IOException("无法取得应用缓存目录。");
            var heartGoldPath = global::System.IO.Path.Combine(cachePath, "heartgold-input.sav");
            var outputPath = global::System.IO.Path.Combine(cachePath, "heartgold-transfer.sav");
            File.WriteAllBytes(heartGoldPath, heartGoldBytes);
            var entities = records.Select(LocalRepository.LoadWorking).ToArray();
            var batch = await Task.Run(() => EmeraldHgssTransfer.TransferStoredMany(entities, heartGoldPath, outputPath, TransferMode.Conversion));
            if (!batch.Succeeded)
            {
                foreach (var record in records)
                    LocalRepository.SetLegality(record, "invalid");
                status.Text = $"批量传送未完成：{batch.Message}";
                return;
            }
            status.Text = "正在安全写入目标存档…";
            var write = await new TargetSaveWriter(ContentResolver!, SavesPath).WriteAsync(heartGoldSave, File.ReadAllBytes(outputPath));
            if (!write.Succeeded)
            {
                status.Text = $"批量传送未完成：{write.Message}";
                return;
            }
            heartGoldSave = SaveRegistry.UpdateSnapshot(heartGoldSave, write.WrittenBytes!);
            heartGoldBytes = write.WrittenBytes;
            foreach (var (record, report) in records.Zip(batch.Reports))
            {
                LocalRepository.SetLegality(record, "valid");
                TransferJournal.Append(TransfersPath, record.Id, emeraldSave?.Game ?? "Unknown", heartGoldSave.Game, report, write.BackupPath);
            }
            status.Text = $"{records.Length} 只宝可梦已传送至 {heartGoldSave.Game} 存档。";
        }
        catch (Exception ex)
        {
            status.Text = $"批量传送失败：{ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    void ShowWarehouseTargetChooser()
    {
        if (selectedWarehouseIds.Count == 0 || status is null)
            return;

        var choices = new List<string>();
        var actions = new List<Action>();
        if (heartGoldSave is not null)
        {
            choices.Add($"{ChineseGameName(heartGoldSave.Game)} · Gen {heartGoldSave.Generation} · {heartGoldExternalState}");
            actions.Add(async () =>
            {
                if (heartGoldExternalState != "已同步")
                {
                    status.Text = "目标存档需重新授权或已被外部修改，未开始下载。";
                    return;
                }
                await GenerateBatchTransferAsync();
            });
        }
        if (emeraldSave is not null)
        {
            choices.Add($"绿宝石 · Gen {emeraldSave.Generation} · 当前发布路线不支持作为目标");
            actions.Add(() => status.Text = "绿宝石作为目标的合法转换尚未发布；仓库实体未被修改。" );
        }
        if (choices.Count == 0)
        {
            status.Text = "尚未登记目标存档。请先导入目标存档并授予读写权限。";
            return;
        }

        var dialog = new AlertDialog.Builder(this);
        if (dialog is null)
            return;
        dialog.SetTitle("选择下载目标存档");
        dialog.SetItems(choices.ToArray(), (_, args) => actions[args?.Which ?? 0]());
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.Show();
    }

    void ShowTransferTargetChooser()
    {
        if (storedPokemon is null || status is null)
            return;

        var choices = new List<string>();
        var actions = new List<Action>();
        if (heartGoldSave is not null)
        {
            if (heartGoldExternalState == "已同步")
            {
                choices.Add($"{ChineseGameName(heartGoldSave.Game)} · Gen {heartGoldSave.Generation} · 可传送");
                actions.Add(() =>
                {
                    selectedTransferTarget = heartGoldSave;
                    status.Text = $"已选择目标：{ChineseGameName(selectedTransferTarget.Game)}。再次点击传送。";
                    UpdateButtons();
                });
            }
            else
            {
                choices.Add($"{ChineseGameName(heartGoldSave.Game)} · Gen {heartGoldSave.Generation} · 需要重新同步");
                actions.Add(() => status.Text = "心金目标存档已变更或未授权，请重新导入后再传送。");
            }
        }
        if (emeraldSave is not null)
        {
            choices.Add($"绿宝石 · Gen {emeraldSave.Generation} · 当前路线不支持目标写入");
            actions.Add(() => status.Text = "绿宝石作为目标的合法转换尚未发布；仓库实体未被修改。" );
        }
        if (choices.Count == 0)
        {
            status.Text = "尚未登记可用的目标存档；请先导入并同步目标存档。";
            return;
        }

        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("选择传送目标存档");
        dialog.SetItems(choices.ToArray(), (_, args) => actions[args?.Which ?? 0]());
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.Show();
    }

    void RegisterSave(int requestCode, byte[] bytes, IReadOnlyList<PokemonSlot> slots, string? sourceUri, int sourceFlags = 0)
    {
        if (requestCode == EmeraldRequest)
        {
            emeraldSave = SaveRegistry.Register(bytes, "emerald.srm", SavesPath, sourceUri, sourceFlags);
            emeraldExternalState = "已同步";
            GetSharedPreferences("saves", FileCreationMode.Private)!.Edit()!.PutString(EmeraldSaveKey, emeraldSave.Id)!.Apply();
            emeraldBytes = File.ReadAllBytes(emeraldSave.SnapshotPath);
            emeraldSlots = BoxReader.Read(emeraldBytes, emeraldSave.DisplayName).ToList();
            emeraldPages = BoxReader.ReadPages(emeraldBytes, emeraldSave.DisplayName).ToList();
            SelectSourceSave(EmeraldRequest, false);
        }
        else
        {
            heartGoldSave = SaveRegistry.Register(bytes, "heartgold.sav", SavesPath, sourceUri, sourceFlags);
            selectedTransferTarget = null;
            heartGoldExternalState = "已同步";
            GetSharedPreferences("saves", FileCreationMode.Private)!.Edit()!.PutString(HeartGoldSaveKey, heartGoldSave.Id)!.Apply();
            heartGoldBytes = File.ReadAllBytes(heartGoldSave.SnapshotPath);
            heartGoldSlots = BoxReader.Read(heartGoldBytes, heartGoldSave.DisplayName).ToList();
            heartGoldPages = BoxReader.ReadPages(heartGoldBytes, heartGoldSave.DisplayName).ToList();
            if (emeraldSave is null)
                SelectSourceSave(HeartGoldRequest, false);
        }
        UpdateButtons();
    }

    void RestoreImportedSaves()
    {
        Restore(EmeraldRequest, EmeraldSaveKey);
        Restore(HeartGoldRequest, HeartGoldSaveKey);
        RefreshWarehouse();
        UpdateButtons();
    }

    void Restore(int requestCode, string preferenceKey)
    {
        var id = GetSharedPreferences("saves", FileCreationMode.Private)?.GetString(preferenceKey, null);
        if (string.IsNullOrWhiteSpace(id))
            return;
        try
        {
            var saved = SaveRegistry.Get(SavesPath, id) ?? throw new InvalidDataException("登记记录不存在。");
            var bytes = File.ReadAllBytes(saved.SnapshotPath);
            if (requestCode == EmeraldRequest)
            {
                emeraldSave = saved;
                emeraldBytes = bytes;
                emeraldExternalState = CheckSourceUri(saved);
                emeraldSlots = BoxReader.Read(bytes, saved.DisplayName).ToList();
                emeraldPages = BoxReader.ReadPages(bytes, saved.DisplayName).ToList();
                if (heartGoldSave is null || activeSourceRequest == EmeraldRequest)
                    SelectSourceSave(EmeraldRequest, false);
            }
            else
            {
                heartGoldSave = saved;
                selectedTransferTarget = null;
                heartGoldBytes = bytes;
                heartGoldExternalState = CheckSourceUri(saved);
                heartGoldSlots = BoxReader.Read(bytes, saved.DisplayName).ToList();
                heartGoldPages = BoxReader.ReadPages(bytes, saved.DisplayName).ToList();
                if (emeraldSave is null)
                    SelectSourceSave(HeartGoldRequest, false);
            }
        }
        catch (Exception ex)
        {
            status!.Text = $"已登记存档无法恢复：{ex.Message}";
        }
    }

    async Task UploadSelectedAsync()
    {
        if (selectedSourceSlots.Count == 0 || status is null)
            return;
        if (ActiveSourceState() != "已同步")
        {
            status.Text = "源存档已变更或无法验证；请重新导入后再上传。";
            return;
        }
        try
        {
            var sourcePath = ActiveSourceSave()?.SnapshotPath ?? throw new InvalidDataException("来源存档未登记。");
            SetBusy(true);
            status.Text = "正在复制宝可梦到本地仓库…";
            await Task.Delay(2000);
            var selected = selectedSourceSlots.ToArray();
            var stored = await Task.Run(() => selected.Select(slot => LocalRepository.Upload(BoxReader.ReadPokemon(sourcePath, slot), WarehousePath)).ToArray());
            storedPokemon = stored.LastOrDefault();
            selectedSourceSlots.Clear();
            RefreshWarehouse(storedPokemon?.Id);
            RenderSourceBoard();
            status.Text = $"已上传 {stored.Length} 只至中央仓库。";
            UpdateButtons();
        }
        catch (Exception ex)
        {
            status.Text = $"上传失败：{ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    void SelectSourceSave(int requestCode, bool announce = true)
    {
        activeSourceRequest = requestCode;
        UpdateSourceArchiveHeader();
        if (ActiveSourceSave(requestCode) is null)
        {
            if (announce && status is not null)
                status.Text = "该来源存档尚未导入。";
            RenderSourceBoard();
            return;
        }

        var pages = ActiveSourcePages();
        sourcePageIndex = Math.Max(0, pages.FindIndex(page => page.Capacity == 30));
        selectedSourceSlots.Clear();
        RenderSourceBoard();
        if (announce && status is not null)
            status.Text = $"已切换来源：{ActiveSourceName()}。下方仓库已同步。";
    }

    RegisteredSave? ActiveSourceSave(int? requestCode = null) => (requestCode ?? activeSourceRequest) == EmeraldRequest ? emeraldSave : heartGoldSave;
    List<StoragePage> ActiveSourcePages() => activeSourceRequest == EmeraldRequest ? emeraldPages : heartGoldPages;
    string ActiveSourceState() => activeSourceRequest == EmeraldRequest ? emeraldExternalState : heartGoldExternalState;
    string ActiveSourceName() => activeSourceRequest == EmeraldRequest ? "绿宝石" : "心金 / 魂银";

    void CycleSourceBox(int delta)
    {
        var pages = ActiveSourcePages();
        if (pages.Count == 0)
            return;
        sourcePageIndex = (sourcePageIndex + delta + pages.Count) % pages.Count;
        RenderSourceBoard();
    }

    void RenderSourceBoard()
    {
        if (sourceBoxGrid is null || sourceBoxTitle is null || sourcePreviousBox is null || sourceNextBox is null)
            return;

        sourceBoxGrid.RemoveAllViews();
        var pages = ActiveSourcePages();
        if (pages.Count == 0)
        {
            sourceBoxTitle.Text = "导入来源存档后显示仓库";
            sourcePreviousBox.Enabled = sourceNextBox.Enabled = false;
            return;
        }

        sourcePreviousBox.Enabled = sourceNextBox.Enabled = true;
        var page = pages[sourcePageIndex];
        var occupied = page.Slots.Count(slot => slot.Pokemon is not null);
        sourceBoxTitle.Text = $"{page.Name}\n{occupied} / {page.Capacity} 槽位";
        var columns = Resources!.DisplayMetrics!.WidthPixels / Resources.DisplayMetrics.Density >= 600 ? 6 : 5;
        sourceBoxGrid.ColumnCount = columns;
        var width = Math.Max(42, (Resources.DisplayMetrics.WidthPixels - (int)(Resources.DisplayMetrics.Density * 64)) / columns);

        foreach (var slot in page.Slots)
        {
            View tile;
            if (slot.Pokemon is null)
            {
                var empty = new TextView(this)
                {
                    Text = (slot.Index + 1).ToString("00"),
                    Gravity = GravityFlags.Center,
                    TextSize = 10,
                    ContentDescription = $"空槽位 {slot.Index + 1}",
                };
                empty.SetTextColor(Color.Rgb(68, 105, 93));
                empty.Background = CreateSlotBackground(false, false, true);
                tile = empty;
            }
            else
            {
                var pokemon = slot.Pokemon;
                var selected = selectedSourceSlots.Contains(pokemon);
                var image = new ImageButton(this)
                {
                    ContentDescription = $"查看 {ChineseSpeciesName(pokemon.Species)} 详情",
                };
                image.SetScaleType(ImageView.ScaleType.CenterInside);
                image.SetPadding(5, 5, 5, 5);
                image.Background = CreateSlotBackground(true, selected, false);
                var icon = Resources.GetIdentifier($"a_{pokemon.Species}", "drawable", PackageName);
                if (icon != 0)
                    image.SetImageResource(icon);
                image.Click += (_, _) => ShowSourceSlotDetail(pokemon);
                image.LongClick += (_, _) => ShowSourceSlotDetail(pokemon);
                tile = image;
            }

            var parameters = new GridLayout.LayoutParams { Width = width, Height = width };
            parameters.SetMargins(3, 3, 3, 3);
            sourceBoxGrid.AddView(tile, parameters);
        }

        UpdateButtons();
    }

    void ShowSourceSlotDetail(PokemonSlot slot)
    {
        var selected = selectedSourceSlots.Contains(slot);
        var source = ActiveSourceSave()?.SnapshotPath;
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            ShowSourceSlotUnavailable(slot, selected, "来源存档快照不可用，请重新导入后查看完整详情。");
            return;
        }
        try
        {
            ShowPokemonDetailDialog(
                BoxReader.ReadPokemon(source, slot),
                $"来源：{ActiveSourceName()} · {slot.Location} {slot.Box + 1}-{slot.Slot + 1}",
                selected ? "移出本次上传" : "加入本次上传",
                () => ToggleSourceSlot(slot));
        }
        catch (Exception ex)
        {
            ShowSourceSlotUnavailable(slot, selected, $"无法读取该槽位的完整数据：{ex.Message}");
        }
    }

    void ToggleSourceSlot(PokemonSlot slot)
    {
        if (!selectedSourceSlots.Add(slot))
            selectedSourceSlots.Remove(slot);
        RenderSourceBoard();
    }

    void ShowSourceSlotUnavailable(PokemonSlot slot, bool selected, string message)
    {
        var fallback = new AlertDialog.Builder(this)!;
        fallback.SetTitle($"{ChineseSpeciesName(slot.Species)} · Lv.{slot.Level}")
            .SetMessage($"{message}\n\n位置：{slot.Location} {slot.Box + 1}-{slot.Slot + 1}\n状态：{(slot.IsShiny ? "闪光" : "普通")}")
            .SetNegativeButton("关闭", (_, _) => { })
            .SetPositiveButton(selected ? "移出本次上传" : "加入本次上传", (_, _) => ToggleSourceSlot(slot))
            .Show();
    }

    static GradientDrawable CreateSlotBackground(bool occupied, bool selected, bool empty)
    {
        var background = new GradientDrawable();
        background.SetCornerRadius(8);
        background.SetColor(selected ? Color.Rgb(35, 70, 57) : Color.Rgb(13, 34, 28));
        background.SetStroke(selected ? 2 : 1, selected ? Color.Rgb(214, 255, 99) : empty ? Color.Rgb(45, 84, 72) : Color.Rgb(49, 84, 72));
        return background;
    }

    void UpdateButtons()
    {
        UpdateArchiveTabs();
        if (uploadButton is not null)
        {
            uploadButton.Visibility = selectedSourceSlots.Count == 0 ? global::Android.Views.ViewStates.Gone : global::Android.Views.ViewStates.Visible;
            uploadButton.Enabled = selectedSourceSlots.Count > 0 && ActiveSourceState() == "已同步";
            uploadButton.Text = $"上传 {selectedSourceSlots.Count} 只至中央仓库";
        }
        if (transferButton is not null)
        {
            var hasTarget = heartGoldSave is not null || emeraldSave is not null;
            transferButton.Text = selectedTransferTarget is null
                ? "选择传送存档"
                : $"传送至 {ChineseGameName(selectedTransferTarget.Game)} 存档";
            transferButton.Enabled = hasTarget && storedPokemon is not null;
        }
        if (saveNicknameButton is not null)
            saveNicknameButton.Enabled = storedPokemon is not null;
        if (discardEditsButton is not null)
            discardEditsButton.Enabled = storedPokemon is not null;
        if (emeraldState is not null)
            emeraldState.Text = emeraldSave is null ? "尚未导入" : $"已登记 · {emeraldSlots.Count} 只 · Gen {emeraldSave.Generation} · {emeraldExternalState}";
        if (heartGoldState is not null)
            heartGoldState.Text = heartGoldSave is null ? "尚未导入" : $"已登记 · {heartGoldSave.Game} · Gen {heartGoldSave.Generation} · {heartGoldExternalState}";
        emeraldSaveIcon?.SetImageResource(Resource.Drawable.a_384);
        heartGoldSaveIcon?.SetImageResource(Resource.Drawable.a_250);
        if (targetPicker is not null)
        {
            string[] targets = heartGoldSave is null
                ? ["尚未登记目标存档"]
                : [$"{heartGoldSave.Game} · 合法转换", $"{heartGoldSave.Game} · 保真传送（仅合法来源）"];
            targetPicker.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, targets);
            targetPicker.Enabled = heartGoldSave is not null;
            if (heartGoldSave is not null)
                targetPicker.SetSelection(transferMode == TransferMode.Fidelity ? 1 : 0);
        }
        if (warehouseState is not null)
            warehouseState.Text = storedPokemon is null
                ? "本地仓库为空"
                : $"仓库记录 · {ChineseSpeciesName(storedPokemon.Species)} · {LegalStatusText(storedPokemon.LegalityStatus)}";
        if (warehouseCount is not null)
            warehouseCount.Text = warehouse.Count.ToString("00");
        if (saveCount is not null)
            saveCount.Text = ((emeraldSave is null ? 0 : 1) + (heartGoldSave is null ? 0 : 1)).ToString("00");
        selectedWarehouseIds.RemoveWhere(id => warehouse.All(record => record.Id != id));
        if (warehouseSelectedCount is not null)
            warehouseSelectedCount.Text = $"已选择 {selectedWarehouseIds.Count} 只";
        if (warehouseActionbar is not null)
            warehouseActionbar.Visibility = selectedWarehouseIds.Count == 0 ? global::Android.Views.ViewStates.Gone : global::Android.Views.ViewStates.Visible;
        if (warehouseBatchDownload is not null)
            warehouseBatchDownload.Enabled = selectedWarehouseIds.Count > 0;
        RenderWarehouseGrid();
        RenderHistory();
    }

    void RefreshWarehouse(string? selectId = null)
    {
        warehouse = LocalRepository.List(WarehousePath).ToList();
        warehousePicker!.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem,
            warehouse.Select(record => $"{ChineseSpeciesName(record.Species)} · {LegalStatusText(record.LegalityStatus)} · {record.UpdatedAt.LocalDateTime:g}").ToArray());
        var index = selectId is null ? 0 : warehouse.FindIndex(record => record.Id == selectId);
        if (index >= 0)
            warehousePicker.SetSelection(index);
        storedPokemon = index >= 0 && index < warehouse.Count ? warehouse[index] : null;
        UpdateButtons();
    }

    void RenderWarehouseGrid()
    {
        if (warehouseGrid is null)
            return;
        warehouseGrid.RemoveAllViews();
        const int slotsPerBox = 30;
        var boxCount = Math.Max(1, (warehouse.Count + slotsPerBox - 1) / slotsPerBox);
        warehousePageIndex = Math.Clamp(warehousePageIndex, 0, boxCount - 1);
        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var previous = new Button(this) { Text = "‹", ContentDescription = "上一个仓库" };
        var next = new Button(this) { Text = "›", ContentDescription = "下一个仓库" };
        previous.SetAllCaps(false);
        next.SetAllCaps(false);
        previous.SetTextColor(Color.ParseColor("#8DE4D1"));
        next.SetTextColor(Color.ParseColor("#8DE4D1"));
        previous.Background = CreateSlotBackground(false, false, true);
        next.Background = CreateSlotBackground(false, false, true);
        var title = new TextView(this)
        {
            Text = $"仓库 {warehousePageIndex + 1}\n{Math.Max(0, Math.Min(warehouse.Count - warehousePageIndex * slotsPerBox, slotsPerBox))} / {slotsPerBox} 槽位",
            Gravity = GravityFlags.Center,
            TextSize = 11,
        };
        title.SetTextColor(Color.ParseColor("#E9F4EF"));
        header.AddView(previous, new LinearLayout.LayoutParams(Dp(42), Dp(48)));
        header.AddView(title, new LinearLayout.LayoutParams(0, Dp(48), 1));
        header.AddView(next, new LinearLayout.LayoutParams(Dp(42), Dp(48)));
        warehouseGrid.AddView(header);
        previous.Click += (_, _) => { warehousePageIndex = (warehousePageIndex - 1 + boxCount) % boxCount; RenderWarehouseGrid(); };
        next.Click += (_, _) => { warehousePageIndex = (warehousePageIndex + 1) % boxCount; RenderWarehouseGrid(); };

        var grid = new GridLayout(this) { ColumnCount = 5, UseDefaultMargins = true };
        var width = Math.Max(42, (Resources!.DisplayMetrics!.WidthPixels - (int)(Resources.DisplayMetrics.Density * 64)) / 5);
        for (var slotIndex = 0; slotIndex < slotsPerBox; slotIndex++)
        {
            var recordIndex = warehousePageIndex * slotsPerBox + slotIndex;
            View tile;
            if (recordIndex >= warehouse.Count)
            {
                var empty = new TextView(this)
                {
                    Text = (slotIndex + 1).ToString("00"),
                    Gravity = GravityFlags.Center,
                    TextSize = 10,
                    ContentDescription = $"空槽位 {slotIndex + 1}",
                };
                empty.SetTextColor(Color.Rgb(68, 105, 93));
                empty.Background = CreateSlotBackground(false, false, true);
                tile = empty;
            }
            else
            {
                var record = warehouse[recordIndex];
                var selected = selectedWarehouseIds.Contains(record.Id);
                var image = new ImageButton(this)
                {
                    ContentDescription = $"查看 {ChineseSpeciesName(record.Species)} 详情",
                };
                image.SetScaleType(ImageView.ScaleType.CenterInside);
                image.SetPadding(5, 5, 5, 5);
                image.Background = CreateSlotBackground(true, selected, false);
                var icon = Resources.GetIdentifier($"a_{record.Species}", "drawable", PackageName);
                if (icon != 0)
                    image.SetImageResource(icon);
                image.Click += (_, _) =>
                {
                    storedPokemon = record;
                    selectedTransferTarget = null;
                    ShowWarehouseDetail(record);
                };
                image.LongClick += (_, _) => ShowWarehouseActions(record);
                tile = image;
            }
            var parameters = new GridLayout.LayoutParams { Width = width, Height = width };
            parameters.SetMargins(3, 3, 3, 3);
            grid.AddView(tile, parameters);
        }
        warehouseGrid.AddView(grid);
    }

    void ShowWarehouseActions(StoredPokemon record)
    {
        var dialog = new AlertDialog.Builder(this)!;
        dialog.SetTitle(ChineseSpeciesName(record.Species));
        dialog.SetItems(["查看个体档案", "查看宝可梦图鉴", "编辑并另存为合法副本"], (_, args) =>
        {
            if (args?.Which == 0)
                ShowWarehouseDetail(record);
            else if (args?.Which == 1)
                StartActivity(new Intent(this, typeof(PokedexActivity)).PutExtra("species", record.Species));
            else
                StartActivityForResult(new Intent(this, typeof(EditCopyActivity)).PutExtra("repository_id", record.Id), 30);
        });
        dialog.Show();
    }

    void ShowWarehouseDetail(StoredPokemon record)
    {
        var pokemon = LocalRepository.LoadWorking(record);
        var selected = selectedWarehouseIds.Contains(record.Id);
        ShowPokemonDetailDialog(
            pokemon,
            $"来源：{pokemon.Version}\n仓库状态：{LegalStatusText(record.LegalityStatus)}",
            selected ? "移出本次下载" : "加入本次下载",
            () =>
            {
                storedPokemon = record;
                selectedTransferTarget = null;
                if (!selectedWarehouseIds.Add(record.Id))
                    selectedWarehouseIds.Remove(record.Id);
                UpdateButtons();
            },
            record);
    }

    void ShowPokemonDetailDialog(PKM pokemon, string footer, string? primaryLabel, Action? primaryAction, StoredPokemon? warehouseRecord = null)
    {
        var strings = GameInfo.GetStrings("zh-Hans");
        var moves = new[] { pokemon.Move1, pokemon.Move2, pokemon.Move3, pokemon.Move4 };
        var item = pokemon.HeldItem == 0 ? "无" : StringAt(strings.GetItemStrings(pokemon.Context, pokemon.Version), pokemon.HeldItem, $"道具 #{pokemon.HeldItem}");
        var ability = StringAt(strings.Ability, pokemon.Ability, $"特性 #{pokemon.Ability}");
        var nature = StringAt(strings.Natures, (int)pokemon.Nature, pokemon.Nature.ToString());
        int Dp(float value) => (int)(value * Resources.DisplayMetrics.Density + 0.5f);
        TextView Text(string value, float size, string color, bool bold = false)
        {
            var view = new TextView(this) { Text = value, TextSize = size };
            view.SetTextColor(Color.ParseColor(color));
            view.SetIncludeFontPadding(false);
            if (bold)
                view.SetTypeface(global::Android.Graphics.Typeface.Default, global::Android.Graphics.TypefaceStyle.Bold);
            return view;
        }
        GradientDrawable Panel(string fill = "#12201F", string stroke = "#315249", float radius = 12)
        {
            var background = new GradientDrawable();
            background.SetColor(Color.ParseColor(fill));
            background.SetCornerRadius(Dp(radius));
            background.SetStroke(Dp(1), Color.ParseColor(stroke));
            return background;
        }
        LinearLayout Card(string title, params string[] lines)
        {
            var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
            card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            card.Background = Panel();
            card.AddView(Text(title, 10, "#D6FF63", true));
            foreach (var line in lines)
            {
                var row = Text(line, 11, "#DCEBE6");
                row.SetPadding(0, Dp(3), 0, 0);
                card.AddView(row);
            }
            return card;
        }
        View TypeBadge(byte typeId, string typeName)
        {
            var iconId = Resources.GetIdentifier($"type_icon_s_{typeId:D2}", "drawable", PackageName);
            if (iconId == 0)
            {
                var fallback = Text(typeName, 8, "#DCEBE6", true);
                fallback.Gravity = GravityFlags.Center;
                return fallback;
            }
            var badge = new ImageView(this);
            badge.SetScaleType(ImageView.ScaleType.CenterInside);
            badge.SetPadding(Dp(2), Dp(2), Dp(2), Dp(2));
            badge.SetImageResource(iconId);
            badge.ContentDescription = $"属性：{typeName}";
            return badge;
        }
        var dialog = new Dialog(this);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(12), Dp(10), Dp(12), Dp(8));
        root.Background = Panel("#0F201C", "#315249", 16);
        var scroll = new ScrollView(this);
        scroll.FillViewport = true;
        scroll.VerticalScrollBarEnabled = false;
        var body = new LinearLayout(this) { Orientation = Orientation.Vertical };
        scroll.AddView(body);

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        header.SetGravity(GravityFlags.CenterVertical);
        var icon = new ImageView(this);
        var iconId = Resources.GetIdentifier($"a_{pokemon.Species}", "drawable", PackageName);
        if (iconId != 0)
            icon.SetImageResource(iconId);
        icon.Background = Panel("#17312B", "#427A68", 14);
        icon.SetPadding(Dp(8), Dp(8), Dp(8), Dp(8));
        header.AddView(icon, new LinearLayout.LayoutParams(Dp(60), Dp(60)));
        var heading = new LinearLayout(this) { Orientation = Orientation.Vertical };
        heading.SetPadding(Dp(12), 0, 0, 0);
        heading.AddView(Text($"{ChineseSpeciesName(pokemon.Species)} · Lv.{pokemon.CurrentLevel}", 17, "#E9F4EF", true));
        heading.AddView(Text($"{(pokemon.IsShiny ? "闪光" : "普通")} · {GenderText(pokemon.Gender)} · {(pokemon.IsEgg ? "蛋" : "已孵化")}", 9, "#8DE4D1"));
        heading.AddView(Text($"{(pokemon.IsEgg ? "尚未孵化" : "可正常使用")} · 形态 {pokemon.Form}", 9, "#91AAA1"));
        header.AddView(heading, new LinearLayout.LayoutParams(0, -2, 1));
        body.AddView(header);

        var training = Card("训练信息", $"性格    {nature}", $"特性    {ability}", $"效果    {AbilityEffectText(ability)}");
        var equipment = Card("装备与状态", $"道具    {item}", $"状态    {StatusText(pokemon)}");
        var cards = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        cards.SetPadding(0, Dp(8), 0, 0);
        cards.AddView(training, new LinearLayout.LayoutParams(0, -2, 1));
        cards.AddView(new Space(this), new LinearLayout.LayoutParams(Dp(8), 1));
        cards.AddView(equipment, new LinearLayout.LayoutParams(0, -2, 1));
        body.AddView(cards);

        var moveCard = new LinearLayout(this) { Orientation = Orientation.Vertical };
        moveCard.SetPadding(Dp(9), Dp(6), Dp(9), Dp(6));
        moveCard.Background = Panel();
        moveCard.AddView(Text("招式", 10, "#D6FF63", true));
        for (var index = 0; index < moves.Length; index++)
        {
            var move = moves[index];
            var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(0, Dp(3), 0, 0);
            row.AddView(Text($"{index + 1:D2}", 10, "#628078"), new LinearLayout.LayoutParams(Dp(28), -2));
            var name = move == 0 ? "—" : StringAt(strings.Move, move, $"招式 #{move}");
            var typeId = move == 0 ? (byte)0 : MoveInfo.GetType(move, pokemon.Context);
            var typeName = move == 0 ? string.Empty : StringAt(strings.Types, typeId, "未知属性");
            row.AddView(Text(name, 11, move == 0 ? "#628078" : "#E9F4EF"), new LinearLayout.LayoutParams(0, -2, 1));
            row.AddView(move == 0 ? new Space(this) : TypeBadge(typeId, typeName), new LinearLayout.LayoutParams(Dp(30), Dp(24)) { RightMargin = Dp(5) });
            if (warehouseRecord is not null)
            {
                var moveSlot = index;
                var edit = new Button(this) { Text = "编辑" };
                edit.SetAllCaps(false);
                edit.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 9);
                edit.SetTextColor(Color.ParseColor("#D6FF63"));
                edit.SetPadding(Dp(2), 0, Dp(2), 0);
                edit.Background = Panel("#132A25", "#315249", 6);
                edit.Click += (_, _) => ShowMoveEditor(warehouseRecord, moveSlot);
                row.AddView(edit, new LinearLayout.LayoutParams(Dp(46), Dp(28)));
            }
            moveCard.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        }
        body.AddView(moveCard, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });

        var ivs = new[] { pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF, pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE };
        var evs = new[] { pokemon.EV_HP, pokemon.EV_ATK, pokemon.EV_DEF, pokemon.EV_SPA, pokemon.EV_SPD, pokemon.EV_SPE };
        var calculated = pokemon.GetStats(pokemon.PersonalInfo);
        var actualStats = new[] { (int)calculated[0], (int)calculated[1], (int)calculated[2], (int)calculated[4], (int)calculated[5], (int)calculated[3] };
        var natureAmps = new sbyte[6];
        NatureAmp.GetAmps(pokemon.Nature).CopyTo(natureAmps.AsSpan(1));
        var stats = new LinearLayout(this) { Orientation = Orientation.Vertical };
        stats.SetPadding(Dp(9), Dp(6), Dp(9), Dp(6));
        stats.Background = Panel();
        stats.AddView(Text("能力数据", 10, "#D6FF63", true));
        var legend = new LinearLayout(this) { Orientation = Orientation.Vertical };
        legend.SetPadding(0, Dp(6), 0, 0);
        legend.AddView(Text("六维能力图", 10, "#91AAA1"));
        var valueLegend = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        valueLegend.AddView(Text("◆ 个体值", 10, "#8DE4D1"));
        valueLegend.AddView(Text("    ◆ 努力值", 10, "#D6FF63"));
        legend.AddView(valueLegend);
        var natureLegend = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        natureLegend.AddView(Text("性格修正：", 10, "#91AAA1"));
        natureLegend.AddView(Text("↑", 11, "#FF6B6B", true));
        natureLegend.AddView(Text(" 红色 / ", 10, "#91AAA1"));
        natureLegend.AddView(Text("↓", 11, "#5BA7FF", true));
        natureLegend.AddView(Text(" 蓝色", 10, "#91AAA1"));
        legend.AddView(natureLegend);
        stats.AddView(legend);
        var statNames = new[] { "HP", "攻击", "防御", "特攻", "特防", "速度" };
        var selectedStat = Text("点击六角图任一维度查看数值", 9, "#628078");
        selectedStat.SetPadding(0, Dp(4), 0, 0);
        var chart = new StatHexagonView(this, ivs, evs, natureAmps, actualStats);
        chart.StatSelected += index => selectedStat.Text = $"已选择：{statNames[index]}  ·  能力值 {actualStats[index]}  ·  个体值 {ivs[index]}  ·  努力值 {evs[index]}";
        stats.AddView(chart, new LinearLayout.LayoutParams(-1, Dp(170)));
        stats.AddView(selectedStat);
        body.AddView(stats, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
        var sourceText = Text(footer, 10, "#91AAA1");
        sourceText.SetPadding(0, Dp(6), 0, Dp(2));
        body.AddView(sourceText);
        root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));

        var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        actions.SetGravity(GravityFlags.CenterVertical);
        var close = new Button(this) { Text = "关闭" };
        close.SetAllCaps(false);
        close.SetTextColor(Color.ParseColor("#8DE4D1"));
        close.Background = Panel("#132A25", "#315249", 8);
        close.Click += (_, _) => dialog.Dismiss();
        actions.AddView(close, new LinearLayout.LayoutParams(0, Dp(44), primaryLabel is null ? 1 : 0.42f));
        if (primaryLabel is not null && primaryAction is not null)
        {
            var primary = new Button(this) { Text = primaryLabel };
            primary.SetAllCaps(false);
            primary.SetTextColor(Color.ParseColor("#142019"));
            primary.Background = Panel("#D6FF63", "#D6FF63", 8);
            primary.Click += (_, _) => { dialog.Dismiss(); primaryAction(); };
            actions.AddView(primary, new LinearLayout.LayoutParams(0, Dp(44), 0.58f) { LeftMargin = Dp(8) });
        }
        root.AddView(actions, new LinearLayout.LayoutParams(-1, Dp(48)) { TopMargin = Dp(10) });
        dialog.SetContentView(root);
        dialog.Show();
        if (dialog.Window is { } window)
        {
            window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            window.SetDimAmount(0.72f);
            window.AddFlags(WindowManagerFlags.DimBehind);
            window.SetLayout((int)(Resources.DisplayMetrics.WidthPixels * 0.94f), (int)(Resources.DisplayMetrics.HeightPixels * 0.96f));
        }
    }

    static string StringAt(IReadOnlyList<string> values, int index, string fallback) => (uint)index < values.Count && !string.IsNullOrWhiteSpace(values[index]) ? values[index] : fallback;
    static string AbilityEffectText(string ability) => ability switch
    {
        "恶臭" => "有时会使对手畏缩。",
        "降雨" => "出场时将天气变为下雨。",
        "加速" => "每回合结束时速度会提高。",
        "结实" => "满HP时受到致命攻击会留下1HP。",
        "湿气" => "场上无法使用自爆和大爆炸。",
        "沙隐" => "沙暴天气下回避率提高。",
        "静电" => "受到接触攻击时有概率使对手麻痹。",
        "蓄电" => "受到电属性招式时不受伤并回复HP。",
        "储水" => "受到水属性招式时不受伤并回复HP。",
        "复眼" => "招式的追加效果和携带物出现率提高。",
        "不眠" => "不会陷入睡眠状态。",
        "引火" => "受到火属性招式时不受伤，火属性招式威力提高。",
        "威吓" => "出场时降低对手的攻击。",
        "粗糙皮肤" => "受到接触攻击时使对手损失HP。",
        "飘浮" => "不会受到地面属性招式影响。",
        "孢子" => "受到接触攻击时有概率使对手陷入异常状态。",
        "自然回复" => "回到队伍时治愈异常状态。",
        "避雷针" => "吸引电属性招式并提高特攻。",
        "天恩" => "招式追加效果出现率提高。",
        "悠游自如" => "下雨天气下速度加倍。",
        "叶绿素" => "晴朗天气下速度加倍。",
        "捡拾" => "战斗结束后有概率捡到道具。",
        "压迫感" => "对手使用招式时消耗更多PP。",
        "厚脂肪" => "火属性和冰属性招式伤害减半。",
        "隔音" => "不会受到声音类招式影响。",
        "早起" => "睡眠状态恢复得更快。",
        "怪力钳" => "攻击不会被对手降低。",
        "黏着" => "携带的道具不会被夺走。",
        "大力士" => "攻击能力值加倍。",
        "火焰之躯" => "受到接触攻击时有概率使对手灼伤。",
        "蜕皮" => "每回合结束时有概率治愈异常状态。",
        "毅力" => "陷入异常状态时攻击提高。",
        "神奇鳞片" => "陷入异常状态时防御提高。",
        "毒疗" => "中毒时不会损失HP，反而会回复HP。",
        "魔法防守" => "只受到会造成直接伤害的招式影响。",
        "无防守" => "自己和对手的招式都不会落空。",
        "技术高手" => "威力较低的招式威力提高。",
        "破格" => "招式可以无视对手特性的影响。",
        "超幸运" => "招式更容易击中要害。",
        "适应力" => "本属性招式的属性一致加成提高。",
        "电气引擎" => "受到电属性招式时不受伤并提高速度。",
        "干燥皮肤" => "受到水属性招式回复HP，火属性招式伤害增加。",
        "活力" => "攻击提高，但物理招式命中率降低。",
        "斗争心" => "面对相同性别对手时攻击提高，异性时降低。",
        "不服输" => "能力被降低时攻击提高。",
        "紧张感" => "对手无法食用树果。",
        "强行" => "招式追加效果消失，但威力提高。",
        "顺手牵羊" => "夺取对手的携带道具。",
        "不屈之心" => "畏缩时速度提高。",
        _ => "暂无本地资料。",
    };

    static ushort[] LearnableMoves(PKM pokemon, int slot)
    {
        try
        {
            var source = GameData.GetLearnSource(pokemon.Version);
            var possible = new bool[pokemon.MaxMoveID + 1];
            var criteria = new EvoCriteria { Species = pokemon.Species, Form = pokemon.Form, LevelMax = pokemon.CurrentLevel };
            source.GetAllMoves(possible, pokemon, criteria, MoveSourceType.All);
            var current = new[] { pokemon.Move1, pokemon.Move2, pokemon.Move3, pokemon.Move4 };
            return possible
                .Select((canLearn, move) => (canLearn, move))
                .Where(entry => entry.canLearn && entry.move != 0)
                .Select(entry => (ushort)entry.move)
                .Where(move =>
                {
                    if (current.Where((_, index) => index != slot).Contains(move))
                        return false;
                    var probe = pokemon.Clone();
                    var candidateMoves = current.ToArray();
                    candidateMoves[slot] = move;
                    probe.SetMoves(candidateMoves);
                    return new LegalityAnalysis(probe).Info.Moves[slot].Valid;
                })
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    void ShowMoveEditor(StoredPokemon record, int slot)
    {
        try
        {
            var current = LocalRepository.LoadWorking(record);
            var moves = LearnableMoves(current, slot);
            if (moves.Length == 0)
            {
                Toast.MakeText(this, "当前来源版本没有可用学习表。", ToastLength.Short)!.Show();
                return;
            }
            var strings = GameInfo.GetStrings("zh-Hans");
            int Dp(float value) => (int)(value * Resources.DisplayMetrics.Density + 0.5f);
            TextView Text(string value, float size, string color, bool bold = false)
            {
                var view = new TextView(this) { Text = value, TextSize = size };
                view.SetTextColor(Color.ParseColor(color));
                view.SetIncludeFontPadding(false);
                if (bold)
                    view.SetTypeface(global::Android.Graphics.Typeface.Default, global::Android.Graphics.TypefaceStyle.Bold);
                return view;
            }
            GradientDrawable Panel(string fill = "#12201F", string stroke = "#315249", float radius = 10)
            {
                var background = new GradientDrawable();
                background.SetColor(Color.ParseColor(fill));
                background.SetCornerRadius(Dp(radius));
                background.SetStroke(Dp(1), Color.ParseColor(stroke));
                return background;
            }
            View TypeIcon(ushort move)
            {
                var typeId = MoveInfo.GetType(move, current.Context);
                var typeName = StringAt(strings.Types, typeId, "未知属性");
                var iconId = Resources.GetIdentifier($"type_icon_s_{typeId:D2}", "drawable", PackageName);
                if (iconId == 0)
                {
                    var fallback = Text(typeName, 8, "#DCEBE6", true);
                    fallback.Gravity = GravityFlags.Center;
                    return fallback;
                }
                var icon = new ImageView(this);
                icon.SetScaleType(ImageView.ScaleType.CenterInside);
                icon.SetPadding(Dp(2), Dp(2), Dp(2), Dp(2));
                icon.SetImageResource(iconId);
                icon.ContentDescription = $"属性：{typeName}";
                return icon;
            }
            var dialog = new Dialog(this);
            var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
            root.SetPadding(Dp(14), Dp(12), Dp(14), Dp(10));
            root.Background = Panel("#0F201C", "#315249", 16);
            root.AddView(Text($"替换第 {slot + 1} 招式", 18, "#E9F4EF", true));
            root.AddView(Text($"{ChineseSpeciesName(current.Species)} · {current.Version} · 仅显示通过 PKHeX 招式位检查的招式", 10, "#91AAA1"));
            var currentMove = current.GetMove(slot);
            var currentName = currentMove == 0 ? "—" : StringAt(strings.Move, currentMove, $"招式 #{currentMove}");
            var currentType = currentMove == 0 ? 0 : MoveInfo.GetType(currentMove, current.Context);
            var currentTypeName = currentMove == 0 ? "—" : StringAt(strings.Types, currentType, "未知属性");
            var currentInfo = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            currentInfo.SetGravity(GravityFlags.CenterVertical);
            currentInfo.SetPadding(Dp(10), Dp(7), Dp(10), Dp(7));
            currentInfo.Background = Panel("#132A25", "#315249", 10);
            var currentLabel = new LinearLayout(this) { Orientation = Orientation.Vertical };
            currentLabel.AddView(Text("当前招式", 9, "#91AAA1", true));
            currentLabel.AddView(Text(currentName, 13, "#E9F4EF", true));
            currentLabel.AddView(Text($"属性 · {currentTypeName}  ·  PP {MoveInfo.GetPP(current.Context, currentMove)}", 9, "#8DE4D1"));
            currentInfo.AddView(currentLabel, new LinearLayout.LayoutParams(0, -2, 1));
            if (currentMove != 0)
                currentInfo.AddView(TypeIcon(currentMove), new LinearLayout.LayoutParams(Dp(30), Dp(30)) { RightMargin = Dp(4) });
            root.AddView(currentInfo, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(10) });
            var scroll = new ScrollView(this) { FillViewport = true };
            scroll.VerticalScrollBarEnabled = false;
            var list = new LinearLayout(this) { Orientation = Orientation.Vertical };
            list.SetPadding(0, Dp(10), 0, 0);
            list.AddView(Text($"可选招式  ·  {moves.Length} 项", 10, "#D6FF63", true), new LinearLayout.LayoutParams(-1, Dp(24)));
            void CommitMove(ushort selectedMove)
            {
                try
                {
                    var nextMoves = new[] { current.Move1, current.Move2, current.Move3, current.Move4 };
                    nextMoves[slot] = selectedMove;
                    var edited = LocalRepository.ApplyEdit(current, new WorkingEdit(null, null, null, null, null, null, Moves: nextMoves.Select(move => (int)move).ToArray()));
                    var updated = LocalRepository.SaveWorking(record, edited);
                    dialog.Dismiss();
                    storedPokemon = updated;
                    RefreshWarehouse(updated.Id);
                    var legalityMessage = updated.LegalityStatus switch
                    {
                        "valid" => "招式已更换，当前副本合法。",
                        "invalid" => "招式已更换；该宝可梦还有其他合法性问题。",
                        _ => "招式已更换，等待合法性检查。",
                    };
                    Toast.MakeText(this, legalityMessage, ToastLength.Short)!.Show();
                    ShowWarehouseDetail(updated);
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)!.Show();
                }
            }
            foreach (var move in moves)
            {
                var name = StringAt(strings.Move, move, $"招式 #{move}");
                var typeId = MoveInfo.GetType(move, current.Context);
                var typeName = StringAt(strings.Types, typeId, "未知属性");
                var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                row.SetGravity(GravityFlags.CenterVertical);
                row.SetPadding(Dp(10), Dp(5), Dp(10), Dp(5));
                row.Background = Panel(move == currentMove ? "#244A3C" : "#12201F", move == currentMove ? "#D6FF63" : "#315249", 9);
                row.Clickable = true;
                var moveLabel = new LinearLayout(this) { Orientation = Orientation.Vertical };
                moveLabel.AddView(Text(name, 12, "#E9F4EF", move == currentMove));
                moveLabel.AddView(Text($"属性 · {typeName}  ·  PP {MoveInfo.GetPP(current.Context, move)}", 9, move == currentMove ? "#B6E8A0" : "#91AAA1"));
                row.AddView(moveLabel, new LinearLayout.LayoutParams(0, Dp(42), 1));
                row.AddView(TypeIcon(move), new LinearLayout.LayoutParams(Dp(30), Dp(30)) { RightMargin = Dp(8) });
                row.AddView(Text(move == currentMove ? "当前" : "可学习", 9, move == currentMove ? "#D6FF63" : "#91AAA1"), new LinearLayout.LayoutParams(Dp(42), -2));
                row.Click += (_, _) => CommitMove(move);
                list.AddView(row, new LinearLayout.LayoutParams(-1, Dp(54)) { BottomMargin = Dp(6) });
            }
            scroll.AddView(list);
            root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
            var cancel = new Button(this) { Text = "取消" };
            cancel.SetAllCaps(false);
            cancel.SetTextColor(Color.ParseColor("#8DE4D1"));
            cancel.Background = Panel("#132A25", "#315249", 8);
            cancel.Click += (_, _) => dialog.Dismiss();
            root.AddView(cancel, new LinearLayout.LayoutParams(-1, Dp(44)) { TopMargin = Dp(8) });
            dialog.SetContentView(root);
            dialog.Show();
            if (dialog.Window is { } window)
            {
                window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
                window.SetDimAmount(0.72f);
                window.AddFlags(WindowManagerFlags.DimBehind);
                window.SetLayout((int)(Resources.DisplayMetrics.WidthPixels * 0.92f), (int)(Resources.DisplayMetrics.HeightPixels * 0.88f));
            }
        }
        catch (Exception ex)
        {
            Toast.MakeText(this, $"招式列表读取失败：{ex.Message}", ToastLength.Long)!.Show();
        }
    }

    static string GenderText(byte gender) => gender switch { 0 => "雄", 1 => "雌", _ => "无性别" };
    static string StatusText(PKM pokemon) => pokemon.Status_Condition == 0 ? "无异常状态" : $"异常状态 #{pokemon.Status_Condition}";

    void SelectWarehousePokemon()
    {
        var index = warehousePicker?.SelectedItemPosition ?? -1;
        if (index < 0 || index >= warehouse.Count)
            return;
        storedPokemon = warehouse[index];
        UpdateButtons();
    }

    static string LegalStatusText(string status) => status switch
    {
        "valid" => "合法",
        "invalid" => "非法",
        "stale" => "需要重新检查",
        "unsupported" => "当前规则不支持",
        _ => "待检查",
    };

    static string RepositoryMeta(StoredPokemon record)
    {
        try
        {
            var pokemon = LocalRepository.LoadWorking(record);
            var tags = new List<string> { LegalStatusText(record.LegalityStatus), "工作副本" };
            if (pokemon.IsShiny) tags.Add("闪光");
            if (pokemon.HeldItem != 0) tags.Add($"道具#{pokemon.HeldItem}");
            if (pokemon.Status_Condition != 0) tags.Add($"状态#{pokemon.Status_Condition}");
            if (pokemon.PokerusStrain != 0) tags.Add($"病毒 {pokemon.PokerusStrain}/{pokemon.PokerusDays}");
            if (pokemon.IsEgg) tags.Add("蛋");
            if (pokemon is IRibbonSetRibbons ribbons && ribbons.RibbonCount != 0) tags.Add($"丝带{ribbons.RibbonCount}");
            return string.Join(" · ", tags);
        }
        catch
        {
            return $"{LegalStatusText(record.LegalityStatus)} · 工作副本";
        }
    }

    void RenderHistory()
    {
        if (historyState is null)
            return;
        try
        {
            var records = TransferJournal.List(TransfersPath).Take(3).ToArray();
            historyState.Text = records.Length == 0
                ? "暂无传送记录"
                : string.Join("\n\n", records.Select(record =>
                    $"{record.SourceGame} → {record.TargetGame} · {record.Status}\n" +
                    $"{record.CreatedAt.LocalDateTime:g} · {record.Changes.Count} 项变化" +
                    (string.IsNullOrWhiteSpace(record.OutputName) ? string.Empty : $" · {record.OutputName}")));
            historyState.SetTextColor(Color.Rgb(145, 170, 161));
        }
        catch (Exception ex)
        {
            historyState.Text = $"记录读取失败：{ex.Message}";
            historyState.SetTextColor(Color.Rgb(255, 158, 100));
        }
    }

    string CheckSourceUri(RegisteredSave saved)
    {
        if (string.IsNullOrWhiteSpace(saved.SourceUri))
            return "仅本地快照";
        try
        {
            var uri = global::Android.Net.Uri.Parse(saved.SourceUri) ?? throw new IOException("URI 无效。");
            using var stream = ContentResolver?.OpenInputStream(uri) ?? throw new IOException("无法读取已登记文件。");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return Convert.ToHexString(SHA256.HashData(memory.ToArray())) == saved.Hash ? "已同步" : "外部已修改，需重新导入";
        }
        catch (Exception)
        {
            return "需要重新授权";
        }
    }

    void RenderSelectedPokemon()
    {
        var index = sourcePicker?.SelectedItemPosition ?? -1;
        if (index < 0 || index >= emeraldSlots.Count)
            return;
        var slot = emeraldSlots[index];
        if (selectedPokemon is not null)
            selectedPokemon.Text = $"{ChineseSpeciesName(slot.Species, 3)} · 等级 {slot.Level}";
        if (pokemonIcon is not null)
        {
            var icon = Resources?.GetIdentifier($"a_{slot.Species}", "drawable", PackageName) ?? 0;
            pokemonIcon.Visibility = icon == 0 ? global::Android.Views.ViewStates.Invisible : global::Android.Views.ViewStates.Visible;
            if (icon != 0)
                pokemonIcon.SetImageResource(icon);
        }
    }

    void SetBusy(bool value)
    {
        if (progress is not null)
            progress.Visibility = value ? global::Android.Views.ViewStates.Visible : global::Android.Views.ViewStates.Gone;
        if (transferVisual is null || transferPacket is null || transferCaption is null)
            return;
        if (!value)
        {
            transferAnimator?.Cancel();
            transferAnimator = null;
            transferVisual.Visibility = global::Android.Views.ViewStates.Gone;
            transferCaption.Visibility = global::Android.Views.ViewStates.Gone;
            return;
        }
        transferVisual.Visibility = global::Android.Views.ViewStates.Visible;
        transferCaption.Visibility = global::Android.Views.ViewStates.Visible;
        transferCaption.Text = "正在建立安全传输通道…";
        transferVisual.Post(() =>
        {
            transferAnimator?.Cancel();
            var distance = Math.Max(40, transferVisual.Width - 120);
            transferAnimator = ObjectAnimator.OfFloat(transferPacket, "translationX", 0f, distance);
            transferAnimator!.SetDuration(900);
            transferAnimator.RepeatCount = ValueAnimator.Infinite;
            transferAnimator.RepeatMode = ValueAnimatorRepeatMode.Reverse;
            transferAnimator.Start();
        });
    }

    static string FormatSlot(PokemonSlot slot)
    {
        var special = string.Concat(
            slot.IsShiny ? "★ " : "",
            slot.HeldItem != 0 ? $"道具#{slot.HeldItem} " : "",
            slot.StatusCondition != 0 ? $"状态#{slot.StatusCondition} " : "",
            slot.PokerusStrain != 0 ? "病毒 " : "",
            slot.IsEgg ? "蛋 " : "",
            slot.RibbonCount != 0 ? $"丝带{slot.RibbonCount} " : "");
        return $"{ChineseSpeciesName(slot.Species, 3)} Lv.{slot.Level} {special}{slot.Location} {slot.Box + 1}-{slot.Slot + 1}";
    }

    static string ChineseSpeciesName(int species, byte generation = 4)
    {
        var name = SpeciesName.GetSpeciesNameGeneration((ushort)species, (int)LanguageID.ChineseS, generation);
        return string.IsNullOrWhiteSpace(name) ? $"宝可梦 #{species}" : name;
    }

    static string ChineseGameName(string game) => game switch
    {
        "HeartGold" => "心灵之金",
        "SoulSilver" => "魂银",
        "Emerald" => "绿宝石",
        _ => game,
    };

    void BeginExport()
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/octet-stream");
        intent.PutExtra(Intent.ExtraTitle, "heartgold-transfer.sav");
        StartActivityForResult(intent, 12);
    }

    string CreateTargetBackup(byte[] bytes)
    {
        if (heartGoldSave is null)
            throw new InvalidDataException("目标存档未登记，无法创建备份。");
        var backupPath = global::System.IO.Path.Combine(SavesPath, heartGoldSave.Id, "backups", $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.sav");
        Directory.CreateDirectory(global::System.IO.Path.GetDirectoryName(backupPath)!);
        File.WriteAllBytes(backupPath, bytes);
        lastBackupPath = backupPath;
        if (exportBackupButton is not null)
        {
            exportBackupButton.Visibility = global::Android.Views.ViewStates.Visible;
            exportBackupButton.Enabled = true;
        }
        return backupPath;
    }

    void BeginBackupExport()
    {
        if (string.IsNullOrWhiteSpace(lastBackupPath) || !File.Exists(lastBackupPath) || status is null)
            return;
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/octet-stream");
        intent.PutExtra(Intent.ExtraTitle, "heartgold-backup.sav");
        StartActivityForResult(intent, 13);
    }

    async Task OverwriteRegisteredHeartGoldAsync()
    {
        if (heartGoldSave is null || string.IsNullOrWhiteSpace(heartGoldSave.SourceUri) || status is null)
            return;
        SetBusy(true);
        string? backupPath = null;
        try
        {
            var transferPath = global::System.IO.Path.Combine(CacheDir!.AbsolutePath, "heartgold-transfer.sav");
            var expected = File.ReadAllBytes(transferPath);
            var uri = global::Android.Net.Uri.Parse(heartGoldSave.SourceUri) ?? throw new IOException("目标存档 URI 无效。");
            var resolver = ContentResolver ?? throw new IOException("Android content resolver unavailable.");
            using (var source = resolver.OpenInputStream(uri) ?? throw new IOException("无法读取目标存档，未执行覆盖。"))
            using (var original = new MemoryStream())
            {
                await source.CopyToAsync(original);
                var originalBytes = original.ToArray();
                backupPath = global::System.IO.Path.Combine(SavesPath, heartGoldSave.Id, "backups", $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.sav");
                Directory.CreateDirectory(global::System.IO.Path.GetDirectoryName(backupPath)!);
                File.WriteAllBytes(backupPath, originalBytes);
                lastBackupPath = backupPath;
                if (exportBackupButton is not null)
                {
                    exportBackupButton.Visibility = global::Android.Views.ViewStates.Visible;
                    exportBackupButton.Enabled = true;
                }
            }
            status.Text = "已创建目标存档备份，正在安全覆盖…";
            using (var destination = resolver.OpenOutputStream(uri, "wt") ?? throw new IOException("目标存档提供者不支持写入；原档未修改。"))
            using (var source = File.OpenRead(transferPath))
                await source.CopyToAsync(destination);
            using var written = resolver.OpenInputStream(uri) ?? throw new IOException("覆盖后无法重新读取目标存档；请使用备份恢复。");
            using var verified = new MemoryStream();
            await written.CopyToAsync(verified);
            var writtenBytes = verified.ToArray();
            if (!CryptographicOperations.FixedTimeEquals(expected, writtenBytes))
                throw new IOException("覆盖后校验不一致；请使用备份恢复。");
            var info = SaveInspector.Inspect(writtenBytes, "heartgold.sav");
            if (info.Game is not ("HeartGold" or "SoulSilver"))
                throw new IOException("覆盖后目标存档格式校验失败；请使用备份恢复。");
            heartGoldSave = SaveRegistry.UpdateSnapshot(heartGoldSave, writtenBytes);
            heartGoldBytes = writtenBytes;
            heartGoldExternalState = "已同步";
            if (!string.IsNullOrWhiteSpace(pendingTransferId))
            {
                TransferJournal.MarkExported(TransfersPath, pendingTransferId, $"overwritten:{uri.LastPathSegment}");
                pendingTransferId = null;
            }
            status.Text = "已备份并覆盖目标存档；覆盖结果已重新读取校验。";
        }
        catch (Exception ex)
        {
            status.Text = backupPath is null
                ? $"覆盖未执行：{ex.Message}"
                : $"覆盖失败；备份仍保留：{backupPath}\n{ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    void SaveNickname()
    {
        if (storedPokemon is null || status is null)
            return;
        try
        {
            var pokerus = pokerusInput?.Text?.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            if (pokerus.Length is not 0 and not 2)
                throw new FormatException("宝可病毒请按“株/天”填写。");
            var pokemon = LocalRepository.LoadWorking(storedPokemon);
            pokemon = LocalRepository.ApplyEdit(pokemon, new WorkingEdit(
                nicknameInput?.Text?.Trim(),
                ParseOptional(levelInput),
                ParseOptional(itemInput),
                ParseOptional(statusInput),
                pokerus.Length == 2 ? int.Parse(pokerus[0]) : null,
                pokerus.Length == 2 ? int.Parse(pokerus[1]) : null,
                ParseOptionalCsv(movesInput),
                ParseOptionalCsv(ivsInput),
                ParseOptionalCsv(evsInput),
                Species: ParseOptional(speciesInput),
                Nature: ParseOptional(natureInput),
                AbilityIndex: ParseOptional(abilityInput),
                Gender: ParseOptional(genderInput),
                Form: ParseOptional(formInput),
                Shiny: ParseOptionalBool(shinyInput),
                Egg: ParseOptionalBool(eggInput)));
            LocalRepository.SaveWorking(storedPokemon, pokemon);
            RefreshWarehouse(storedPokemon.Id);
            status.Text = "已保存到工作副本；导出前仍会进行目标存档合法性检查。";
        }
        catch (Exception ex)
        {
            status.Text = $"保存失败：{ex.Message}";
        }
    }

    void DiscardEdits()
    {
        if (storedPokemon is null || status is null)
            return;
        try
        {
            storedPokemon = LocalRepository.DiscardEdits(storedPokemon);
            RefreshWarehouse(storedPokemon.Id);
            speciesInput!.Text = string.Empty;
            nicknameInput!.Text = string.Empty;
            levelInput!.Text = string.Empty;
            natureInput!.Text = string.Empty;
            abilityInput!.Text = string.Empty;
            genderInput!.Text = string.Empty;
            formInput!.Text = string.Empty;
            shinyInput!.Text = string.Empty;
            eggInput!.Text = string.Empty;
            itemInput!.Text = string.Empty;
            statusInput!.Text = string.Empty;
            pokerusInput!.Text = string.Empty;
            movesInput!.Text = string.Empty;
            ivsInput!.Text = string.Empty;
            evsInput!.Text = string.Empty;
            status.Text = "已恢复仓库原始副本；原始存档从未被修改。";
        }
        catch (Exception ex)
        {
            status.Text = $"恢复失败：{ex.Message}";
        }
    }

    static int? ParseOptional(EditText? input)
    {
        var text = input?.Text?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : int.Parse(text);
    }

    static int[]? ParseOptionalCsv(EditText? input)
    {
        var text = input?.Text?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text.Split(',', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
    }

    static bool? ParseOptionalBool(EditText? input)
    {
        var value = ParseOptional(input);
        return value is null ? null : value switch
        {
            0 => false,
            1 => true,
            _ => throw new FormatException("布尔字段只能填写 0 或 1。"),
        };
    }
}

sealed class StatHexagonView : View
{
    readonly int[] ivs;
    readonly int[] evs;
    readonly int[] actualStats;
    readonly sbyte[] natureAmps;
    static readonly string[] Names = ["HP", "攻击", "防御", "特攻", "特防", "速度"];
    public event Action<int>? StatSelected;

    public StatHexagonView(Context context, int[] ivs, int[] evs, sbyte[] natureAmps, int[] actualStats) : base(context)
    {
        this.ivs = ivs;
        this.evs = evs;
        this.natureAmps = natureAmps;
        this.actualStats = actualStats;
        SetWillNotDraw(false);
        Clickable = true;
    }

    int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        var centerX = Width * 0.5f;
        var centerY = Height * 0.48f;
        var radius = Math.Min(Width * 0.36f, Height * 0.34f);
        var grid = new Paint { AntiAlias = true, Color = Color.ParseColor("#315249") };
        grid.SetStyle(Paint.Style.Stroke);
        grid.StrokeWidth = Dp(1);
        for (var level = 1; level <= 3; level++)
            canvas.DrawPath(Polygon(centerX, centerY, radius * level / 3f, _ => 1f), grid);
        for (var i = 0; i < 6; i++)
        {
            var point = Point(centerX, centerY, radius, i);
            canvas.DrawLine(centerX, centerY, point.x, point.y, grid);
        }
        DrawData(canvas, centerX, centerY, radius, ivs, 31f, Color.ParseColor("#8DE4D1"), Color.ParseColor("#8DE4D1"));
        DrawData(canvas, centerX, centerY, radius, evs, 252f, Color.ParseColor("#D6FF63"), Color.ParseColor("#D6FF63"));

        var label = new Paint { AntiAlias = true, Color = Color.ParseColor("#91AAA1"), TextSize = Dp(10) };
        label.TextAlign = Paint.Align.Center;
        for (var i = 0; i < 6; i++)
        {
            var point = Point(centerX, centerY, radius + Dp(18), i);
            canvas.DrawText(Names[i], point.x, point.y + Dp(4), label);
            if (natureAmps[i] != 0)
            {
                var arrow = new Paint { AntiAlias = true, Color = natureAmps[i] > 0 ? Color.ParseColor("#FF6B6B") : Color.ParseColor("#5BA7FF"), TextSize = Dp(12) };
                arrow.TextAlign = Paint.Align.Center;
                canvas.DrawText(natureAmps[i] > 0 ? "↑" : "↓", point.x, point.y + Dp(18), arrow);
            }
        }
        if (selectedIndex >= 0)
        {
            var point = Point(centerX, centerY, radius + Dp(34), selectedIndex);
            var value = new Paint { AntiAlias = true, Color = Color.ParseColor("#D6FF63"), TextSize = Dp(9) };
            value.TextAlign = Paint.Align.Center;
            canvas.DrawText($"能力 {actualStats[selectedIndex]} · IV {ivs[selectedIndex]} / EV {evs[selectedIndex]}", point.x, point.y + Dp(4), value);
        }
    }

    int selectedIndex = -1;

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e?.Action == MotionEventActions.Up)
        {
            var centerX = Width * 0.5f;
            var centerY = Height * 0.48f;
            var angle = Math.Atan2(e.GetY() - centerY, e.GetX() - centerX) + Math.PI / 2;
            if (angle < 0)
                angle += Math.PI * 2;
            selectedIndex = ((int)Math.Round(angle / (Math.PI / 3))) % 6;
            Invalidate();
            StatSelected?.Invoke(selectedIndex);
        }
        return true;
    }

    void DrawData(Canvas canvas, float centerX, float centerY, float radius, int[] values, float max, int color, int fill)
    {
        var path = Polygon(centerX, centerY, radius, i => Math.Clamp(values[i] / max, 0.08f, 1f));
        var fillPaint = new Paint { AntiAlias = true, Alpha = 45 };
        fillPaint.Color = new Color(fill);
        fillPaint.SetStyle(Paint.Style.Fill);
        canvas.DrawPath(path, fillPaint);
        var line = new Paint { AntiAlias = true, StrokeWidth = Dp(2) };
        line.Color = new Color(color);
        line.SetStyle(Paint.Style.Stroke);
        canvas.DrawPath(path, line);
    }

    Path Polygon(float centerX, float centerY, float radius, Func<int, float> scale)
    {
        var path = new Path();
        for (var i = 0; i < 6; i++)
        {
            var point = Point(centerX, centerY, radius * scale(i), i);
            if (i == 0)
                path.MoveTo(point.x, point.y);
            else
                path.LineTo(point.x, point.y);
        }
        path.Close();
        return path;
    }

    static (float x, float y) Point(float centerX, float centerY, float radius, int index)
    {
        var angle = -Math.PI / 2 + index * Math.PI / 3;
        return (centerX + radius * (float)Math.Cos(angle), centerY + radius * (float)Math.Sin(angle));
    }
}
