# MONO / HOME 存档远程同步实施计划

> **2026-09-29 PAT 修订（优先于下文 OAuth 任务）：** 本人私有版不再配置 GitHub App Client ID。认证改为细粒度 PAT，范围仅限 `mono-home-saves`，仅有 Contents 读写。保留 Core 的仓库、commit、条件写入、历史、lineage 和恢复点实现；替换 Android 的 Device Flow 入口、刷新逻辑、资源文案与 verifier。完整约束见 `docs/superpowers/specs/2026-09-29-save-sync-pat.md`。

### PAT 改造小版本

1. **PAT 绑定**：先写 verifier 断言，验证 PAT 可作为无过期 bearer token 保存/读取；设置页用密码输入 PAT，校验用户与私有可写仓库后保存。
2. **移除 OAuth 依赖**：删除 UI 到 `GitHubDeviceFlowClient` 的调用、空 Client ID 前置条件和刷新路径；Core 同步 HTTP 与 lineage 不回滚。
3. **回归验收**：运行 verifier、Android Release 构建、模拟器 smoke；生成 PAT 后完成真实双设备测试，最后才合并。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不影响本地中央仓库的前提下，为每个已登记游戏存档增加 GitHub 私有仓库绑定、版本同步、历史恢复和分叉存档能力。

**Architecture:** 同步逻辑放进 `MonoHome.Core`，通过 `IRemoteSaveProvider` 隔离 GitHub；Android 只负责授权、弹窗和 Storage/Keystore 适配。V1 使用 GitHub App user access token + REST API 提交二进制存档，不在设备上创建完整 Git 工作树；历史分叉通过 Git refs/branches 保留旧存档线。

**Tech Stack:** .NET 10、C#、Android native Views/XML、`HttpClient`、`System.Text.Json`、PKHeX.Core、Android Keystore、MonoHome.Verifier。

**Spec:** `docs/superpowers/specs/2026-09-29-save-sync-design.md`

## Global Constraints

- 远程同步只管理当前游戏存档文件；中央宝可梦仓库仍为本地能力。
- 文件内容 hash 相同必须是 no-op；推送成功后本地和远端 head 的版本/hash 一致。
- 远端最新版本不是远端唯一版本；历史版本只能在拉取流程中选择。
- 不自动覆盖本地文件，不自动合并二进制存档；上传和拉取前都创建恢复点。
- 每个用户绑定自己的私有 GitHub 仓库，禁止把开发者仓库或 Token 写死进应用。
- 所有网络请求可取消；失败、权限失效、并发冲突和回读校验失败都保留旧数据。
- 继续复用 `SaveRegistry` 的本地快照和 `SaveInspector` 的格式识别，不复制一套存档登记模型。

## Review Focus

- 相同 hash 重复上传：测试必须证明不会产生 commit。
- 推送成功后的版本对齐：测试必须证明 local/remote 指向同一 commit/hash。
- 远端在本地之外发生变化：测试必须阻止静默覆盖并显示冲突。
- 拉取历史版本后继续游玩：测试必须保留旧版本并创建新的 lineage，而不是改写旧版本。
- Token、仓库绑定和本地恢复点：测试必须证明日志和普通配置中没有明文 Token，失败不会丢原档。

## 文件与模块地图

**新增 Core 文件：**

- `src/MonoHome.Core/Sync/SyncModels.cs`：绑定、版本、同步状态和结果 record。
- `src/MonoHome.Core/Sync/IRemoteSaveProvider.cs`：远端提供者接口。
- `src/MonoHome.Core/Sync/SaveSyncService.cs`：hash、BaseCommitSha、no-op、冲突和恢复点流程。
- `src/MonoHome.Core/Sync/GitHub/GitHubRemoteSaveProvider.cs`：GitHub REST API 实现。
- `src/MonoHome.Core/Sync/GitHub/GitHubApiClient.cs`：HTTP、JSON、错误码和分页封装。
- `src/MonoHome.Core/Sync/GitHub/GitHubAuthClient.cs`：授权码 + PKCE；device flow 仅作为受限环境后备。
- `src/MonoHome.Verifier/SyncFakeRemoteSaveProvider.cs`：不联网的版本图、分支和故障模拟器。

**新增 Android 文件：**

- `src/MonoHome.Android/Sync/AndroidTokenStore.cs`：Android Keystore 保护的 Token 存储。
- `src/MonoHome.Android/Sync/AndroidRepositoryBindingStore.cs`：SharedPreferences 只保存非敏感绑定元数据，包括每个 save key 的当前 lineage。

**修改文件：**

