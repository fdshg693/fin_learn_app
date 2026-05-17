# Task 8: 回帰確認とドキュメント

[← Back to plan](../entra-auth.md)

全テスト・型・ビルドを通し、`docs/AUTH.md` を追加して認証構成を文書化し、設計書 §9 の受け入れ基準を1つずつ確認する。

**Files:**
- Create: `docs/AUTH.md`
- Test: 全テストスイート（追加なし、回帰確認のみ）

---

- [ ] **Step 1: API 側の全テストを実行**

Run: `dotnet test`
Expected: 全件 PASS。`GameApiTests`（テスト用認証で従来通り）・`HtmxPagesTests`（`/play` 未保護で従来通り）・`AuthApiTests`（401/403/未保護回帰）・`FinLearn.Tests`（ドメイン、無影響）すべて緑。

- [ ] **Step 2: フロント側の型チェック・テスト・本番ビルドを実行**

Run: `cd frontend; npm run typecheck; npm test; npm run build`
Expected: すべて成功。`build`（SSR + クライアント）が `AuthGate` の SSR ガード込みで通ること。

- [ ] **Step 3: リポジトリにシークレットが混入していないことを確認**

Run: `git grep -nE "client_secret|ClientSecret|VITE_ENTRA_CLIENT_SECRET" -- ":!docs/writing-plans"`
Expected: ヒット0件（公開情報のみ。`appsettings*.json` と `.env.example` はテナントID/クライアントID/Application ID URI のみ）。

> `git grep` がヒット0件のとき終了コードは1になる。出力が空であることを確認すればよい。

- [ ] **Step 4: `docs/AUTH.md` を作成**

`docs/AUTH.md`:

```markdown
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
```

- [ ] **Step 5: `update-docs` スキルで既存ドキュメントの整合を確認**

Run: `update-docs` スキルを起動し、本実装（Entra 認証導入）で更新が必要な `CLAUDE.md` / `.claude/rules` 配下を点検する。少なくとも次の2ファイルに追記されることを想定する（スキルの判断で過不足は調整。最低ラインとして under-document を避ける目安）:

- `.claude/rules/src/api-project.md` — Design Decisions に「`/api/games`・`/api/admin` は名前付きポリシー `ApiScope`（認証済み + scope `access_as_user`）で保護。`/play`・`/`・静的ファイルは未保護（グローバルフォールバックポリシー無し）。テストは `AuthTestWebApplicationFactory` + `TestAuthHandler`」を追記。
- `.claude/rules/big_picture/frontend.md` — Architecture に「全ルートを `app/auth/AuthGate.tsx` がラップし、未認証はサインイン画面。API 呼び出しは `gameApi` が `Authorization: Bearer` を透過付与」を追記。

いずれも本文末尾または該当節に `docs/AUTH.md` への参照リンクを張る。

Expected: スキルの指示に従い必要箇所のみ追記される（プレースホルダや過剰な書き換えをしない）。

- [ ] **Step 6: 受け入れ基準（設計書 §9）を1つずつ確認**

以下を目視/コマンドで確認する:

1. 未認証で React を開くとランディング+「サインイン」が出てゲーム画面は描画されない → `app/auth/AuthGate.test.tsx` の「未認証」ケースで担保。
2. サインイン後に既存のゲーム作成・売買・待機が従来通り動く → `gameApi` シグネチャ不変・既存ルートテスト緑で担保。
3. `Authorization` 無しで `/api/games`・`/api/admin` を叩くと 401 → `AuthApiTests` の 401 ケースで担保。
4. `/`・`/play` は認証なしで従来通り → `AuthApiTests` の `/`・`/play` 200 ケース、`HtmxPagesTests` 無改修で担保。
5. リポジトリ・設定ファイルにシークレットが含まれない → Step 3 の `git grep` で担保。
6. 既存テストが全てグリーンのまま新規テストが追加された → Step 1・2 の全体実行で担保。

すべて満たされていることを確認する。満たさない項目があれば該当 Task に戻って修正する。

- [ ] **Step 7: コミット**

```powershell
git add docs/AUTH.md
git add -A -- ":(glob)**/CLAUDE.md" ".claude/rules"
git commit -m "docs: add Entra auth configuration guide and update cross-cutting docs"
```
