import type { GameResponse, OrderRequest, OrderBookResponse } from "~/types/game";

const BASE = import.meta.env.VITE_API_URL ?? "http://localhost:5088";

const ERROR_MESSAGES: Record<number, string> = {
  400: "リクエストが不正です。入力内容を確認してください。",
  404: "ゲームが見つかりません。",
  500: "サーバーでエラーが発生しました。しばらく待ってから再試行してください。",
};

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const message = ERROR_MESSAGES[res.status] ?? `エラーが発生しました（${res.status}）`;
    throw new Error(message);
  }
  return res.json();
}

export async function createGame(): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games`, { method: "POST" });
  return handleResponse(res);
}

export async function getGame(id: string): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games/${id}`);
  return handleResponse(res);
}

export async function placeOrder(id: string, order: OrderRequest): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games/${id}/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(order),
  });
  return handleResponse(res);
}

export async function wait(id: string): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games/${id}/wait`, { method: "POST" });
  return handleResponse(res);
}

export async function getOrderBook(
  id: string,
  page?: number,
  pageSize?: number,
): Promise<OrderBookResponse> {
  const params = new URLSearchParams();
  if (page !== undefined) params.set("page", String(page));
  if (pageSize !== undefined) params.set("pageSize", String(pageSize));
  const query = params.toString();
  const url = `${BASE}/api/admin/games/${id}/orderbook${query ? `?${query}` : ""}`;
  const res = await fetch(url);
  return handleResponse(res);
}
