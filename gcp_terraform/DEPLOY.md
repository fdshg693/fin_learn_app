# GCP デプロイ手順 (Cloud Run)

## 前提条件

- [Google Cloud CLI (`gcloud`)](https://cloud.google.com/sdk/docs/install) インストール済み
- [Terraform](https://developer.hashicorp.com/terraform/install) >= 1.5.0
- [Docker](https://docs.docker.com/get-docker/) インストール済み
- GCP プロジェクト作成済み、課金有効化済み

```shell
# gcloud 認証
gcloud auth login
gcloud config set project <YOUR_PROJECT_ID>

# Docker 認証を Artifact Registry に設定
gcloud auth configure-docker asia-northeast1-docker.pkg.dev
```

## 初回デプロイ (2フェーズ)

Cloud Run はイメージが存在しないとサービスを作成できないため、初回は 2 フェーズに分けてデプロイする。

### Phase 1 — Artifact Registry の作成 & イメージのプッシュ

```shell
cd gcp_terraform

# terraform.tfvars を作成
Copy-Item terraform.tfvars.example terraform.tfvars
# → project_id を自分の GCP プロジェクト ID に変更

terraform init

# Registry のみ作成 (-target で Cloud Run をスキップ)
terraform apply -target="google_artifact_registry_repository.main"

# レジストリパスを取得
$REPO = terraform output -raw artifact_registry_repository
```

プロジェクトルートに戻ってイメージをビルド & プッシュ:

```shell
cd ..

# Backend API (.NET 9)
docker build -f Dockerfile.api -t "$REPO/api:1" .
docker push "$REPO/api:1"

# Frontend (React Router v7) — 初回は VITE_API_URL 未設定で仮ビルド
docker build -f Dockerfile.web -t "$REPO/web:1" .
docker push "$REPO/web:1"
```

### Phase 2 — Cloud Run サービスの作成 & フロントエンド再ビルド

```shell
cd gcp_terraform

# 全リソースを作成 (イメージが存在するので Cloud Run も成功する)
terraform apply

# 出力値を取得
$BACKEND_URL  = terraform output -raw backend_url
$FRONTEND_URL = terraform output -raw frontend_url
```

Backend URL 確定後、フロントエンドを正しい API URL で再ビルド:

```shell
cd ..

docker build -f Dockerfile.web `
  --build-arg "VITE_API_URL=$BACKEND_URL" `
  -t "$REPO/web:1" .
docker push "$REPO/web:1"

# Cloud Run を最新イメージに更新
cd gcp_terraform
gcloud run deploy (terraform output -raw frontend_service_name) `
  --image "$REPO/web:1" `
  --region asia-northeast1
```

### CORS の絞り込み (推奨)

初期値は `cors_allowed_origins = "*"` なので、フロントエンド URL 確定後に絞り込む:

```shell
(Get-Content terraform.tfvars) -replace 'cors_allowed_origins\s*=.*', "cors_allowed_origins = `"$FRONTEND_URL`"" |
  Set-Content terraform.tfvars
terraform apply
```

## 2 回目以降のデプロイ

イメージが既に存在するので、ビルド → プッシュ → apply のみ:

```shell
cd gcp_terraform
$REPO        = terraform output -raw artifact_registry_repository
$BACKEND_URL = terraform output -raw backend_url

cd ..

# Backend
docker build -f Dockerfile.api -t "$REPO/api:1" .
docker push "$REPO/api:1"

# Frontend
docker build -f Dockerfile.web `
  --build-arg "VITE_API_URL=$BACKEND_URL" `
  -t "$REPO/web:1" .
docker push "$REPO/web:1"

# Cloud Run を更新
cd gcp_terraform
terraform apply
```

## 動作確認

```shell
# Backend ヘルスチェック
Invoke-RestMethod -Uri "$BACKEND_URL/api/games" -Method Post -ContentType "application/json"

# Frontend アクセス
Write-Host "Frontend: $FRONTEND_URL"
Start-Process $FRONTEND_URL
```

## リソース構成

| リソース | 用途 |
|---|---|
| Artifact Registry | Docker イメージ保管 |
| Cloud Run (`*-api`) | Backend — .NET 9 API (port 8080) |
| Cloud Run (`*-web`) | Frontend — React Router v7 SSR (port 8080) |

## 環境変数

| 変数 | サービス | 説明 |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | backend | `Production` |
| `CORS_ALLOWED_ORIGINS` | backend | フロントエンド URL |
| `VITE_API_URL` | frontend (ビルド時) | バックエンド URL |
| `PORT` | frontend | `8080` |
