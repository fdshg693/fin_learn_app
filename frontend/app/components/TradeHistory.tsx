import { memo } from "react";
import type { TradeResultDto } from "~/types/game";
import { formatJPY } from "~/utils/format";

type Props = {
  trades: TradeResultDto[];
};

export const TradeHistory = memo(function TradeHistory({ trades }: Props) {
  if (trades.length === 0) return null;

  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-500 mb-2">直近の約定結果</h2>
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-gray-500">
            <th className="py-2">売買</th>
            <th className="py-2">銘柄 ID</th>
            <th className="py-2 text-right">約定数量</th>
            <th className="py-2 text-right">約定金額</th>
            <th className="py-2 text-right">手数料</th>
          </tr>
        </thead>
        <tbody>
          {[...trades].reverse().map((trade, i) => (
            <tr key={i} className="border-b">
              <td className={`py-2 font-medium ${trade.side === "Buy" ? "text-red-600" : "text-blue-600"}`}>
                {trade.side === "Buy" ? "買" : "売"}
              </td>
              <td className="py-2">{trade.instrumentId}</td>
              <td className="py-2 text-right">{trade.filledQuantity}</td>
              <td className="py-2 text-right">{formatJPY(trade.totalAmount)}</td>
              <td className="py-2 text-right">{formatJPY(trade.fee)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
});
