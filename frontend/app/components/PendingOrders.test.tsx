import { describe, test, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { PendingOrders } from "./PendingOrders";
import type { PendingOrderDto } from "~/types/game";

const order = (overrides: Partial<PendingOrderDto> = {}): PendingOrderDto => ({
  id: 1,
  instrumentId: 1,
  side: "Buy",
  type: "Limit",
  quantity: 5,
  price: 100,
  stopPrice: null,
  createdAtTurn: 1,
  expiresAtTurn: 4,
  ...overrides,
});

describe("PendingOrders", () => {
  test("空の場合は『未約定注文はありません』を表示する", () => {
    render(<PendingOrders orders={[]} currentTurn={1} />);
    expect(screen.getByText("未約定注文")).toBeInTheDocument();
    expect(screen.getByText("未約定注文はありません")).toBeInTheDocument();
  });

  test("指値買い注文を表示する", () => {
    render(<PendingOrders orders={[order()]} currentTurn={1} />);

    expect(screen.getByText("買")).toBeInTheDocument();
    expect(screen.getByText("指値")).toBeInTheDocument();
    expect(screen.getByText("5")).toBeInTheDocument();
    expect(screen.getByText("¥100")).toBeInTheDocument();
  });

  test("売り注文は『売』と表示される", () => {
    render(<PendingOrders orders={[order({ side: "Sell" })]} currentTurn={1} />);
    expect(screen.getByText("売")).toBeInTheDocument();
  });

  test("成行注文は『成行』と表示され価格はハイフン", () => {
    render(<PendingOrders orders={[order({ type: "Market", price: null })]} currentTurn={1} />);
    expect(screen.getByText("成行")).toBeInTheDocument();
    expect(screen.getByText("-")).toBeInTheDocument();
  });

  test("残ターンは expiresAtTurn - currentTurn", () => {
    render(<PendingOrders orders={[order({ expiresAtTurn: 5 })]} currentTurn={2} />);
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  test("期限超過時は残ターンを 0 で表示する", () => {
    render(<PendingOrders orders={[order({ expiresAtTurn: 2 })]} currentTurn={5} />);
    expect(screen.getByText("0")).toBeInTheDocument();
  });
});