- `src/MonoHome.Android/MainActivity.cs`：替换现有本地设置弹窗，接入绑定和当前存档同步弹窗。
- `src/MonoHome.Android/Resources/layout/activity_main.xml`：为当前存档加入紧凑的“存档同步”入口。
- `src/MonoHome.Verifier/Program.cs`：增加 fake provider 和同步状态回归测试。
- `README.md`：新增绑定、权限、恢复点和 GitHub 仓库说明。

## 实现任务边界

### Task 1：冻结同步模型和比较状态

**Files:**

- Create: `src/MonoHome.Core/Sync/SyncModels.cs`
- Create: `src/MonoHome.Core/Sync/IRemoteSaveProvider.cs`
- Create: `src/MonoHome.Core/Sync/SaveSyncService.cs`
- Create: `src/MonoHome.Verifier/SyncFakeRemoteSaveProvider.cs`
- Modify: `src/MonoHome.Verifier/Program.cs`

**Interfaces:** 使用设计文档中的 `RepositoryBinding`、`RemoteSaveVersion`、`LocalSaveSnapshot`、`SaveRemoteBinding`、`SaveSyncState`、`SyncOperationResult`、`IRemoteSaveProvider` 和 `SaveSyncService` 签名。`IRemoteSaveProvider` 不包含仓库创建；仓库创建保留给后续 `IRepositoryProvisioner`，V1 不调用。

- [ ] 写固定 byte 数组测试：相同内容返回 `已同步` 和 `NoOp=true`。
- [ ] 写测试：只改变本地内容返回 `本地较新`；只改变远端 commit 返回 `远端较新`；双方都改变返回 `两端都有修改`。
- [ ] 写测试：相同 hash 的上传不调用 fake provider 的 commit 方法。
- [ ] 实现 SHA256、BaseCommitSha 比较和结果 record，不引入网络依赖。
- [ ] 使用 `SyncStatus` 枚举而不是 UI 字符串；`vN` 仅为显示序号，`CommitSha` 才是远端版本身份。
- [ ] 运行 verifier，确认 V0 状态机通过后再进入网络实现。

### Task 2：绑定仓库和安全存储边界

**Files:**

- Create: `src/MonoHome.Core/Sync/RepositoryBindingService.cs`
- Create: `src/MonoHome.Android/Sync/AndroidTokenStore.cs`
- Create: `src/MonoHome.Android/Sync/AndroidRepositoryBindingStore.cs`
- Modify: `src/MonoHome.Android/MainActivity.cs`
- Modify: `src/MonoHome.Android/Resources/layout/activity_main.xml`

**Interfaces:** `RepositoryBindingService` 只接收 `IGitHubAuthClient` 和 `IRemoteSaveProvider`；Android store 提供 `SaveTokenAsync`, `LoadTokenAsync`, `ClearTokenAsync` 与绑定元数据的保存/读取。它不调用 `IRepositoryProvisioner`，创建专用仓库只负责打开 GitHub 新建私有仓库页面并在返回后绑定。

- [ ] 用 mock OAuth 结果测试“打开 GitHub 新建私有仓库后绑定”和“绑定已有仓库”两条路径；V1 不在客户端直接调用创建仓库 API。
- [ ] 在 GitHub App 测试配置中验证 Android 回调、Windows loopback 回调、PKCE `state/code_verifier`；验证前不把认证流程标记为可发布。
- [ ] 首次绑定通过 `GET /repos/{owner}/{repo}` 验证仓库存在、私有状态和 Contents 写权限。
- [ ] 把 Token 放入 Android Keystore 保护的存储；普通 `SharedPreferences` 只写 owner、repo、branch、schema。
- [ ] 替换当前 `ShowSettingsDialog()` 的“所有数据只保存在本机”文案，增加首次绑定和已绑定状态。
- [ ] 初始化 `.mono-home/manifest.json`，重复初始化时先读取并校验 schema，不覆盖已有 manifest。
- [ ] 测试重启后恢复绑定；测试解除绑定只清本机凭据，不删除远端仓库。

### Task 3：GitHub API 最新版本同步

**Files:**

- Create: `src/MonoHome.Core/Sync/GitHub/GitHubApiClient.cs`
- Create: `src/MonoHome.Core/Sync/GitHub/GitHubRemoteSaveProvider.cs`
- Modify: `src/MonoHome.Core/Sync/SaveSyncService.cs`
- Modify: `src/MonoHome.Android/MainActivity.cs`

**Interfaces:** API client 接受 `HttpClient`、Token provider 和 `CancellationToken`; provider 实现 `IRemoteSaveProvider`，UI 只调用 `SaveSyncService`。

