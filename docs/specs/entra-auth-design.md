# Entra 認証導入 設計書

## 1. 目的とスコープ

### 目的

React SPA + JSON API に **Microsoft Entra ID 認証**を導入し、**アプリ全体のアクセス制限**を実現する。未認証ユーザーはゲーム作成・操作を一切行えない。

### 利用者

単一テナント（single tenant）の組織アカウント（Microsoft Entra ID / workforce アカウント）。社外ユーザーのセルフ登録・マルチテナントは対象外。

### 認証対象

| 対象 | 認証 |
|---|---|
| React 版フロントエンド（`frontend/`, 既定 localhost:5173） | 対象 |
| JSON API の `/api/games`・`/api/admin`（`src/FinLearn.Api`） | 対象 |
| HTMX 版（Razor Pages `/play`） | **スコープ外**（未保護のまま） |
| ナビゲーション `/`・静的ファイル | **スコープ外**（未保護のまま） |

### 採用アプローチ

**MSAL.js（SPA, Authorization Code + PKCE）+ Microsoft.Identity.Web（API の JWT Bearer 検証）。**
比較した他案と不採用理由:

- **App Service 組み込み認証（Easy Auth）** — API と Web が別 App Service のため Web→API のトークン受け渡し設定が煩雑。ローカル開発で認証を再現しにくく制御も効かない。
- **BFF（Backend-for-Frontend）** — React Router の Node SSR サーバ側に OIDC 実装が必要で構成が重く、現状の `clientLoader`/`clientAction` 中心の作りと相性が悪い。

採用案は標準的でローカル開発が壊れず、API がフロント実装に依存しない。

## 2. アーキテクチャ

```
[Browser]
  └─ React SPA (MSAL.js, PKCE) ──(Authorization: Bearer)──> [.NET Minimal API]
        │  未認証: ランディング + サインインボタン                │  Microsoft.Identity.Web
        │  認証済: 既存ゲーム画面                                  │  JWT 検証 → /api/* を [Authorize]
        └─ Entra ID (single tenant) <───── OIDC redirect ─────────┘
```

- React は public client（クライアントシークレットなし、PKCE）。
- API は Bearer トークンの検証のみ（クライアントシークレットなし）。
- 結果として **機密情報をリポジトリ・設定ファイルに一切持たない**（テナントID・クライアントID・Application ID URI はすべて公開情報）。

### Entra App Registration

単一テナント内に App Registration を **2つ**作成する（手作業の前提条件。本設計の実装では作成しない）。

1. **API 用 App Registration**
   - 「API の公開」でスコープ `access_as_user`（管理者/ユーザー同意、種別は任意）を定義。
   - Application ID URI: `api://<api-client-id>`。
2. **SPA 用 App Registration**
   - プラットフォーム: SPA（Single-page application）。
   - リダイレクト URI: 開発 `http://localhost:5173`、本番は Web App Service の URL。
   - API のアクセス許可: 上記 API の `access_as_user` を委任スコープとして追加し、管理者の同意を付与。

> 注: 1つの App Registration に SPA プラットフォームと公開スコープを同居させる単一登録構成も技術的には可能だが、API と SPA の責務分離・将来の権限管理のため本設計は2登録構成を正とする。

## 3. API 側設計（`src/FinLearn.Api`）

### 依存パッケージ

- `FinLearn.Api.csproj` に `Microsoft.Identity.Web` を追加。対象バージョンは **3.x 系の最新安定版**（.NET 9 対応。`Microsoft.Identity.Web` 3.5.0 以上を最低ラインとし、実装時に当時の最新安定版を確認）。

### `Program.cs` 変更

