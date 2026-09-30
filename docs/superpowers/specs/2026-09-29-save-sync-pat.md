# MONO / HOME 私有存档同步：PAT 方案

> 本文取代旧设计中 GitHub App、PKCE 与 Device Flow 的认证部分；存档版本、恢复点、条件写入和 lineage 规则保持不变。

## 目标

为个人存档提供无需 GitHub App Client ID 的远程同步。每位用户绑定自己拥有且可写的私有仓库；`mono-home-saves` 只是建议仓库名。1.2.3 继续采用用户自备 PAT，未来面向更广泛用户时可接入正式授权服务。

## 认证与绑定

1. 用户在 GitHub 创建细粒度 PAT，仓库范围仅为自己的私有存档仓库，权限仅为 `Contents: Read and write`（`Metadata: Read-only` 为 GitHub 必需项）；建议设置有效期。
2. Android 以密码输入框收集 PAT，不记录、不显示回显；经 `GET /user` 和 `GET /repos/{owner}/{repo}` 校验后，用现有 Android Keystore 存储。
3. 普通配置只保存 owner、repository、branch、schema 和每个存档的 lineage；不得保存 PAT。
4. 401/403 只提示替换 PAT，绝不删除本地存档、绑定或远端历史。解绑仅清本机凭据和绑定。

## 同步不变量

- 存档内容 SHA-256、Git blob SHA、commit SHA 是不同字段，禁止混用。
- 写入继续使用 Contents API 的文件 SHA 条件更新；409 进入现有冲突选择，不自动覆盖。
- 上传和拉取前继续创建本地 recovery；下载后继续经 `SaveInspector` 验证并原子写回。
- 历史回滚和分叉继续使用 Git commit/ref；不采用 Gist，因为 secret Gist 不是私有且 PATCH 无条件写入。

## 改造范围

保留 `GitHubApiClient`、`GitHubRemoteSaveProvider`、`SaveSyncService`、`AndroidTokenStore`、版本/lineage UI。删除或不再调用 `GitHubAuthClient`、`GitHubDeviceFlowClient`、`github_client_id` 和刷新 Token 路径。

`AndroidTokenStore` 直接把 PAT 作为 bearer token 保存；过期后由用户更新 PAT，不新增凭据格式或依赖。设置页为“输入 PAT 并绑定私有仓库”，默认预填建议仓库名，仍允许手工修改 owner/repository 以便恢复或迁移。

## 验收

1. PAT 校验失败、仓库不存在/非私有/无写权限时不保存凭据。
2. 重启后 Keystore 可恢复 PAT，普通 SharedPreferences 与日志不含 PAT。
3. 首次上传、重复上传 no-op、拉取 recovery、历史回滚与冲突分叉的既有 verifier 均通过。
4. 两台设备使用不同、但同样只限该仓库的 PAT 完成真实上传、拉取和冲突验收。

## 安全边界

1.2.3 公开分发 APK，但同步是面向愿意自行管理 GitHub 凭据的用户的可选功能。每位用户只把自己创建的 PAT 输入自己的设备；APK 不内置开发者令牌，也不共享任何用户令牌。PAT 应仅限一个私有仓库的最低权限，设置有效期，并可随时在 GitHub 撤销。更广泛用户的授权体验应改为正式授权服务。
