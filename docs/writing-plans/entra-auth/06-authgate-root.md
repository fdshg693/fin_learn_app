# Task 6: AuthGate と root 統合

[← Back to plan](../entra-auth.md)

`AuthGate.tsx` を作り、`root.tsx` の `<Outlet/>` をラップする。SSR 中・ハイドレーション前は中立なローディングを返し、`useEffect` で MSAL を初期化してから `<MsalProvider>` を描画する。未認証はランディング+「サインイン」、認証済はアカウント名+「サインアウト」ヘッダ+子。`@azure/msal-react` と `~/auth/msal` をモックして TDD で検証する。

**Files:**
- Create: `frontend/app/auth/AuthGate.tsx`
- Test: `frontend/app/auth/AuthGate.test.tsx`
- Modify: `frontend/app/root.tsx:31-33`

---

- [ ] **Step 1: `AuthGate.test.tsx`（失敗テスト）を作成**

`frontend/app/auth/AuthGate.test.tsx`:

```tsx
import React from "react";
import { describe, test, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

const state = vi.hoisted(() => ({ authed: false }));
const loginRedirect = vi.hoisted(() => vi.fn());
const logoutRedirect = vi.hoisted(() => vi.fn());

vi.mock("~/auth/msal", () => ({
  initializeMsal: vi.fn().mockResolvedValue({}),
  getMsalInstance: vi.fn(() => ({})),
  loginRequest: { scopes: ["openid", "profile"] },
  apiRequest: { scopes: ["api://x/access_as_user"] },
}));

vi.mock("@azure/msal-react", () => ({
  MsalProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  AuthenticatedTemplate: ({ children }: { children: React.ReactNode }) =>
    state.authed ? <>{children}</> : null,
  UnauthenticatedTemplate: ({ children }: { children: React.ReactNode }) =>
    state.authed ? null : <>{children}</>,
  useMsal: () => ({
    instance: { loginRedirect, logoutRedirect },
    accounts: state.authed ? [{ name: "テスト太郎", username: "t@example.com" }] : [],
    inProgress: "none",
  }),
}));

import { AuthGate } from "./AuthGate";

describe("AuthGate", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  test("未認証ならサインインボタンを表示し、子は描画しない", async () => {
    state.authed = false;
    render(
      <AuthGate>
        <div>protected-content</div>
      </AuthGate>,
    );

    const button = await screen.findByRole("button", { name: "サインイン" });
    expect(button).toBeInTheDocument();
    expect(screen.queryByText("protected-content")).not.toBeInTheDocument();
  });

  test("認証済なら子とサインアウトを表示する", async () => {
    state.authed = true;
    render(
      <AuthGate>
        <div>protected-content</div>
      </AuthGate>,
    );

    await waitFor(() => {
      expect(screen.getByText("protected-content")).toBeInTheDocument();
    });
    expect(screen.getByText("テスト太郎")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "サインアウト" })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `cd frontend; npx vitest run app/auth/AuthGate.test.tsx`
Expected: FAIL（`./AuthGate` が存在しない）。

- [ ] **Step 3: `AuthGate.tsx` を実装**

`frontend/app/auth/AuthGate.tsx`:

```tsx
import { useEffect, useState } from "react";
import {
  MsalProvider,
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import type { IPublicClientApplication } from "@azure/msal-browser";
import { initializeMsal, loginRequest } from "~/auth/msal";

function Loading() {
  return (
    <main className="flex items-center justify-center min-h-screen">
      <p className="text-gray-500 text-lg animate-pulse">読み込み中...</p>
    </main>
  );
}

function SignInScreen() {
  const { instance } = useMsal();
  return (
    <main className="flex items-center justify-center min-h-screen">
      <div className="text-center space-y-6">
        <h1 className="text-3xl font-bold">株売買シミュレーター</h1>
        <p className="text-gray-500">続けるにはサインインしてください</p>
        <button
          onClick={() => instance.loginRedirect(loginRequest)}
          className="bg-blue-600 hover:bg-blue-700 text-white font-bold py-3 px-8 rounded-lg text-lg"
        >
          サインイン
        </button>
      </div>
    </main>
  );
}

function AccountHeader() {
  const { instance, accounts } = useMsal();
  const name = accounts[0]?.name ?? accounts[0]?.username ?? "";
  return (
    <div className="flex items-center justify-end gap-3 bg-gray-100 px-4 py-2 text-sm">
      <span>{name}</span>
      <button
        onClick={() => instance.logoutRedirect()}
        className="text-blue-600 hover:underline"
      >
        サインアウト
      </button>
    </div>
  );
}

export function AuthGate({ children }: { children: React.ReactNode }) {
  const [instance, setInstance] = useState<IPublicClientApplication | null>(null);

  useEffect(() => {
    let mounted = true;
    initializeMsal()
      .then((msal) => {
        if (mounted) setInstance(msal);
      })
      .catch(() => {
        /* SSR 拒否やブラウザ未対応時はローディングのまま */
      });
    return () => {
      mounted = false;
    };
  }, []);

  if (!instance) {
    return <Loading />;
  }

  return (
    <MsalProvider instance={instance}>
      <UnauthenticatedTemplate>
        <SignInScreen />
      </UnauthenticatedTemplate>
      <AuthenticatedTemplate>
        <AccountHeader />
        {children}
      </AuthenticatedTemplate>
    </MsalProvider>
  );
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `cd frontend; npx vitest run app/auth/AuthGate.test.tsx`
Expected: PASS（2件）。

- [ ] **Step 5: `root.tsx` の Outlet を AuthGate でラップ**

`frontend/app/root.tsx` の import に1行追加:

```tsx
import type { Route } from "./+types/root";
import { AuthGate } from "~/auth/AuthGate";
import "./app.css";
```

`App()`（31-33 行）を変更:

変更前:

```tsx
export default function App() {
  return <Outlet />;
}
```

変更後:

```tsx
export default function App() {
  return (
    <AuthGate>
      <Outlet />
    </AuthGate>
  );
}
```

> `Layout` の HTML 骨格は不変。`ErrorBoundary` も不変。

- [ ] **Step 6: 型チェックと全テストが壊れていないことを確認**

Run: `cd frontend; npm run typecheck; npm test`
Expected: 両方 PASS。既存のルートテスト（`games.$id.test.tsx` 等）は `createRoutesStub` でルートコンポーネントを直接描画し `root.tsx` の `App` を通らないため `AuthGate` に到達せず無影響。

- [ ] **Step 7: コミット**

```powershell
git add frontend/app/auth/AuthGate.tsx `
        frontend/app/auth/AuthGate.test.tsx `
        frontend/app/root.tsx
git commit -m "feat(frontend): AuthGate wrapping Outlet with sign-in/out UI"
```
