---
paths:
  - "gcp_terraform/**"
---

## GCP Terraform Infrastructure

GCP Cloud Run を使った FinLearn アプリのデプロイ構成。`azure_infra/` の GCP 版。

### リソース構成

- **Artifact Registry** — Docker イメージ保管
- **Cloud Run v2 (`*-api`)** — .NET 9 Backend API
- **Cloud Run v2 (`*-web`)** — React Router v7 Frontend (SSR)
- **IAM** — 未認証アクセス許可 (allUsers → run.invoker)

### 対応 Dockerfile (プロジェクトルート)

- `Dockerfile.api` — Backend 用マルチステージビルド (.NET SDK → ASP.NET runtime)
- `Dockerfile.web` — Frontend 用マルチステージビルド (Node.js build → Node.js slim)
  - `VITE_API_URL` はビルド時 ARG で渡す (クライアントバンドルに埋め込み)

### 命名規則

全リソース名に `{project_name}-{environment}` プレフィックスを付与 (例: `finlearn-dev-api`)。

### デプロイ手順

`DEPLOY.md` を参照。初回は Cloud Run がイメージ未存在でエラーになるため、`-target` で Artifact Registry のみ先に作成 → イメージ push → 全体 apply の 2 フェーズで行う。
