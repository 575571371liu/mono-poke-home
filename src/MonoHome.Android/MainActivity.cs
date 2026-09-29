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
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using PKHeX.Core;
using MonoHome.Core.Repository;
using MonoHome.Core.Saves;
using MonoHome.Core.Sync;
using MonoHome.Core.Sync.GitHub;
using MonoHome.Core.Transfers;
using MonoHome.Android.Sync;

[Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/AppTheme")]
public class MainActivity : Activity
{
    const int EmeraldRequest = 10;
    const int HeartGoldRequest = 11;
    const int AnySaveRequest = 14;
    const string CurrentVersion = "1.1.0";
    const string ReleaseApiUrl = "https://api.github.com/repos/575571371liu/mono-poke-home/releases/latest";
    const string EmeraldSaveKey = "emerald-save-id";
    const string HeartGoldSaveKey = "heartgold-save-id";
    const string OtherSaveKey = "other-save-id";
    const string DefaultRemoteRepository = "mono-home-saves";
    TextView? status;
    ProgressBar? progress;
    Button? transferButton;
    Button? exportBackupButton;
    Button? topImportButton;
    Button? refreshSavesButton;
    Button? warehouseFilterButton;
    Button? settingsButton;
    Button? emeraldSyncButton;
    Button? heartGoldSyncButton;
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
    byte[]? otherBytes;
    List<PokemonSlot> emeraldSlots = [];
    List<StoragePage> emeraldPages = [];
    List<PokemonSlot> heartGoldSlots = [];
    List<StoragePage> heartGoldPages = [];
    List<PokemonSlot> otherSlots = [];
    List<StoragePage> otherPages = [];
    int activeSourceRequest = EmeraldRequest;
    int sourcePageIndex;
    int warehousePageIndex;
    readonly HashSet<PokemonSlot> selectedSourceSlots = [];
    List<StoredPokemon> allWarehouse = [];
    List<StoredPokemon> warehouse = [];
    int? filterMinLevel;
    int? filterMaxLevel;
    int filterType = -1;
    int filterEggGroup = -1;
    int filterGender = -1;
    int filterShiny = -1;
    int filterEgg = -1;
    StoredPokemon? storedPokemon;
    RegisteredSave? emeraldSave;
    RegisteredSave? heartGoldSave;
    RegisteredSave? otherSave;
    RegisteredSave? selectedTransferTarget;
    int selectedTransferSlot = -1;
    string? pendingTransferId;
    readonly List<string> pendingTransferIds = [];
    string? lastBackupPath;
    IReadOnlyDictionary<string, string>? abilityEffects;
    IReadOnlyDictionary<int, MoveEffectData>? moveEffects;
    string emeraldExternalState = "尚未导入";
    string heartGoldExternalState = "尚未导入";
    string otherExternalState = "尚未导入";

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
        refreshSavesButton = FindViewById<Button>(Resource.Id.refresh_saves_button);
        warehouseFilterButton = FindViewById<Button>(Resource.Id.warehouse_filter_button);
        settingsButton = FindViewById<Button>(Resource.Id.settings_button);
        emeraldSyncButton = FindViewById<Button>(Resource.Id.emerald_sync_button);
        heartGoldSyncButton = FindViewById<Button>(Resource.Id.heartgold_sync_button);
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
        FindViewById<Button>(Resource.Id.import_emerald)!.Click += (_, _) => PickSave(AnySaveRequest);
        FindViewById<Button>(Resource.Id.import_heartgold)!.Click += (_, _) => PickSave(AnySaveRequest);
        emeraldSourceCard!.Click += (_, _) => SelectSourceSave(EmeraldRequest);
        heartGoldSourceCard!.Click += (_, _) => SelectSourceSave(HeartGoldRequest);
        uploadButton!.Click += async (_, _) => await UploadSelectedAsync();
        sourcePreviousBox!.Click += (_, _) => CycleSourceBox(-1);
        sourceNextBox!.Click += (_, _) => CycleSourceBox(1);
        warehouseBatchDownload!.Click += (_, _) => ShowWarehouseTargetChooser();
        saveNicknameButton!.Click += (_, _) => SaveNickname();
        discardEditsButton!.Click += (_, _) => DiscardEdits();
        transferButton!.Click += (_, _) =>
        {
            if (selectedWarehouseIds.Count > 1)
            {
                ShowWarehouseTargetChooser();
                return;
            }
            if (storedPokemon is null)
            {
                status!.Text = "请先在中央仓库选择要传送的宝可梦。";
                return;
            }
            if (selectedTransferTarget is null)
            {
                ShowTransferTargetChooser();
                return;
            }
            ShowTransferPlacementDialog(selectedTransferTarget);
        };
        exportBackupButton!.Click += (_, _) => BeginBackupExport();
        topImportButton!.Click += (_, _) => ShowImportChooser();
        refreshSavesButton!.Click += async (_, _) => await RefreshRegisteredSavesAsync();
        warehouseFilterButton!.Click += (_, _) => ShowWarehouseFilterDialog();
        settingsButton!.Click += (_, _) => ShowSettingsDialog();
        emeraldSyncButton!.Click += async (_, _) => await ShowSaveSyncDialogAsync("emerald");
        heartGoldSyncButton!.Click += async (_, _) => await ShowSaveSyncDialogAsync("heartgold");
        navHome!.Click += (_, _) => SwitchPage("warehouse", navHome);
        navEmerald!.Click += (_, _) => SwitchPage("emerald", navEmerald);
        navHeartGold!.Click += (_, _) => SwitchPage("heartgold", navHeartGold);
        navSaves!.Click += (_, _) => SwitchPage(otherSave is null ? "warehouse" : "other", otherSave is null ? navHome : navSaves);
        navHistory!.Click += (_, _) => ScrollToSection(historySection, navHistory);
        RestoreImportedSaves();
        SwitchPage("warehouse", navHome);
    }

    void ShowImportChooser()
    {
        if (status is null)
            return;
        PickSave(AnySaveRequest);
    }

    void ShowSettingsDialog()
    {
        var saves = new[] { emeraldSave, heartGoldSave, otherSave }.Count(save => save is not null);
        var message = $"版本：本地仓库模式\n存档：{saves} 个已登记\n仓库：{allWarehouse.Count} 条记录\n\n中央仓库默认只保存在本机。远程存档同步为可选功能，仅在你绑定私有仓库并主动操作时访问网络。";
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("设置");
        dialog.SetMessage(message);
        dialog.SetNegativeButton("关闭", (_, _) => { });
        dialog.SetNeutralButton("存档仓库", async (_, _) => await ShowRepositoryDialogAsync());
        dialog.SetPositiveButton("检查版本更新", async (_, _) => await CheckForUpdatesAsync());
        dialog.Show();
    }

    async Task ShowRepositoryDialogAsync()
    {
        var bindingStore = new AndroidRepositoryBindingStore(this);
        var repository = await bindingStore.LoadRepositoryAsync();
        var token = await new AndroidTokenStore(this).LoadTokenAsync();
        var message = repository is null
            ? "尚未绑定存档仓库。\n\n远程同步只访问你明确绑定的私有 GitHub 仓库。"
            : $"已绑定：{repository.Owner}/{repository.Repository}\n默认分支：{repository.DefaultBranch}\n授权：{(string.IsNullOrWhiteSpace(token) ? "未连接" : "已连接")}";
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("存档仓库");
        dialog.SetMessage(message);
        if (repository is null)
            dialog.SetNegativeButton("关闭", (_, _) => { });
        else
            dialog.SetNegativeButton("解除绑定", (_, _) => ConfirmUnbindRepository());
        dialog.SetNeutralButton(string.IsNullOrWhiteSpace(token) ? "连接 GitHub" : "重新连接", (_, _) => _ = ConnectGitHubAsync());
        dialog.SetPositiveButton(repository is null ? "绑定仓库" : "更换仓库", (_, _) => ShowRepositoryChoice());
        dialog.Show();
    }

    void ConfirmUnbindRepository()
    {
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("解除本机绑定");
        dialog.SetMessage("只清除本机保存的 GitHub 凭据、仓库绑定和存档线记录，不删除远端仓库或远端存档。之后仍可重新连接并绑定。");
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.SetPositiveButton("确认解除", async (_, _) => await UnbindRepositoryAsync());
        dialog.Show();
    }

    async Task UnbindRepositoryAsync()
    {
        SetBusy(true);
        try
        {
            await new AndroidTokenStore(this).ClearTokenAsync();
            await new AndroidRepositoryBindingStore(this).ClearAsync();
            status!.Text = "已解除本机存档仓库绑定，远端仓库未删除。";
            ShowSyncMessage("解除绑定完成", "本机凭据和绑定记录已清除；远端仓库与存档未删除。");
        }
        catch (Exception ex)
        {
            ShowSyncMessage("解除绑定失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    void ShowRepositoryChoice()
    {
        var tokenStore = new AndroidTokenStore(this);
        _ = ShowRepositoryChoiceAsync(tokenStore);
    }

    async Task ShowRepositoryChoiceAsync(AndroidTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(await tokenStore.LoadTokenAsync()))
        {
            ShowSyncMessage("存档仓库", "请先连接 GitHub 账号，再绑定私有仓库。");
            return;
        }

        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("选择仓库操作");
        dialog.SetMessage("创建专用仓库会打开 GitHub 的新建私有仓库页面；已有仓库则直接填写 owner 和仓库名。两种方式都会在绑定前验证私有状态和写权限。");
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.SetNeutralButton("创建专用仓库", (_, _) =>
        {
            var url = $"https://github.com/new?name={Uri.EscapeDataString(DefaultRemoteRepository)}&visibility=private&auto_init=1";
            StartActivity(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url)));
            ShowRepositoryBindingInput(DefaultRemoteRepository);
        });
        dialog.SetPositiveButton("绑定已有仓库", (_, _) => ShowRepositoryBindingInput());
        dialog.Show();
    }

    void ShowRepositoryBindingInput(string? defaultRepository = null)
    {
        var ownerInput = new EditText(this) { Hint = "GitHub 用户名或组织" };
        var repositoryInput = new EditText(this) { Hint = "仓库名" };
        if (!string.IsNullOrWhiteSpace(defaultRepository))
            repositoryInput.Text = defaultRepository;
        var fields = new LinearLayout(this) { Orientation = Orientation.Vertical };
        fields.SetPadding(Dp(20), 0, Dp(20), 0);
        fields.AddView(ownerInput);
        fields.AddView(repositoryInput);

        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("绑定 GitHub 私有仓库");
        dialog.SetView(fields);
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.SetPositiveButton("验证并绑定", async (_, _) => await BindRepositoryAsync(ownerInput.Text?.Trim(), repositoryInput.Text?.Trim()));
        dialog.Show();
    }

    async Task ConnectGitHubAsync()
    {
        var clientId = GetString(Resource.String.github_client_id);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            ShowSyncMessage("连接 GitHub", "当前 APK 尚未配置 GitHub App Client ID。配置公开的 Client ID 后即可使用 device flow；应用不会要求在普通输入框粘贴 Token。");
            return;
        }

        SetBusy(true);
        using var authCancellation = new CancellationTokenSource();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri("https://github.com/") };
            var flow = new GitHubDeviceFlowClient(client, clientId);
            var device = await flow.RequestDeviceCodeAsync(authCancellation.Token);
            var authDialog = new AlertDialog.Builder(this);
            authDialog.SetTitle("连接 GitHub");
            authDialog.SetMessage($"请在浏览器打开：\n{device.VerificationUri}\n\n输入一次性代码：{device.UserCode}\n\n完成授权后返回应用，应用会自动等待结果。");
            authDialog.SetNegativeButton("取消", (_, _) => authCancellation.Cancel());
            var shown = authDialog.Show();
            StartActivity(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(device.VerificationUri.ToString())));
            var access = await flow.WaitForAccessTokenAsync(device, authCancellation.Token);
            await new AndroidTokenStore(this).SaveTokenAsync(access.AccessToken);
            if (shown?.IsShowing == true)
                shown.Dismiss();
            status!.Text = "GitHub 已连接，请继续绑定私有存档仓库。";
            await ShowRepositoryDialogAsync();
        }
        catch (OperationCanceledException)
        {
            status!.Text = "已取消 GitHub 连接。";
        }
        catch (Exception ex)
        {
            ShowSyncMessage("连接 GitHub 失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task BindRepositoryAsync(string? owner, string? repositoryName)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repositoryName))
        {
            ShowSyncMessage("绑定仓库", "用户名/组织和仓库名都不能为空。");
            return;
        }

        SetBusy(true);
        using var operation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            var token = await new AndroidTokenStore(this).LoadTokenAsync(operation.Token) ?? throw new InvalidOperationException("尚未连接 GitHub 账号。");
            using var client = new HttpClient();
            var api = new GitHubApiClient(client, _ => Task.FromResult(token));
            var placeholder = new RepositoryBinding("github", owner, repositoryName, "main", DateTimeOffset.UtcNow, 1);
            var provider = new GitHubRemoteSaveProvider(api, placeholder);
            var binding = await provider.BindRepositoryAsync(owner, repositoryName, operation.Token);
            var boundProvider = new GitHubRemoteSaveProvider(api, binding);
            await boundProvider.GetManifestAsync(binding.DefaultBranch, true, operation.Token);
            await new AndroidRepositoryBindingStore(this).SaveRepositoryAsync(binding, operation.Token);
            status!.Text = $"已绑定 GitHub 私有仓库：{binding.Owner}/{binding.Repository}。";
            ShowSyncMessage("绑定成功", $"已验证并初始化 {binding.Owner}/{binding.Repository}。\n\n现在可以打开存档卡片中的“存档同步”。");
        }
        catch (OperationCanceledException)
        {
            ShowSyncMessage("绑定仓库失败", "GitHub 请求超时或已取消，请稍后重试。");
        }
        catch (GitHubApiException ex)
        {
            ShowSyncMessage("绑定仓库失败", ex.UserMessage);
        }
        catch (Exception ex)
        {
            ShowSyncMessage("绑定仓库失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task ShowSaveSyncDialogAsync(string saveKey)
    {
        var requestCode = saveKey == "emerald" ? EmeraldRequest : HeartGoldRequest;
        var current = saveKey == "emerald" ? emeraldSave : heartGoldSave;
        if (current is null)
        {
            ShowSyncMessage("存档同步", "请先导入对应存档。");
            return;
        }

        SetBusy(true);
        using var operation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await RefreshRegisteredSaveAsync(requestCode);
            current = saveKey == "emerald" ? emeraldSave : heartGoldSave;
            if (current is null)
            {
                ShowSyncMessage("存档同步", "存档刷新失败，请重新导入。");
                return;
            }
            var save = current;

            var local = File.ReadAllBytes(save.SnapshotPath);
            var localHash = SaveSyncService.ComputeHash(local);
            var bindingStore = new AndroidRepositoryBindingStore(this);
            var repository = await bindingStore.LoadRepositoryAsync(operation.Token);
            var token = await new AndroidTokenStore(this).LoadTokenAsync(operation.Token);
            if (repository is null || string.IsNullOrWhiteSpace(token))
            {
                ShowSyncMessage("存档同步", $"本地 SHA-256：{localHash}\n\n尚未绑定远端仓库或 GitHub 账号。\n远程同步需要先完成私有仓库绑定。");
                return;
            }

            using var client = new HttpClient();
            var api = new GitHubApiClient(client, _ => Task.FromResult(token));
            var provider = new GitHubRemoteSaveProvider(api, repository);
            var saveBinding = await bindingStore.LoadSaveBindingAsync(saveKey)
                ?? new SaveRemoteBinding(saveKey, repository.DefaultBranch, null);
            await provider.GetManifestAsync(saveBinding.LineageId, false, operation.Token);
            var remote = await provider.GetLatestAsync(saveKey, saveBinding.LineageId, operation.Token);
            var state = new SaveSyncService(provider).Compare(saveKey, local, saveBinding, remote);
            var message = $"仓库：{repository.Owner}/{repository.Repository}\n本地 SHA-256：{state.LocalHash}\n远端版本：{remote?.CommitSha ?? "尚无远端存档"}\n状态：{SyncStatusText(state.Status)}";
            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle($"{save.DisplayName} · 存档同步");
            dialog.SetMessage(message);
            dialog.SetNegativeButton("关闭", (_, _) => { });
            dialog.SetNeutralButton("历史版本", (_, _) => _ = ShowSaveHistoryAsync(saveKey));
            dialog.SetPositiveButton("操作", (_, _) => ShowSaveSyncActions(saveKey));
            dialog.Show();
        }
        catch (OperationCanceledException)
        {
            ShowSyncMessage("存档同步失败", "GitHub 请求超时或已取消，请稍后重试。");
        }
        catch (GitHubApiException ex)
        {
            ShowSyncMessage("存档同步失败", ex.UserMessage);
        }
        catch (Exception ex)
        {
            ShowSyncMessage("存档同步失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    void ConfirmSaveSyncAction(string saveKey, bool upload)
    {
        var action = upload ? "上传本地存档并创建远端版本" : "用远端版本覆盖本地快照";
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle(upload ? "确认上传" : "确认拉取");
        dialog.SetMessage($"将要{action}。{(upload ? "远端内容不一致时不会直接覆盖。" : "当前本地快照会先保存为 recovery 文件。")}");
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.SetPositiveButton("确认", async (_, _) => await RunSaveSyncActionAsync(saveKey, upload));
        dialog.Show();
    }

    void ConfirmSaveForkAction(string saveKey)
    {
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("创建新的存档线");
        dialog.SetMessage("远端和本地都已偏离共同基线。将从本地基线 commit 创建新存档线，再上传当前本地内容；原有远端历史不会被覆盖。");
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.SetPositiveButton("创建并上传", async (_, _) => await RunSaveSyncActionAsync(saveKey, true, null, true));
        dialog.Show();
    }

    static string CreateSaveRecoveryPoint(RegisteredSave save)
    {
        var recoveryPath = $"{save.SnapshotPath}.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.recovery";
        File.Copy(save.SnapshotPath, recoveryPath, overwrite: false);
        return recoveryPath;
    }

    void ShowSaveSyncActions(string saveKey)
    {
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("存档同步操作");
        dialog.SetMessage("所有上传、拉取都会再次确认；拉取前会保留本地 recovery 文件。");
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.SetNeutralButton("上传", (_, _) => ConfirmSaveSyncAction(saveKey, true));
        dialog.SetPositiveButton("拉取最新", (_, _) => ConfirmSaveSyncAction(saveKey, false));
        dialog.Show();
    }

    async Task ShowSaveHistoryAsync(string saveKey)
    {
        SetBusy(true);
        using var operation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            var bindingStore = new AndroidRepositoryBindingStore(this);
            var repository = await bindingStore.LoadRepositoryAsync(operation.Token) ?? throw new InvalidOperationException("尚未绑定远端仓库。");
            var token = await new AndroidTokenStore(this).LoadTokenAsync(operation.Token) ?? throw new InvalidOperationException("尚未连接 GitHub 账号。");
            var saveBinding = await bindingStore.LoadSaveBindingAsync(saveKey, operation.Token)
                ?? new SaveRemoteBinding(saveKey, repository.DefaultBranch, null);
            using var client = new HttpClient();
            var provider = new GitHubRemoteSaveProvider(new GitHubApiClient(client, _ => Task.FromResult(token)), repository);
            await provider.GetManifestAsync(saveBinding.LineageId, false, operation.Token);
            var versions = await provider.ListAllVersionsAsync(saveKey, saveBinding.LineageId, operation.Token);
            if (versions.Count == 0)
            {
                ShowSyncMessage("历史版本", "当前存档线还没有可选择的历史版本。");
                return;
            }

            var labels = versions.Select((version, index) =>
                $"v{versions.Count - index}{(version.LineageId == saveBinding.LineageId ? " · 当前线" : " · 其他线")} · {version.ModifiedAt.ToLocalTime():yyyy-MM-dd HH:mm}\n{version.CommitSha}\n{version.Message}\n存档线：{version.LineageId}").ToArray();
            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("选择历史版本");
            dialog.SetItems(labels, (_, args) => ConfirmSaveVersionPull(saveKey, versions[args.Which]));
            dialog.SetNegativeButton("取消", (_, _) => { });
            dialog.Show();
        }
        catch (OperationCanceledException)
        {
            ShowSyncMessage("历史版本", "GitHub 请求超时或已取消，请稍后重试。");
        }
        catch (GitHubApiException ex)
        {
            ShowSyncMessage("历史版本", ex.UserMessage);
        }
        catch (Exception ex)
        {
            ShowSyncMessage("历史版本", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    void ConfirmSaveVersionPull(string saveKey, RemoteSaveVersion version)
    {
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("确认回滚到历史版本");
        dialog.SetMessage($"版本：{version.CommitSha}\n时间：{version.ModifiedAt.ToLocalTime():yyyy-MM-dd HH:mm}\n\n当前本地快照会先保存为 recovery 文件。");
        dialog.SetNegativeButton("取消", (_, _) => { });
        dialog.SetPositiveButton("确认拉取", async (_, _) => await RunSaveSyncActionAsync(saveKey, false, version));
        dialog.Show();
    }

    async Task RunSaveSyncActionAsync(string saveKey, bool upload, RemoteSaveVersion? selectedVersion = null, bool createLineage = false)
    {
        SetBusy(true);
        using var operation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            var requestCode = saveKey == "emerald" ? EmeraldRequest : HeartGoldRequest;
            await RefreshRegisteredSaveAsync(requestCode);
            var current = saveKey == "emerald" ? emeraldSave : heartGoldSave;
            if (current is null)
                throw new InvalidOperationException("请先导入对应存档。");

            var bindingStore = new AndroidRepositoryBindingStore(this);
            var repository = await bindingStore.LoadRepositoryAsync(operation.Token) ?? throw new InvalidOperationException("尚未绑定远端仓库。");
            var token = await new AndroidTokenStore(this).LoadTokenAsync(operation.Token) ?? throw new InvalidOperationException("尚未连接 GitHub 账号。");
            var saveBinding = await bindingStore.LoadSaveBindingAsync(saveKey, operation.Token)
                ?? new SaveRemoteBinding(saveKey, repository.DefaultBranch, null);
            using var client = new HttpClient();
            var provider = new GitHubRemoteSaveProvider(new GitHubApiClient(client, _ => Task.FromResult(token)), repository);
            await provider.GetManifestAsync(saveBinding.LineageId, false, operation.Token);
            var service = new SaveSyncService(provider);
            SyncOperationResult result;
            if (upload)
            {
                var local = File.ReadAllBytes(current.SnapshotPath);
                var snapshot = new LocalSaveSnapshot(saveKey, local, SaveSyncService.ComputeHash(local), DateTimeOffset.UtcNow);
                var recoveryPath = CreateSaveRecoveryPoint(current);
                result = createLineage
                    ? await service.ForkAndUploadAsync(snapshot, repository, saveBinding, operation.Token)
                    : await service.UploadAsync(snapshot, repository, saveBinding, operation.Token);
                result = result with { RecoveryPointPath = recoveryPath };
            }
            else
            {
                var remote = selectedVersion ?? await provider.GetLatestAsync(saveKey, saveBinding.LineageId, operation.Token)
                    ?? throw new InvalidOperationException("远端还没有这个存档版本。");
                result = await service.PullAsync(current, remote, repository, saveBinding, operation.Token);
                if (saveKey == "emerald")
                    emeraldSave = SaveRegistry.Get(SavesPath, current.Id);
                else
                    heartGoldSave = SaveRegistry.Get(SavesPath, current.Id);
            }

            if (!result.Succeeded)
            {
                if (upload && result.State.Status == SyncStatus.Diverged)
                    ConfirmSaveForkAction(saveKey);
                else
                    ShowSyncMessage("存档同步", result.Message);
                return;
            }

            await bindingStore.SaveSaveBindingAsync(new SaveRemoteBinding(
                saveKey,
                result.State.LineageId,
                result.State.BaseCommitSha,
                result.State.RemoteLatest?.ContentHash ?? result.State.LocalHash), operation.Token);
            UpdateButtons();
            var completionMessage = result.RecoveryPointPath is null
                ? result.Message
                : $"{result.Message}\n\n本地 recovery：{result.RecoveryPointPath}";
            status!.Text = completionMessage;
            ShowSyncMessage("存档同步完成", completionMessage);
        }
        catch (OperationCanceledException)
        {
            ShowSyncMessage("存档同步失败", "GitHub 请求超时或已取消，请稍后重试。");
        }
        catch (GitHubApiException ex)
        {
            ShowSyncMessage("存档同步失败", ex.UserMessage);
        }
        catch (Exception ex)
        {
            ShowSyncMessage("存档同步失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    static string SyncStatusText(SyncStatus status) => status switch
    {
        SyncStatus.Aligned => "已对齐",
        SyncStatus.LocalNewer => "本地较新，可上传",
        SyncStatus.RemoteNewer => "远端较新，可拉取",
        SyncStatus.Diverged => "两端分叉，请先选择版本",
        _ => "尚未建立同步基线",
    };

    void ShowSyncMessage(string title, string message)
    {
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle(title);
        dialog.SetMessage(message);
        dialog.SetPositiveButton("关闭", (_, _) => { });
        dialog.Show();
    }

    async Task CheckForUpdatesAsync()
    {
        try
        {
            status!.Text = "正在检查版本更新…";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MonoHome", CurrentVersion));
            using var response = await client.GetAsync(ReleaseApiUrl);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var latest = ParseReleaseVersion(tag);
            var releaseUrl = root.GetProperty("html_url").GetString() ?? "https://github.com/575571371liu/mono-poke-home/releases";
            var apkUrl = root.TryGetProperty("assets", out var assets)
                ? assets.EnumerateArray().Select(asset => asset.TryGetProperty("browser_download_url", out var url) ? url.GetString() : null).FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
                : null;
            if (latest.CompareTo(ParseReleaseVersion(CurrentVersion)) <= 0)
            {
                status.Text = $"当前已是最新版本（{CurrentVersion}）。";
                return;
            }

            var name = root.TryGetProperty("name", out var releaseName) ? releaseName.GetString() : null;
            var notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
            var message = $"发现新版本 {tag}。\n{(string.IsNullOrWhiteSpace(name) ? "" : name + "\n")}{(string.IsNullOrWhiteSpace(notes) ? "" : notes)}";
            var update = new AlertDialog.Builder(this);
            update.SetTitle("发现新版本");
            update.SetMessage(message);
            update.SetNegativeButton("稍后", (_, _) => { });
            update.SetPositiveButton("打开下载", (_, _) => OpenReleaseUrl(apkUrl ?? releaseUrl));
            update.Show();
            status.Text = $"发现新版本：{tag}。";
        }
        catch (Exception ex)
        {
            status!.Text = $"版本检查失败：{ex.Message}";
        }
    }

    static Version ParseReleaseVersion(string value)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out var version) ? version : new Version(0, 0);
    }

    void OpenReleaseUrl(string url)
    {
        try
        {
            StartActivity(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url)));
        }
        catch (Exception ex)
        {
            status!.Text = $"无法打开下载页面：{ex.Message}";
        }
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
            var requestCode = page switch
            {
                "emerald" => EmeraldRequest,
                "heartgold" => HeartGoldRequest,
                _ => AnySaveRequest,
            };
            SelectSourceSave(requestCode, false);
            UpdateSourceArchiveHeader();
        }
        mainScroll?.Post(() => mainScroll.SmoothScrollTo(0, 0));
        UpdateButtons();
    }

    void UpdateSourceArchiveHeader()
    {
        if (sourceArchiveIcon is not null)
        {
            sourceArchiveIcon.SetImageResource(activeSourceRequest == EmeraldRequest ? Resource.Drawable.a_384 : Resource.Drawable.a_250);
            sourceArchiveIcon.ContentDescription = activeSourceRequest == EmeraldRequest ? "绿宝石代表宝可梦 烈空坐" : $"{ActiveSourceName()}存档";
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
        if (navSaves is not null)
        {
            navSaves.Visibility = otherSave is null ? global::Android.Views.ViewStates.Gone : global::Android.Views.ViewStates.Visible;
            navSaves.Text = otherSave is null ? "▣  存档信息" : $"▣  {otherSave.Game}";
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
            var resolvedRequest = requestCode == AnySaveRequest
                ? info.Game switch
                {
                    "Emerald" => EmeraldRequest,
                    "HeartGold" or "SoulSilver" => HeartGoldRequest,
                    _ => AnySaveRequest,
                }
                : requestCode;
            var slots = BoxReader.Read(bytes, name);
            status.Text = "正在登记本地快照…";
            RegisterSave(resolvedRequest, bytes, slots, uri.ToString(), (int)grantedFlags, name);
            status.Text = $"{info.Game} / Gen {info.Generation}\n可读取宝可梦：{slots.Count}\n已登记，本地处理。";
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

    async Task GenerateTransferAsync(int destinationSlot)
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
            var preparation = await Task.Run(() => TargetPreparationService.Prepare(current, heartGoldSave, heartGoldPath, global::System.IO.Path.Combine(cachePath, "prepared"), destinationSlot));
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
            selectedTransferSlot = destinationSlot;
            heartGoldBytes = write.WrittenBytes;
            heartGoldSlots = BoxReader.Read(heartGoldBytes, heartGoldSave.DisplayName).ToList();
            heartGoldPages = BoxReader.ReadPages(heartGoldBytes, heartGoldSave.DisplayName).ToList();
            TransferJournal.Append(TransfersPath, current.Id, emeraldSave?.Game ?? "Unknown", heartGoldSave.Game,
                new(true, current.Species, "prepared", true, preparation.PreparedSavePath, write.Message, preparation.Changes), write.BackupPath);
            LocalRepository.Remove(current);
            selectedWarehouseIds.Remove(current.Id);
            storedPokemon = null;
            selectedTransferTarget = null;
            selectedTransferSlot = -1;
            RefreshWarehouse();
            status.Text = $"{ChineseSpeciesName(current.Species)} 已传送至 {heartGoldSave.Game} 存档，中央仓库记录已移除。";
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

    async Task GenerateBatchTransferAsync(int destinationSlot = -1)
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
            var batch = await Task.Run(() => EmeraldHgssTransfer.TransferStoredMany(entities, heartGoldPath, outputPath, TransferMode.Conversion, destinationSlot));
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
            heartGoldSlots = BoxReader.Read(heartGoldBytes, heartGoldSave.DisplayName).ToList();
            heartGoldPages = BoxReader.ReadPages(heartGoldBytes, heartGoldSave.DisplayName).ToList();
            foreach (var (record, report) in records.Zip(batch.Reports))
            {
                TransferJournal.Append(TransfersPath, record.Id, emeraldSave?.Game ?? "Unknown", heartGoldSave.Game, report, write.BackupPath);
                LocalRepository.Remove(record);
                selectedWarehouseIds.Remove(record.Id);
            }
            storedPokemon = null;
            selectedTransferTarget = null;
            selectedTransferSlot = -1;
            RefreshWarehouse();
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
            actions.Add(() =>
            {
                if (heartGoldExternalState != "已同步")
                {
                    status.Text = "目标存档需重新授权或已被外部修改，未开始下载。";
                    return;
                }
                ShowBatchTransferPlacementDialog(heartGoldSave);
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
                    selectedTransferSlot = -1;
                    UpdateButtons();
                    ShowTransferPlacementDialog(selectedTransferTarget);
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

    void ShowTransferPlacementDialog(RegisteredSave target)
    {
        if (storedPokemon is null || target.Game is not "HeartGold" and not "SoulSilver")
            return;
        if (heartGoldPages.Count <= 1)
        {
            status!.Text = "目标存档没有可用盒子。";
            return;
        }

        var pages = heartGoldPages.Skip(1).ToArray();
        var dialog = new Dialog(this);
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(12), Dp(12), Dp(12), Dp(10));
        var panelBackground = new GradientDrawable();
        panelBackground.SetColor(Color.ParseColor("#0F201C"));
        panelBackground.SetCornerRadius(Dp(16));
        panelBackground.SetStroke(Dp(1), Color.ParseColor("#315249"));
        panel.Background = panelBackground;

        var title = new TextView(this) { Text = $"选择 {ChineseGameName(target.Game)} 放置位置", TextSize = 18 };
        title.SetTextColor(Color.ParseColor("#E9F4EF"));
        title.SetTypeface(global::Android.Graphics.Typeface.Default, global::Android.Graphics.TypefaceStyle.Bold);
        panel.AddView(title);
        var hint = new TextView(this) { Text = $"来源：{ChineseSpeciesName(storedPokemon.Species)} · 点击目标盒子中的具体槽位", TextSize = 11 };
        hint.SetTextColor(Color.ParseColor("#91AAA1"));
        panel.AddView(hint, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var previous = new Button(this) { Text = "‹" };
        var next = new Button(this) { Text = "›" };
        previous.SetAllCaps(false);
        next.SetAllCaps(false);
        var pageTitle = new TextView(this) { Gravity = GravityFlags.Center, TextSize = 11 };
        pageTitle.SetTextColor(Color.ParseColor("#E9F4EF"));
        header.AddView(previous, new LinearLayout.LayoutParams(Dp(42), Dp(42)));
        header.AddView(pageTitle, new LinearLayout.LayoutParams(0, Dp(42), 1));
        header.AddView(next, new LinearLayout.LayoutParams(Dp(42), Dp(42)));
        panel.AddView(header, new LinearLayout.LayoutParams(-1, Dp(42)) { TopMargin = Dp(10) });

        var grid = new GridLayout(this) { ColumnCount = 5, UseDefaultMargins = true };
        panel.AddView(grid, new LinearLayout.LayoutParams(-1, 0, 1) { TopMargin = Dp(8) });
        var close = new Button(this) { Text = "取消" };
        close.SetAllCaps(false);
        close.SetTextColor(Color.ParseColor("#8DE4D1"));
        close.Background = CreateSlotBackground(false, false, true);
        close.Click += (_, _) => dialog.Dismiss();
        panel.AddView(close, new LinearLayout.LayoutParams(-1, Dp(44)) { TopMargin = Dp(8) });

        var pageIndex = Math.Clamp(selectedTransferSlot >= 0 ? selectedTransferSlot / 30 : 0, 0, pages.Length - 1);
        void RenderPage()
        {
            var page = pages[pageIndex];
            pageTitle.Text = $"{page.Name}\n点击槽位确认";
            previous.Enabled = pageIndex > 0;
            next.Enabled = pageIndex < pages.Length - 1;
            grid.RemoveAllViews();
            var width = Math.Max(42, (Resources.DisplayMetrics.WidthPixels - Dp(70)) / 5);
            foreach (var slot in page.Slots)
            {
                View tile;
                if (slot.Pokemon is null)
                {
                    var empty = new TextView(this) { Text = (slot.Index + 1).ToString("00"), Gravity = GravityFlags.Center, TextSize = 10, ContentDescription = $"空槽位 {slot.Index + 1}" };
                    empty.SetTextColor(Color.Rgb(68, 105, 93));
                    empty.Background = CreateSlotBackground(false, false, true);
                    empty.Click += (_, _) => ConfirmTransferPlacement(dialog, target, pageIndex * page.Capacity + slot.Index, null);
                    tile = empty;
                }
                else
                {
                    var occupied = slot.Pokemon;
                    var image = new ImageButton(this) { ContentDescription = $"覆盖 {ChineseSpeciesName(occupied.Species)} · 槽位 {slot.Index + 1}" };
                    image.SetScaleType(ImageView.ScaleType.CenterInside);
                    image.SetPadding(Dp(5), Dp(5), Dp(5), Dp(5));
                    image.Background = CreateSlotBackground(true, false, false);
                    var icon = Resources.GetIdentifier($"a_{occupied.Species}", "drawable", PackageName);
                    if (icon != 0)
                        image.SetImageResource(icon);
                    image.Click += (_, _) => ConfirmTransferPlacement(dialog, target, pageIndex * page.Capacity + slot.Index, occupied);
                    tile = image;
                }
                var parameters = new GridLayout.LayoutParams { Width = width, Height = width };
                parameters.SetMargins(3, 3, 3, 3);
                grid.AddView(tile, parameters);
            }
        }
        previous.Click += (_, _) => { pageIndex--; RenderPage(); };
        next.Click += (_, _) => { pageIndex++; RenderPage(); };
        RenderPage();
        dialog.SetContentView(panel);
        dialog.Show();
        if (dialog.Window is { } window)
        {
            window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            window.SetDimAmount(0.72f);
            window.AddFlags(WindowManagerFlags.DimBehind);
            window.SetLayout((int)(Resources.DisplayMetrics.WidthPixels * 0.94f), (int)(Resources.DisplayMetrics.HeightPixels * 0.88f));
        }
    }

    void ConfirmTransferPlacement(Dialog placement, RegisteredSave target, int destinationSlot, PokemonSlot? occupied)
    {
        var location = $"仓库 {destinationSlot / 30 + 1} · 槽位 {destinationSlot % 30 + 1}";
        var message = occupied is null
            ? $"将 {ChineseSpeciesName(storedPokemon!.Species)} 写入 {location}。"
            : $"{location} 当前是 {ChineseSpeciesName(occupied.Species)}，确认覆盖并传送吗？";
        var confirm = new AlertDialog.Builder(this);
        confirm.SetTitle("确认传送");
        confirm.SetMessage(message);
        confirm.SetNegativeButton("取消", (_, _) => { });
        confirm.SetPositiveButton("确认传送", async (_, _) =>
        {
            selectedTransferTarget = target;
            selectedTransferSlot = destinationSlot;
            placement.Dismiss();
            await GenerateTransferAsync(destinationSlot);
        });
        confirm.Show();
    }

    void ShowBatchTransferPlacementDialog(RegisteredSave target)
    {
        var records = warehouse.Where(record => selectedWarehouseIds.Contains(record.Id)).ToArray();
        if (records.Length == 0 || target.Game is not ("HeartGold" or "SoulSilver"))
            return;
        if (heartGoldPages.Count <= 1)
        {
            status!.Text = "目标存档没有可用盒子。";
            return;
        }

        var pages = heartGoldPages.Skip(1).ToArray();
        var allSlots = pages.SelectMany(page => page.Slots).ToArray();
        var dialog = new Dialog(this);
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(12), Dp(12), Dp(12), Dp(10));
        var panelBackground = new GradientDrawable();
        panelBackground.SetColor(Color.ParseColor("#0F201C"));
        panelBackground.SetCornerRadius(Dp(16));
        panelBackground.SetStroke(Dp(1), Color.ParseColor("#315249"));
        panel.Background = panelBackground;

        var title = new TextView(this) { Text = $"选择 {ChineseGameName(target.Game)} 批量放置位置", TextSize = 18 };
        title.SetTextColor(Color.ParseColor("#E9F4EF"));
        title.SetTypeface(global::Android.Graphics.Typeface.Default, global::Android.Graphics.TypefaceStyle.Bold);
        panel.AddView(title);
        var hint = new TextView(this)
        {
            Text = $"已选择 {records.Length} 只 · 点击起始槽位，按顺序放入后续槽位",
            TextSize = 11,
        };
        hint.SetTextColor(Color.ParseColor("#91AAA1"));
        panel.AddView(hint, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(4) });

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var previous = new Button(this) { Text = "‹" };
        var next = new Button(this) { Text = "›" };
        previous.SetAllCaps(false);
        next.SetAllCaps(false);
        var pageTitle = new TextView(this) { Gravity = GravityFlags.Center, TextSize = 11 };
        pageTitle.SetTextColor(Color.ParseColor("#E9F4EF"));
        header.AddView(previous, new LinearLayout.LayoutParams(Dp(42), Dp(42)));
        header.AddView(pageTitle, new LinearLayout.LayoutParams(0, Dp(42), 1));
        header.AddView(next, new LinearLayout.LayoutParams(Dp(42), Dp(42)));
        panel.AddView(header, new LinearLayout.LayoutParams(-1, Dp(42)) { TopMargin = Dp(10) });

        var grid = new GridLayout(this) { ColumnCount = 5, UseDefaultMargins = true };
        panel.AddView(grid, new LinearLayout.LayoutParams(-1, 0, 1) { TopMargin = Dp(8) });
        var close = new Button(this) { Text = "取消" };
        close.SetAllCaps(false);
        close.SetTextColor(Color.ParseColor("#8DE4D1"));
        close.Background = CreateSlotBackground(false, false, true);
        close.Click += (_, _) => dialog.Dismiss();
        panel.AddView(close, new LinearLayout.LayoutParams(-1, Dp(44)) { TopMargin = Dp(8) });

        var pageIndex = 0;
        void RenderPage()
        {
            var page = pages[pageIndex];
            pageTitle.Text = $"{page.Name}\n点击起始槽位";
            previous.Enabled = pageIndex > 0;
            next.Enabled = pageIndex < pages.Length - 1;
            grid.RemoveAllViews();
            var width = Math.Max(42, (Resources.DisplayMetrics.WidthPixels - Dp(70)) / 5);
            foreach (var slot in page.Slots)
            {
                var destinationSlot = pageIndex * page.Capacity + slot.Index;
                View tile;
                if (slot.Pokemon is null)
                {
                    var empty = new TextView(this)
                    {
                        Text = (slot.Index + 1).ToString("00"),
                        Gravity = GravityFlags.Center,
                        TextSize = 10,
                        ContentDescription = $"批量起始槽位 {slot.Index + 1}",
                    };
                    empty.SetTextColor(Color.Rgb(68, 105, 93));
                    empty.Background = CreateSlotBackground(false, false, true);
                    empty.Click += (_, _) => ConfirmBatchTransferPlacement(dialog, target, destinationSlot, records.Length, allSlots);
                    tile = empty;
                }
                else
                {
                    var occupied = slot.Pokemon;
                    var image = new ImageButton(this) { ContentDescription = $"从此处批量放置 · 槽位 {slot.Index + 1}" };
                    image.SetScaleType(ImageView.ScaleType.CenterInside);
                    image.SetPadding(Dp(5), Dp(5), Dp(5), Dp(5));
                    image.Background = CreateSlotBackground(true, false, false);
                    var icon = Resources.GetIdentifier($"a_{occupied.Species}", "drawable", PackageName);
                    if (icon != 0)
                        image.SetImageResource(icon);
                    image.Click += (_, _) => ConfirmBatchTransferPlacement(dialog, target, destinationSlot, records.Length, allSlots);
                    tile = image;
                }
                var parameters = new GridLayout.LayoutParams { Width = width, Height = width };
                parameters.SetMargins(3, 3, 3, 3);
                grid.AddView(tile, parameters);
            }
        }
        previous.Click += (_, _) => { pageIndex--; RenderPage(); };
        next.Click += (_, _) => { pageIndex++; RenderPage(); };
        RenderPage();
        dialog.SetContentView(panel);
        dialog.Show();
        if (dialog.Window is { } window)
        {
            window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            window.SetDimAmount(0.72f);
            window.AddFlags(WindowManagerFlags.DimBehind);
            window.SetLayout((int)(Resources.DisplayMetrics.WidthPixels * 0.94f), (int)(Resources.DisplayMetrics.HeightPixels * 0.88f));
        }
    }

    void ConfirmBatchTransferPlacement(Dialog placement, RegisteredSave target, int destinationSlot, int count, IReadOnlyList<StorageSlot> allSlots)
    {
        if (destinationSlot + count > allSlots.Count)
        {
            status!.Text = $"从此位置开始不足 {count} 个连续槽位，请换一个起点。";
            return;
        }
        var occupied = allSlots.Skip(destinationSlot).Take(count).Count(slot => slot.Pokemon is not null);
        var location = $"仓库 {destinationSlot / 30 + 1} · 槽位 {destinationSlot % 30 + 1}";
        var message = occupied == 0
            ? $"将 {count} 只宝可梦从 {location} 开始依次写入。"
            : $"从 {location} 开始的 {count} 个槽位中有 {occupied} 个已有宝可梦，确认覆盖并传送吗？";
        var confirm = new AlertDialog.Builder(this);
        confirm.SetTitle("确认批量传送");
        confirm.SetMessage(message);
        confirm.SetNegativeButton("取消", (_, _) => { });
        confirm.SetPositiveButton("确认传送", async (_, _) =>
        {
            selectedTransferTarget = target;
            selectedTransferSlot = destinationSlot;
            placement.Dismiss();
            await GenerateBatchTransferAsync(destinationSlot);
        });
        confirm.Show();
    }

    void RegisterSave(int requestCode, byte[] bytes, IReadOnlyList<PokemonSlot> slots, string? sourceUri, int sourceFlags = 0, string? displayName = null)
    {
        if (requestCode == EmeraldRequest)
        {
            emeraldSave = SaveRegistry.Register(bytes, displayName ?? "emerald.srm", SavesPath, sourceUri, sourceFlags);
            emeraldExternalState = "已同步";
            GetSharedPreferences("saves", FileCreationMode.Private)!.Edit()!.PutString(EmeraldSaveKey, emeraldSave.Id)!.Apply();
            emeraldBytes = File.ReadAllBytes(emeraldSave.SnapshotPath);
            emeraldSlots = BoxReader.Read(emeraldBytes, emeraldSave.DisplayName).ToList();
            emeraldPages = BoxReader.ReadPages(emeraldBytes, emeraldSave.DisplayName).ToList();
            SelectSourceSave(EmeraldRequest, false);
        }
        else
        {
            if (requestCode == HeartGoldRequest)
            {
                heartGoldSave = SaveRegistry.Register(bytes, displayName ?? "heartgold.sav", SavesPath, sourceUri, sourceFlags);
                selectedTransferTarget = null;
                heartGoldExternalState = "已同步";
                GetSharedPreferences("saves", FileCreationMode.Private)!.Edit()!.PutString(HeartGoldSaveKey, heartGoldSave.Id)!.Apply();
                heartGoldBytes = File.ReadAllBytes(heartGoldSave.SnapshotPath);
                heartGoldSlots = BoxReader.Read(heartGoldBytes, heartGoldSave.DisplayName).ToList();
                heartGoldPages = BoxReader.ReadPages(heartGoldBytes, heartGoldSave.DisplayName).ToList();
                if (emeraldSave is null)
                    SelectSourceSave(HeartGoldRequest, false);
                UpdateButtons();
                return;
            }

            otherSave = SaveRegistry.Register(bytes, displayName ?? "pokemon-save", SavesPath, sourceUri, sourceFlags);
            otherExternalState = "已同步";
            GetSharedPreferences("saves", FileCreationMode.Private)!.Edit()!.PutString(OtherSaveKey, otherSave.Id)!.Apply();
            otherBytes = File.ReadAllBytes(otherSave.SnapshotPath);
            otherSlots = BoxReader.Read(otherBytes, otherSave.DisplayName).ToList();
            otherPages = BoxReader.ReadPages(otherBytes, otherSave.DisplayName).ToList();
            SelectSourceSave(AnySaveRequest, false);
            SwitchPage("other", navSaves);
        }
        UpdateButtons();
    }

    void RestoreImportedSaves()
    {
        Restore(EmeraldRequest, EmeraldSaveKey);
        Restore(HeartGoldRequest, HeartGoldSaveKey);
        Restore(AnySaveRequest, OtherSaveKey);
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
            else if (requestCode == HeartGoldRequest)
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
            else
            {
                otherSave = saved;
                otherBytes = bytes;
                otherExternalState = CheckSourceUri(saved);
                otherSlots = BoxReader.Read(bytes, saved.DisplayName).ToList();
                otherPages = BoxReader.ReadPages(bytes, saved.DisplayName).ToList();
                if (emeraldSave is null && heartGoldSave is null)
                    SelectSourceSave(AnySaveRequest, false);
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

    RegisteredSave? ActiveSourceSave(int? requestCode = null) => (requestCode ?? activeSourceRequest) switch
    {
        EmeraldRequest => emeraldSave,
        HeartGoldRequest => heartGoldSave,
        _ => otherSave,
    };
    List<StoragePage> ActiveSourcePages() => activeSourceRequest switch
    {
        EmeraldRequest => emeraldPages,
        HeartGoldRequest => heartGoldPages,
        _ => otherPages,
    };
    string ActiveSourceState() => activeSourceRequest switch
    {
        EmeraldRequest => emeraldExternalState,
        HeartGoldRequest => heartGoldExternalState,
        _ => otherExternalState,
    };
    string ActiveSourceName() => activeSourceRequest switch
    {
        EmeraldRequest => "绿宝石",
        HeartGoldRequest => "心金 / 魂银",
        _ => otherSave?.Game ?? "其他存档",
    };

    void CycleSourceBox(int delta)
    {
        var pages = ActiveSourcePages();
        sourcePageIndex = Math.Clamp(sourcePageIndex, 0, Math.Max(0, pages.Count - 1));
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
        var centralPage = centralWarehouseContent?.Visibility == global::Android.Views.ViewStates.Visible;
        if (connectedSavesSection is not null)
        {
            var hasConnectedSaves = emeraldSave is not null || heartGoldSave is not null;
            connectedSavesSection.Visibility = centralPage && hasConnectedSaves
                ? global::Android.Views.ViewStates.Visible
                : global::Android.Views.ViewStates.Gone;
        }
        if (uploadButton is not null)
        {
            uploadButton.Visibility = selectedSourceSlots.Count == 0 ? global::Android.Views.ViewStates.Gone : global::Android.Views.ViewStates.Visible;
            uploadButton.Enabled = selectedSourceSlots.Count > 0 && ActiveSourceState() == "已同步";
            uploadButton.Text = $"上传 {selectedSourceSlots.Count} 只至中央仓库";
        }
        if (transferButton is not null)
        {
            transferButton.Visibility = centralPage ? global::Android.Views.ViewStates.Visible : global::Android.Views.ViewStates.Gone;
            transferButton.Text = selectedWarehouseIds.Count > 1
                ? $"传送已选 {selectedWarehouseIds.Count} 只"
                : storedPokemon is null
                ? "先选择中央仓库宝可梦"
                : selectedTransferTarget is null
                    ? "选择目标存档"
                    : "选择目标盒子位置";
            transferButton.Enabled = centralPage && (storedPokemon is not null || selectedWarehouseIds.Count > 1) && heartGoldSave is not null && heartGoldExternalState == "已同步";
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
        {
            warehouseState.Text = allWarehouse.Count == 0
                ? "本地仓库为空"
                : warehouse.Count != allWarehouse.Count
                    ? $"筛选结果 · {warehouse.Count} / {allWarehouse.Count} 条记录"
                    : storedPokemon is null
                        ? $"仓库记录 · {allWarehouse.Count} 条"
                        : $"仓库记录 · {ChineseSpeciesName(storedPokemon.Species)} · {LegalStatusText(storedPokemon.LegalityStatus)}";
        }
        selectedWarehouseIds.RemoveWhere(id => allWarehouse.All(record => record.Id != id));
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
        allWarehouse = LocalRepository.List(WarehousePath).ToList();
        ApplyWarehouseFilter(selectId);
    }

    void ApplyWarehouseFilter(string? selectId = null)
    {
        warehouse = allWarehouse.Where(WarehouseRecordMatches).ToList();
        RenderFilteredWarehouse(selectId);
    }

    async Task ApplyWarehouseFilterAsync(string? selectId = null)
    {
        if (warehouseFilterButton is not null)
        {
            warehouseFilterButton.Enabled = false;
            warehouseFilterButton.Text = "筛选中…";
        }
        var filtered = await Task.Run(() => allWarehouse.Where(WarehouseRecordMatches).ToList());
        warehouse = filtered;
        RenderFilteredWarehouse(selectId);
        if (warehouseFilterButton is not null)
            warehouseFilterButton.Enabled = true;
    }

    void RenderFilteredWarehouse(string? selectId)
    {
        warehousePicker!.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem,
            warehouse.Select(record => $"{ChineseSpeciesName(record.Species)} · {LegalStatusText(record.LegalityStatus)} · {record.UpdatedAt.LocalDateTime:g}").ToArray());
        var index = selectId is null ? 0 : warehouse.FindIndex(record => record.Id == selectId);
        if (index < 0 && warehouse.Count > 0)
            index = 0;
        if (index >= 0)
            warehousePicker.SetSelection(index);
        storedPokemon = index >= 0 && index < warehouse.Count ? warehouse[index] : null;
        warehousePageIndex = 0;
        UpdateWarehouseFilterButton();
        UpdateButtons();
    }

    bool WarehouseRecordMatches(StoredPokemon record)
    {
        if (filterMinLevel is null && filterMaxLevel is null && filterType < 0 && filterEggGroup < 0 && filterGender < 0 && filterShiny < 0 && filterEgg < 0)
            return true;
        try
        {
            var pokemon = LocalRepository.LoadWorking(record);
            if (filterMinLevel is not null && pokemon.CurrentLevel < filterMinLevel)
                return false;
            if (filterMaxLevel is not null && pokemon.CurrentLevel > filterMaxLevel)
                return false;
            if (filterType >= 0 && pokemon.PersonalInfo.Type1 != filterType && pokemon.PersonalInfo.Type2 != filterType)
                return false;
            if (filterEggGroup >= 0 && pokemon.PersonalInfo.EggGroup1 != filterEggGroup && pokemon.PersonalInfo.EggGroup2 != filterEggGroup)
                return false;
            if (filterGender >= 0 && (filterGender == 2 ? pokemon.Gender is 0 or 1 : pokemon.Gender != filterGender))
                return false;
            if (filterShiny >= 0 && pokemon.IsShiny != (filterShiny == 1))
                return false;
            if (filterEgg >= 0 && pokemon.IsEgg != (filterEgg == 1))
                return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    void UpdateWarehouseFilterButton()
    {
        if (warehouseFilterButton is null)
            return;
        var count = (filterMinLevel is not null ? 1 : 0) + (filterMaxLevel is not null ? 1 : 0) +
            (filterType >= 0 ? 1 : 0) + (filterEggGroup >= 0 ? 1 : 0) + (filterGender >= 0 ? 1 : 0) +
            (filterShiny >= 0 ? 1 : 0) + (filterEgg >= 0 ? 1 : 0);
        warehouseFilterButton.Text = count == 0 ? "全部 ⌄" : $"筛选 · {count} ⌄";
        warehouseFilterButton.SetTextColor(Color.ParseColor(count == 0 ? "#91AAA1" : "#D6FF63"));
    }

    void ShowWarehouseFilterDialog()
    {
        var strings = GameInfo.GetStrings("zh-Hans");
        var typeValues = Enumerable.Range(0, Math.Min(18, strings.Types.Count)).ToArray();
        var typeOptions = new[] { "全部" }.Concat(typeValues.Select(value => StringAt(strings.Types, value, $"属性 {value}"))).ToArray();
        var eggValues = Enum.GetValues<EggGroup>().Where(group => group != EggGroup.None).ToArray();
        var eggOptions = new[] { "全部" }.Concat(eggValues.Select(group => EggGroupText((int)group))).ToArray();
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(18), Dp(2), Dp(18), 0);

        var initialLower = filterMinLevel ?? 1;
        var initialUpper = filterMaxLevel ?? 100;
        var levelLabel = new TextView(this) { Text = $"等级范围 · {initialLower} - {initialUpper}", TextSize = 10 };
        levelLabel.SetTextColor(Color.ParseColor("#91AAA1"));
        levelLabel.SetIncludeFontPadding(false);
        root.AddView(levelLabel);
        var levelRange = new LevelRangeView(this,
            initialLower,
            initialUpper);
        levelRange.RangeChanged += (_, _) =>
            levelLabel.Text = $"等级范围 · {levelRange.LowerValue} - {levelRange.UpperValue}";
        root.AddView(levelRange, new LinearLayout.LayoutParams(-1, Dp(42)));

        ArrayAdapter<string> SpinnerAdapter(string[] values)
        {
            return new CompactSpinnerAdapter(this, values);
        }
        var typeSpinner = new Spinner(this);
        typeSpinner.Adapter = SpinnerAdapter(typeOptions);
        typeSpinner.SetSelection(filterType < 0 ? 0 : Array.IndexOf(typeValues, filterType) + 1);
        var eggSpinner = new Spinner(this);
        eggSpinner.Adapter = SpinnerAdapter(eggOptions);
        eggSpinner.SetSelection(filterEggGroup < 0 ? 0 : Array.IndexOf(eggValues, (EggGroup)filterEggGroup) + 1);
        var genderSpinner = new Spinner(this);
        genderSpinner.Adapter = SpinnerAdapter(["全部", "雄", "雌", "无性别"]);
        genderSpinner.SetSelection(filterGender < 0 ? 0 : filterGender + 1);
        var shinySpinner = new Spinner(this);
        shinySpinner.Adapter = SpinnerAdapter(["闪光：全部", "普通", "闪光"]);
        shinySpinner.SetSelection(filterShiny < 0 ? 0 : filterShiny + 1);
        var eggStateSpinner = new Spinner(this);
        eggStateSpinner.Adapter = SpinnerAdapter(["孵化状态：全部", "已孵化", "蛋"]);
        eggStateSpinner.SetSelection(filterEgg < 0 ? 0 : filterEgg + 1);
        GradientDrawable Panel(string fill = "#132A25", string stroke = "#315249", float radius = 8)
        {
            var background = new GradientDrawable();
            background.SetColor(Color.ParseColor(fill));
            background.SetCornerRadius(Dp(radius));
            background.SetStroke(Dp(1), Color.ParseColor(stroke));
            return background;
        }
        foreach (var spinner in new[] { typeSpinner, eggSpinner, genderSpinner, shinySpinner, eggStateSpinner })
        {
            spinner.Background = Panel();
            spinner.SetPadding(Dp(10), 0, Dp(10), 0);
        }
        foreach (var pair in new[] { ("属性", typeSpinner), ("蛋组", eggSpinner), ("性别", genderSpinner), ("闪光", shinySpinner), ("孵化状态", eggStateSpinner) })
        {
            var label = new TextView(this) { Text = pair.Item1, TextSize = 10 };
            label.SetTextColor(Color.ParseColor("#91AAA1"));
            label.SetIncludeFontPadding(false);
            root.AddView(label, new LinearLayout.LayoutParams(-1, Dp(22)) { TopMargin = Dp(5) });
            root.AddView(pair.Item2, new LinearLayout.LayoutParams(-1, Dp(44)));
        }

        var dialog = new Dialog(this);
        var shell = new LinearLayout(this) { Orientation = Orientation.Vertical };
        shell.SetPadding(Dp(16), Dp(14), Dp(16), Dp(10));
        shell.Background = Panel("#0F201C", "#315249", 16);
        var title = new TextView(this) { Text = "筛选仓库", TextSize = 17 };
        title.SetTextColor(Color.ParseColor("#E9F4EF"));
        title.SetIncludeFontPadding(false);
        title.SetTypeface(global::Android.Graphics.Typeface.Default, global::Android.Graphics.TypefaceStyle.Bold);
        shell.AddView(title, new LinearLayout.LayoutParams(-1, Dp(38)));
        var scroll = new ScrollView(this) { FillViewport = true, VerticalScrollBarEnabled = false };
        scroll.AddView(root);
        shell.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        actions.SetGravity(GravityFlags.CenterVertical);
        Button Action(string text, string fill, string textColor)
        {
            var button = new Button(this) { Text = text };
            button.SetAllCaps(false);
            button.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 12);
            button.SetTextColor(Color.ParseColor(textColor));
            button.Background = Panel(fill, fill == "#D6FF63" ? "#D6FF63" : "#315249", 8);
            return button;
        }
        var clear = Action("清除", "#132A25", "#D6FF63");
        var cancel = Action("取消", "#132A25", "#8DE4D1");
        var apply = Action("应用", "#D6FF63", "#0B1414");
        actions.AddView(clear, new LinearLayout.LayoutParams(0, Dp(44), 1) { RightMargin = Dp(6) });
        actions.AddView(cancel, new LinearLayout.LayoutParams(0, Dp(44), 1) { RightMargin = Dp(6) });
        actions.AddView(apply, new LinearLayout.LayoutParams(0, Dp(44), 1));
        shell.AddView(actions, new LinearLayout.LayoutParams(-1, Dp(50)) { TopMargin = Dp(8) });
        clear.Click += (_, _) =>
        {
            filterMinLevel = null;
            filterMaxLevel = null;
            filterType = filterEggGroup = filterGender = filterShiny = filterEgg = -1;
            dialog.Dismiss();
            _ = ApplyWarehouseFilterAsync();
        };
        cancel.Click += (_, _) => dialog.Dismiss();
        apply.Click += (_, _) =>
        {
            filterMinLevel = levelRange.LowerValue == 1 ? null : levelRange.LowerValue;
            filterMaxLevel = levelRange.UpperValue == 100 ? null : levelRange.UpperValue;
            filterType = typeSpinner.SelectedItemPosition == 0 ? -1 : typeValues[typeSpinner.SelectedItemPosition - 1];
            filterEggGroup = eggSpinner.SelectedItemPosition == 0 ? -1 : (int)eggValues[eggSpinner.SelectedItemPosition - 1];
            filterGender = genderSpinner.SelectedItemPosition - 1;
            filterShiny = shinySpinner.SelectedItemPosition - 1;
            filterEgg = eggStateSpinner.SelectedItemPosition - 1;
            dialog.Dismiss();
            _ = ApplyWarehouseFilterAsync();
        };
        dialog.SetContentView(shell);
        dialog.Show();
        if (dialog.Window is { } window)
        {
            window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            window.SetDimAmount(0.72f);
            window.AddFlags(WindowManagerFlags.DimBehind);
            window.SetLayout((int)(Resources.DisplayMetrics.WidthPixels * 0.92f), (int)(Resources.DisplayMetrics.HeightPixels * 0.78f));
        }
    }

    static int? ParseFilterLevel(string? value)
    {
        return int.TryParse(value, out var level) ? Math.Clamp(level, 1, 100) : null;
    }

    static string EggGroupText(int value) => (EggGroup)value switch
    {
        EggGroup.Monster => "怪兽",
        EggGroup.Water1 => "水中1",
        EggGroup.Bug => "虫",
        EggGroup.Flying => "飞行",
        EggGroup.Field => "陆上",
        EggGroup.Fairy => "妖精",
        EggGroup.Grass => "植物",
        EggGroup.HumanLike => "人形",
        EggGroup.Water3 => "水中3",
        EggGroup.Mineral => "矿物",
        EggGroup.Amorphous => "不定形",
        EggGroup.Water2 => "水中2",
        EggGroup.Ditto => "百变怪",
        EggGroup.Dragon => "龙",
        EggGroup.Undiscovered => "未发现蛋组",
        _ => "未知蛋组",
    };

    async Task RefreshRegisteredSavesAsync()
    {
        if (status is null)
            return;
        SetBusy(true);
        try
        {
            status.Text = "正在刷新已登记存档…";
            var refreshed = new List<string>();
            if (emeraldSave is not null)
            {
                await RefreshRegisteredSaveAsync(EmeraldRequest);
                refreshed.Add($"绿宝石：{emeraldExternalState}");
            }
            if (heartGoldSave is not null)
            {
                await RefreshRegisteredSaveAsync(HeartGoldRequest);
                refreshed.Add($"心金 / 魂银：{heartGoldExternalState}");
            }
            if (otherSave is not null)
            {
                await RefreshRegisteredSaveAsync(AnySaveRequest);
                refreshed.Add($"{otherSave.Game}：{otherExternalState}");
            }
            selectedSourceSlots.Clear();
            UpdateArchiveTabs();
            UpdateSourceArchiveHeader();
            RenderSourceBoard();
            UpdateButtons();
            status.Text = refreshed.Count == 0 ? "尚未导入可刷新的存档。" : string.Join("\n", refreshed);
        }
        catch (Exception ex)
        {
            status.Text = $"刷新失败：{ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task RefreshRegisteredSaveAsync(int requestCode)
    {
        var current = requestCode switch
        {
            EmeraldRequest => emeraldSave,
            HeartGoldRequest => heartGoldSave,
            _ => otherSave,
        };
        if (current is null || string.IsNullOrWhiteSpace(current.SourceUri))
        {
            if (requestCode == EmeraldRequest)
                emeraldExternalState = "仅本地快照";
            else
                heartGoldExternalState = "仅本地快照";
            return;
        }

        try
        {
            var uri = global::Android.Net.Uri.Parse(current.SourceUri) ?? throw new IOException("存档地址无效。");
            using var stream = ContentResolver?.OpenInputStream(uri) ?? throw new IOException("无法读取已登记存档。");
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bytes = memory.ToArray();
            var inspection = SaveInspector.Inspect(bytes, current.DisplayName);
            if (!string.Equals(inspection.Game, current.Game, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("当前文件已不是原登记的游戏存档。");

            var updated = Convert.ToHexString(SHA256.HashData(bytes)) == current.Hash
                ? current
                : SaveRegistry.UpdateSnapshot(current, bytes);
            if (requestCode == EmeraldRequest)
            {
                emeraldSave = updated;
                emeraldBytes = bytes;
                emeraldSlots = BoxReader.Read(bytes, updated.DisplayName).ToList();
                emeraldPages = BoxReader.ReadPages(bytes, updated.DisplayName).ToList();
                emeraldExternalState = "已同步";
            }
            else if (requestCode == HeartGoldRequest)
            {
                heartGoldSave = updated;
                heartGoldBytes = bytes;
                heartGoldSlots = BoxReader.Read(bytes, updated.DisplayName).ToList();
                heartGoldPages = BoxReader.ReadPages(bytes, updated.DisplayName).ToList();
                heartGoldExternalState = "已同步";
                if (selectedTransferTarget?.Id == updated.Id)
                    selectedTransferTarget = updated;
            }
            else
            {
                otherSave = updated;
                otherBytes = bytes;
                otherSlots = BoxReader.Read(bytes, updated.DisplayName).ToList();
                otherPages = BoxReader.ReadPages(bytes, updated.DisplayName).ToList();
                otherExternalState = "已同步";
            }
        }
        catch (Exception ex)
        {
            if (requestCode == EmeraldRequest)
                emeraldExternalState = ex.Message.Contains("授权", StringComparison.Ordinal) ? "需要重新授权" : "刷新失败";
            else if (requestCode == HeartGoldRequest)
                heartGoldExternalState = ex.Message.Contains("授权", StringComparison.Ordinal) ? "需要重新授权" : "刷新失败";
            else
                otherExternalState = ex.Message.Contains("授权", StringComparison.Ordinal) ? "需要重新授权" : "刷新失败";
        }
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
                var selected = selectedWarehouseIds.Contains(record.Id) || storedPokemon?.Id == record.Id;
                var image = new ImageButton(this)
                {
                    ContentDescription = storedPokemon?.Id == record.Id
                        ? $"已选传送对象：{ChineseSpeciesName(record.Species)}"
                        : $"查看 {ChineseSpeciesName(record.Species)} 详情",
                };
                image.SetScaleType(ImageView.ScaleType.CenterInside);
                image.SetPadding(5, 5, 5, 5);
                image.Background = CreateSlotBackground(true, selected, false);
                var icon = Resources.GetIdentifier($"a_{record.Species}", "drawable", PackageName);
                if (icon != 0)
                    image.SetImageResource(icon);
                image.Click += (_, _) =>
                {
                    SelectWarehouseForTransfer(record);
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
            selected ? "取消选择" : "选择传送对象",
            () =>
            {
                SelectWarehouseForTransfer(record);
                if (!selectedWarehouseIds.Add(record.Id))
                    selectedWarehouseIds.Remove(record.Id);
                RenderWarehouseGrid();
                UpdateButtons();
            },
            record);
    }

    void SelectWarehouseForTransfer(StoredPokemon record)
    {
        storedPokemon = record;
        selectedTransferTarget = null;
        selectedTransferSlot = -1;
        RenderWarehouseGrid();
        UpdateButtons();
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

        var trainingLines = new List<string> { $"性格    {nature}", $"特性    {ability}" };
        var training = Card("训练信息", trainingLines.ToArray());
        var effectLine = Text($"效果    {AbilityEffectText(ability) ?? "当前未收录该特性的详细效果。"}", 10, "#8DE4D1");
        effectLine.SetPadding(0, Dp(2), 0, 0);
        training.AddView(effectLine);
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
            var name = move == 0 ? "—" : StringAt(strings.Move, move, $"招式 #{move}");
            var typeId = move == 0 ? (byte)0 : MoveInfo.GetType(move, pokemon.Context);
            var typeName = move == 0 ? string.Empty : StringAt(strings.Types, typeId, "未知属性");
            row.AddView(move == 0 ? new Space(this) : TypeBadge(typeId, typeName), new LinearLayout.LayoutParams(Dp(30), Dp(24)) { RightMargin = Dp(4) });
            var moveData = move == 0 ? null : GetMoveEffectData(move);
            var category = move == 0 ? "—" : MoveCategoryText(move, typeId, pokemon.Context, moveData);
            var categoryKey = category == "物理" ? "physical" : category == "特殊" ? "special" : "status";
            var categoryIcon = new ImageView(this);
            var categoryIconId = Resources.GetIdentifier($"move_category_{categoryKey}", "drawable", PackageName);
            if (categoryIconId != 0)
                categoryIcon.SetImageResource(categoryIconId);
            categoryIcon.SetScaleType(ImageView.ScaleType.CenterInside);
            categoryIcon.ContentDescription = category == "物理" ? "物理" : category == "特殊" ? "特殊" : "变化";
            row.AddView(categoryIcon, new LinearLayout.LayoutParams(Dp(30), Dp(24)) { RightMargin = Dp(4) });
            row.AddView(Text(name, 11, move == 0 ? "#628078" : "#E9F4EF"), new LinearLayout.LayoutParams(0, -2, 1));
            var power = move == 0 || moveData?.Power is not > 0 ? "—" : moveData.Power.Value.ToString();
            row.AddView(Text(power, 10, "#DCEBE6"), new LinearLayout.LayoutParams(Dp(32), -2) { RightMargin = Dp(4) });
            var currentPp = move == 0 ? 0 : MoveCurrentPp(pokemon, index);
            var maxPp = move == 0 ? 0 : moveData?.PP is > 0 ? moveData.PP.Value : MoveInfo.GetPP(pokemon.Context, (ushort)move);
            row.AddView(Text(move == 0 ? "—" : $"{currentPp}/{maxPp}", 10, "#91AAA1"), new LinearLayout.LayoutParams(Dp(48), -2));
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
        var repairable = warehouseRecord is not null && warehouseRecord.LegalityStatus == "invalid";
        actions.AddView(close, new LinearLayout.LayoutParams(0, Dp(44), repairable ? 0.27f : primaryLabel is null ? 1 : 0.42f));
        if (repairable && warehouseRecord is not null)
        {
            var repair = new Button(this) { Text = "自动修复" };
            repair.SetAllCaps(false);
            repair.SetTextColor(Color.ParseColor("#D6FF63"));
            repair.Background = Panel("#132A25", "#D6FF63", 8);
            repair.Click += async (_, _) =>
            {
                repair.Enabled = false;
                repair.Text = "修复中…";
                try
                {
                    var outcome = await Task.Run(() => LocalRepository.RepairWithStrategy(pokemon));
                    if (!outcome.Valid)
                    {
                        ShowRepairOutcome(outcome);
                        return;
                    }
                    var updated = LocalRepository.SaveWorking(warehouseRecord, outcome.Pokemon);
                    dialog.Dismiss();
                    storedPokemon = updated;
                    RefreshWarehouse(updated.Id);
                    ShowWarehouseDetail(updated);
                    ShowRepairOutcome(outcome);
                }
                catch (Exception ex)
                {
                    ShowRepairOutcome(new RepairOutcome(pokemon, false, "未完成", [], $"修复过程无法完成：{ex.Message}"));
                }
                finally
                {
                    repair.Enabled = true;
                    repair.Text = "自动修复";
                }
            };
            actions.AddView(repair, new LinearLayout.LayoutParams(0, Dp(44), 0.31f) { LeftMargin = Dp(6) });
        }
        if (primaryLabel is not null && primaryAction is not null)
        {
            var primary = new Button(this) { Text = primaryLabel };
            primary.SetAllCaps(false);
            primary.SetTextColor(Color.ParseColor("#142019"));
            primary.Background = Panel("#D6FF63", "#D6FF63", 8);
            primary.Click += (_, _) => { dialog.Dismiss(); primaryAction(); };
            actions.AddView(primary, new LinearLayout.LayoutParams(0, Dp(44), repairable ? 0.42f : 0.58f) { LeftMargin = Dp(8) });
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

    void ShowRepairOutcome(RepairOutcome outcome)
    {
        var message = outcome.Valid
            ? $"已生成并保存合法工作副本。\n模板：{outcome.Template}\n处理：{string.Join("、", outcome.Changes)}"
            : $"未生成可写入的副本。\n模板：{outcome.Template}\n已尝试：{(outcome.Changes.Count == 0 ? "未找到可用模板" : string.Join("、", outcome.Changes))}\n应对：{outcome.FailureReason}";
        var dialog = new Dialog(this);
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(18), Dp(16), Dp(18), Dp(12));
        var panelBackground = new GradientDrawable();
        panelBackground.SetColor(Color.ParseColor("#0F201C"));
        panelBackground.SetCornerRadius(Dp(16));
        panelBackground.SetStroke(Dp(1), Color.ParseColor(outcome.Valid ? "#D6FF63" : "#B86A55"));
        panel.Background = panelBackground;
        var title = new TextView(this) { Text = outcome.Valid ? "自动修复完成" : "自动修复未完成", TextSize = 19 };
        title.SetTextColor(Color.ParseColor(outcome.Valid ? "#D6FF63" : "#FF9E78"));
        title.SetTypeface(global::Android.Graphics.Typeface.Default, global::Android.Graphics.TypefaceStyle.Bold);
        title.SetIncludeFontPadding(false);
        panel.AddView(title, new LinearLayout.LayoutParams(-1, Dp(34)));
        var body = new TextView(this) { Text = message, TextSize = 13 };
        body.SetTextColor(Color.ParseColor("#E9F4EF"));
        body.SetLineSpacing(Dp(2), 1f);
        panel.AddView(body, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(8) });
        var close = new Button(this) { Text = "知道了" };
        close.SetAllCaps(false);
        close.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 14);
        close.SetTextColor(Color.ParseColor("#0B1414"));
        var closeBackground = new GradientDrawable();
        closeBackground.SetColor(Color.ParseColor("#D6FF63"));
        closeBackground.SetCornerRadius(Dp(8));
        close.Background = closeBackground;
        close.Click += (_, _) => dialog.Dismiss();
        panel.AddView(close, new LinearLayout.LayoutParams(-1, Dp(44)) { TopMargin = Dp(14) });
        dialog.SetContentView(panel);
        dialog.Show();
        if (dialog.Window is { } window)
        {
            window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            window.SetDimAmount(0.72f);
            window.AddFlags(WindowManagerFlags.DimBehind);
            window.SetLayout((int)(Resources.DisplayMetrics.WidthPixels * 0.86f), WindowManagerLayoutParams.WrapContent);
        }
    }

    static string StringAt(IReadOnlyList<string> values, int index, string fallback) => (uint)index < values.Count && !string.IsNullOrWhiteSpace(values[index]) ? values[index] : fallback;
    string? AbilityEffectText(string ability)
    {
        if (abilityEffects is null)
        {
            try
            {
                using var stream = Resources.OpenRawResource(Resource.Raw.ability_effects_zh);
                using var reader = new StreamReader(stream);
                var entries = JsonSerializer.Deserialize<List<AbilityEffectEntry>>(reader.ReadToEnd()) ?? [];
                abilityEffects = entries
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Name) && !string.IsNullOrWhiteSpace(entry.Description))
                    .GroupBy(entry => entry.Name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First().Description, StringComparer.Ordinal);
            }
            catch
            {
                abilityEffects = new Dictionary<string, string>();
            }
        }
        return abilityEffects.TryGetValue(ability, out var effect) ? effect : null;
    }

    sealed record AbilityEffectEntry(
        [property: JsonPropertyName("name_zh")] string Name,
        [property: JsonPropertyName("description")] string Description);

    MoveEffectData? GetMoveEffectData(int move)
    {
        if (moveEffects is null)
        {
            try
            {
                using var stream = Resources.OpenRawResource(Resource.Raw.move_data_zh);
                using var reader = new StreamReader(stream);
                var entries = JsonSerializer.Deserialize<List<MoveEffectData>>(reader.ReadToEnd()) ?? [];
                moveEffects = entries.GroupBy(entry => entry.Id).ToDictionary(group => group.Key, group => group.First());
            }
            catch
            {
                moveEffects = new Dictionary<int, MoveEffectData>();
            }
        }
        return moveEffects.TryGetValue(move, out var data) ? data : null;
    }

    static string MoveCategoryText(int move, byte typeId, EntityContext context, MoveEffectData? data)
    {
        if (context is EntityContext.Gen1 or EntityContext.Gen2 or EntityContext.Gen3)
            return typeId is 0 or 1 or 2 or 3 or 4 or 5 or 7 or 8 or 9 ? "物理" : "特殊";
        return data?.Category switch
        {
            "物理" => "物理",
            "特殊" => "特殊",
            _ => "变化",
        };
    }

    static int MoveCurrentPp(PKM pokemon, int index) => index switch
    {
        0 => pokemon.Move1_PP,
        1 => pokemon.Move2_PP,
        2 => pokemon.Move3_PP,
        3 => pokemon.Move4_PP,
        _ => 0,
    };

    sealed record MoveEffectData(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name_zh")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("power")] int? Power,
        [property: JsonPropertyName("pp")] int? PP);

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
        if (refreshSavesButton is not null)
            refreshSavesButton.Enabled = !value;
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
        mainScroll?.Post(() => mainScroll.SmoothScrollTo(0, Math.Max(0, transferVisual.Top - Dp(18))));
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

sealed class LevelRangeView : View
{
    const int Minimum = 1;
    const int Maximum = 100;
    readonly Paint trackPaint = new() { AntiAlias = true };
    readonly Paint selectedPaint = new() { AntiAlias = true };
    readonly Paint handlePaint = new() { AntiAlias = true };
    bool trackingLower;
    float density;
    public int LowerValue { get; private set; }
    public int UpperValue { get; private set; }
    public event EventHandler? RangeChanged;

    public LevelRangeView(Context context, int lower, int upper) : base(context)
    {
        density = Resources?.DisplayMetrics?.Density ?? 1;
        LowerValue = Math.Clamp(lower, Minimum, Maximum);
        UpperValue = Math.Clamp(upper, LowerValue, Maximum);
        trackPaint.Color = Color.ParseColor("#315249");
        selectedPaint.Color = Color.ParseColor("#D6FF63");
        handlePaint.Color = Color.ParseColor("#E9F4EF");
        SetWillNotDraw(false);
    }

    float Px(float value) => value * density;
    float Position(int value) => Px(14) + (Width - Px(28)) * (value - Minimum) / (Maximum - Minimum);

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        var y = Height / 2f;
        var left = Px(14);
        var right = Width - Px(14);
        var track = Px(4);
        canvas.DrawRoundRect(new RectF(left, y - track, right, y + track), track, track, trackPaint);
        var selectedLeft = Position(LowerValue);
        var selectedRight = Position(UpperValue);
        canvas.DrawRoundRect(new RectF(selectedLeft, y - Px(5), selectedRight, y + Px(5)), Px(5), Px(5), selectedPaint);
        canvas.DrawCircle(selectedLeft, y, Px(9), handlePaint);
        canvas.DrawCircle(selectedRight, y, Px(9), handlePaint);
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null)
            return false;
        var x = e.GetX();
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                trackingLower = Math.Abs(x - Position(LowerValue)) <= Math.Abs(x - Position(UpperValue));
                Parent?.RequestDisallowInterceptTouchEvent(true);
                UpdateValue(x);
                return true;
            case MotionEventActions.Move:
                UpdateValue(x);
                return true;
            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                Parent?.RequestDisallowInterceptTouchEvent(false);
                UpdateValue(x);
                return true;
            default:
                return true;
        }
    }

    void UpdateValue(float x)
    {
        var ratio = Math.Clamp((x - Px(14)) / Math.Max(1, Width - Px(28)), 0, 1);
        var value = Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
        if (trackingLower)
            LowerValue = Math.Min(value, UpperValue);
        else
            UpperValue = Math.Max(value, LowerValue);
        Invalidate();
        RangeChanged?.Invoke(this, EventArgs.Empty);
    }
}

sealed class CompactSpinnerAdapter : ArrayAdapter<string>
{
    public CompactSpinnerAdapter(Context context, string[] values)
        : base(context, global::Android.Resource.Layout.SimpleSpinnerItem, values)
    {
        SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
    }

    public override View GetView(int position, View? convertView, ViewGroup? parent) => Style(base.GetView(position, convertView, parent));

    public override View GetDropDownView(int position, View? convertView, ViewGroup? parent) => Style(base.GetDropDownView(position, convertView, parent));

    static View Style(View view)
    {
        if (view is TextView text)
        {
            text.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 12);
            text.SetTextColor(Color.ParseColor("#E9F4EF"));
            text.SetIncludeFontPadding(false);
        }
        return view;
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
