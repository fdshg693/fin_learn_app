import { memo } from "react";
import type { PendingOrderDto } from "~/types/game";
import { OrderSide, OrderType } from "~/lib/enums";
import { formatJPY } from "~/utils/format";

type Props = {
  orders: PendingOrderDto[];
  currentTurn: number;
};

export const PendingOrders = memo(function PendingOrders({ orders, currentTurn }: Props) {
  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-500 mb-2">未約定注文</h2>
      {orders.length === 0 ? (
        <p className="text-sm text-gray-400">未約定注文はありません</p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-gray-500">
              <th className="py-2">売買</th>
              <th className="py-2">銘柄 ID</th>
              <th className="py-2">種類</th>
              <th className="py-2 text-right">数量</th>
              <th className="py-2 text-right">価格</th>
              <th className="py-2 text-right">残ターン</th>
            </tr>
          </thead>
          <tbody>
            {orders.map((order) => (
              <tr
                key={order.id}
                className={`border-b ${
                  order.side === OrderSide.Buy
                    ? "text-red-600 dark:text-red-400"
                    : "text-blue-600 dark:text-blue-400"
                }`}
              >
                <td className="py-2 font-medium">
                  {order.side === OrderSide.Buy ? "買" : "売"}
                </td>
                <td className="py-2">{order.instrumentId}</td>
                <td className="py-2">{order.type === OrderType.Limit ? "指値" : "成行"}</td>
                <td className="py-2 text-right">{order.quantity}</td>
                <td className="py-2 text-right">{order.price != null ? formatJPY(order.price) : "-"}</td>
                <td className="py-2 text-right">{Math.max(0, order.expiresAtTurn - currentTurn)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
});
