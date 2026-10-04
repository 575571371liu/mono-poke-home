# 心金合法性错误逐项诊断

日期：2026-10-03。输入：本地私有 heartgold.sav，69 只，5 只通过原版规则、64 只被原版规则拒绝。所有试验只操作内存副本，未修改来源存档。

## 四种实际错误（三组个体）

| 错误码 | 数量 | 原因 | 在固定要求下的办法 |
|---|---:|---|---|
| NickMatchLanguageFail | 16 | Language=1（日文）、IsNicknamed=false，但名字不是对应日文种类名 | 按当前语言恢复标准种类名；只调整昵称与昵称标记 |
| NickInvalidChar | 47 | 名字包含第四世代命名规则不允许的字符；部分含中途终止符 | 重写为该语言有效种类名并规范昵称缓冲区 |
| EncTradeChangedNickname | 1 | Box 1-15 壶壶属于有固定昵称的 NPC 交换个体 | 恢复 NPC 固定昵称「ネッシー」 |
| EncTradeChangedOT | 1 | 同一只壶壶的训练家名与该 NPC 交换模板要求不一致 | 保留该交换来源时必须恢复 NPC 训练家名「セイジ」；此操作违反用户锁定训练家的要求，因此应用应拒绝 |

壶壶同时出现后两种错误，所以非法个体总数为 16 + 47 + 1 = 64，不是 65。

## 已运行的修复试验

- 16 + 47 = 63 只：只重写昵称为原语言的有效种类名，设置非昵称标记，全部变为合法。
- 每只都将昵称字节与昵称标记恢复到修复前，再比较整个解密数据，确保其他字节完全相同；个体值、性格、性别、闪光、种类、形态、等级、招式、PP、道具、状态、病毒、蛋、训练家、来源游戏、精灵球、特性全部不变。
- 壶壶：只恢复交换昵称仍非法，剩余唯一错误为 EncTradeChangedOT。
- 仅用于诊断的额外副本：再恢复 NPC 训练家名后合法。这个实验明确违反固定训练家名条件，不能作为可写入结果。

用户已确认使用中文汉化游戏。上述名称错误属于汉化名称与原版校验规则的兼容问题，不能据此认定游戏中的宝可梦损坏。用户明确允许恢复对应语言的原版名称；该操作会替换汉化名称。

## 当前应用实现的边界

RepairPreservingFields 已接入 Gen 4 非蛋、日文或英文记录的标准名称恢复；语言取宝可梦自身 Language，名称取来源世代。NPC 交换个体使用对应语言的固定昵称。只有完整合法性校验通过才返回可确认结果；训练家冲突时返回原始数据。实际应用方法与仓库持久化共 159 项检查、0 失败，包括日文和英文正向对照；心金 63 只修复成功，5 只原本合法保留，1 只壶壶拒绝且原始字节不变。证据：.verify-tmp/fixed-fields/run-20261003-152452-f0e3748405594e6b97386ade21aae7fc/fixed-fields/fixed-field-results.json。

壶壶的结论限定为目前识别出的 NPC 交换来源。其他来源是否存在同时满足全部固定条件的合法模板尚未穷尽测试，因此不声称该个体在所有可能来源下绝对无解。

## 全部非法个体清单

