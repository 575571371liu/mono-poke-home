# PKHeX 26.10.18 升级验证

## 依赖

- 升级前：`17157eb18013dc29a44f7bb7810117390431087b`，版本号 `26.08.26`。
- 升级后：官方 tag `26.10.18`，提交 `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`。
- 依赖通过 `../PKHeX/PKHeX.Core` 的 ProjectReference 引用；同级源码目录已切换到上述 tag，复现命令见 README。
- Android Release 输出的 `PKHeX.Core.dll` 程序集版本确认为 `26.10.18.0`。
- 未修改上游源码。Home 原有的 MainActivity.cs、FixedFieldChecks.cs 未提交修改保留并参与本次构建、验证。

## 验证结果

| 检查 | 结果 |
|---|---|
| 升级前 `pwsh -File tools/verify.ps1` | 通过，作为基线 |
| 升级后相同完整回归 | 通过，结果与基线一致 |
| `--fixed-fields-only` | 614 项，0 失败 |
| `--evolution-guide-only` | 通过 |
| `--warehouse-release-only` | 通过，含 SQLite 回滚、迁移、过期修订拒绝和真实子进程终止恢复 |
| `tools/verify-move-data.ps1` | 通过 |
| `scripts/check-pokemon-sprites.ps1` | 通过，493 只普通／闪光图片及蛋图片 |
| Android Release `-warnaserror` 构建 | 通过，0 警告、0 错误 |
| API 30 模拟器覆盖安装新版 APK | Success |
| 应用启动、主仓库、设置、系统文件选择器打开、取消后重新启动 | 通过，应用进程持续存在，前台 Activity 正确 |

完整回归覆盖真实绿宝石（83 只）和心金（69 只）存档识别／读取、仓库副本、修复、通讯进化、指定仓位、闪光与训练家保留、批量传送、写出后回读合法性、覆盖保护、传送日志、同步状态／恢复及 GitHub provider 假 HTTP 检查。83/83 只转换传送成功；保真传送 81/83 接受，拒绝结果与升级前一致。

本地日志：`artifacts/pkhex-before-verification.log`、`artifacts/pkhex-after-verification.log`、`artifacts/pkhex--*-only.log`、`artifacts/pkhex-android-build.log`；这些产物不提交。

## 验证边界

- 模拟器 DocumentsUI 能打开及浏览目录，但点击文件／目录未完成选择；未据此宣称 Android 界面导入、上传和下载全流程通过。核心真实存档读写由上述回归覆盖。
- Android CLI instrumentation 在 API 30 上因 `InputStream.readNBytes` 缺失崩溃，后续界面检查使用系统 UIAutomator。模拟器冷启动还出现过 System UI 无响应提示，选择等待后恢复；未见 `io.github.monohome` 崩溃。
- 没有魂银私有 fixture、实体手机或双设备在线同步验收；同步测试使用假远端／假 HTTP，不访问用户私有仓库。
- 本次升级不扩展传送路线，不发布 APK。现有支持范围仍以 support-matrix.md 为准。

APK：`src/MonoHome.Android/bin/Release/net10.0-android/io.github.monohome-Signed.apk`。
