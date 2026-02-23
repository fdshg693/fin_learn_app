import type { GameResponse, OrderRequest } from "~/types/game";

const BASE = import.meta.env.VITE_API_URL ?? "http://localhost:5088";

export async function createGame(): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games`, { method: "POST" });
  if (!res.ok) throw new Error(`createGame failed: ${res.status}`);
  return res.json();
}

export async function getGame(id: string): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games/${id}`);
  if (!res.ok) throw new Error(`getGame failed: ${res.status}`);
  return res.json();
}

export async function buy(id: string, order: OrderRequest): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games/${id}/buy`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(order),
  });
  if (!res.ok) throw new Error(`buy failed: ${res.status}`);
  return res.json();
}

export async function sell(id: string, order: OrderRequest): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games/${id}/sell`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(order),
  });
  if (!res.ok) throw new Error(`sell failed: ${res.status}`);
  return res.json();
}

export async function wait(id: string): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games/${id}/wait`, { method: "POST" });
  if (!res.ok) throw new Error(`wait failed: ${res.status}`);
  return res.json();
}
