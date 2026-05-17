# Task 5: MSAL シングルトンとトークン取得

[← Back to plan](../entra-auth.md)

`app/auth/msal.ts`（`PublicClientApplication` シングルトン + 初期化の一元化 + リクエスト定義）と `app/auth/token.ts`（`getAccessToken()`）を実装する。`token.ts` は TDD で検証する（`~/auth/msal` をモック）。SSR ガード（`typeof window === "undefined"`）を入れ、サーバ側ではインスタンスを生成しない。

**Files:**
- Create: `frontend/app/auth/msal.ts`
- Create: `frontend/app/auth/token.ts`
- Test: `frontend/app/auth/token.test.ts`

---

- [ ] **Step 1: `msal.ts` を作成**

`frontend/app/auth/msal.ts`:

```ts
import { PublicClientApplication, type Configuration } from "@azure/msal-browser";
import {
  ENTRA_CLIENT_ID,
  ENTRA_AUTHORITY,
  ENTRA_REDIRECT_URI,
  ENTRA_API_SCOPE,
} from "~/config";

const configuration: Configuration = {
  auth: {
    clientId: ENTRA_CLIENT_ID,
    authority: ENTRA_AUTHORITY,
    redirectUri: ENTRA_REDIRECT_URI,
  },
  cache: {
    cacheLocation: "sessionStorage",
  },
};

export const loginRequest = { scopes: ["openid", "profile"] };
export const apiRequest = { scopes: [ENTRA_API_SCOPE] };

let instance: PublicClientApplication | null = null;
let initPromise: Promise<PublicClientApplication> | null = null;

/** SSR 中は null。ブラウザでは未生成なら生成して同一インスタンスを返す。 */
export function getMsalInstance(): PublicClientApplication | null {
  if (typeof window === "undefined") return null;
  if (!instance) {
    instance = new PublicClientApplication(configuration);
  }
  return instance;
}

/**
 * MSAL v3/v4 は明示 initialize() が必須。初期化＋リダイレクト応答処理＋
 * アクティブアカウント設定を一度だけ行い、結果を使い回す。
 */
export function initializeMsal(): Promise<PublicClientApplication> {
  if (typeof window === "undefined") {
    return Promise.reject(new Error("MSAL is browser-only"));
  }
  if (!initPromise) {
    const msal = getMsalInstance()!;
    initPromise = msal
      .initialize()
      .then(() => msal.handleRedirectPromise())
      .then((result) => {
        if (result?.account) {
          msal.setActiveAccount(result.account);
        } else if (!msal.getActiveAccount()) {
          const accounts = msal.getAllAccounts();
          if (accounts.length > 0) {
            msal.setActiveAccount(accounts[0]);
          }
        }
        return msal;
      });
  }
  return initPromise;
}
```

- [ ] **Step 2: `token.test.ts`（失敗テスト）を作成**

`frontend/app/auth/token.test.ts`:

```ts
import { describe, test, expect, vi, beforeEach } from "vitest";
import { InteractionRequiredAuthError } from "@azure/msal-browser";

const mockInstance = {
  getActiveAccount: vi.fn(),
  acquireTokenSilent: vi.fn(),
  acquireTokenRedirect: vi.fn(),
};

vi.mock("~/auth/msal", () => ({
  getMsalInstance: () => mockInstance,
  apiRequest: { scopes: ["api://api-client-id/access_as_user"] },
}));

import { getAccessToken } from "./token";

describe("getAccessToken", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  test("アクティブアカウントがあり silent 成功でトークン文字列を返す", async () => {
    mockInstance.getActiveAccount.mockReturnValue({ username: "u@example.com" });
    mockInstance.acquireTokenSilent.mockResolvedValue({ accessToken: "tok-123" });

    const token = await getAccessToken();

    expect(token).toBe("tok-123");
    expect(mockInstance.acquireTokenSilent).toHaveBeenCalledWith({
      scopes: ["api://api-client-id/access_as_user"],
      account: { username: "u@example.com" },
    });
    expect(mockInstance.acquireTokenRedirect).not.toHaveBeenCalled();
  });

  test("InteractionRequiredAuthError なら acquireTokenRedirect を呼ぶ", async () => {
    mockInstance.getActiveAccount.mockReturnValue({ username: "u@example.com" });
    mockInstance.acquireTokenSilent.mockRejectedValue(
      new InteractionRequiredAuthError("interaction_required"),
    );
    mockInstance.acquireTokenRedirect.mockResolvedValue(undefined);

    await expect(getAccessToken()).rejects.toThrow();
    expect(mockInstance.acquireTokenRedirect).toHaveBeenCalledWith({
      scopes: ["api://api-client-id/access_as_user"],
    });
  });

  test("アクティブアカウントが無ければ acquireTokenRedirect を呼ぶ", async () => {
    mockInstance.getActiveAccount.mockReturnValue(null);
    mockInstance.acquireTokenRedirect.mockResolvedValue(undefined);

    await expect(getAccessToken()).rejects.toThrow();
    expect(mockInstance.acquireTokenRedirect).toHaveBeenCalledWith({
      scopes: ["api://api-client-id/access_as_user"],
    });
    expect(mockInstance.acquireTokenSilent).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 3: テストが失敗することを確認**

Run: `cd frontend; npx vitest run app/auth/token.test.ts`
Expected: FAIL（`./token` が存在しない＝モジュール解決エラー）。

- [ ] **Step 4: `token.ts` を実装**

`frontend/app/auth/token.ts`:

```ts
import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { getMsalInstance, apiRequest } from "~/auth/msal";

/**
 * アクセストークン文字列を返す。
 * - アクティブアカウントが無い → acquireTokenRedirect（ページ遷移。Promise は解決しない）
 * - silent 成功 → accessToken を返す
 * - InteractionRequiredAuthError → acquireTokenRedirect（同上）
 * いずれのリダイレクト経路でも、遷移しなかった場合の防御として例外を投げる。
 */
export async function getAccessToken(): Promise<string> {
  const msal = getMsalInstance();
  if (!msal) {
    throw new Error("認証が初期化されていません。");
  }

  const account = msal.getActiveAccount();
  if (!account) {
    await msal.acquireTokenRedirect({ scopes: apiRequest.scopes });
    throw new Error("認証へリダイレクトしています。");
  }

  try {
    const result = await msal.acquireTokenSilent({
      scopes: apiRequest.scopes,
      account,
    });
    return result.accessToken;
  } catch (e) {
    if (e instanceof InteractionRequiredAuthError) {
      await msal.acquireTokenRedirect({ scopes: apiRequest.scopes });
      throw new Error("認証へリダイレクトしています。");
    }
    throw e;
  }
}
```

- [ ] **Step 5: テストが通ることを確認**

Run: `cd frontend; npx vitest run app/auth/token.test.ts`
Expected: PASS（3件）。

- [ ] **Step 6: 型チェックと全テストが壊れていないことを確認**

Run: `cd frontend; npm run typecheck; npm test`
Expected: 両方 PASS（既存テストは `~/auth/*` に到達しないため影響なし）。

- [ ] **Step 7: コミット**

```powershell
git add frontend/app/auth/msal.ts `
        frontend/app/auth/token.ts `
        frontend/app/auth/token.test.ts
git commit -m "feat(frontend): MSAL singleton and getAccessToken with redirect fallback"
```
