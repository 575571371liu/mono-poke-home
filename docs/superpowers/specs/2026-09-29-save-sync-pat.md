# MONO / HOME 私有存档同步：PAT 方案

> 本文取代旧设计中 GitHub App、PKCE 与 Device Flow 的认证部分；存档版本、恢复点、条件写入和 lineage 规则保持不变。

## 目标

为个人私有工具提供无需 GitHub App Client ID 的远程存档同步。远端固定为用户拥有的私有仓库 `575571371liu/mono-home-saves`；未来公开产品另行接入正式授权服务，不复用个人 PAT。

## 认证与绑定

1. 用户在 GitHub 创建细粒度 PAT，仓库范围仅为 `mono-home-saves`，权限仅为 `Contents: Read and write`（`Metadata: Read-only` 为 GitHub 必需项）。
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

`AndroidTokenStore` 直接保存 PAT 为无过期 bearer token；不新增凭据格式或依赖。设置页改为“输入 PAT 并绑定私有仓库”，默认预填个人仓库，仍允许手工修改 owner/repository 以便恢复或迁移。

## 验收

1. PAT 校验失败、仓库不存在/非私有/无写权限时不保存凭据。
2. 重启后 Keystore 可恢复 PAT，普通 SharedPreferences 与日志不含 PAT。
3. 首次上传、重复上传 no-op、拉取 recovery、历史回滚与冲突分叉的既有 verifier 均通过。
4. 两台设备使用不同、但同样只限该仓库的 PAT 完成真实上传、拉取和冲突验收。

## 安全边界

该路径仅服务个人私有使用。PAT 永不过期是已确认的个人取舍，但仍仅限一个私有仓库且可在 GitHub 撤销；任何公开发布版本必须改用独立的正式授权/同步服务，不能内置或共享此 PAT。
