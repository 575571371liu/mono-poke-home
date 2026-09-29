# MONO / HOME 存档同步交接文档

## 交接结论

认证方案已改为个人细粒度 PAT + 私有仓库。GitHub App/Client ID/Device Flow 不再是本分支的发布前置条件；权威设计见 [PAT 方案](../superpowers/specs/2026-09-29-save-sync-pat.md)。

当前会话完成了远程存档同步的交互原型、技术边界和分阶段实施计划，但没有实现真实 GitHub 同步。下一会话应先阅读以下两个文件，再按计划从 V0 开始：

- 设计：[2026-09-29-save-sync-design.md](../superpowers/specs/2026-09-29-save-sync-design.md)
- 计划：[2026-09-29-save-sync-implementation.md](../superpowers/plans/2026-09-29-save-sync-implementation.md)

## 当前仓库状态

- 工作目录：`C:\Users\liujinwen\code\ljw.cmpy.com\apk\mono-poke-home`
- 项目现状：.NET 10 native Android；`MonoHome.Core` 放存档/仓库/传送逻辑，`MonoHome.Android` 放原生 UI，`MonoHome.Verifier` 是现有命令行验证器。当前仓库没有 Windows UI 工程；先复用 Core，不能把 PC 端已交付写进验收结论。
- 当前设置实现仍是本地模式：`src/MonoHome.Android/MainActivity.cs:268` 的 `ShowSettingsDialog()` 明确显示“所有数据只保存在本机”。真实同步接入时必须替换它。
- 当前存档登记：`src/MonoHome.Core/Saves/SaveRegistry.cs`，每份存档有 `RegisteredSave`、本地 snapshot、manifest、hash 和 Android `SourceUri` 权限。
- 当前本地仓库：`src/MonoHome.Core/Repository/LocalRepository.cs`，只存中央宝可梦仓库原件/工作副本，不要把它误当作远程存档仓库。
- 当前 Android 入口绑定在 `src/MonoHome.Android/MainActivity.cs`；布局在 `src/MonoHome.Android/Resources/layout/activity_main.xml`。
- 当前没有独立单元测试项目；现有回归入口是 `dotnet run --project src/MonoHome.Verifier/MonoHome.Verifier.csproj -c Release`。

## 交互原型

文件：[mono-save-sync-prototype.html](../prototypes/mono-save-sync-prototype.html)

预览地址：`http://localhost:8765/docs/prototypes/mono-save-sync-prototype.html`

预览启动：

```powershell
python -m http.server 8765
```

原型当前表达的规则：

- 存档页只显示紧凑的“存档同步”入口。
- 点击入口打开同步弹窗；弹窗显示本地版本、远端最新版本、版本数量和修改时间。
- 点击“上传本地”时，同 hash 直接 no-op；内容变化才创建 commit。
- 推送后本地和远端最新版本号/hash 对齐。
- 点击“拉取远端”默认使用最新版本，历史版本在同一个流程里选择。
- 从旧版本拉取后继续上传，文案表达为基于旧版本的新分支，保留原历史。
- 设置弹窗表达“我的 GitHub 仓库 / 更换或初始化仓库 / 创建专用仓库 / 绑定已有仓库”。这些按钮目前只是演示，不会真正授权或创建仓库。

原型是 mock，不要把其中的 `v15`、设备名、hash 和仓库名当成真实数据模型。当前所有交互状态都在 HTML 内存中，刷新即丢失。

## 已做过的原型验证

- HTML `<script>` 可通过 Node `new Function` 语法检查。
- 存档同步入口可以收起远程同步内容并打开弹窗。
- 拉取流程可以列出远端多个版本并选择旧版本。
- 相同 hash 上传显示 no-op。
- 内容不同上传确认后，本地和远端都指向新版本。
- 设置流程可以进入“创建专用仓库/绑定已有仓库”的 mock 初始化步骤。

## 关键设计决定

### 不共用开发者仓库

