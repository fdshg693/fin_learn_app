# 認証構成（Microsoft Entra ID）

React SPA + JSON API に単一テナントの Entra ID 認証を導入している。設計詳細は
[docs/specs/entra-auth-design.md](specs/entra-auth-design.md)。

## 全体像

- React SPA: MSAL.js（Authorization Code + PKCE, public client）でサインインし、
  API 呼び出し時に `Authorization: Bearer <access_token>` を付与する。
- JSON API: `Microsoft.Identity.Web` が JWT の署名・issuer・audience・スコープ
  `access_as_user` を検証する。
- 機密情報なし: テナントID・クライアントID・Application ID URI はすべて公開情報。
  リポジトリにシークレットは持たない。

## 保護範囲

| 対象 | 認証 |
|---|---|
| React 版（`frontend/`, localhost:5173） | 対象（`AuthGate` が全ルートをラップ） |
| `/api/games`・`/api/admin` | 対象（名前付きポリシー `ApiScope`） |
| HTMX 版 `/play`（Razor Pages） | 未保護 |
| `/`・静的ファイル | 未保護 |

グローバルフォールバックポリシーは設定せず、ルートグループ単位で
`RequireAuthorization("ApiScope")` を適用している。

## Entra 側の設定（手作業の前提条件）

単一テナント内に App Registration を **2つ**作る（コードでは作成しない）。
いずれも「サポートされているアカウントの種類」は **この組織ディレクトリのみ（単一テナント）**。

### 1. API 用 App Registration

| 項目 | 設定 |
|---|---|
| Application ID URI | `api://<api-client-id>`（「API の公開」で設定。既定の GUID のままで可） |
| スコープ | 「API の公開」→ スコープの追加で `access_as_user` を定義（同意は管理者/ユーザーどちらでも可） |
| プラットフォーム | 不要（トークンを受け取る側。リダイレクト URI は持たない） |
| クライアントシークレット | **作らない**（検証は署名・issuer・audience のみ。シークレット不要） |

→ ここで決まる値: `AzureAd:ClientId` = この登録の **クライアントID**、
`AzureAd:Audience` = `api://<api-client-id>`、`VITE_ENTRA_API_SCOPE` = `api://<api-client-id>/access_as_user`。

### 2. SPA 用 App Registration

| 項目 | 設定 |
|---|---|
| プラットフォーム | **SPA（Single-page application）**（「Web」ではない。PKCE 前提でシークレット不可） |
| リダイレクト URI | 下表（ローカル・本番の両方を**同じ登録に複数追加**してよい） |
| API のアクセス許可 | 上記 API の委任スコープ `access_as_user` を追加 → **「管理者の同意を与える」を実行** |
| クライアントシークレット | **作らない**（public client + PKCE） |

→ ここで決まる値: `VITE_ENTRA_CLIENT_ID` = この登録の **クライアントID**、
`VITE_ENTRA_TENANT_ID` = テナントID（両登録で共通）。

### リダイレクト URI（ローカル / 本番）

MSAL.js は SPA のオリジンへ戻るだけなので、リダイレクト URI = **アプリのトップ URL**
（パスは付けない）。SPA 登録の「リダイレクト URI」に以下を**両方とも**登録しておけば
ローカルと本番で登録を分ける必要はない。

| 環境 | SPA 登録に追加するリダイレクト URI | フロント `VITE_ENTRA_REDIRECT_URI` |
|---|---|---|
| ローカル開発 | `http://localhost:5173` | `http://localhost:5173` |
| 本番 | `https://<your-app>.azurewebsites.net` | 同左（本番ビルド時に注入する値） |

注意点:
- ここに登録した URL と、ビルド時の `VITE_ENTRA_REDIRECT_URI`、実際に配信される
  Web のオリジンが**完全一致**（スキーム・ホスト・ポート、末尾スラッシュ無し）でないと
  `AADSTS50011`（redirect URI 不一致）になる。
- 本番 URL はカスタムドメインを使う場合そのドメインを登録する。
- サインアウト後の戻り先を制御したい場合のみ、SPA 登録の
  「フロントチャネル ログアウト URL／ログアウト後リダイレクト URI」にトップ URL を設定
  （未設定でも `logoutRedirect` は動作する）。

## API 設定（`AzureAd` セクション）

`appsettings.json` はプレースホルダのみ。本番（Azure App Service）は環境変数
`AzureAd__Instance` / `AzureAd__TenantId` / `AzureAd__ClientId` / `AzureAd__Audience`
で上書きする。`appsettings.Development.json` に開発用 App Registration の公開値を置く。

## フロント環境変数

`frontend/.env.example` を `frontend/.env`（gitignore 済み）へコピーして設定する:
`VITE_ENTRA_CLIENT_ID` / `VITE_ENTRA_TENANT_ID` / `VITE_ENTRA_API_SCOPE` /
`VITE_ENTRA_REDIRECT_URI`。ビルド時に Vite の `import.meta.env` で注入。

## デプロイ時の整合条件（コードではなく環境設定）

- `CORS_ALLOWED_ORIGINS` の Web オリジンと SPA App Registration のリダイレクト
  URI（`VITE_ENTRA_REDIRECT_URI`）を同一の本番 Web URL で揃える。

## テスト

- API: `tests/FinLearn.Api.Tests/TestAuthHandler.cs` がヘッダ `X-Test-Auth` で
  認証挙動を切り替える（既定=認証成功+scp、`none`=未認証、`noscope`=scp なし）。
  `AuthTestWebApplicationFactory` がテスト用スキームを既定に差し替える。
- フロント: `@azure/msal-react` と `~/auth/msal` をモックして `AuthGate` /
  `token.ts` / `gameApi` を検証する。
