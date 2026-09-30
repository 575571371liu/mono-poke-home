# 代码审查与修复报告

日期：2026-09-30
范围：`src/MonoHome.Core`、`src/MonoHome.Android`、`src/MonoHome.Verifier`、构建与安全配置

本次工作按“多子任务审查 → 修复严重问题 → 梳理结构 → 回归验证”推进。
所有改动都经过 `MonoHome.Verifier` 全量回归 + Android Release 构建验证。

## 一、审查方式

拆成 5 个互相独立的审查子任务，各自只读、只报告证据，不做修改：

| 子任务 | 范围 |
|---|---|
| UI / 生命周期 / SAF | `MainActivity.cs` |
| 核心仓库与存档 | `LocalRepository`、`BoxReader`、`SaveInspector`、`SaveRegistry` |
| 远程同步 | `SaveSyncService`、`SyncModels`、`GitHub*` |
| 传送引擎与验证器 | `EmeraldHgssTransfer`、`TargetPreparation`、`TransferJournal`、`Program.cs`、fake |
| Android 持久化与安全 | `AndroidTokenStore`、`AndroidRepositoryBindingStore`、`TargetSaveWriter`、Manifest、`EditCopyActivity` |

审查结论中“已验证无问题”的部分也一并记录，避免后续重复调查。

## 二、修复的问题

### 严重（会造成数据损坏、崩溃或安全风险）

1. **闪光状态在转换传送中被静默丢失**（`EmeraldHgssTransfer`）
   - 成因：先按目标训练家改写 `TID16/SID16`，而闪光由 `PID ^ TID ^ SID` 推导，改写即丢失。
   - 证据：新增回归断言后，修复前必然失败：
     `conversion keeps a shiny source shiny ...: expected true`。
   - 修复：目标训练家写入后调用 `RestoreShiny`，按源状态重新应用；结果仍受原有合法性闸门约束。
   - 该行为同时违反已发布 profile 的 `"shiny": "preserve-or-reject"` 约定。

2. **“取消闪光”实际永远无效，并静默改写训练家 SID**（`LocalRepository.ApplyEdit`）
   - 成因：`SetShinySID(Shiny.Never)` 内部取 `Rand.Next(8)`，而闪光判定为 `XOR < 8`，因此结果**必定仍是闪光**，只是把 `SID16` 随机异或了一次。
   - 修复：改用 `SetPIDGender`（PKHeX 自身的去闪光实现，内部 `while (IsShiny)` 重掷 PID），并加上 64 次上限避免极端个体死循环。
   - 顺带修正：修改性格/性别后按源状态恢复闪光，而不是仅在其中一个分支处理。

3. **测试基线不可移植，等于没有回归闸门**（`MonoHome.Verifier`）
   - 成因：`Program.cs` 硬编码 `C:\Users\liujinwen\Desktop\...`，并把所有中间产物写进系统 `%TEMP%`。
   - 修复：新增 `TestEnvironment`，按仓库相对路径查找 fixture，支持 `MONO_HOME_EMERALD` / `MONO_HOME_HEARTGOLD` / `MONO_HOME_SCRATCH` 覆盖；scratch 目录按“环境变量 → 系统临时目录 → 仓库内 `.verify-tmp`”探测可写性。
   - 新增 `tools/verify.ps1` 固化运行方式。

4. **批量传送依赖环境临时目录，且在部分环境下完全跑不通**（`EmeraldHgssTransfer.TransferStoredMany`）
   - 成因：中间产物写入 `Path.GetTempPath()`。
   - 修复：改为在输出文件同目录暂存，全程同一卷；顺带收敛清理逻辑到 `AtomicFile.TryDelete`。

5. **device flow 未请求 JSON，真实 GitHub 下必然解析失败**（`GitHubDeviceFlowClient`）
   - 成因：GitHub 这些端点默认返回 `application/x-www-form-urlencoded`，未带 `Accept: application/json` 时 `JsonDocument.Parse` 直接抛异常；而 fake 无条件返回 JSON，把这个集成缺陷完全掩盖了。
   - 修复：统一走 `PostFormAsync` 并显式带 `Accept: application/json`。
   - fake 同时改为**复刻真实契约**：未请求 JSON 时返回 form-encoded，使该缺陷无法再被掩盖。

6. **令牌与 device code 会经由 `ToString()` 泄漏；OAuth 响应体会进入异常消息**
   - 修复：两个 record 覆写 `ToString()` 做脱敏；失败响应体截断并对 `access_token`/`refresh_token`/`device_code` 打码。

