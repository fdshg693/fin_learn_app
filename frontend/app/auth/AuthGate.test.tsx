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