`https://github.com/575571371liu/mono-home-saves` 是开发测试用仓库，不能作为所有用户的默认仓库，也不能把它的 Token 打进 APK。生产用户必须 OAuth 登录自己的 GitHub 账号，创建或绑定自己的私有仓库。

### V1 用 GitHub API，不引入完整 Git 工作树

首版使用 `HttpClient` + `System.Text.Json` 调 GitHub REST API，存档文件通过 Contents/Commits 形成 Git 历史；历史分叉阶段使用 Git Database Refs/Trees/Blobs/Commits。这样 Android 不需要 libgit2、clone 目录或系统 git。认证采用 GitHub App 授权码 + PKCE；不能把 App private key/client secret 打进 APK。device flow 只能作为无回调环境的后备。

V1 的“创建专用仓库”是打开 GitHub 新建私有仓库页面，用户创建完成后回到应用绑定并初始化 manifest；客户端直接调用创建仓库 API 需要额外权限验证，不能在未验证前写成默认实现。

### hash、commit 和时间不是同一个字段

- 文件 SHA256：判断两端内容是否相同。
- GitHub blob SHA：只用于 Contents API 更新时的文件并发校验，不能当作文件 SHA256。
- commit SHA：定位远端版本和历史。
- 修改时间：给用户解释新旧的辅助信息。
- BaseCommitSha：判断两端是否从同一版本分叉。

原型中的 `vN` 只是存档线内的显示序号；历史内容未下载时可以暂时没有文件 SHA256，不能用显示序号或 blob SHA 冒充内容 hash。
若最新远端记录没有内容 SHA256，比较前必须下载它计算；否则状态只能是未知，不能直接判定 no-op。

### manifest 与分支的边界

`.mono-home/manifest.json` 只保存静态的存档键、文件路径和格式，不保存 `activeLineage`。当前存档线是每台设备的本地绑定状态；分叉时先从历史 commit 创建新 ref，再在新 ref 上提交文件，成功后本机切换 lineage。这样不需要把 `main` 上的 manifest 和另一条存档分支强行放进同一个 commit，也不会产生半更新状态。

不能用 UI 版本号单独决定覆盖方向。

### 中央仓库不上传

用户之前确认远程同步只针对每个游戏存档文件。中央仓库中的宝可梦副本、编辑记录和传送数据继续走 `LocalRepository` 的本地路径。

## 下一会话第一步

1. 阅读设计和计划文件。
2. 运行 `git status --short`，保留当前用户已有修改，不做 reset/checkout。
3. 运行原有 verifier，确认基线通过。
4. 先实现 V0：`SyncModels`、`IRemoteSaveProvider`、`SaveSyncService` 和 fake provider。
5. 不要一开始改真实 GitHub、OAuth 或大段 `MainActivity`；先用纯内存测试锁定 no-op、对齐、冲突和分叉规则。
6. Android 比较/上传前先从授权 `SourceUri` 读取当前文件并刷新 `LocalSaveSnapshot`；登记时留下的 `source.snapshot` 不能直接代表用户刚刚游玩后的文件。

## 本次只读验证记录（2026-09-29）

- 对公开仓库 `github/gitignore` 进行只读请求：默认分支为 `main`。
- `GET /contents/README.md?ref=main` 能得到文件内容和 blob SHA。
- `GET /commits?path=README.md&sha=main&per_page=1` 能得到路径对应的 commit SHA。
- `GET /git/matching-refs/heads/` 能得到分支 refs。
- 未执行 POST/PUT/DELETE，没有改动任何 GitHub 仓库；`octocat/Hello-World` 的 README 路径返回 404，因此不作为 API fixture。

## 暂不做的事情

- 不实现百度网盘 provider。
- 不实现自动 merge。
- 不把开发者 GitHub 仓库配置成默认用户仓库。
- 不在 Android 普通 SharedPreferences 保存明文 Token。
- 不把“远端最新版本”误显示为“远端唯一版本”。
- 不把中央仓库实体上传到远端。
