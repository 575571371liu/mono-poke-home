# MONO / HOME

MONO / HOME 是一个运行在 Android 上的本地宝可梦存档管理与传送工具。它使用 PKHeX.Core 读取和校验存档；中央仓库仍保存在设备本地，远程存档同步是需要用户主动开启的可选能力。

## 1.2.0 功能

### 存档与仓库

- 通过 Android 文件选择器导入存档，并自动识别支持的游戏格式。
- 为每个已导入存档保留本地快照，可手动刷新以重新读取游戏中最新的仓库数据。
- 中央仓库保存独立副本；查看详情时显示合法性、形态、闪光、性别、蛋、携带道具、状态、丝带与特性效果。
- 仓库支持按等级、属性、蛋组、性别、闪光和孵化状态筛选；等级使用 1–100 双端滑块。
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
- 个人版通过细粒度 PAT 连接指定私有仓库；PAT 仅保存在 Android Keystore，永不过期是个人使用的可撤销取舍。公开版本不得复用该 PAT。

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
