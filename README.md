# MONO / HOME

MONO / HOME 是一个运行在 Android 上的本地宝可梦存档管理与传送工具。它使用 PKHeX.Core 读取和校验存档；中央仓库仍保存在设备本地，远程存档同步是需要用户主动开启的可选能力。

## 1.2.4 更新

- 修正宝可梦详情中招式属性与分类图标的排列顺序；空白招式格不再显示占位信息。
- 调整顶部状态栏与页面按钮的间距，避免时间和按钮重叠。

## 1.2.3 更新

- 绿宝石与心金 / 魂银存档各自管理同步；紧凑的「存档同步」按钮位于当前存档页面右侧，主仓库不再占用空间显示已连接存档卡片。
- 导入存档统一从右上角进入，移除重复入口与多余状态文案。
- 同步详情恢复本地与远端进度、修改时间、SHA-256、最新提交和历史版本数量的对比；可选择不同存档线上的历史版本。
- GitHub 历史提交与存档线按页读取，超过 100 个版本或分支时也能继续查找。
- 游戏存档盒子现在也能按等级、属性、蛋组、性别、闪光和孵化状态筛选。
- 包含下述审查修复。1.2.1 与 1.2.2 只存在于本地开发记录，公开版从 1.2.0 升级到 1.2.3。

远程同步由每位用户自行创建细粒度 PAT 并绑定自己的**私有**仓库：仅选该仓库，授予 `Contents: Read and write`，建议设置有效期。令牌存于设备 Android Keystore 保护的私有存储，APK 不包含任何共享令牌。令牌过期或撤销后可在设置中更新；双设备冲突的实机验收仍待完成。

## 1.2.3 包含的审查修复

以下为此前审查阶段完成、现随 1.2.3 一同公开的修复。

> 说明：审查分支内部曾使用 1.2.1 和 1.2.2 版本号，均未对外发布；首个包含这些修复的公开版本是 1.2.3。

- 修正“转换传送”会静默丢失闪光的问题：闪光由 `PID ^ TID ^ SID` 推导，写入目标训练家后按来源状态重新应用，不再无声丢失。
- 修正“取消闪光”实际无效的问题：原先只改写训练家 SID 且结果仍是闪光，现改为重掷 PID，并保留性格与性别。
- “保真传送”现在真正生效：此前界面的转换方式选择未传入传送流程，选与不选执行的都是转换。
- 导出目标存档前若写入校验不一致或写入中断，恢复点路径不再丢失，界面可直接导出“最近备份”。
- 修复无队伍存档（Pokémon Box RS / Stadium / Bank 导出）导入时崩溃的问题。
- 修复远程同步的设备授权流程未请求 JSON 响应、在真实 GitHub 上必然失败的问题。
- 令牌与设备码不再出现在 `ToString()` 或错误消息中。
- GitHub 凭据与仓库绑定不再随备份离开设备：通过备份规则把这两个偏好文件同时排除出云备份与设备间直传；用户自己的存档与中央仓库仍可正常备份迁移。令牌无法解密时自动清除并回到“未连接”，不再崩溃。
- 修复切换页面或旋转屏幕时，异步回调在已销毁的 Activity 上弹窗导致的崩溃。

完整审查与验证记录见 [docs/review/2026-09-30-code-review-fixes.md](docs/review/2026-09-30-code-review-fixes.md)。

## 1.2.0 功能

### 存档与仓库

- 通过 Android 文件选择器导入存档，并自动识别支持的游戏格式。
- 为每个已导入存档保留本地快照，可手动刷新以重新读取游戏中最新的仓库数据。
- 中央仓库保存独立副本；查看详情时显示合法性、形态、闪光、性别、蛋、携带道具、状态、丝带与特性效果。
- 中央仓库和游戏存档页均支持按等级、属性、蛋组、性别、闪光和孵化状态筛选；等级使用 1–100 双端滑块。
- 招式详情按“属性图标、物理/特殊/变化图标、名称、威力、PP”排列，并按来源世代计算属性与分类。

### 传送与恢复

- 在来源存档页面选择要上传的宝可梦，再进入中央仓库管理副本。
- 传送时先选择对象，再选择目标存档和具体仓位；目标仓库出现后才确认写入。
- 支持多选和批量传送，批量对象按连续仓位写入，并显示传送过程动画与结果。
- 写入目标存档前自动创建应用私有恢复点，写入后回读并校验；失败时保留来源和恢复点。
- 自动修复按背景模板、来源关系、招式与合法性逐步尝试，只有通过校验的结果才会写入。

### 设置与更新