- `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));` を追加。
- 認可は **名前付きポリシー1つ**で表現する。`builder.Services.AddAuthorization(options => options.AddPolicy("ApiScope", policy => { policy.RequireAuthenticatedUser(); policy.RequireScope("access_as_user"); }));` を追加（`RequireScope` は `Microsoft.Identity.Web` の `AuthorizationPolicyBuilder` 拡張）。`[RequiredScope]` 属性やエンドポイント単位の `RequireScope` ラムダは**使わない**（機構を1つに統一）。
- ミドルウェア順序: `app.UseCors();` の後、エンドポイントマップの前に `app.UseAuthentication(); app.UseAuthorization();` を追加。
- 保護はルートグループ単位で適用する（**グローバルなフォールバックポリシーは設定しない**。Razor Pages・`/`・静的ファイルを未保護に保つため）。両グループに上記の名前付きポリシーを適用: `.RequireAuthorization("ApiScope")`。

> 実装メモ: `MapGameEndpoints` / `MapAdminEndpoints` は `RouteGroupBuilder` を return するが、現状の `Program.cs`（該当行: `app.MapGameEndpoints(); app.MapAdminEndpoints();`）は **戻り値を破棄している**。この2行を戻り値を受ける形に書き換え（例: `app.MapGameEndpoints().RequireAuthorization("ApiScope");` / `app.MapAdminEndpoints().RequireAuthorization("ApiScope");`）、エンドポイント定義ファイル（`GameEndpoints.cs` / `AdminEndpoints.cs`）自体の構造は変えない。呼び出し側の書き換えが必須である点に注意。

### 設定 (`AzureAd` セクション)

`appsettings.Development.json` に追加（すべて公開情報、シークレットなし）:

```jsonc
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "<tenant-id>",
  "ClientId": "<api-client-id>",
  "Audience": "api://<api-client-id>"
}
```

本番（Azure App Service）は同名キーをアプリ設定 `AzureAd__Instance` / `AzureAd__TenantId` / `AzureAd__ClientId` / `AzureAd__Audience` で上書きする。`appsettings.json`（本番既定）にはプレースホルダのみを置き、実値は環境側で注入する。

## 4. React 側設計（`frontend/app`）

### 依存パッケージ

- `@azure/msal-browser`（**4.x 系**の最新安定版を対象）、`@azure/msal-react`（**3.x 系**の最新安定版を対象。msal-browser 4.x と組み合わせるバージョン。実装時に当時の最新安定版を確認）。

### 新規モジュール

| ファイル | 役割 |
|---|---|
| `app/auth/msal.ts` | `PublicClientApplication` シングルトン生成。SSR ガード（`typeof window === "undefined"` 時はインスタンス未生成）。`loginRequest`（scopes: `openid`, `profile`）と `apiRequest`（scopes: `[<api-scope>]`）を定義。MSAL v3 は明示 `initialize()` が必要なため初期化を一元化。 |
| `app/auth/AuthGate.tsx` | `<MsalProvider>` で子をラップ。`UnauthenticatedTemplate` → ランディング+「サインイン」ボタン（`instance.loginRedirect(loginRequest)`）。`AuthenticatedTemplate` → 子（既存アプリ）+ ヘッダにアカウント名と「サインアウト」ボタン（`instance.logoutRedirect`）。SSR 中およびハイドレーション前は中立なローディング表示を返し、`useEffect` でハイドレーション後に MSAL を初期化する。 |
| `app/auth/token.ts` | `getAccessToken()`: アクティブアカウントで `acquireTokenSilent({ scopes: apiRequest.scopes })` を試行しアクセストークン文字列を返す。`InteractionRequiredAuthError` 時は `acquireTokenRedirect`（ブラウザは Entra へ遷移しページがアンロードされる＝呼び出し中の Promise は解決されない）。**アクティブアカウントが無い場合**は `acquireTokenRedirect` を呼んでログインへ誘導する（同じくページ遷移）。通常フローでは `AuthGate` が一次ゲートのため API 呼び出し時点でアカウントは存在する前提だが、本関数はその防御的フォールバックを担う。 |

### 既存ファイル変更