7. **`allowBackup="true"` 让令牌密文与私有仓库标识离开设备**（`AndroidManifest.xml`）
   - 成因：Auto Backup 默认包含 `shared_prefs/`；而 Keystore 密钥不随备份走，恢复后密文永远解不开。
   - 修复：改为**按文件排除**——`Resources/xml/data_extraction_rules.xml`（API 31+）与
     `backup_rules.xml`（API 30）把 `remote-save-secret.xml` 与 `remote-save-binding.xml`
     同时排除出 **cloud-backup 和 device-transfer**，`allowBackup` 保持 `true`，使用户自己的
     存档与中央仓库仍可随设备迁移。
   - 注意（初版修复不准确，已修正）：最初只是把 `allowBackup` 设为 `false`。在 Android 12+
     上这**只关闭云备份，不阻止设备间直传**，因此“令牌不再离开设备”当时并不成立；而且它
     连用户自己的 `files/saves`、`files/warehouse` 备份也一并关掉了。现方案才真正覆盖两种通道。
   - 另在 `.gitignore` 增加 `*.jks`/`*.keystore`/`keystore.properties`/`local.properties`。

8. **令牌解不开时直接崩溃**（`AndroidTokenStore`）
   - 成因：`cipher.DoFinal` 的解密失败与 `Convert.FromBase64String` 的 `FormatException` 都没在调用点捕获，而入口是 `async void` 对话框回调，进程必崩。
   - 修复：解密失败视为“未连接”，清理无效凭据并把 Keystore 条目一并 `DeleteEntry`（原来只删了密文），返回 `null` 走既有的“输入 PAT”流程。

### 中等（正确性、健壮性、可恢复性）

9. **存档写出后校验不一致时丢失恢复点路径**（`TargetSaveWriter`）：`backupPath` 提至 `try` 之外，写入中断也会把恢复点交回调用方；并把“已写入但无法重新识别”与普通失败区分开。调用方在**成功和失败两条分支**都调用 `AdoptRecoveryPoint(write.BackupPath)`，因此失败时提示的“恢复点已保留”确有导出入口（初版只在成功分支调用，提示是空头支票，已修正）。
10. **`WritePokemon` 先落盘再校验**（`LocalRepository`）：校验提前到写入之前，避免用不可读的载荷覆盖旧工作副本。
11. **`ReadRecord` 只捕获 `JsonException`**（`LocalRepository`/`SaveRegistry`/`TransferJournal`）：一并捕获 `IOException`/`UnauthorizedAccessException`，否则单条坏记录会让整个列表查询失败。
12. **`Remove` 在残留临时文件时抛异常**：改为递归删除并容忍锁定残留，避免把已成功的传送报告成失败。删除前校验目标目录确实是该记录自己的 `root/{id}` 目录（`List` 会在任意深度枚举 `record.json`，否则写在仓库根目录的清单会导致整仓被删），三个文件删除也改走 `AtomicFile.TryDelete`。
13. **`BoxReader.ReadPages` 无条件读取 6 个队伍槽**：像 `Read` 一样先判断 `HasParty`。`Pokémon Box RS`/`Stadium`/`Bank` 导出的队伍偏移为负，原先会抛 `ArgumentOutOfRangeException`，并在导入失败后留下每次启动都报错的孤儿记录。
14. **app 的“保真传送”选项完全没接进传送调用**：`TargetPreparationService.Prepare` 增加 `TransferMode` 参数并纳入缓存键，`MainActivity` 的单个与批量路径都传入用户选择。
15. **显式目标仓位会静默覆盖占用者**：新增 `allowOverwrite` 开关；允许覆盖时记录 `Overwritten` 变更，使日志可审计。说明：app 自身始终允许覆盖（UI 另有确认弹窗），该开关目前只有验证器使用，因此它保护的是引擎层面的其他调用方，不是现有 UI 流程。
16. **`MarkExported` 重试会抛异常**：导出确认天然会重试，已导出记录改为幂等返回。
17. **重试策略漏掉 GitHub 的次限流**：`403 + Retry-After` 现在识别为可重试；`Retry-After` 兼容 HTTP-date 形式，并回退到 `x-ratelimit-reset`。
18. **备份导出按钮永远不可达**：`lastBackupPath` 只在两个无调用方的方法里赋值。新增 `AdoptRecoveryPoint`，在传送/批量传送写入后（含**失败**分支）接管恢复点并启用按钮，使 app 承诺的“恢复点已保留”真正可导出。

### 结构梳理

