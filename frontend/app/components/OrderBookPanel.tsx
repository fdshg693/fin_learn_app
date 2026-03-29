import { memo } from "react";
import type { OrderDto } from "~/types/game";
import { formatJPY } from "~/utils/format";

type Props = {
  orders: OrderDto[];
};

export const OrderBookPanel = memo(function OrderBookPanel({ orders }: Props) {
  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-500 mb-2">注文板</h2>
      {orders.length === 0 ? (
        <p className="text-sm text-gray-400">注文なし</p>
      ) : (
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
            {orders.map((order) => (
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