- 设置页显示已登记存档、仓库数量和当前版本。
- 支持手动刷新已登记存档。
- 可连接用户自己的 GitHub 账号，绑定或初始化自己的私有存档仓库；不会使用开发者仓库或共享 Token。
- 存档同步入口显示本地/远端 hash、commit、同步状态、历史版本和存档线；上传、拉取、回滚都会明确确认。
- 支持检查 GitHub Releases，并引导下载最新 APK。

### 远程存档同步

- 只同步当前游戏存档文件，不上传中央宝可梦仓库、编辑记录或传送数据。
- GitHub 仓库必须是用户自己的私有仓库；绑定时会验证仓库存在、私有状态和 Contents 写权限，并初始化 `.mono-home/manifest.json`。
- Token 只保存于 Android Keystore 保护的应用私有存储；普通绑定配置只保存 owner、仓库、分支和 lineage。
- 设置中的“解除绑定”只清除本机凭据和绑定记录，不删除远端仓库或远端存档。
- 上传前和拉取前都会保留本地 recovery。远端历史版本只读保留；从旧版本继续游玩并上传时，需明确确认创建新的存档线。
- 每位用户通过自备细粒度 PAT 连接自己的私有仓库；PAT 仅保存在 Android Keystore，不随 APK 分发，建议设置有效期并按需撤销。

### 远程同步验收状态

1. 创建细粒度 PAT，仅选择 `mono-home-saves`，授予 `Contents: Read and write`；不得扩大仓库范围，也不得提交 PAT。
2. v1.2.0 已在独立私有测试仓库完成一台 Android 模拟器的首次绑定、绿宝石上传/拉取、历史读取和 recovery 验收；双设备互传、冲突与历史分叉验收留待后续版本。
3. 验收前确认普通配置和日志不含 PAT；Token 只应存在于 Android Keystore。
4. 验收完成后再构建 Release APK；不要提交测试存档、Token、私有仓库 URL 或签名 APK。

## 支持范围

导入和浏览使用 PKHeX.Core 的存档识别能力；正式传送路线目前是 Emerald → HeartGold / SoulSilver。其他世代可以导入、查看或管理时，不会被界面伪装成已支持的传送路线。

| 源存档 | 目标存档 | 状态 |
|---|---|---|
| Pokémon Emerald | Pokémon HeartGold | released |
| Pokémon Emerald | Pokémon SoulSilver | released |
| 其他 Gen I–IX 组合 | 任意 | unsupported transfer |

完整矩阵见 [docs/support-matrix.md](docs/support-matrix.md)。

## 构建

开发环境需要：

- .NET 10 SDK（Android workload）
- JDK 21
- Android SDK Platform 30 或更高、Platform-Tools、Emulator

构建 Release APK：

```powershell
dotnet build src/MonoHome.Android/MonoHome.Android.csproj -c Release --no-restore
```

APK 输出：

```text
src/MonoHome.Android/bin/Release/net10.0-android/io.github.monohome-Signed.apk
```

安装到已启动的模拟器：

```powershell
adb install -r src/MonoHome.Android/bin/Release/net10.0-android/io.github.monohome-Signed.apk
adb shell am force-stop io.github.monohome
adb shell monkey -p io.github.monohome 1
```

如果使用 Android CLI，可准备并启动项目内的 AVD：

```powershell
android emulator list
android emulator start mono-home-api30
```

## 验证

配置本地私有 fixture 后运行：

```powershell
dotnet run --project src/MonoHome.Verifier/MonoHome.Verifier.csproj -c Release
```

验证器覆盖存档识别、逐只转换、保真路线、批量写入、仓库副本、目标适配、合法性回读与恢复策略。私有 `.sav`、`.srm`、`.dsv` 文件不提交到仓库。

## 数据与安全

来源存档和中央仓库原件不会被传送流程直接改写。目标存档必须由 Android Storage Access Framework 授予可验证的读写权限；权限失效、文件被外部修改或回读校验失败时，流程会停止并提示重新授权。所有恢复点保存在应用私有目录。远程同步不会自动覆盖本地存档，也不会把开发者账号、仓库或 Token 写入应用。

## 文档

- [支持矩阵](docs/support-matrix.md)
- [传送中心设计](docs/superpowers/specs/2026-09-27-transfer-center-iteration-design.md)
- [通用存档中心设计](docs/superpowers/specs/2026-09-27-universal-save-hub-design.md)
- [Android 原型实施计划](docs/superpowers/plans/2026-09-28-android-prototype-warehouse.md)

## 许可证与第三方

项目许可证见 [LICENSE](LICENSE)。PKHeX.Core 声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。本项目是非官方兴趣项目，与 Nintendo、The Pokémon Company 或 Pokémon HOME 无关联。
