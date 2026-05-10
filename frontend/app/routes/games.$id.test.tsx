import { describe, test, expect } from "vitest";
import { createRoutesStub } from "react-router";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { GameResponse, OrderBookResponse } from "~/types/game";

const mockGame: GameResponse = {
  gameId: "test-game-1",
  turn: 5,
  player: {
    name: "テストプレイヤー",
    cash: 100000,
    positions: [{ instrumentId: 1, quantity: 10, currentPrice: 1500, amount: 15000 }],
    totalAssets: 115000,
    profitLoss: 15000,
    pendingOrders: [],
  },
  instruments: [
    { id: 1, price: 1500 },
    { id: 2, price: 3000 },
  ],
  recentTrades: [
    { instrumentId: 1, side: "Buy", filledQuantity: 10, totalAmount: 14000, fee: 0 },
  ],
  warning: null,
};

const mockOrderBook: OrderBookResponse = {
  orders: [
    {
      id: 1,
      traderId: "cpu-1",
      instrumentId: 1,
      side: "Sell",
      type: "Limit",
      quantity: 5,
      price: 1600,
      stopPrice: null,
      createdAtTurn: 4,
      expiresAtTurn: 6,
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 20,
};

async function renderGamePage(game: GameResponse, orderBook: OrderBookResponse, action?: () => unknown) {
  const { default: GamePage } = await import("./games.$id");

  const Stub = createRoutesStub([
    {
      path: "/games/:id",      
      Component: GamePage,
      loader() {
        return { game, orderBook };
      },
      ...(action ? { action } : {}),
    },
  ]);

  render(<Stub initialEntries={["/games/test-game-1"]} />);

  // createRoutesStub は hydration 中に重複レンダリングすることがある
  // main 要素が表示されるまで待つ
  const main = await screen.findByRole("main");
  return within(main);
}

describe("GamePage route", () => {
  test("ゲーム画面の全要素が表示される", async () => {
    const view = await renderGamePage(mockGame, mockOrderBook);

    // ターン・プレイヤー
    expect(view.getByText("ターン 5")).toBeInTheDocument();
    expect(view.getByText("テストプレイヤー")).toBeInTheDocument();

    // プレイヤー情報
    expect(view.getByText("¥100,000")).toBeInTheDocument();
    expect(view.getByText("¥115,000")).toBeInTheDocument();

    // 注文フォーム
    expect(view.getByText("買う")).toBeInTheDocument();
    expect(view.getByText("売る")).toBeInTheDocument();
    expect(view.getByText("待つ")).toBeInTheDocument();

    // 約定結果
    expect(view.getByText("直近の約定結果")).toBeInTheDocument();

    // 注文板
    expect(view.getByText("注文板")).toBeInTheDocument();
  });

  test("warningがある場合に警告を表示する", async () => {
    const gameWithWarning = { ...mockGame, warning: "現金不足です" };
    const view = await renderGamePage(gameWithWarning, mockOrderBook);

    expect(view.getByText("現金不足です")).toBeInTheDocument();
  });

  test("「待つ」ボタンでactionが呼ばれる", async () => {
    const user = userEvent.setup();
    const nextTurnGame = { ...mockGame, turn: 6 };

    const view = await renderGamePage(mockGame, mockOrderBook, () => {
      return { game: nextTurnGame, orderBook: mockOrderBook };
    });

    expect(view.getByText("ターン 5")).toBeInTheDocument();

    await user.click(view.getByText("待つ"));

    await waitFor(() => {
      expect(view.getByText("ターン 6")).toBeInTheDocument();
    });
  });
});
