import { memo } from "react";
import type { PositionDto } from "~/types/game";

type Props = {
  positions: PositionDto[];
};

export const PositionList = memo(function PositionList({ positions }: Props) {
  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-500 mb-2">保有ポジション</h2>
      {positions.length === 0 ? (
        <p className="text-sm text-gray-400">ポジションなし</p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-gray-500">
              <th className="py-2">銘柄 ID</th>
              <th className="py-2 text-right">数量</th>
              <th className="py-2 text-right">現在価格</th>
              <th className="py-2 text-right">評価額</th>
            </tr>
          </thead>
          <tbody>
            {positions.map((pos) => (
              <tr key={pos.instrumentId} className="border-b">
                <td className="py-2">{pos.instrumentId}</td>
                <td className="py-2 text-right">{pos.quantity}</td>
                <td className="py-2 text-right">¥{pos.currentPrice.toLocaleString()}</td>
                <td className="py-2 text-right">¥{pos.amount.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
});
