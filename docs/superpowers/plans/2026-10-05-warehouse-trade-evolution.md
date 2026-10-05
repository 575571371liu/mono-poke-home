# 主仓库通讯条件进化 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** 按已确认设计实现仅主仓库可执行的第三、第四世代通讯条件进化并测试。
**Architecture:** 本地进化规则产生合法克隆预览，仓库专用提交入口重复校验并以 Revision 原子更新。现有资料弹窗通过可选回调接入主仓库，来源保持只读。
**Tech Stack:** C# / .NET 10 / .NET Android / PKHeX.Core / SQLite，零新依赖。
**Spec:** ../specs/2026-10-05-warehouse-trade-evolution-design.md

## Global Constraints

- PK3／PK4；作品必须与个体 Context 匹配；仅直接 Trade／TradeHeldItem 边。
- 保留原始 payload、个体身份及招式；不伪造训练家、交换经历或剧情解锁。
- 内存和实际 party payload 回读均合法，才提交；ID／Revision 控制竞争。
- 主仓库限定；深绿面板与荧光绿按钮；不增加首页／长按／批量入口。

## Review Focus

- 起源版本与当前格式不同：默认选择当前格式参考作品。
- 第四世代勇基拉不变之石例外：允许且不消耗；其他分支按规则阻止。
- 能力缓存：进化不能治愈异常或濒死状态。
- 数据库提交后 UI 刷新失败：不能误报回滚或重复提交。
- 浏览家族其他节点和跨格式作品：不能操作错误个体。

### Task 1: 规则与克隆预览

Files: Core/Pokemon/TradeEvolution.cs; Verifier/TradeEvolutionChecks.cs; Verifier/Program.cs。
Interfaces: Evaluate(PKM, GameVersion) 返回原因及分支；Preview(PKM, GameVersion, EvolutionNodeKey) 返回合法 PKM。

- [x] 新增 --trade-evolution 检查入口，先断言功能不存在时失败。
- [x] 以合法样本验证勇基拉、携带道具、珍珠贝分支、不变之石、蛋、格式与任意目标拒绝。
- [x] 实现规则过滤与最小克隆更新；保持身份、经验、招式、状态并重算能力缓存。
- [x] 运行 dotnet run --project src/MonoHome.Verifier -- --trade-evolution；期望全部检查通过。

核心检查形式：
```csharp
var result = TradeEvolution.Preview(source, GameVersion.E, new(65, 0));
Require(result.Species == 65 && result.PID == source.PID, "evolves without replacing identity");
Require(new LegalityAnalysis(result).Valid, "result is legal");
```

### Task 2: 受控仓库提交

Files: Core/Repository/LocalRepository.cs; Verifier/TradeEvolutionChecks.cs。
Interface: ApplyTradeEvolution(StoredPokemon, GameVersion, EvolutionNodeKey) 返回更新后的 StoredPokemon。

- [x] 先检查预期新接口缺失；写入失败、过期 Revision 与删除记录不能覆盖。
- [x] 重新读取、预览、EntityBytes 回读校验后使用 WarehouseDatabase.Update。
- [x] 检查成功数量／ID／original 不变、Revision 加一、回读合法；拒绝不改变 working。
- [x] 加入进化后现有传送流程回归，确认重新生成适配输出。

提交检查形式：
```csharp
var updated = LocalRepository.ApplyTradeEvolution(record, game, target);
Require(updated.Id == record.Id && updated.Revision == record.Revision + 1, "updates same record");
Require(original.SequenceEqual(LocalRepository.ReadOriginalBytes(updated)), "backup unchanged");
```

### Task 3: Android 交互

Files: Android/EvolutionGuideDialog.cs; Android/MainActivity.cs; Android/MainActivity.TradeEvolution.cs。
Interface: Create 可选 Action<GameVersion, EvolutionNodeKey> 执行回调，仅 warehouseRecord 非空时注入。

- [x] 操作区仅当前个体节点显示；同格式作品可执行，其他作品显示理由。
- [x] 预览异步生成，确认弹窗含前后头像、差异、作品、个体信息保留与招式说明。
- [x] 提交使用 operationBusy，重复点击／返回禁止竞争；成功关闭旧弹窗、刷新详情并失效传送缓存。
- [x] 错误保持数据，提交后刷新错误单独反馈；生命周期变化重新读取仓库。
- [x] Release 严格构建与模拟器测试入口、条件、取消、成功、重复点击、布局、只读来源。

### Task 4: 完整验证与交付

- [x] dotnet run --project src/MonoHome.Verifier，期望完整现有回归通过。
- [x] dotnet build src/MonoHome.Android -c Release -warnaserror，期望 0 警告／0 错误。
- [x] 检查真实 UI 截图和运行日志，记录结果及设计裁决至 docs/review/2026-10-05-trade-evolution.md。
- [x] 对照设计逐条检查字段、不写来源、缓存刷新及异常边界；修复发现并重跑受影响检查。

## Execution ledger

- 2026-10-05：用户明确要求按开发方案完成实现及测试；本会话直接执行，不另开用户聊天。
- 基线：main，无产品代码改动，只有已写设计文档；建立 codex/warehouse-trade-evolution 分支。
- Task 1→2：仓库入口消费 Preview；Task 2→3：UI 只提交 ID／Revision／作品／目标，不接受任意编辑 PKM。

- Task 1–4: complete。核心／持久化／Android／完整回归证据见 ../../review/2026-10-05-trade-evolution.md。
- Ruling: Shared Task 用于 Activity 重建后等待提交；仅同一应用内单次操作，无新服务或事件系统。
- Final review: 独立复审两项 P2 已修复，复审确认无剩余问题。
