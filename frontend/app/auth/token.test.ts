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