- `app/root.tsx` — `App()` の `<Outlet/>` を `<AuthGate>` でラップ（`Layout` の HTML 骨格は不変）。
- `app/api/gameApi.ts` — `apiFetch` 内で `getAccessToken()` を呼び、全リクエストに `Authorization: Bearer <token>` ヘッダを付与。既存のエクスポート関数（`createGame` 等）のシグネチャは不変。`ERROR_MESSAGES` に `401`・`403` を追加。**401 受信時の順序**: ① `getAccessToken()` を再実行（silent 更新を試行）し、`InteractionRequiredAuthError` 等で対話的再認証が必要なら `acquireTokenRedirect` が走る＝ページが Entra へ遷移し以降の処理は実行されない。② リダイレクトが発生しない（＝アカウントは有効だがトークンが拒否される）ケースのみ、`handleResponse` が日本語メッセージ（再サインインを促す）で例外を投げ、既存の `clientLoader`/`clientAction` の `try/catch`・`ErrorBoundary` が表示を担う。再試行の自動ループは行わない。403 は「権限がありません（再ログインしても解消しません）」旨のメッセージで例外を投げる（再認証は誘発しない）。
- `app/config.ts` — `VITE_ENTRA_CLIENT_ID`、`VITE_ENTRA_TENANT_ID`（authority 構築用）、`VITE_ENTRA_API_SCOPE`、`VITE_ENTRA_REDIRECT_URI` を追加。
- `home.tsx` / `routes/games.$id.tsx` — ロジック変更なし（トークンは `gameApi` 経由で透過的に付与される）。
- `app/routes.ts` — ルート構成変更なし（`AuthGate` は `root.tsx` で全ルートを横断的にラップするため）。

### 環境変数

| 変数 | 用途 | 例 |
|---|---|---|
| `VITE_ENTRA_CLIENT_ID` | SPA App Registration のクライアントID | `<spa-client-id>` |
| `VITE_ENTRA_TENANT_ID` | authority（`https://login.microsoftonline.com/<tenant-id>`）構築 | `<tenant-id>` |
| `VITE_ENTRA_API_SCOPE` | API アクセストークン要求スコープ | `api://<api-client-id>/access_as_user` |
| `VITE_ENTRA_REDIRECT_URI` | リダイレクトURI（環境別） | `http://localhost:5173` |

ビルド時に注入（Vite の `import.meta.env`）。すべて公開情報のためシークレット管理は不要。

## 5. データフロー

1. ブラウザが React にアクセス → SSR は中立ローディングを返す。
2. ハイドレーション後、MSAL を初期化。アカウント未検出 → `UnauthenticatedTemplate`（ランディング+「サインイン」）。
3. 「サインイン」→ `loginRedirect(loginRequest)` → Entra → リダイレクトバックでアカウント確定。
4. `AuthenticatedTemplate` で既存アプリ描画。`clientLoader` / `clientAction` が `gameApi` 経由で API を呼び出し、`apiFetch` が `getAccessToken()`（`acquireTokenSilent`）でトークンを取得し `Authorization: Bearer` を付与。
5. API が JWT の署名・issuer・audience とスコープ `access_as_user` を検証。通れば既存処理（`TurnProcessor` 等）に到達。

## 6. エラー処理

| 事象 | 挙動 |
|---|---|
| アクセストークン期限切れ（silent 更新可） | `acquireTokenSilent` が自動更新し透過的に継続 |
| silent 失敗（`InteractionRequiredAuthError`） | `acquireTokenRedirect` で対話的に再取得 |
| API が 401（無効/欠落トークン） | `gameApi` がまず silent 再取得を試行 → 必要なら `acquireTokenRedirect`（ページ遷移）。リダイレクトしない場合のみ日本語メッセージで例外を投げ既存 `ErrorBoundary` が表示（自動再試行ループなし） |
| API が 403（スコープ不足） | 再認証は誘発せず「権限がありません（再ログインしても解消しません）」で例外を投げ表示 |
| サインアウト | `logoutRedirect` → Entra ログアウト → ランディングへ戻る |

