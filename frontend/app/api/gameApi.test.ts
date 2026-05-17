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