19. 抽出 `MonoHome.Core/Storage/AtomicFile`：原先 `WriteAtomic` 与 `ReadRecord` 在三个 store 里逐字重复，导致每处加固都要改三遍、且已经开始漂移。现在统一为一份实现，并集中补齐 **fsync** 与临时文件失败清理。
20. `BoxReader` 抽出 `Open` helper，消除三处重复的 `GetSaveFile(...) ?? throw`。
21. 传送引擎的 `try { File.Delete } catch` 重复收敛到 `AtomicFile.TryDelete`。
22. 批量传送新增“输出路径不得等于目标存档”的前置保护——该调用会先删除输出，等于会删掉用户存档。

## 三、验证证据

| 项目 | 结果 |
|---|---|
| `MonoHome.Verifier` 全量回归 | 全部 PASS |
| Android Release 构建 | 0 error |
| 编译警告与基线对比 | 基线 `CS8602=54, CS8604=4`；改动后完全一致，**未新增任何警告** |

关键行为回归：

- `PASS: shiny state survives a trainer-rewriting conversion.`
- `PASS: shiny editing is honest in both directions.`
- `PASS: all 83 supplied Emerald Pokémon convert legally to HeartGold.`
- `PASS: fidelity route legally accepted 81/83 supplied Pokémon`
- `PASS: batch transfer wrote 2 legal Pokémon.`
- 批量路径在改动前**从未被执行到**（写入系统临时目录被拒绝），现已被真实覆盖。
- device flow 的 `Accept` 头与脱敏 `ToString()` 均有断言。

刻意采用的验证手法：先写断言、再跑，确认**修复前失败**，然后才修（闪光两项均如此），避免写出“永远不会失败”的断言。

## 四、Activity 生命周期修复（第二轮）

> 版本说明：审查分支内部曾用 1.2.1 和 1.2.2 作为版本号，均未对外发布；首个包含这些修复的公开版本是 **1.2.3**。

优先级最高的遗留项已在本轮修复，因为它是唯一会在真实使用中直接崩进程的问题。

**问题**：`MainActivity` 只有 `OnCreate` 和 `async void OnActivityResult`，没有任何
`OnPause`/`OnDestroy`。6 处长操作各自持有 2 分钟超时的 `CancellationTokenSource`，
与 Activity 生命周期完全无关；操作完成后会调用 `AlertDialog.Builder(this).Show()`。
Activity 已销毁时弹窗会抛 `WindowManager.BadTokenException` 并杀死进程——
用户场景：点开“存档同步”后旋转屏幕或按返回键。

**修复**：

1. 新增 `lifetime`（与 Activity 绑定的 `CancellationTokenSource`）与 `destroyed` 标志，
   在 `OnDestroy` 中取消并置位；`BeginOperation(timeout)` 用
   `CreateLinkedTokenSource` 把操作同时绑到"自身超时"和"Activity 生命周期"。
   6 处 `new CancellationTokenSource(...)` 全部改为 `BeginOperation(...)`。
2. `CanTouchUi`（`!destroyed && !IsFinishing && !IsDestroyed`）与
   `ShowSafely(...)` 重载（`AlertDialog.Builder` / `AlertDialog` / `Dialog`）。
   全部 27 处弹窗显示统一走 `ShowSafely`，Activity 已销毁时改为 `Dismiss()`。
3. `OnPause`/`OnDestroy` 调用 `StopTransferAnimation()`，停止 `RepeatCount = Infinite`
   的传送动画；动画的 `Post` 回调也加了 `CanTouchUi` 守卫。

**验证**（模拟器 `emulator-5554`，API 30，安装 Release APK）：

| 检查项 | 结果 |
|---|---|
| 安装 + 启动 | 成功，无崩溃 |
| 首页渲染（两个已登记存档、83 只 / HeartGold） | 正常 |
| 打开设置弹窗 | 正常 |
| **持弹窗状态旋转屏幕**（销毁 + 重建 Activity） | 无崩溃；陈旧弹窗未再显示；进程存活 |
| 绿宝石来源页渲染（真实存档盒子 30/30） | 正常 |
| 宝可梦详情弹窗（性格/特性/效果/招式/能力图） | 正常 |
| 槽位选择切换（加入 ↔ 移出本次上传） | 正常 |
| **在异步操作进行中销毁 Activity** | 无崩溃 |
| `BadTokenException` 计数 | **0** |
| App 崩溃计数（`FATAL EXCEPTION` 属于本应用） | **0** |

警告数未增加，反而减少（`CS8602` 54 → 52，`CS8604` 仍为 4）。

