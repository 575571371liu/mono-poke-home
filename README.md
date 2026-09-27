# MONO / HOME

一个面向 Android 模拟器存档的本地宝可梦传送中心。项目基于 PKHeX.Core 解析存档、生成目标世代合法实体并进行本地校验；不使用账号、云同步、广告或遥测。

## 当前能力

- 导入并持续登记绿宝石、心金或魂银存档。
- 从绿宝石存档上传宝可梦到本地仓库。
- 仓库使用简体中文种类名与 National Dex 图标，保留闪光、携带道具、状态、宝可病毒、蛋、形态和丝带摘要。
- Emerald → HeartGold / SoulSilver：生成目标规则下通过合法性检查的实体。
- 目标文件拥有持久读写权限时，先创建应用私有恢复点，再写入、回读并校验目标存档。
- 支持批量传送、仓库长按查看个体档案、进入本地图鉴与创建新的合法编辑副本。

## 支持范围

当前正式路线仅为 Emerald → HeartGold / SoulSilver。其余世代和游戏不会在界面中伪装成可传送，详细矩阵见 [docs/support-matrix.md](docs/support-matrix.md)。

## 构建

需要 .NET 10 Android SDK、JDK 21 与 Android SDK。

```powershell
dotnet build src/MonoHome.Android/MonoHome.Android.csproj -c Release -t:Rebuild
```

APK 输出为：

```text
src/MonoHome.Android/bin/Release/net10.0-android/io.github.monohome-Signed.apk
```

## 验证

私有 fixture 不提交到仓库。配置本地 fixture 后运行：

```powershell
dotnet run --project src/MonoHome.Verifier/MonoHome.Verifier.csproj -c Release
```

验证器覆盖真实绿宝石/心金存档识别、逐只转换、保真路线、批量写入、仓库副本、目标适配与合法性回读。

## 存储与安全

来源存档和仓库原件不被传送或编辑流程改写。目标存档必须由 Android Storage Access Framework 授予可验证的读写权限；没有权限、文件外部变更或回读不一致时，传送会停止并要求重新授权。

应用在写入前保存应用私有恢复点。Android 对 `Android/data` 等目录有访问限制；模拟器应将存档放在用户可授权的目录后再导入。

## 文档

- [迭代设计](docs/superpowers/specs/2026-09-27-transfer-center-iteration-design.md)
- [实施计划](docs/superpowers/plans/2026-09-27-transfer-center-iteration.md)
- [支持矩阵](docs/support-matrix.md)

## 许可证与第三方

许可证见 [LICENSE](LICENSE)。PKHeX 相关第三方声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。本项目为非官方兴趣项目，与 Nintendo、The Pokémon Company 或 Pokémon HOME 无关联。
