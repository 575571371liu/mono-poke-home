# MONO / HOME 存档远程同步技术设计

## 目标

为每一份已登记的游戏存档增加可选的远程版本同步能力。首个真实支持对象是绿宝石、心金/魂银存档文件，但同步层只处理“文件快照 + 版本”，不依赖宝可梦解析，因此以后可以复用到其他游戏。

远程同步是中央仓库之外的独立能力：中央仓库继续保存在设备本地，只有当前游戏存档文件进入远程仓库。

## 已确认的交互规则

1. 存档页只显示一行紧凑的“存档同步”入口；上传、拉取和版本列表放在弹窗中。
2. 弹窗显示当前本地版本、远端最新版本、各自修改时间、文件 hash 和同步状态。
3. “远端最新版本”只是远端当前指针，远端可以保留很多历史版本。
4. hash 相同表示两端对齐；重复点击上传不得创建新版本。
5. 推送成功后，本地和远端都指向同一个新 commit，版本号/hash 一致。
6. 拉取默认使用远端最新版本；历史版本只在拉取流程中选择，用于回滚。
7. 从历史版本恢复后继续游玩，再次上传必须保留原有版本，并形成新的存档线；不能覆盖历史版本或把历史列表错误地显示成单条线性版本。
8. 每个用户绑定自己的 GitHub 仓库。应用不能把开发者的仓库、Token 或账号作为默认共享仓库。
9. 首次绑定提供“创建专用私有仓库”和“绑定已有仓库”两个入口；后续设备登录同一 GitHub 账号并选择该仓库即可继续使用。
10. 上传、拉取、回滚都必须由用户明确确认，不自动覆盖本地存档。

## 版本状态模型

版本号不是新旧判断依据；Git commit SHA 和存档内容 SHA256 是两个不同字段：

- `ContentHash`：存档文件内容 SHA256，用于判断两端是否实际相同。
- `CommitSha`：远端 Git commit 标识，用于定位历史版本。
- `BlobSha`：GitHub Contents API 返回的文件 blob SHA，只用于更新请求的并发校验，不能当作 `ContentHash`。
- `ModifiedAt`：本地文件或远端 commit 的显示时间，用于解释“谁看起来更新”。
- `BaseCommitSha`：本地最近一次同步所基于的远端 commit，用于识别分叉。

界面中的 `v10`、`v15` 只是当前存档线内的显示序号，不是 GitHub 的真实版本号；真实定位必须使用 `CommitSha`。历史列表在未下载内容时可以没有 `ContentHash`，选中版本下载后再计算 SHA256。

比较规则：

| 条件 | UI 状态 | 行为 |
|---|---|---|
| 本地 hash = 远端最新 hash | 已同步 | 上传为 no-op |
| 本地改变，远端仍是 BaseCommitSha | 本地较新 | 允许上传 |
| 远端改变，本地仍是上次同步内容 | 远端较新 | 提示先拉取或选择远端版本 |
| 本地和远端都偏离 BaseCommitSha | 两端都有修改 | 禁止静默覆盖，要求用户选择 |
| 没有 BaseCommitSha | 无法判断 | 只显示时间/hash，要求首次明确选择 |

修改时间只用于展示和辅助判断，不能替代 hash 与 BaseCommitSha。Android 文件恢复后不要把“刚刚写入文件”的系统 mtime 当成游戏存档内容的游玩时间。

如果 `RemoteSaveVersion.ContentHash` 尚未获取，不能用 `BlobSha` 推断对齐；比较服务必须先下载当前远端内容计算 SHA256，或者把状态保持为 `Unknown`。只有确认内容 hash 相同后，上传才可以返回 no-op。

## 远端仓库布局

每个用户一个私有仓库，默认名称 `mono-home-saves`，允许用户改名。仓库初始化后至少包含：

```text
.mono-home/manifest.json
saves/emerald/emerald.srm
saves/heartgold/heartgold.sav
```

`manifest.json` 记录远端 schema、应用标识、存档键、文件路径和格式；它不记录会随设备变化的当前存档线：

```json
{
  "schema": 1,
  "app": "mono-home",
  "saves": {
    "emerald": {
      "displayName": "绿宝石",
      "path": "saves/emerald/emerald.srm",
      "format": "srm"
    }
  }
}
```

