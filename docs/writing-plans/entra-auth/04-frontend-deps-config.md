# Task 4: フロント依存と設定

[← Back to plan](../entra-auth.md)

MSAL パッケージを追加し、`config.ts` に Entra 用の設定値（すべて公開情報・シークレットなし）を追加する。`.env.example` をコミットし、ローカル `.env`（`.gitignore` 済み）にコピーして使う運用にする。`config.ts` はフォールバック付きで読むため、値未設定でも `npm test` / `npm run typecheck` は壊れない。

**Files:**
- Modify: `frontend/package.json`（依存追加。`npm install` が自動更新）
- Modify: `frontend/app/config.ts`
- Create: `frontend/.env.example`

すべての `npm` コマンドは `frontend/` ディレクトリで実行する。

---

- [ ] **Step 1: フロントの現状テスト・型がグリーンであることを確認（ベースライン）**

Run: `cd frontend; npm test; npm run typecheck`
Expected: 両方 PASS。以降の変更で既存テストが壊れないことの基準。

- [ ] **Step 2: MSAL パッケージを追加**

Run（`frontend/` で）: `npm install @azure/msal-browser@^4 @azure/msal-react@^3`
Expected: `package.json` の `dependencies` に `@azure/msal-browser`（4.x）と `@azure/msal-react`（3.x）が追加され、`package-lock.json` が更新される。

> `@azure/msal-react` 3.x は `@azure/msal-browser` 4.x を peer dependency とする組み合わせ。インストール時に peer 警告が出ないことを確認する。出る場合は表示された互換バージョンに合わせる。

- [ ] **Step 3: `config.ts` に Entra 設定を追加**

`frontend/app/config.ts` を以下に書き換える（既存3行はそのまま、Entra 用を追記）:

```ts
export const API_BASE_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5088";
export const DEFAULT_TIMEOUT_MS = 8000;
export const ORDERBOOK_PAGE_SIZE = 20;

export const ENTRA_CLIENT_ID = import.meta.env.VITE_ENTRA_CLIENT_ID ?? "";
export const ENTRA_TENANT_ID = import.meta.env.VITE_ENTRA_TENANT_ID ?? "";
export const ENTRA_API_SCOPE = import.meta.env.VITE_ENTRA_API_SCOPE ?? "";
export const ENTRA_REDIRECT_URI =
  import.meta.env.VITE_ENTRA_REDIRECT_URI ?? "http://localhost:5173";
export const ENTRA_AUTHORITY = `https://login.microsoftonline.com/${ENTRA_TENANT_ID}`;
```

- [ ] **Step 4: `.env.example` を作成**

`frontend/.env.example`:

```dotenv
# Microsoft Entra ID (single tenant) — all values are public, no secrets.
# Copy to frontend/.env (gitignored) and fill in your registration values.
VITE_ENTRA_CLIENT_ID=<spa-client-id>
VITE_ENTRA_TENANT_ID=<tenant-id>
VITE_ENTRA_API_SCOPE=api://<api-client-id>/access_as_user
VITE_ENTRA_REDIRECT_URI=http://localhost:5173
```

- [ ] **Step 5: 型チェックとテストが依然グリーンであることを確認**

Run: `cd frontend; npm run typecheck; npm test`
Expected: 両方 PASS。`config.ts` の追加 export はフォールバック付きで型エラーにならず、既存テストにも影響しない。

- [ ] **Step 6: コミット**

```powershell
git add frontend/package.json frontend/package-lock.json `
        frontend/app/config.ts frontend/.env.example
git commit -m "chore(frontend): add MSAL deps and Entra config values"
```
