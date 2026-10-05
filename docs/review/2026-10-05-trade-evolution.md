# 主仓库按通讯条件进化：实现与验证

设计日期 2026-10-05；验证跨 2026-10-05／06（Asia/Shanghai）。开发分支 codex/warehouse-trade-evolution。

## 交付行为

仅主仓库个体档案的「进化与形态」提供「按通讯条件进化」。首版执行 PK3／PK4 的普通交换和携带道具交换进化；资料浏览仍覆盖原有作品。跨格式作品和浏览其他家族节点不会提供执行按钮。来源存档保持只读。

确认弹窗沿用深绿圆角面板、荧光绿主按钮与中文头像名称，显示作品、昵称／特性／道具差异，保留招式提示；大字体改纵排并允许滚动，底部按钮固定。

进化在克隆上执行，不自动合法化或伪造交换训练家。内存合法、保留字段逐字节比对、实际 party payload 回读合法才更新同一条仓库记录。数据库沿用 ID／Revision 原子更新，original 不变。更新后旧传送适配失效。

## 开发裁决

- 用户已明确要求按方案开发并测试，因此当前会话直接执行实施计划；不另开聊天、不再重复请求开发授权。
- 使用现有工程引用的本地 PKHeX，未新增依赖或数据库迁移。
- 仓库没有当前所在游戏信息；默认仅使用与格式匹配的起源作品，否则使用格式参考作品。没有全国图鉴／剧情进度判断，不宣称完整交换模拟。
- 现有 ResetPartyStats 会清除异常并恢复 HP，进化使用 SetStats 后按原 HP 缺口修正；濒死维持 0，异常不变。
- 通用 SaveWorking 仍保留既有编辑行为；进化通过独立 ApplyTradeEvolution 强制合法性验证。
- Activity 重建通过共享的待提交 Task 重新读取最终仓库；已提交后的详情异常单独提示保存成功、刷新失败。
- 通用 RefreshWarehouse 错误文案改为「仓库读取失败，请刷新重试」，避免进化后的刷新故障被错误描述为放生未执行。

## 自动检查

使用本机既有 C:/Users/liujinwen/.dotnet/dotnet.exe（.NET 10）。系统 PATH 中另一个 dotnet 没有 SDK，不作为构建依据。

| 命令（dotnet 指上述 SDK） | 结果 |
| --- | --- |
| run --project src/MonoHome.Verifier -- --trade-evolution | 通过；覆盖两个格式的通讯分支、10 个作品、道具与不变之石例外、语言昵称、闪光、HP／异常、字段保留、回读合法性、并发与 SQL 故障 |
| run --project src/MonoHome.Verifier | 通过；新增进化检查已接入完整入口，83 只绿宝石个体转换、批量传送、保真模式及同步回归通过 |
| run --project src/MonoHome.Verifier -- --warehouse-release-only | 通过；迁移、原始／工作字节保真、真实事务故障、进程中断恢复、放生与失败传送重试 |
| run --project src/MonoHome.Verifier -- --fixed-fields-only | 162 项，0 失败 |
| run --project src/MonoHome.Verifier -- --evolution-guide-only | 通过 |
| build src/MonoHome.Android -c Release -warnaserror --no-restore | 0 警告、0 错误 |

关键检查：预览不写库；成功 ID／数量／original 不变、Revision 只加一；仍可进化的陈旧记录由数据库版本条件拒绝；已删除记录不能复活；真实 UPDATE trigger 失败后 payload 和 Revision 不变；更新前适配 IsCurrentFor 返回 false，新适配路径不同且目标盒子含进化后的种类。

首次失败检查明确报告缺少 TradeEvolution 实现，随后实现并使检查通过。测试样本由本地 PKHeX 合法相遇生成，不依赖联网或 ROM。

## Android 隔离验收

模拟器 emulator-5554，独立包名 io.github.monohome.tradecheck。使用五条合法测试记录，未覆盖现有 io.github.monohome 应用仓库。工具 android layout 在本机返回 instrumentation 错误，改用 adb uiautomator 实际布局；截图用 android screen capture 并逐张检查。

- 主仓库勇基拉 → 胡地：确认前取消，数据库 SHA256 完全一致；随后确认并连续点击，真实记录仅由 Revision 1 变为 2，ID／数量／original 保持不变，进化结果合法。
- 珍珠贝携带深海之牙：猎斑鱼分支启用，樱花鱼分支禁用并显示当前／所需道具。预览说明特性、默认昵称与道具消耗；保存后 366 → 367，HeldItem 192 → 0。
- 鬼斯通携带不变之石：明确显示阻止原因，按钮禁用。
- 第四世代勇基拉携带不变之石：可以进化，结果道具 229 → 229；第三世代对应情况由自动检查拒绝。
- 数据库持有真实 BEGIN EXCLUSIVE 锁时提交并旋转屏幕，释放锁后重建的 Activity 显示胡地；数据库比对确认仅一次提交且结果合法。
- 960×1600／420dpi、font_scale 1.4：确认头像自动纵排，正文在 ScrollView 中，取消／确认按钮在导航栏上方可达。
- PK3 选择心金规则：显示「该作品与个体格式不匹配，仅可查看资料」，无执行按钮。浏览胡地节点显示「正在查看」，移除当前个体操作区。
- 来源存档珍珠贝：进化资料可查看，无执行操作；来源文件 SHA256 与本地 fixture 相同。
- 实际 Android 数据库注入 UPDATE trigger 故障：显示「进化未完成」，确认禁用、关闭可用；所有行的元数据、Revision、original／working BLOB 完全不变。数据库文件仅 SQLite 头部计数／版本字节变化，不能用文件整体哈希代替事务数据比对。

真实数据库比较可重跑：

```powershell
dotnet run --project src/MonoHome.Verifier -- --trade-evolution-compare-android BEFORE.sqlite3 AFTER.sqlite3
dotnet run --project src/MonoHome.Verifier -- --trade-evolution-compare-android BEFORE.sqlite3 FAILED.sqlite3 0
```

第一条要求恰好一条更新、Revision 恰好加一、所有原始 BLOB 不变、结果字段和合法性正确；第二条要求全部行与元数据保持不变。

本地证据在 .verify-tmp/trade-evolution-*.log、tradecheck-*.sqlite3、tradecheck-*-ui.xml、tradecheck-preview.png 和 tradecheck-large-preview.png，测试数据和日志不进入 Git。

## 独立复审与完成审计

独立复审发现两项 P2：旧 Activity 后台提交可能遗漏重建后的刷新；详情重新打开在提交异常保护之外。均已修复，复审确认无剩余可执行缺陷。没有修改 PKHeX 上游。

对照设计：入口权限、首版范围、作品语义、直接规则筛选、道具消费、字段保留、默认昵称／特性、招式保留、HP 与异常、严格回读、Revision／事务、预览取消、传送缓存、失败提示、生命周期刷新、样式／大字体、构建和回归均有代码或上述运行证据。

仍明确限定为第三、第四世代的仓库进化编辑；不提供实际通讯、后世代交换搭档、剧情解锁或自动学招。没有发布、推送或合并到 main。
