# 主仓库事务放生实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Execute in this session, with one final independent review.

**Goal:** 实现已确认的主仓库事务放生与可信验收。
**Architecture:** SQLite 一行保存元数据与两份实体；保留 LocalRepository 入口，增加整批事务放生；迁移验证后清理旧文件并支持中断恢复；UI 复用现有选择。
**Tech Stack:** .NET 10、Android native views、Microsoft.Data.Sqlite（包含 SQLitePCLRaw native bundle）、现有 Verifier。
**Spec:** docs/superpowers/specs/2026-10-05-warehouse-release-design.md

## Global Constraints
- 整批成功或整批保留；无回收站；不改游戏存档；一次确认与即时刷新。
- 临时迁移源仅在核验后清理；清理完成前不可放生；不长期保留旧实体副本。
- 仓库条目使用 ID 而非种类定位，每次修改递增 Revision。
- 不自动提交、推送或发布；先交付可安装测试版本。

## Review Focus
- 对损坏或越界旧路径必须拒绝迁移，不能默默丢记录。
- 清理中断或旧 manifest 残留不能导致放生后重新导入。
- 陈旧确认对象和筛选隐藏的选择不能导致误删或只删部分。
- 失败与提交边界发生中断时，下一进程应看到全部或零个。
- 刷新不能自动选中第一只并残留旧的传送目标。

### Task 1: 仓库数据与迁移
- [x] 在 Verifier 添加 SQLite 持久化断言，确认旧实现失败。
- [x] 新建 Repository/WarehouseDatabase.cs：数据表、受约束路径迁移、校验和续清理。
- [x] 改 LocalRepository 数据入口，调整直接文件读取的测试与调用者。
- [x] 测试两份实体字节与元数据保真、重复打开、损坏拒绝、路径边界、编辑并发和恢复。

### Task 2: 事务放生
- [x] 添加单只、批量、陈旧版本与 SQL trigger 故障检查。
- [x] 增加 Release(root, records)：即时事务、逐条 Revision 校验、提交/回滚。
- [x] 子进程真实终止测试提交前/后、迁移前/后，重新打开校验整批结果。

### Task 3: 现有选择与确认 UI
- [x] 在现有菜单和操作栏增加放生；统一选择文案。
- [x] 固定确认名单、按完整仓库 ID 解析；一次滚动确认、执行期间保护。
- [x] 成功后立即读取刷新并保留有效分页/其他选择，失败保持全部。
- [x] 独立 Android 测试包演练单只/批量/取消/跨页/筛选/全部放空，检查排版。

### Task 4: 交付
- [x] 完整 Verifier、严格 Release 构建、签名/版本核验。
- [x] 独立复审，修复重要问题并重跑对应证据。
- [x] 保存测试报告与测试 APK，完成逐项目标审计后邀请用户验收。

## 执行记录
- 2026-10-05：主分支 0756c16，无未提交改动。用户已明确授权按已确认方案实施，方案与实施计划落盘后持续执行，不重复申请设计许可。
- 技术检索：dotnet/efcore 的 Microsoft.Data.Sqlite 和 ericsink/SQLitePCL.raw 提供事务与 Android native bundle；采用官方 ADO.NET 接口，不引入 ORM。参考 https://learn.microsoft.com/dotnet/standard/data/sqlite/transactions 和 https://learn.microsoft.com/dotnet/standard/data/sqlite/custom-versions 。
- 2026-10-05：warehouse-release-only 六组检查通过，覆盖字节保真、迁移清理失败、陈旧版本、真实第二条 DELETE 故障、提交前后强制杀进程和迁移中断。完整 Verifier 通过（83 只 Emerald 合法转换、批量传送及同步回归）。Android 严格 Release 构建进行中，设备验收和最终独立复审尚未完成。
- 2026-10-05：严格 Release 与隔离 Debug 构建均零警告零错误；固定字段 162/162、进化指引检查通过。Android 隔离包完成 34 条旧数据迁移（cleanup=0，无旧实体文件）；取消前后 DB SHA256 一致；单只 ID1 删除后 33 条；跨页 ID2/ID32 批量删除后 31 条，保持仓库 2。发现底部旧传送按钮与现有操作栏下载重复，收拢为统一操作栏，待重建验证。
- Android：筛选毒属性时保留隐藏的 ID34 皮卡丘选择，确认包含 ID3 超音蝠与 ID34，成功后 29 条/筛选 15 条。注入 SQLite BEFORE DELETE ID7 trigger，选择 ID5/ID7 后第二条真实失败，UI 显示本次未删除，关闭后已选 2 只；对比 pokemon 的全部字段及两份 BLOB、warehouse_meta、legacy_cleanup 均完全相同。数据库文件 SHA 不同仅不能用整文件哈希证明事务失败，需要以实际记录字节为证据。
- 2026-10-05：Android 960x1600、420dpi（约366dp宽）验证确认按钮安全区域、统一操作栏且无旧传送按钮；两条测试记录全部放空后立即显示 0/30，数据库 pokemon=0，目录仅 warehouse.sqlite3。最终独立复审发现 Important：批量下载失败更新合法性丢弃新 Revision；失败分支增加保持分页的 RefreshWarehouse，新增真实转换失败→合法性更新→刷新→重试删除检查，验证中。
- 最终复审：无 Critical；1 个 Important（失败后批量下载重试使用陈旧 Revision）已修复，新增真实存档转换重试检查通过；完整 Verifier 再次通过。最终 Release 构建零警告零错误，apksigner 验证通过，包 io.github.monohome / 1.2.8 / code11，arm64-v8a+x86_64。候选 APK SHA256 ED4EFB3D97DEF6DCD47DC0C5D2AB8217AF0608245C89D20715780C0624F255ED，交付目录 C:/Users/liujinwen/.codex/releases/mono-poke-home-warehouse-acceptance。保持现有版本号作为验收候选，不自动发布。
- Android：16 只确认清单 scrollable=true，滚动后末尾 Lv20 可见，确认按钮保持 [550,1387][1010,1513]，正常滚动与固定动作通过。

