import { describe, test, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { OrderBookPanel } from "./OrderBookPanel";

describe("OrderBookPanel", () => {
  test("注文がない場合はメッセージを表示", () => {
    render(<OrderBookPanel orders={[]} />);
    expect(screen.getByText("注文なし")).toBeInTheDocument();
  });

  test("注文一覧を表示する", () => {
    const orders = [
      {
        id: 1,
        traderId: "player-1",
        instrumentId: 1,
        side: "Buy",
        type: "Limit",
        quantity: 10,
        price: 1500,
        stopPrice: null,
        createdAtTurn: 1,
      },
    ];
    render(<OrderBookPanel orders={orders} />);

    expect(screen.getByText("買")).toBeInTheDocument();
    expect(screen.getByText("指値")).toBeInTheDocument();
    expect(screen.getByText("10")).toBeInTheDocument();
    expect(screen.getByText("¥1,500")).toBeInTheDocument();
    expect(screen.getByText("player-1")).toBeInTheDocument();
  });

  test("成行注文の価格は「-」と表示する", () => {
    const orders = [
      {
        id: 2,
        traderId: "cpu-1",
        instrumentId: 2,
        side: "Sell",
        type: "Market",
        quantity: 5,
        price: null,
        stopPrice: null,
        createdAtTurn: 2,
      },
    ];
    render(<OrderBookPanel orders={orders} />);

    expect(screen.getByText("売")).toBeInTheDocument();
    expect(screen.getByText("成行")).toBeInTheDocument();
  });
});