- [ ] 用 fake `HttpMessageHandler` 测试 `GET /repos/{owner}/{repo}`、`GET/PUT /contents/{path}` 和 commit 响应解析。
- [ ] 上传前读取远端最新内容和 commit；内容 hash 相同直接返回 no-op。
- [ ] 若远端版本尚未有 `ContentHash`，先下载并计算 SHA256；禁止用 Git blob SHA 冒充文件 hash。
- [ ] 上传使用 expected commit/file SHA，收到 409 时返回冲突而不是重试覆盖。
- [ ] 拉取前写应用私有恢复点；下载后用 `SaveInspector.Inspect` 验证文件格式，再原子替换本地快照。
- [ ] 在真实私有测试仓库上完成 Emerald 一次上传/拉取，再复用路径配置验证 HeartGold。
- [ ] Android 读取授权的源文件并刷新 `LocalSaveSnapshot` 后，才能进入比较/上传；不能直接把登记时的旧 `RegisteredSave.SnapshotPath` 当作当前游戏存档。

### Task 4：历史版本和分叉 lineage

**Files:**

- Modify: `src/MonoHome.Core/Sync/GitHub/GitHubRemoteSaveProvider.cs`
- Modify: `src/MonoHome.Core/Sync/SaveSyncService.cs`
- Modify: `src/MonoHome.Android/MainActivity.cs`
- Modify: `src/MonoHome.Verifier/Program.cs`

**Interfaces:** `CreateLineageAsync(saveKey, fromCommitSha, cancellationToken)` 创建新 ref；`RemoteSaveVersion.ParentCommitSha` 和 `LineageId` 必须保留。

- [ ] 测试从 v10 拉取后产生新内容，上传创建新 lineage；v10、v14、v15 仍可下载。
- [ ] 历史弹窗显示 commit SHA、可用时的内容 hash、时间、设备、lineage 和“分支自 v10”；未下载历史内容时不伪造 `ContentHash`。
- [ ] 旧版本拉取只改变本地和 BaseCommitSha，不删除远端 ref。
- [ ] 两台设备从同一历史版本分叉时，provider 返回两个 lineage，UI 显示选择而不是覆盖。

### Task 5：真实设备验收和发布硬化

**Files:**

- Modify: `src/MonoHome.Android/MainActivity.cs`
- Modify: `README.md`
- Modify: `docs/support-matrix.md`
- Modify: `src/MonoHome.Verifier/Program.cs`

- [ ] 测试 401、403、404、409、429、断网、取消、Token 过期和仓库删除；每种错误都保留本地原档。
- [ ] 测试推送后两端版本/hash 一致，未修改重复推送为 no-op。
- [ ] 在已有 Android 客户端之间完成最新拉取、历史拉取、分叉上传和恢复点恢复；Windows 客户端工程建立后，再用同一组用例补做 PC↔Android 验收。
- [ ] 更新 README 的账号授权、私有仓库、解绑、恢复和隐私说明。
- [ ] 构建 Release APK；只提交源代码和文档，不提交 Token、私有存档、测试仓库内容或 APK。

## 小版本路线

### V0：本地状态机与假远端（先锁定规则）

目标：不联网，先把 UI 状态和版本语义测通。

**交付：**

- `SyncModels`、`SaveSyncService` 和内存 `FakeRemoteSaveProvider`。
- 当前存档页的紧凑同步入口和弹窗；设置弹窗包含“创建专用仓库/绑定已有仓库”两条路径。
- 覆盖已同步、本地较新、远端较新、两端分叉、历史拉取和 no-op 上传。

**测试：**

- 用固定 byte 数组验证 `SHA256` 相同即 no-op。
- 上传后 local 与 remote 的 `CommitSha`、`ContentHash` 相同。
- 从 v10 拉取后修改并上传，旧 v14/v15 仍在 fake provider，新增版本记录 `ParentCommitSha=v10` 或新 lineage。
- 运行 `dotnet run --project src/MonoHome.Verifier/MonoHome.Verifier.csproj -c Release`。

### V1：GitHub 账号绑定与仓库初始化

目标：一个用户可以在自己的 GitHub 账号下绑定并初始化私有仓库；V1 的创建动作由 GitHub 网页完成，客户端自动创建仓库留作后续权限验证后的增强。

**交付：**

