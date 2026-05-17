# Entra 認証導入 実装プラン

**Goal:** React SPA + JSON API に Microsoft Entra ID 認証（単一テナント）を導入し、`/api/games`・`/api/admin` を JWT Bearer で保護する。未認証ユーザーは React アプリのゲーム画面に到達できない。HTMX 版 `/play`・`/`・静的ファイルは未保護のまま。

**Architecture:** SPA は MSAL.js（Authorization Code + PKCE, public client）でトークン取得。API は `Microsoft.Identity.Web` で Bearer トークンを検証し、ルートグループ単位に名前付きポリシー `ApiScope`（認証済み + スコープ `access_as_user`）を要求。グローバルフォールバックポリシーは設定しない（Razor Pages・`/`・静的ファイルを開いたままにするため）。機密情報はリポジトリに置かない（テナントID・クライアントID・Application ID URI はすべて公開情報）。

**Tech Stack:**
- API: .NET 9 Minimal API + `Microsoft.Identity.Web` 3.x（JWT Bearer 検証）
- フロント: React 19 + React Router v7 + `@azure/msal-browser` 4.x + `@azure/msal-react` 3.x
- テスト: xUnit + `WebApplicationFactory<Program>`（テスト用認証スキーム）、Vitest + Testing Library（`@azure/msal-react` をモック）

---

## File Structure

新規作成:

```
src/FinLearn.Api/
  （新規ファイルなし。Program.cs と appsettings に追記のみ）
tests/FinLearn.Api.Tests/
  TestAuthHandler.cs                    ← ヘッダ駆動のテスト用認証ハンドラ
  AuthTestWebApplicationFactory.cs      ← テスト用認証スキームを既定に差し替えるファクトリ
  AuthApiTests.cs                       ← 401 / 403 / /play 未保護の回帰テスト
frontend/
  .env.example                          ← VITE_ENTRA_* のテンプレート（コミット）
  app/auth/msal.ts                      ← PublicClientApplication シングルトン + リクエスト定義
  app/auth/token.ts                     ← getAccessToken()（silent → redirect フォールバック）
  app/auth/AuthGate.tsx                 ← MsalProvider + 認証/未認証テンプレート
  app/auth/token.test.ts                ← token.ts の単体テスト
  app/auth/AuthGate.test.tsx            ← AuthGate の単体テスト
  app/api/gameApi.test.ts               ← Bearer 付与・401/403 の単体テスト（新規）
docs/
  AUTH.md                               ← 認証構成ドキュメント
```

修正:

```
src/FinLearn.Api/
  FinLearn.Api.csproj                   ← Microsoft.Identity.Web パッケージ追加
  Program.cs                             ← 認証/認可ミドルウェア + ルートグループ保護
  appsettings.json                       ← AzureAd プレースホルダ（本番は環境変数で上書き）
  appsettings.Development.json           ← AzureAd セクション（公開情報のみ）
tests/FinLearn.Api.Tests/
  GameApiTests.cs                        ← フィクスチャをカスタムファクトリへ差し替え（本体不変）
  HtmxPagesTests.cs                      ← 同上（1行フィクスチャ差し替えのみ。/api/games 経由でゲーム作成するため）
frontend/
  app/config.ts                          ← VITE_ENTRA_* の読み出しと authority 構築
  app/root.tsx                           ← <Outlet/> を <AuthGate> でラップ
  app/api/gameApi.ts                     ← Authorization ヘッダ付与 + 401/403 処理
```

既存フロントコンポーネントテスト・`app/routes.ts`・`home.tsx`・`routes/games.$id.tsx` は**無改修**（設計書 §7 の通り）。`HtmxPagesTests.cs` は設計書 §7 では「無改修」とされているが、`/api/games` でゲームを作るため Task 2 で `GameApiTests` と同じ1行フィクスチャ差し替えのみ行う（テスト本体は不変。設計書側の見落としを補正）。CORS のコード変更は不要（`AllowAnyHeader` で `Authorization` 許容済み）。

---

## Tasks

1. [API パッケージと設定](entra-auth/01-api-package-config.md) — `Microsoft.Identity.Web` を追加し、`AzureAd` 設定を appsettings に置く（挙動変更なし）。
2. [テスト用認証基盤](entra-auth/02-test-auth-infra.md) — `TestAuthHandler` とカスタムファクトリを作り、`GameApiTests`・`HtmxPagesTests` のフィクスチャを差し替えてグリーン維持。
3. [API エンドポイント保護](entra-auth/03-api-protect-endpoints.md) — `Program.cs` に認証/認可を配線し、401/403 と `/play` 未保護を回帰テスト。
4. [フロント依存と設定](entra-auth/04-frontend-deps-config.md) — MSAL パッケージ追加、`config.ts` 拡張、`.env.example` 作成。
5. [MSAL シングルトンとトークン取得](entra-auth/05-msal-singleton-token.md) — `msal.ts`・`token.ts` を実装し、`token.ts` を TDD で検証。
6. [AuthGate と root 統合](entra-auth/06-authgate-root.md) — `AuthGate.tsx` を作り `root.tsx` でラップ、未認証/認証の表示を TDD で検証。
7. [gameApi に Bearer 付与](entra-auth/07-gameapi-bearer.md) — 全 API リクエストに `Authorization` を付け、401/403 を処理。TDD で検証。
8. [回帰確認とドキュメント](entra-auth/08-regression-docs.md) — 全テスト・型・ビルドを通し、`docs/AUTH.md` を追加して受け入れ基準を確認。