| 位置 | 宝可梦 | 错误码 |
|---|---|---|
| 随身携带 1 | 熔岩虫 | NickMatchLanguageFail |
| 仓库 1-1 | 鬼斯 | NickInvalidChar |
| 仓库 1-2 | 圆丝蛛 | NickInvalidChar |
| 仓库 1-3 | 小拉达 | NickMatchLanguageFail |
| 仓库 1-4 | 玛瑙水母 | NickMatchLanguageFail |
| 仓库 1-5 | 尼多兰 | NickInvalidChar |
| 仓库 1-6 | 尼多朗 | NickMatchLanguageFail |
| 仓库 1-7 | 铁甲蛹 | NickMatchLanguageFail |
| 仓库 1-8 | 绿毛虫 | NickMatchLanguageFail |
| 仓库 1-9 | 尼多兰 | NickInvalidChar |
| 仓库 1-10 | 向日种子 | NickInvalidChar |
| 仓库 1-11 | 百变怪 | NickInvalidChar |
| 仓库 1-12 | 大奶罐 | NickMatchLanguageFail |
| 仓库 1-13 | 鲤鱼王 | NickInvalidChar |
| 仓库 1-14 | 玛瑙水母 | NickMatchLanguageFail |
| 仓库 1-15 | 壶壶 | EncTradeChangedOT、EncTradeChangedNickname |
| 仓库 1-16 | 可达鸭 | NickInvalidChar |
| 仓库 1-17 | 隆隆石 | NickMatchLanguageFail |
| 仓库 1-18 | 小山猪 | NickMatchLanguageFail |
| 仓库 1-19 | 迷你龙 | NickInvalidChar |
| 仓库 1-20 | 大力鳄 | NickInvalidChar |
| 仓库 1-22 | 喇叭芽 | NickInvalidChar |
| 仓库 2-2 | 卡比兽 | NickInvalidChar |
| 仓库 2-3 | 顿甲 | NickInvalidChar |
| 仓库 2-4 | 蚊香君 | NickInvalidChar |
| 仓库 2-5 | 大岩蛇 | NickMatchLanguageFail |
| 仓库 2-6 | 金鱼王 | NickInvalidChar |
| 仓库 2-7 | 催眠貘 | NickInvalidChar |
| 仓库 2-8 | 尾立 | NickMatchLanguageFail |
| 仓库 2-9 | 波波 | NickMatchLanguageFail |
| 仓库 2-10 | 暴鲤龙 | NickInvalidChar |
| 仓库 2-11 | 烈雀 | NickInvalidChar |
| 仓库 2-12 | 乌波 | NickInvalidChar |
| 仓库 2-13 | 烈雀 | NickInvalidChar |
| 仓库 2-14 | 大葱鸭 | NickInvalidChar |
| 仓库 2-15 | 尼多兰 | NickInvalidChar |
| 仓库 2-16 | 图图犬 | NickMatchLanguageFail |
| 仓库 2-17 | 麒麟奇 | NickInvalidChar |
| 仓库 2-18 | 麒麟奇 | NickInvalidChar |
| 仓库 2-19 | 胖丁 | NickInvalidChar |
| 仓库 2-20 | 小拳石 | NickMatchLanguageFail |
| 仓库 2-21 | 波克基古 | NickInvalidChar |
| 仓库 2-22 | 小拳石 | NickMatchLanguageFail |
| 仓库 3-1 | 伊布 | NickInvalidChar |
| 仓库 3-2 | 伊布 | NickInvalidChar |
| 仓库 3-3 | 咕咕 | NickInvalidChar |
| 仓库 3-4 | 伊布 | NickInvalidChar |
| 仓库 3-5 | 伊布 | NickInvalidChar |
| 仓库 3-6 | 伊布 | NickInvalidChar |
| 仓库 3-7 | 伊布 | NickInvalidChar |
| 仓库 3-8 | 伊布 | NickInvalidChar |
| 仓库 3-9 | 伊布 | NickInvalidChar |
| 仓库 3-10 | 伊布 | NickInvalidChar |
| 仓库 3-11 | 伊布 | NickInvalidChar |
| 仓库 3-12 | 伊布 | NickInvalidChar |
| 仓库 3-13 | 伊布 | NickInvalidChar |
| 仓库 3-14 | 伊布 | NickInvalidChar |
| 仓库 3-15 | 伊布 | NickInvalidChar |
| 仓库 3-16 | 伊布 | NickInvalidChar |
| 仓库 4-1 | 伊布 | NickInvalidChar |
| 仓库 4-2 | 伊布 | NickInvalidChar |
| 仓库 4-3 | 伊布 | NickInvalidChar |
| 仓库 4-4 | 伊布 | NickInvalidChar |
| 仓库 4-5 | 伊布 | NickInvalidChar |

## 原始证据

- 全量逐只报告：../../.verify-tmp/heartgold-diagnosis.json
- 逐只昵称修复结果：../../.verify-tmp/heartgold-diagnosis.json.repair.json
- 可重跑诊断：../../.verify-tmp/heartgold-diagnosis/Program.cs


## 最新：已授权并实现 NPC 固定名称特例

用户允许明确识别的 NPC 个体恢复固定名称。当前覆盖 HG/SS 的 EncounterTrade4PID 非语言交换模板，并要求原记录 PID 和训练家 ID 与模板一致；名称恢复后仍必须通过完整合法性校验。语言交换模板暂不自动处理，以免误选名称语言。

实际壶壶恢复固定昵称「ネッシー」和训练家名「セイジ」后合法。除昵称、昵称标记、训练家名称字节外，整个解密数据不变；训练家 ID、性别、来源及全部游戏属性保持不变。界面列出昵称和训练家名差异，确认后保存。

162 项实际方法与仓库持久化检查全部通过。心金 64 只名称问题均可处理，5 只原本合法保留；PID、训练家 ID、个体值不匹配的 NPC 负向对照均拒绝且原始字节不变。证据：.verify-tmp/fixed-fields/run-20261003-153406-fc026b115155468c852122a33e1765a5/fixed-fields/fixed-field-results.json。此前壶壶拒绝的统计为本特例接入前结果。
