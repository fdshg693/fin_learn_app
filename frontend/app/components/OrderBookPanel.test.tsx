import { describe, test, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OrderBookPanel } from "./OrderBookPanel";
import type { OrderBookResponse, OrderDto } from "~/types/game";
import { getOrderBook } from "~/api/gameApi";

vi.mock("~/api/gameApi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("~/api/gameApi")>();
  return { ...actual, getOrderBook: vi.fn() };
});

const getOrderBookMock = vi.mocked(getOrderBook);

function makeOrder(id: number): OrderDto {
  return {
    id,
    traderId: `trader-${id}`,
    instrumentId: 1,
    side: "Buy",
    type: "Limit",
    quantity: 1,
    price: 100 + id,
    stopPrice: null,
    createdAtTurn: 1,
  };
}

function makeBook(orders: OrderDto[], totalCount: number, page = 1, pageSize = 20): OrderBookResponse {
  return { orders, totalCount, page, pageSize };
}

describe("OrderBookPanel", () => {
  beforeEach(() => {
    getOrderBookMock.mockReset();
  });

  test("注文がない場合はメッセージを表示", () => {
    render(<OrderBookPanel gameId="g1" orderBook={makeBook([], 0)} />);
    expect(screen.getByText("注文なし")).toBeInTheDocument();
  });

  test("注文一覧を表示する", () => {
    const orders: OrderDto[] = [
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
    render(<OrderBookPanel gameId="g1" orderBook={makeBook(orders, 1)} />);

    expect(screen.getByText("買")).toBeInTheDocument();
    expect(screen.getByText("指値")).toBeInTheDocument();
    expect(screen.getByText("10")).toBeInTheDocument();
    expect(screen.getByText("¥1,500")).toBeInTheDocument();
    expect(screen.getByText("player-1")).toBeInTheDocument();
  });

  test("成行注文の価格は「-」と表示する", () => {
    const orders: OrderDto[] = [
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
    render(<OrderBookPanel gameId="g1" orderBook={makeBook(orders, 1)} />);

    expect(screen.getByText("売")).toBeInTheDocument();
    expect(screen.getByText("成行")).toBeInTheDocument();
  });

  test("totalCountが1ページに収まるときは前/次ボタンが両方disabled", () => {
    const orders = [makeOrder(1), makeOrder(2)];
    render(<OrderBookPanel gameId="g1" orderBook={makeBook(orders, 2)} pageSize={20} />);

    expect(screen.getByRole("button", { name: "前へ" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "次へ" })).toBeDisabled();
  });

  test("複数ページある場合の初期表示で「1–20 / 45」のようなページ情報が出る", () => {
    const orders = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));
    render(<OrderBookPanel gameId="g1" orderBook={makeBook(orders, 45, 1, 20)} pageSize={20} />);

    expect(screen.getByText("1–20 / 45")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "前へ" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "次へ" })).toBeEnabled();
  });

  test("「次へ」クリックで getOrderBook(id, 2, pageSize) が呼ばれ、結果で再描画", async () => {
    const user = userEvent.setup();
    const page1 = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));
    const page2 = [makeOrder(21), makeOrder(22)];
    getOrderBookMock.mockResolvedValueOnce(makeBook(page2, 22, 2, 20));

    render(<OrderBookPanel gameId="g1" orderBook={makeBook(page1, 22, 1, 20)} pageSize={20} />);

    await user.click(screen.getByRole("button", { name: "次へ" }));

    await waitFor(() => {
      expect(getOrderBookMock).toHaveBeenCalledWith("g1", 2, 20);
    });
    await waitFor(() => {
      expect(screen.getByText("21–22 / 22")).toBeInTheDocument();
    });
    // ページ 1 の注文は消えている
    expect(screen.queryByText("trader-1")).not.toBeInTheDocument();
    // ページ 2 の注文が見える
    expect(screen.getByText("trader-21")).toBeInTheDocument();
  });

  test("「前へ」クリックで getOrderBook(id, 1, pageSize) が呼ばれる", async () => {
    const user = userEvent.setup();
    const page2 = [makeOrder(21), makeOrder(22)];
    const page1 = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));

    // 初期 props は page 2 の状態を模擬するため、1 度「次へ」→「前へ」の流れ
    getOrderBookMock
      .mockResolvedValueOnce(makeBook(page2, 22, 2, 20))
      .mockResolvedValueOnce(makeBook(page1, 22, 1, 20));

    render(<OrderBookPanel gameId="g1" orderBook={makeBook(page1, 22, 1, 20)} pageSize={20} />);

    await user.click(screen.getByRole("button", { name: "次へ" }));
    await waitFor(() => expect(screen.getByText("21–22 / 22")).toBeInTheDocument());

    await user.click(screen.getByRole("button", { name: "前へ" }));
    await waitFor(() => {
      expect(getOrderBookMock).toHaveBeenLastCalledWith("g1", 1, 20);
    });
    await waitFor(() => expect(screen.getByText("1–20 / 22")).toBeInTheDocument());
  });

  test("fetchが失敗した場合はエラーメッセージをpanel内に表示する", async () => {
    const user = userEvent.setup();
    const page1 = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));
    getOrderBookMock.mockRejectedValueOnce(new Error("boom"));

    render(<OrderBookPanel gameId="g1" orderBook={makeBook(page1, 45, 1, 20)} pageSize={20} />);

    await user.click(screen.getByRole("button", { name: "次へ" }));

    await waitFor(() => {
      expect(screen.getByText("取得に失敗しました")).toBeInTheDocument();
    });
    // 元のページ 1 の表示は維持される
    expect(screen.getByText("trader-1")).toBeInTheDocument();
  });

  test("props.orderBook が新しく差し替わったらページ1にリセットされる", async () => {
    const user = userEvent.setup();
    const page1 = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));
    const page2 = [makeOrder(21), makeOrder(22)];
    getOrderBookMock.mockResolvedValueOnce(makeBook(page2, 22, 2, 20));

    const { rerender } = render(
      <OrderBookPanel gameId="g1" orderBook={makeBook(page1, 22, 1, 20)} pageSize={20} />,
    );

    // ページ 2 に移動
    await user.click(screen.getByRole("button", { name: "次へ" }));
    await waitFor(() => expect(screen.getByText("21–22 / 22")).toBeInTheDocument());

    // clientAction 後を模擬: 新しい orderBook が props で降ってくる
    const newFirstPage = [makeOrder(100)];
    rerender(
      <OrderBookPanel gameId="g1" orderBook={makeBook(newFirstPage, 1, 1, 20)} pageSize={20} />,
    );

    await waitFor(() => {
      expect(screen.getByText("1–1 / 1")).toBeInTheDocument();
    });
    expect(screen.getByText("trader-100")).toBeInTheDocument();
  });
});