**端到端测试的局限（必须说明）**：模拟器上的 SAF 文件选择器（DocumentsUI）不接受
`adb shell input` 注入的点击，因此本轮**未能在设备上跑通**
“导入 → 上传仓库 → 下载传送 → 导出备份”整条链路。
传送引擎本身由验证器在真实存档上覆盖（见第三节），但
`AdoptRecoveryPoint`（备份导出按钮）与 `transferMode` 接线
**只经过编译验证和逻辑审查，没有设备端证据**。
这是本轮唯一未闭合的验证缺口。

## 五、合入前的独立评审与整改

为避免"自己写自己批"，合入前另起了一个**只读、对抗性**的评审子任务，范围是
`git diff main...HEAD`（50 文件），结论为 **MERGE WITH FIXES，无阻塞项**。

它确认了闪光相关结论（并对照 PKHeX 源码复核了 `SetShiny`/`SetPIDGender`/`SetShinySID`
的循环语义）、27 处 `ShowSafely` 无遗漏、`AtomicFile` 去重彻底，以及仓库内
`bin/.../.verify-tmp/` 的产物可佐证"先失败后修复"的过程。同时提出以下问题，本轮全部整改：

| 评审项 | 结论 | 整改 |
|---|---|---|
| 恢复点在失败时不可达 | 确认（中） | 两个失败分支补 `AdoptRecoveryPoint(write.BackupPath)`；文档同步更正 |
| 令牌自愈可能反抛 | 确认（低-中） | 自愈清理改为 best-effort、不传调用方 token、不向外抛 |
| 闪光测试的失败分支断言不可满足，且 `PASS` 无条件打印 | 确认 | 失败分支改为断言"必须给出说明"；`PASS` 移入成功分支 |
| 保真缓存键断言被 revision 变更混淆 | 确认 | 两种模式改为同一 revision 下准备，并新增"共享 revision"断言；硬编码槽位改为 `DestinationSlot` |
| `allowBackup="false"` 在 Android 12+ 不阻止设备间直传 | 确认（安全表述夸大） | 改用 `dataExtractionRules` + `fullBackupContent` 双规则排除两个偏好文件，`allowBackup` 恢复 `true` |
| `Remove` 的递归删除可能删掉整个仓库根 | 确认（加固） | 删除前校验目录 == `root/{id}`；文件删除走 `TryDelete`；新增回归断言 |
| `PathsEqual` 在大小写敏感文件系统上误判 | 确认（加固） | 改为按平台选择 `Ordinal` / `OrdinalIgnoreCase` |
| 脱敏漏掉 JSON 形态的响应体 | 确认（加固） | `RedactField` 同时支持 `key=value` 与 `"key":"value"` |
| `allowOverwrite` 无生产调用方 | 确认（表述夸大） | 文档据实说明它保护的是引擎层其他调用方，非现有 UI |

评审同时记录了两项**已知遗留**（本次未改）：`AtomicFile` 只 fsync 文件未 fsync 目录
（重命名本身未做掉电持久化，且 fsync 在主线程）；4 处 `Toast` 未加销毁守卫（不会抛
`BadTokenException`，仅可能在 Activity 结束后短暂显示）。

## 六、已知遗留（本次未改，附理由）

这些均已核实为真实但非严重，改动收益低于回归风险，留待后续：

- **`GitHubRemoteSaveProvider` 把 404 一律当作“远端无存档”**：会跳过覆盖保护。正确修复需要区分“路径缺失”与“分支缺失”，属于接口语义变更。
- **历史版本的 `ContentHash` 为 null**，导致拉取历史版本时跳过哈希校验（`Program.cs` 甚至把这个事实固化成断言）。补全需要额外请求。
- **`GitHubApiClient`/`GitHubDeviceFlowClient` 会写调用方的 `HttpClient.BaseAddress`**：当前每个操作都新建 client，尚不可达；已把默认地址提为常量便于后续收紧。
- **`LocalRepository` 缺少按记录的锁与 revision CAS**：修好需要引入锁/版本语义，属于行为变更。
- **`ReadPages`/`Read` 仍是各解析一次存档**：真实性能问题，但改动会触及多处签名。
- **`TransferJournal.BackupPath` 无任何读取方**（无回滚/重放功能）。
- **`CreateTargetBackup`/`OverwriteRegisteredHeartGoldAsync` 仍是无调用方的死代码**：已确认无引用，未删除以免移除潜在的产品意图；备份导出改由 `AdoptRecoveryPoint` 驱动。
- **`MainActivity` 仍在主线程做重活**（`LearnableMoves` 对每个可学招式跑一次
  `LegalityAnalysis`、`OnCreate` 内阻塞读 SAF 与多次整档解析）：会造成卡顿甚至 ANR。
  属于性能改造，建议单独立项。