API 側は Microsoft.Identity.Web / JwtBearer の既定挙動に従い、認証失敗で 401、スコープ不足で 403 を返す。

## 7. テスト戦略（既存テストを壊さない）

### API（`tests/FinLearn.Api.Tests`）

- テスト専用 `TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>` を追加。常に認証成功し、`access_as_user` スコープ（`scp` クレーム）を持つ固定の偽ユーザーチケットを返す。
- `WebApplicationFactory<Program>` を継承したカスタムファクトリを追加し、`ConfigureTestServices` でテスト用認証スキームを既定スキームとして登録する。
- 既存の `GameApiTests`（`IClassFixture<WebApplicationFactory<Program>>`）はフィクスチャをカスタムファクトリへ差し替えるのみでグリーン維持（テストメソッド本体は不変）。
- 新規テスト:
  - `Authorization` ヘッダ無しのリクエスト → `401 Unauthorized`（テスト認証を無効化したクライアントで検証）。
  - スコープ無しトークン → `403 Forbidden`。
- `HtmxPagesTests` は `/play`（Razor Pages）が未保護のため**無改修**。`/api/*` のみ保護される点を回帰確認として明記。

### フロント（vitest）

- `@azure/msal-react` および `~/auth/msal` を `vi.mock` でモック化。
- 既存コンポーネントテスト（`GameHeader.test.tsx` 等）と `routes/games.$id.test.tsx`（`createRoutesStub` で loader/action をスタブ化）は MSAL に到達しないため**無改修**。
- 新規テスト:
  - `AuthGate`: 未認証 → サインインボタン表示、認証済 → 子コンポーネント描画。
  - `token.ts`: `acquireTokenSilent` 成功でトークン返却、`InteractionRequiredAuthError` で `acquireTokenRedirect` 呼び出し。

### CORS

既存 CORS はオリジンを設定値 `CORS_ALLOWED_ORIGINS`（未設定時は `http://localhost:5173`）から構築し、`AllowAnyHeader` + `AllowAnyMethod` を許可している（`Program.cs` の現行実装）。`Authorization` ヘッダは `AllowAnyHeader` で許容済み。Cookie を用いない（Bearer トークン方式）ため `AllowCredentials` は不要。**CORS のコード変更は不要**。ただし本番では「`CORS_ALLOWED_ORIGINS` に設定する Web オリジン」と「SPA App Registration のリダイレクト URI（`VITE_ENTRA_REDIRECT_URI`）」を同じ本番 Web URL で揃えて環境側に設定する必要がある（コードではなくデプロイ設定の前提条件。§2・§4 の環境別設定と整合させる）。

## 8. スコープ外（YAGNI）

- ユーザーとゲーム状態の紐付け・永続化（ゲームは引き続き匿名・インメモリ `GameStore`）。
- ロール/管理者権限の区別（認証済みの組織ユーザーは全機能を利用可。`/api/admin` も同一の認証要件）。
- HTMX 版 `/play` の認証。
- マルチテナント・社外ユーザーのセルフ登録（Entra External ID）。
- リフレッシュトークンのローテーション等の詳細（MSAL の標準挙動に委譲）。

## 9. 受け入れ基準

1. 未認証で React アプリを開くとランディング+「サインイン」が表示され、ゲーム画面は描画されない。
2. 組織アカウントでサインイン後、既存のゲーム作成・売買・待機が従来どおり動作する。
3. `Authorization` ヘッダ無しで `/api/games`・`/api/admin` を直接呼ぶと 401 が返る。
4. `/`・`/play`（HTMX）は認証なしで従来どおり動作する。
5. リポジトリ・設定ファイルにクライアントシークレット等の機密情報が含まれない。
6. 既存の API 統合テスト・フロントエンドテストがすべてグリーンのまま、上記の新規テストが追加される。
