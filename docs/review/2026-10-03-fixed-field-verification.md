# 固定字段方案验证

日期：2026-10-03；分支 codex/preserve-warehouse-stats。

## 验证结论及边界

应用实际调用的 RepairPreservingFields 已经通过固定字段、安全失败、仓库保存回读和模拟器确认更新测试。测试证明已覆盖用例的数据保护规则，不证明任意宝可梦都能合法化。目前应用中的受约束修复仅处理 Gen 3 非蛋昵称缓冲区问题；心金的其他类型需要扩展，详见同目录心金诊断报告。

## 测试对象与结果

157 个用例，0 个失败，独立运行两次结果相同：

| 样本 | 原本合法 | 修复成功 | 原样拒绝 |
|---|---:|---:|---:|
| 绿宝石 83 只 | 25 | 58 | 0 |
| 心金 69 只 | 5 | 0 | 64 |

另外 5 个控制用例：真实盾甲茧昵称异常可成功修复；未修改等级的编辑不改实体字节；非法招式必须失败并原样返回；超限 PP 必须失败并原样返回；非零异常状态和病毒保存回读必须不变。

## 固定字段逐项断言

种类、形态、等级、性格、能力性格修正、性别、闪光、六项个体值、六项努力值、六维能力、四招式、当前 PP、PP 提升次数、道具、异常状态、病毒、蛋状态、训练家名/TID/SID/性别/原始名字字节/语言、来源游戏、精灵球、特性及特性槽位。

每只真实样本都检查：输入未被修改；结果固定字段相同；成功结果合法；失败结果所有数据原样；应用实际 Upload/SaveWorking/LoadWorking 回读后固定字段相同且成功结果仍合法；原始备份未改；来源存档 SHA-256 未改。Gen 3 成功样本另有逐字节断言，昵称缓冲区之外的整个实体完全一致。

测试先使用原应用旧 RepairWithStrategy 运行，155 用例记录 305 项断言失败，明确捕获个体值、能力、等级、招式、PP、球、特性等改动，以及存储丢失异常状态。不是只写了会通过的测试。

## 模拟器证据

最终 APK 已安装到 mono-home-api30。用来源 Box 1-1 的新上传盾甲茧测试：打开编辑页、预览合法化、取消时仓库文件未变化，确认后只工作副本和元数据更新，原始备份不变。拉取工作副本，用验证器对修复前后的实体比较：固定字段差异为空、昵称缓冲区之外的解密数据完全相同、持久化后的实体合法。

模拟器回读证据：../../.verify-tmp/emulator-final-result.json。

最后一次固定检查结果：../../.verify-tmp/fixed-fields/run-20261003-150811-5e4c14590f0a48fba9971b1de4e93176/fixed-fields/fixed-field-results.json。

## 重跑

```powershell
$env:MONO_HOME_SCRATCH = Join-Path $PWD '.verify-tmp/fixed-fields'
dotnet build src/MonoHome.Verifier/MonoHome.Verifier.csproj -c Release --no-restore
dotnet src/MonoHome.Verifier/bin/Release/net10.0/MonoHome.Verifier.dll --fixed-fields-only
```

测试源码：src/MonoHome.Verifier/FixedFieldChecks.cs；应用入口：EditCopyActivity.PreviewLegalization；实际方法：LocalRepository.RepairPreservingFields。

曾遇到本机 Windows 文件替换权限的间歇性错误，未将这些中断运行当成通过；以上结果仅引用退出码 0 的完整运行。Android 构建退出码 0，保留 27 个现有空引用警告。

最新追加：用户允许将汉化名称恢复为记录语言的标准名称。Gen 4 日文/英文标准名称恢复现已接入实际应用方法；159 项检查，0 失败；心金 63 只恢复合法，5 只保留，1 只因锁定训练家名称冲突拒绝且不改数据。旧的 64 只拒绝统计为接入前结果。

## 最新：已授权并实现 NPC 固定名称特例

用户允许明确识别的 NPC 个体恢复固定名称。当前覆盖 HG/SS 的 EncounterTrade4PID 非语言交换模板，并要求原记录 PID 和训练家 ID 与模板一致；名称恢复后仍必须通过完整合法性校验。语言交换模板暂不自动处理，以免误选名称语言。

实际壶壶恢复固定昵称「ネッシー」和训练家名「セイジ」后合法。除昵称、昵称标记、训练家名称字节外，整个解密数据不变；训练家 ID、性别、来源及全部游戏属性保持不变。界面列出昵称和训练家名差异，确认后保存。

162 项实际方法与仓库持久化检查全部通过。心金 64 只名称问题均可处理，5 只原本合法保留；PID、训练家 ID、个体值不匹配的 NPC 负向对照均拒绝且原始字节不变。证据：.verify-tmp/fixed-fields/run-20261003-153406-fc026b115155468c852122a33e1765a5/fixed-fields/fixed-field-results.json。此前壶壶拒绝的统计为本特例接入前结果。

## 编辑页改版验证

已按确认方案采用现有深绿卡片、薄荷绿文字、荧光绿按钮。中文名称选择器支持搜索；六维按 HP/攻击/防御/特攻/特防/速度逐行排列，内部读写仍使用 PKHeX 的索引。闪光/蛋为开关，病毒和原始合法性报告折叠，底部操作固定。PP 按当前记录回显。

验证：Android Release 构建成功（0 错误，27 项既有警告）。162 项数据检查成功，并新增对全部 152 只真实样本的不修改编辑提交逐字节检查。模拟器：打开/滚动编辑页、性格选择器、六维回显、合法化预览和取消；不编辑保存后仓库所有 10 个 PKM 文件 SHA256 完全不变，目标记录 revision 从 1 到 2，证明保存确实执行；预览取消后所有 15 个仓库文件完全不变。

私有证据：.verify-tmp/editor-no-edit-evidence.json；.verify-tmp/editor-redesign-top.png；.verify-tmp/editor-redesign-six.png；.verify-tmp/editor-redesign-picker-final.png；.verify-tmp/editor-redesign-preview.png。数据检查结果：.verify-tmp/fixed-fields/run-20261003-154156-e64f238b97e845c0bac80689af087375/fixed-fields/fixed-field-results.json。

种类固定：编辑页改为只读展示，并移除 WorkingEdit.Species 参数。正式名称按钮、形态及特性选项均基于当前宝可梦的原种类。162 项检查通过，证据：.verify-tmp/fixed-fields/run-20261003-155820-1635d618d0fe454981569ee4c10fa214/fixed-fields/fixed-field-results.json。

2026-10-04 图片修复：统一来源槽位、仓库槽位、详情、传送目标和编辑预览的闪光/蛋图片选择。复用 PKHeX 本地真实素材；第一至第四世代全部 493 个闪光素材存在且不同于普通素材，检查脚本 scripts/check-pokemon-sprites.ps1 通过。模拟器已确认保存的闪光玛瑙水母在仓库和编辑页显示绿色闪光配色；关闭闪光显示普通图片，开启蛋显示蛋图片，退出未保存后全部 15 个仓库文件哈希不变。Release 构建 0 错误，162 项数据检查 0 失败。证据：.verify-tmp/shiny-warehouse.png、shiny-editor.png、sprite-preview-normal.png、sprite-preview-egg.png。

1.2.6 发布验证（2026-10-04）：Release APK 构建成功，签名校验通过；versionName=1.2.6、versionCode=9。模拟器覆盖安装成功。162 项检查及完整 Verifier 全部通过；493 个闪光素材检查通过。发布构建存在 30 项 nullable 警告，无错误。
