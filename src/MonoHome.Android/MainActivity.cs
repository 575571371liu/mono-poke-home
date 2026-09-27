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
    Button? navHistory;
    Button? uploadButton;
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
    List<StoredPokemon> warehouse = [];
    StoredPokemon? storedPokemon;
    RegisteredSave? emeraldSave;
    RegisteredSave? heartGoldSave;
    string? pendingTransferId;
    readonly List<string> pendingTransferIds = [];
    string? lastBackupPath;
    string emeraldExternalState = "尚未导入";
    string heartGoldExternalState = "尚未导入";

    string WarehousePath => global::System.IO.Path.Combine(FilesDir!.AbsolutePath, "warehouse");
    string SavesPath => global::System.IO.Path.Combine(FilesDir!.AbsolutePath, "saves");
    string TransfersPath => global::System.IO.Path.Combine(FilesDir!.AbsolutePath, "transfers");

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
        navHistory = FindViewById<Button>(Resource.Id.nav_history);
        mainScroll = FindViewById<ScrollView>(Resource.Id.main_scroll);
        connectedSavesSection = FindViewById(Resource.Id.connected_saves_section);
        warehouseSection = FindViewById(Resource.Id.warehouse_section);
        historySection = FindViewById(Resource.Id.history_section);
        uploadButton = FindViewById<Button>(Resource.Id.upload_button);
        sourcePicker = FindViewById<Spinner>(Resource.Id.source_picker);
        warehousePicker = FindViewById<Spinner>(Resource.Id.warehouse_picker);
        targetPicker = FindViewById<Spinner>(Resource.Id.target_picker);
        targetPicker!.ItemSelected += (_, args) => transferMode = args.Position == 1 ? TransferMode.Fidelity : TransferMode.Conversion;
        pokemonIcon = FindViewById<ImageView>(Resource.Id.pokemon_icon);
        selectedPokemon = FindViewById<TextView>(Resource.Id.selected_pokemon);
        emeraldState = FindViewById<TextView>(Resource.Id.emerald_state);
        heartGoldState = FindViewById<TextView>(Resource.Id.heartgold_state);
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
        sourcePicker!.ItemSelected += (_, _) => RenderSelectedPokemon();
        warehousePicker!.ItemSelected += (_, _) => SelectWarehousePokemon();
        FindViewById<Button>(Resource.Id.import_emerald)!.Click += (_, _) => PickSave(EmeraldRequest);
        FindViewById<Button>(Resource.Id.import_heartgold)!.Click += (_, _) => PickSave(HeartGoldRequest);
        uploadButton!.Click += async (_, _) => await UploadSelectedAsync();
        warehouseBatchDownload!.Click += async (_, _) => await GenerateBatchTransferAsync();
        saveNicknameButton!.Click += (_, _) => SaveNickname();
        discardEditsButton!.Click += (_, _) => DiscardEdits();
        transferButton!.Click += async (_, _) => await GenerateTransferAsync();
        exportBackupButton!.Click += (_, _) => BeginBackupExport();
        topImportButton!.Click += (_, _) => ShowImportChooser();
        settingsButton!.Click += (_, _) => status!.Text = "当前版本：本地仓库模式 · 所有数据仅在设备内处理。";
        navHome!.Click += (_, _) => ScrollToSection(warehouseSection, navHome);
        navSaves!.Click += (_, _) => ScrollToSection(connectedSavesSection, navSaves);
        navHistory!.Click += (_, _) => ScrollToSection(historySection, navHistory);
        RestoreImportedSaves();
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
        if (heartGoldBytes is null || storedPokemon is null || status is null)
            return;
        if (heartGoldExternalState != "已同步")
        {
            status.Text = "目标存档已变更或无法验证；请重新导入后再生成副本。";
            return;
        }
        SetBusy(true);
        try
        {
            if (heartGoldSave is null)
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

    void RegisterSave(int requestCode, byte[] bytes, IReadOnlyList<PokemonSlot> slots, string? sourceUri, int sourceFlags = 0)
    {
        if (requestCode == EmeraldRequest)
        {
            emeraldSave = SaveRegistry.Register(bytes, "emerald.srm", SavesPath, sourceUri, sourceFlags);
            emeraldExternalState = "已同步";
            GetSharedPreferences("saves", FileCreationMode.Private)!.Edit()!.PutString(EmeraldSaveKey, emeraldSave.Id)!.Apply();
            emeraldBytes = File.ReadAllBytes(emeraldSave.SnapshotPath);
            emeraldSlots = BoxReader.Read(emeraldBytes, emeraldSave.DisplayName).ToList();
            var labels = emeraldSlots.Select(FormatSlot).ToArray();
            sourcePicker!.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, labels);
            RenderSelectedPokemon();
        }
        else
        {
            heartGoldSave = SaveRegistry.Register(bytes, "heartgold.sav", SavesPath, sourceUri, sourceFlags);
            heartGoldExternalState = "已同步";
            GetSharedPreferences("saves", FileCreationMode.Private)!.Edit()!.PutString(HeartGoldSaveKey, heartGoldSave.Id)!.Apply();
            heartGoldBytes = File.ReadAllBytes(heartGoldSave.SnapshotPath);
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
                sourcePicker!.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem,
                    emeraldSlots.Select(FormatSlot).ToArray());
                RenderSelectedPokemon();
            }
            else
            {
                heartGoldSave = saved;
                heartGoldBytes = bytes;
                heartGoldExternalState = CheckSourceUri(saved);
            }
        }
        catch (Exception ex)
        {
            status!.Text = $"已登记存档无法恢复：{ex.Message}";
        }
    }

    async Task UploadSelectedAsync()
    {
        var selectedIndex = sourcePicker?.SelectedItemPosition ?? -1;
        if (selectedIndex < 0 || selectedIndex >= emeraldSlots.Count || status is null)
            return;
        if (emeraldExternalState != "已同步")
        {
            status.Text = "源存档已变更或无法验证；请重新导入后再上传。";
            return;
        }
        try
        {
            var sourcePath = emeraldSave?.SnapshotPath ?? throw new InvalidDataException("绿宝石存档未登记。");
            SetBusy(true);
            status.Text = "正在复制宝可梦到本地仓库…";
            await Task.Yield();
            await Task.Delay(450);
            var slot = emeraldSlots[selectedIndex];
            storedPokemon = await Task.Run(() => LocalRepository.Upload(BoxReader.ReadPokemon(sourcePath, slot), WarehousePath));
            RefreshWarehouse(storedPokemon.Id);
            status.Text = $"已上传 #{storedPokemon.Species} 到本地仓库。可选择心金存档导出。";
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

    void UpdateButtons()
    {
        if (uploadButton is not null)
            uploadButton.Enabled = emeraldSlots.Count > 0 && emeraldExternalState == "已同步";
        if (transferButton is not null)
            transferButton.Enabled = heartGoldBytes is not null && storedPokemon is not null && heartGoldExternalState == "已同步";
        if (saveNicknameButton is not null)
            saveNicknameButton.Enabled = storedPokemon is not null;
        if (discardEditsButton is not null)
            discardEditsButton.Enabled = storedPokemon is not null;
        if (emeraldState is not null)
            emeraldState.Text = emeraldSave is null ? "尚未导入" : $"已登记 · {emeraldSlots.Count} 只 · Gen {emeraldSave.Generation} · {emeraldExternalState}";
        if (heartGoldState is not null)
            heartGoldState.Text = heartGoldSave is null ? "尚未导入" : $"已登记 · {heartGoldSave.Game} · Gen {heartGoldSave.Generation} · {heartGoldExternalState}";
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
            warehouseBatchDownload.Enabled = selectedWarehouseIds.Count > 0 && heartGoldBytes is not null && heartGoldExternalState == "已同步";
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
        LinearLayout? row = null;
        foreach (var record in warehouse)
        {
            if (row is null || row.ChildCount == 2)
            {
                row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                row.SetGravity(GravityFlags.Top);
                warehouseGrid.AddView(row, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            }
            var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
            card.SetPadding(12, 10, 12, 10);
            var selected = selectedWarehouseIds.Contains(record.Id);
            var background = new GradientDrawable();
            background.SetColor(selected ? Color.Rgb(27, 49, 45) : Color.Rgb(15, 29, 28));
            background.SetCornerRadius(12);
            background.SetStroke(selected ? 2 : 1, selected ? Color.Rgb(214, 255, 99) : Color.Rgb(37, 64, 58));
            card.Background = background;
            var cardLp = new LinearLayout.LayoutParams(0, 178, 1f);
            cardLp.SetMargins(row.ChildCount == 0 ? 0 : 6, 0, row.ChildCount == 0 ? 6 : 0, 8);
            card.LayoutParameters = cardLp;
            var icon = new ImageView(this) { LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 82) };
            icon.SetScaleType(ImageView.ScaleType.CenterInside);
            var iconId = Resources?.GetIdentifier($"a_{record.Species}", "drawable", PackageName) ?? 0;
            if (iconId != 0) icon.SetImageResource(iconId);
            card.AddView(icon);
            var title = new TextView(this) { Text = ChineseSpeciesName(record.Species), TextSize = 13 };
            title.SetTextColor(Color.Rgb(233, 244, 239));
            title.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
            card.AddView(title);
            var meta = new TextView(this) { Text = RepositoryMeta(record), TextSize = 10 };
            meta.SetMaxLines(3);
            meta.SetTextColor(Color.Rgb(145, 170, 161));
            card.AddView(meta);
            card.Click += (_, _) =>
            {
                storedPokemon = record;
                if (!selectedWarehouseIds.Add(record.Id))
                    selectedWarehouseIds.Remove(record.Id);
                UpdateButtons();
            };
            card.LongClick += (_, _) => ShowWarehouseActions(record);
            row.AddView(card);
        }
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
        var text = $"{ChineseSpeciesName(record.Species)}\n等级 {pokemon.CurrentLevel} · {(pokemon.IsShiny ? "闪光" : "普通")}\n" +
            $"携带道具 #{pokemon.HeldItem}\n招式：{pokemon.Move1}, {pokemon.Move2}, {pokemon.Move3}, {pokemon.Move4}\n" +
            $"来源：{pokemon.Version}\n{LegalStatusText(record.LegalityStatus)}";
        var dialog = new AlertDialog.Builder(this)!;
        dialog.SetTitle("个体档案");
        dialog.SetMessage(text);
        dialog.SetPositiveButton("关闭", (_, _) => { });
        dialog.Show();
    }

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