首个简单版本可以让所有存档先使用默认分支；实现历史分叉后，存档线使用 `save/<saveKey>/<lineageId>` 分支，旧分支永远不强制改写。每台设备在本地绑定中记录当前 `lineageId`；远端最新版本指当前设备所选存档线的 head，历史弹窗列出当前线和其他存档线的 commit。不同设备可以先处于不同存档线，用户明确选择后再切换。

## 用户绑定与初始化

### 首次使用

1. 用户打开“设置 → 存档仓库”。
2. 选择“连接 GitHub 并创建专用仓库”或“绑定已有仓库”。
3. 通过 GitHub App 的浏览器授权码 + PKCE 流程授权，不让用户把 Token 粘贴到普通输入框；只有无法使用回调的受限环境才启用 device flow。
4. V1 先打开 GitHub 的新建仓库页面，由用户创建私有仓库；应用随后绑定并初始化它。应用内自动调用创建仓库 API 作为后续增强，必须先验证权限范围后才能开放。
5. 写入 `.mono-home/manifest.json`，创建首个空初始化 commit。
6. 用户确认仓库名称、账号和私有状态后保存绑定。

### 后续设备

用户在另一台设备登录同一 GitHub 账号，应用读取可访问仓库，优先匹配 `.mono-home/manifest.json`；用户确认后保存本机绑定。应用不复制开发者的仓库配置，也不要求用户手工配置 Git remote URL。

## 技术方案评估

| 方案 | 优点 | 问题 | 结论 |
|---|---|---|---|
| GitHub REST API + commit | Android/Windows 都能使用；不用在设备上维护完整 Git 工作树；可直接读取指定 commit；依赖少 | 需要处理 GitHub App 授权、并发和分支 ref；Contents API 对大文件不适合 | **V1 推荐**，存档文件小，使用 `HttpClient` + `System.Text.Json` |
| libgit2/LibGit2Sharp | Git 语义完整，分支/祖先关系自然 | Android 原生绑定、二进制体积、凭据和文件工作树复杂 | V2 以后再评估，不作为首版依赖 |
| GitHub Desktop/系统 git | Windows 方便 | Android 不适用，无法作为 app 内能力 | 不采用 |
| 百度网盘 API | 国内网络与用户已有账号方便 | 版本图、冲突和开发者应用审核复杂，不能直接复用 Git 语义 | 作为未来 `IRemoteSaveProvider` 实现，不进入首版 |

V1 使用 GitHub App user access token 和 GitHub API 的 Repositories/Contents/Commits 接口；V3 分叉时再使用 Git Database 的 Refs/Trees/Blobs/Commits 接口。不要把 GitHub API 调用散落在 `MainActivity`。

认证约束：原生 Android/Windows 是 public client，不能把 GitHub App private key 或 client secret 打进 APK。主流程使用授权码 + PKCE、随机 `state` 和平台回调；device flow 只作为没有可靠回调能力时的后备，并遵守 GitHub 返回的轮询间隔和过期时间。

## 模块边界

### 1. 绑定与认证

负责 GitHub App 授权、当前 GitHub 用户、仓库选择、权限验证和 Token 安全存储。V1 的“创建专用仓库”是打开 GitHub 新建私有仓库页面，不在客户端直接请求高权限的仓库创建接口。

不负责存档比较和文件上传。

### 2. 远端提供者

`IRemoteSaveProvider` 隔离 GitHub。首版实现 `GitHubRemoteSaveProvider`，未来百度网盘只需实现同一接口。

提供：绑定并读取 manifest、查询远端 head、列出版本、下载指定版本、提交新版本、创建分支。仓库创建是单独的 `IRepositoryProvisioner` 能力；V1 不由客户端调用，只打开 GitHub 新建私有仓库页面。

### 3. 存档同步服务

把 `RegisteredSave`、本地快照、远端 head 和用户选择组合成同步状态；负责 hash 比较、no-op、冲突判断、上传前后原子性和回滚恢复点。

### 4. 本地绑定与安全存储

普通配置保存仓库 owner/name、分支、save key、最近同步 commit；Token 只能进入平台安全存储。Android 使用 Keystore 保护；未来 Windows 使用 DPAPI/Credential Manager 等系统凭据存储。

### 5. Android UI

`MainActivity` 只负责打开绑定弹窗和当前存档同步弹窗，不能直接拼 GitHub URL、解析 JSON 或操作 Token。网络操作必须异步、可取消，并在失败时保留原存档。

## 建议接口

