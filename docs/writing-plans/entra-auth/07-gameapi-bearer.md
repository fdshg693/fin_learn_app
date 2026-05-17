# Task 7: gameApi に Bearer 付与

[← Back to plan](../entra-auth.md)

`gameApi.ts` の `apiFetch` で `getAccessToken()` を呼び、全リクエストに `Authorization: Bearer <token>` を付与する。`ERROR_MESSAGES` に 401・403 を追加。401 受信時はまず silent 再取得を試行（必要なら `getAccessToken` 内の `acquireTokenRedirect` でページ遷移）。リダイレクトしなければ日本語メッセージで例外を投げる（自動再試行ループなし）。403 は「権限がありません（再ログインしても解消しません）」で例外。既存エクスポート関数のシグネチャは不変。`~/auth/token` をモックして TDD で検証する。

**Files:**
- Modify: `frontend/app/api/gameApi.ts:1-30`
- Test: `frontend/app/api/gameApi.test.ts`

---

- [ ] **Step 1: `gameApi.test.ts`（失敗テスト）を作成**

`frontend/app/api/gameApi.test.ts`:

```ts
import { describe, test, expect, vi, beforeEach } from "vitest";

vi.mock("~/auth/token", () => ({
  getAccessToken: vi.fn(),
}));

import { getAccessToken } from "~/auth/token";
import { getGame } from "./gameApi";

const mockedToken = vi.mocked(getAccessToken);

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as unknown as Response;
}

describe("gameApi auth", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  test("全リクエストに Authorization: Bearer を付与する", async () => {
    mockedToken.mockResolvedValue("tok-abc");
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { gameId: "g1" }));
    vi.stubGlobal("fetch", fetchMock);

    await getGame("g1");

    const [, init] = fetchMock.mock.calls[0];
    const headers = new Headers(init.headers);
    expect(headers.get("Authorization")).toBe("Bearer tok-abc");

    vi.unstubAllGlobals();
  });

  test("401 は silent 再取得を試みた上で再サインインを促す例外を投げる", async () => {
    mockedToken.mockResolvedValue("tok-abc");
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(401, {}));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getGame("g1")).rejects.toThrow(
      "認証の有効期限が切れました。再度サインインしてください。",
    );
    // apiFetch で1回 + handleResponse の 401 分岐で1回 = 計2回
    expect(mockedToken).toHaveBeenCalledTimes(2);

    vi.unstubAllGlobals();
  });

  test("403 は権限なしメッセージの例外を投げる（再認証は誘発しない）", async () => {
    mockedToken.mockResolvedValue("tok-abc");
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(403, {}));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getGame("g1")).rejects.toThrow(
      "権限がありません（再ログインしても解消しません）。",
    );
    // apiFetch の1回のみ（403 分岐では再取得しない）
    expect(mockedToken).toHaveBeenCalledTimes(1);

    vi.unstubAllGlobals();
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `cd frontend; npx vitest run app/api/gameApi.test.ts`
Expected: FAIL（`getAccessToken` 未配線で Authorization 無し、401/403 メッセージ未定義）。

- [ ] **Step 3: `gameApi.ts` の冒頭を書き換え**

`frontend/app/api/gameApi.ts` の 1-30 行（import・`ERROR_MESSAGES`・`apiFetch`・`handleResponse`）を以下に置き換える。32 行目以降の `createGame` 等のエクスポート関数は**変更しない**:

```ts
import type { GameResponse, OrderRequest, OrderBookResponse } from "~/types/game";
import { API_BASE_URL, DEFAULT_TIMEOUT_MS } from "~/config";
import { getAccessToken } from "~/auth/token";

const ERROR_MESSAGES: Record<number, string> = {
  400: "リクエストが不正です。入力内容を確認してください。",
  401: "認証の有効期限が切れました。再度サインインしてください。",
  403: "権限がありません（再ログインしても解消しません）。",
  404: "ゲームが見つかりません。",
  500: "サーバーでエラーが発生しました。しばらく待ってから再試行してください。",
};

async function apiFetch(url: string, init?: RequestInit, timeoutMs: number = DEFAULT_TIMEOUT_MS): Promise<Response> {
  // トークン取得は try の外。acquireTokenRedirect の例外はそのまま伝播させる
  // （ネットワークエラー文言に変換しない）。
  const token = await getAccessToken();
  const headers = new Headers(init?.headers);
  headers.set("Authorization", `Bearer ${token}`);
  try {
    return await fetch(url, { ...init, headers, signal: AbortSignal.timeout(timeoutMs) });
  } catch (e) {
    if (e instanceof DOMException && e.name === "TimeoutError") {
      throw new Error("サーバーへの接続がタイムアウトしました。しばらく待ってから再試行してください。");
    }
    if (e instanceof TypeError) {
      throw new Error("サーバーに接続できません。ネットワーク接続を確認してください。");
    }
    throw e;
  }
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    if (res.status === 401) {
      // silent 更新を試行。対話的再認証が必要なら getAccessToken 内の
      // acquireTokenRedirect でページが Entra へ遷移し、以降は実行されない。
      // リダイレクトしなかった場合のみ再サインインを促す例外を投げる（再試行ループはしない）。
      await getAccessToken();
      throw new Error(ERROR_MESSAGES[401]);
    }
    const message = ERROR_MESSAGES[res.status] ?? `エラーが発生しました（${res.status}）`;
    throw new Error(message);
  }
  return res.json();
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `cd frontend; npx vitest run app/api/gameApi.test.ts`
Expected: PASS（3件）。

- [ ] **Step 5: 型チェックと全テストが壊れていないことを確認**

Run: `cd frontend; npm run typecheck; npm test`
Expected: 両方 PASS。既存のルートテストは loader/action をスタブ化し `gameApi` を呼ばないため無影響。

- [ ] **Step 6: コミット**

```powershell
git add frontend/app/api/gameApi.ts `
        frontend/app/api/gameApi.test.ts
git commit -m "feat(frontend): attach Bearer token and handle 401/403 in gameApi"
```