- GitHub App 授权码 + PKCE 浏览器授权；device flow 仅作为无可靠回调环境的后备；不接受普通文本框粘贴 Token 作为主路径。
- “创建专用仓库”打开 GitHub 新建私有仓库页面；已有仓库通过 `GET /repos/{owner}/{repo}` 验证私有状态和 Contents 写权限。
- 首次写入 `.mono-home/manifest.json`，保存 schema、存档键、路径和格式；当前 lineage 只保存在本机绑定。
- Android Keystore Token 存储；普通配置只保存 owner/repository/branch/boundAt。

**测试：**

- fake HTTP handler 验证绑定已有仓库和 manifest 初始化请求；“创建专用仓库”只验证浏览器引导后的绑定，不伪造客户端自动创建仓库。
- 401、403、404、409、429 分别得到可操作中文错误。
- 重新启动 app 后能恢复绑定元数据；日志中不能出现 Token。
- 模拟第二台设备绑定同一 manifest，不能误创建第二个仓库。

### V2：单存档最新版本上传/拉取

目标：先只做一条存档线，真实完成绿宝石上传和拉取。

**交付：**

- 使用 GitHub Contents API 读取/写入 `saves/emerald/emerald.srm`；读取内容 hash、文件 blob SHA 和 commit SHA 时分别保存，不能混用。
- 上传前获取远端 head；hash 相同直接 no-op。
- 远端 head 未变化时创建 commit，并将本地 `BaseCommitSha` 更新到新 commit。
- 拉取最新版本前保存应用私有恢复点，下载后用 `SaveInspector` 重新识别；失败恢复旧文件。
- 把同一服务复用于 `heartgold.sav`，路径由 `RegisteredSave` 的 save key 决定。

**测试：**

- 使用 fake server 测试首次上传、重复上传、远端较新和下载失败；Contents API 的更新请求必须串行，并携带 expected file SHA。
- 使用真实 GitHub 私有测试仓库手工验证：两台 Android 设备各上传一次，另一设备能拉取；Windows 客户端尚未存在时不宣称 PC 已验收。
- 现有 Emerald/HeartGold fixture 在拉取后仍被 `SaveInspector` 识别为原格式。

### V3：历史版本和分叉存档线

目标：支持“拉回 v10 → 继续游玩 → 上传新版本”，不改写 v14/v15。

**交付：**

- 拉取历史版本时保存 `BaseCommitSha` 和 `BaseLineageId`。
- 本地内容从历史版本发生变化后，使用 Git Database API 从历史 commit 创建新 branch/ref，再在新 lineage 上提交。
- manifest 保持静态，不写入 active lineage；创建新 lineage 时先从历史 commit 创建 ref，再在新 ref 上提交存档文件。
- 只有新分支 commit 成功后才更新本机的 `SaveRemoteBinding.LineageId`；旧 lineage 和旧 commit 只读保留。
- 历史列表显示 commit SHA、内容 hash、修改时间、设备、存档线和“分支自 v10”。

**测试：**

- v10 → 新内容上传得到新 lineage；v10、v14、v15 文件和 commit 仍可下载。
- 同一内容再次上传无新 commit；不同内容才创建新 commit。
- 选择历史版本只改变本地，不删除远端历史。
- 同时在两个设备从同一 v10 分叉，两个 lineage 都保留，UI 不把它们伪装成单一线性列表。

### V4：多设备冲突、安全与发布

目标：从“能同步”提升到“不会误覆盖”。

**交付：**

- 远端 head 与 `BaseCommitSha` 不一致时显示“两端都有修改”，禁止静默 push。
- 提供“使用本地创建新分支”“使用远端最新”“查看历史”三种明确选择。
- 加入请求超时、有限重试、取消、429 backoff、离线状态和恢复点清理策略。
- Windows 端只预留同一 Core 接口；Token 采用 Windows Credential Manager/DPAPI 实现，待 Windows UI 工程建立后再进入可执行验收。
- 更新 README、隐私说明、故障恢复说明和真实设备验收表。

**测试：**

- 双设备并发修改同一 save key，确认不丢任何一边。
- 断网、进程被杀、磁盘写入失败、Token 过期、仓库被删除、权限被撤销均保留本地原档。
- Release Android APK 在真实设备执行绑定、上传、拉取、历史分叉和恢复点恢复。

## 实施顺序与提交建议

每个小版本单独提交并可运行：

```text
feat(sync): add local save sync state model
feat(sync): add github repository binding
feat(sync): sync latest save snapshot
feat(sync): preserve historical save lineages
feat(sync): harden multi-device conflict handling
```

在 V0 通过前不接真实 GitHub；在 V2 通过真实设备验收前不开放默认同步；在 V4 通过前不自动处理冲突。
