# 支持矩阵

正式界面只开放标记为 `released` 的路径。其余组合会显示为“不支持”，不会尝试写入。

| 源存档 | 目标存档 | 模式 | Profile | 状态 |
|---|---|---|---|---|
| Pokémon Emerald | Pokémon HeartGold | 合法转换 | `profiles/emerald-to-hgss.json` | released |
| Pokémon Emerald | Pokémon SoulSilver | 合法转换 | `profiles/emerald-to-hgss.json` | released |
| Pokémon Emerald | Pokémon HeartGold / SoulSilver | 保真传送 | `profiles/emerald-to-hgss.json`（来源合法性检查） | released（逐只检查） |
| Gen I–IX 其余组合 | 任意 | — | — | unsupported |

首条 Profile 的回归样本是用户提供的绿宝石 `.srm` 与心金 `.sav`，仅在本地私有测试目录中使用，不提交到源码库。