```csharp
public sealed record RepositoryBinding(
    string Provider,
    string Owner,
    string Repository,
    string DefaultBranch,
    DateTimeOffset BoundAt,
    int Schema);

public sealed record SaveRemoteBinding(
    string SaveKey,
    string LineageId,
    string? BaseCommitSha);

public sealed record RemoteSaveVersion(
    string SaveKey,
    string LineageId,
    string CommitSha,
    string? ContentHash,
    string? BlobSha,
    DateTimeOffset ModifiedAt,
    string Device,
    string Message,
    string? ParentCommitSha = null);

public sealed record SaveSyncState(
    string SaveKey,
    string LineageId,
    string LocalHash,
    string? BaseCommitSha,
    RemoteSaveVersion? RemoteLatest,
    SyncStatus Status);

public enum SyncStatus
{
    Unknown,
    Aligned,
    LocalNewer,
    RemoteNewer,
    Diverged,
}

public interface IRemoteSaveProvider
{
    Task<RepositoryBinding> BindRepositoryAsync(string owner, string repository, CancellationToken cancellationToken);
    Task<RemoteSaveVersion?> GetLatestAsync(string saveKey, string lineageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RemoteSaveVersion>> ListVersionsAsync(string saveKey, CancellationToken cancellationToken);
    Task<byte[]> DownloadAsync(RemoteSaveVersion version, CancellationToken cancellationToken);
    Task<RemoteSaveVersion> UploadAsync(string saveKey, string lineageId, byte[] content, string? expectedCommitSha, string message, CancellationToken cancellationToken);
    Task<string> CreateLineageAsync(string saveKey, string fromCommitSha, CancellationToken cancellationToken);
}

public interface IRepositoryProvisioner
{
    Task<RepositoryBinding> CreateRepositoryAsync(string name, CancellationToken cancellationToken);
}

public sealed record LocalSaveSnapshot(
    string SaveKey,
    byte[] Content,
    string ContentHash,
    DateTimeOffset ModifiedAt);
```

同步服务对 UI 暴露的最小边界：

```csharp
public sealed class SaveSyncService
{
    public SaveSyncState Compare(
        string saveKey,
        ReadOnlyMemory<byte> localContent,
        string? baseCommitSha,
        RemoteSaveVersion? remoteLatest);

    public Task<SyncOperationResult> UploadAsync(
        LocalSaveSnapshot localSnapshot,
        RepositoryBinding binding,
        SaveRemoteBinding saveBinding,
        CancellationToken cancellationToken);

    public Task<SyncOperationResult> PullAsync(
        RegisteredSave localSave,
        RemoteSaveVersion version,
        RepositoryBinding binding,
        SaveRemoteBinding saveBinding,
        CancellationToken cancellationToken);
}

public sealed record SyncOperationResult(
    bool Succeeded,
    bool NoOp,
    SaveSyncState State,
    string Message,
    string? RecoveryPointPath = null);
```

`SaveSyncService` 不直接依赖 Android `Context`、`Uri` 或 GitHub HTTP；恢复点由平台传入的文件存储适配器完成，远端操作由 `IRemoteSaveProvider` 完成。

## 安全与数据保护

- V1 默认引导用户创建私有仓库后再绑定；绑定已有仓库必须验证当前账号对仓库有读写权限。客户端自动创建仓库不属于首版承诺。
- 不把 OAuth Token、仓库私密 URL 或用户文件内容写入日志。
- 上传前在应用私有目录创建本地恢复点；拉取前同样保留当前本地文件。
- GitHub 请求必须带取消令牌、超时、有限重试和明确的 401/403/404/409/422/429 文案。
- 远端 commit 不等于本地存档合法性；下载后仍按已有 `SaveInspector` 重新识别并校验。
- 任何 hash 不匹配、写回后回读不一致或权限失效都停止流程，不删除旧文件。

## 非目标

- 首版不自动合并两个不同存档；二进制存档没有通用安全 merge。
- 首版不把中央宝可梦仓库上传到远端。
- 首版不支持匿名共享仓库、开发者公共 Token 或把 `mono-home-saves` 固定为所有用户的仓库。
- 首版不引入完整 Git 工作树或 Git LFS。

## 外部依据

- [GitHub App 用户授权与 PKCE](https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-user-access-token-for-a-github-app)
- [GitHub OAuth 授权与 device flow](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps)
- [Repository contents API](https://docs.github.com/en/rest/repos/contents)
- [Git references API](https://docs.github.com/en/rest/git/refs)
- [Commits API](https://docs.github.com/en/rest/commits/commits)
