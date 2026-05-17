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
