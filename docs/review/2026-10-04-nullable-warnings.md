# Nullable 警告核对

范围：1.2.6 发布构建的 30 项警告，共 18 个不同源码位置，全部位于 MainActivity.cs。发布打包包含重复编译，故汇总数量大于位置数量。

| 位置类别 | 不同位置数量 | 原因与修复 | 保持的行为 |
|---|---:|---|---|
| 屏幕宽高、密度 | 10 | Android 绑定将 Resources / DisplayMetrics 标为可空；集中获取并检查资源与屏幕参数，删除两处重复 Dp 函数，复用已有换算 | 使用相同密度、宽高、比例与舍入规则 |
| 属性、招式分类图标 | 3 | 可空 Resources 被直接解引用，改用同一已检查资源入口 | 图标 ID 为 0 时仍沿用原有文字回退 |
| 特性与招式说明 JSON | 2 | 可空 Resources 被直接解引用，改用同一资源入口 | 原有异常捕获和空说明降级不变 |
| 无法显示个体详情的弹窗 | 1 | Builder 的链式返回值被标为可空，拆为对原构造器逐条设置 | 标题、正文、按钮和回调保持原样 |
| 筛选下拉框适配器 | 2 | GetView 的 parent 声明与基类非空契约不一致；GetDropDownView 的返回可空 | 与基类签名对齐；空下拉视图原样返回，非空视图沿用原样式 |

未关闭 Nullable，未使用 NoWarn 或 pragma 屏蔽警告，未修改存档、合法化、传送或仓库持久化逻辑。

验证命令：

```powershell
dotnet build src/MonoHome.Android/MonoHome.Android.csproj -c Release --no-restore -p:TreatWarningsAsErrors=true
dotnet src/MonoHome.Verifier/bin/Release/net10.0/MonoHome.Verifier.dll --fixed-fields-only
dotnet src/MonoHome.Verifier/bin/Release/net10.0/MonoHome.Verifier.dll
```

严格构建 0 警告、0 错误；162 项固定属性检查及完整 Verifier 通过。私有构建和验证日志保存在 .verify-tmp/nullable-*.log，未提交测试存档。

最终代码严格 Release 构建再次确认 0 警告、0 错误，日志 .verify-tmp/nullable-final-build.log。模拟器覆盖安装成功，筛选下拉框打开并选择中文属性正常；详情显示中文特性说明；招式简介显示属性、分类、命中率、PP、中文效果；招式编辑弹窗显示可选招式与属性图标；编辑页正常打开并回显当前记录。检查没有确认保存或执行传送。修复由 codex/fix-nullable-warnings 分支纳入 1.2.7；最终发布构建同样为 0 警告、0 错误。
