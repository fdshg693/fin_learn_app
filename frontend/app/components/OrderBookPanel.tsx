import { memo, useCallback, useEffect, useState } from "react";
import type { OrderBookResponse, OrderDto } from "~/types/game";
import { getOrderBook } from "~/api/gameApi";
import { formatJPY } from "~/utils/format";

export const ORDERBOOK_PAGE_SIZE = 20;

type Props = {
  gameId: string;
  orderBook: OrderBookResponse;
  pageSize?: number;
};

export const OrderBookPanel = memo(function OrderBookPanel({
  gameId,
  orderBook,
  pageSize = ORDERBOOK_PAGE_SIZE,
}: Props) {
  const [currentBook, setCurrentBook] = useState<OrderBookResponse>(orderBook);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // props.orderBook が差し替わったらページ 1 にリセット
  useEffect(() => {
    setCurrentBook(orderBook);
    setError(null);
  }, [orderBook]);

  const totalPages = Math.max(1, Math.ceil(currentBook.totalCount / pageSize));
  const currentPage = currentBook.page;
  const canPrev = currentPage > 1 && !isLoading;
  const canNext = currentPage < totalPages && !isLoading;

  const rangeStart = currentBook.orders.length === 0
    ? 0
    : (currentPage - 1) * pageSize + 1;
  const rangeEnd = (currentPage - 1) * pageSize + currentBook.orders.length;

  const goToPage = useCallback(
    async (nextPage: number) => {
      setIsLoading(true);
      setError(null);
      try {
        const book = await getOrderBook(gameId, nextPage, pageSize);
        setCurrentBook(book);
      } catch {
        setError("取得に失敗しました");
      } finally {
        setIsLoading(false);
      }
    },
    [gameId, pageSize],
  );

  const onPrev = useCallback(() => goToPage(currentPage - 1), [goToPage, currentPage]);
  const onNext = useCallback(() => goToPage(currentPage + 1), [goToPage, currentPage]);

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h2 className="text-sm font-semibold text-gray-500">注文板</h2>
        {currentBook.totalCount > 0 && (
          <div className="flex items-center gap-2 text-sm text-gray-500">
            <span>
              {rangeStart}–{rangeEnd} / {currentBook.totalCount}
            </span>
            {isLoading && <span className="text-xs">読み込み中...</span>}
            <button
              type="button"
              onClick={onPrev}
              disabled={!canPrev}
              className="px-2 py-1 border rounded disabled:opacity-40 disabled:cursor-not-allowed"
            >
              前へ
            </button>
            <button
              type="button"
              onClick={onNext}
              disabled={!canNext}
              className="px-2 py-1 border rounded disabled:opacity-40 disabled:cursor-not-allowed"
            >
              次へ
            </button>
          </div>
        )}
      </div>
      {error && <p className="text-sm text-red-600 mb-2">{error}</p>}
      {currentBook.orders.length === 0 && !error ? (
        <p className="text-sm text-gray-400">注文なし</p>
      ) : currentBook.orders.length === 0 ? null : (
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-gray-500">
              <th className="py-2">ID</th>
              <th className="py-2">銘柄</th>
              <th className="py-2">売買</th>
              <th className="py-2">種類</th>
              <th className="py-2 text-right">数量</th>
              <th className="py-2 text-right">価格</th>
              <th className="py-2 text-right">ストップ</th>
              <th className="py-2 text-right">ターン</th>
              <th className="py-2">トレーダー</th>
            </tr>
          </thead>
          <tbody>
            {currentBook.orders.map((order: OrderDto) => (
              <tr
                key={order.id}
                className={`border-b ${
                  order.side === "Buy"
                    ? "text-red-600 dark:text-red-400"
                    : "text-blue-600 dark:text-blue-400"
                }`}
              >
                <td className="py-2">{order.id}</td>
                <td className="py-2">{order.instrumentId}</td>
                <td className="py-2">{order.side === "Buy" ? "買" : "売"}</td>
                <td className="py-2">{order.type === "Limit" ? "指値" : "成行"}</td>
                <td className="py-2 text-right">{order.quantity}</td>
                <td className="py-2 text-right">{order.price != null ? formatJPY(order.price) : "-"}</td>
                <td className="py-2 text-right">{order.stopPrice != null ? formatJPY(order.stopPrice) : "-"}</td>
                <td className="py-2 text-right">{order.createdAtTurn}</td>
                <td className="py-2">{order.traderId}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
});
