## Azure Infrastructure (Terraform)

Azure App Service を使った FinLearn アプリのデプロイ構成。`gcp_terraform/` の Azure 版。

### リソース構成

- **Resource Group** — `rg-{prefix}`
- **App Service Plan × 2** — Backend (`plan-{prefix}-api`) + Frontend (`plan-{prefix}-web`)
- **Linux Web App × 2** — Backend (.NET 9) + Frontend (React Router v7 SSR, Node.js 20 LTS)

### ファイル構成

| File | Description |
|---|---|
| `main.tf` | リソース定義（RG, App Service Plan, Web App × 2） |
| `variables.tf` | 変数定義（project_name, environment, location, SKU） |
| `outputs.tf` | 出力値（URL, アプリ名） |
| `terraform.tfvars.example` | 設定テンプレート |

### 命名規則

`{project_name}-{environment}` プレフィックス（例: `finlearn-dev-api`）。`variables.tf` でデフォルト定義。

### デプロイ

- Frontend startup command: `npx react-router-serve ./build/server/index.js`（ポート 8080）
- CORS: Backend が Frontend URL を動的に許可
- Provider: azurerm ~> 4.0（ロック: 4.61.0）、Terraform >= 1.5.0

<!-- Last updated by agent: 2026-03-08 -->
