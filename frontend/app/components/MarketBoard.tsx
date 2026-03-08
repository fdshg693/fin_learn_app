import { memo } from "react";
import type { InstrumentDto } from "~/types/game";
import { formatJPY } from "~/utils/format";

type Props = {
  instruments: InstrumentDto[];
  selectedId: number | null;
  onSelect: (id: number) => void;
};

export const MarketBoard = memo(function MarketBoard({ instruments, selectedId, onSelect }: Props) {
  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-500 mb-2">銘柄一覧</h2>
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-gray-500">
            <th className="py-2">銘柄 ID</th>
            <th className="py-2 text-right">現在価格</th>
          </tr>
        </thead>
        <tbody>
          {instruments.map((inst) => (
            <tr
              key={inst.id}
              onClick={() => onSelect(inst.id)}
              className={`cursor-pointer border-b hover:bg-gray-100 dark:hover:bg-gray-700 ${
                selectedId === inst.id ? "bg-blue-50 dark:bg-blue-900/30" : ""
              }`}
            >
              <td className="py-2">{inst.id}</td>
              <td className="py-2 text-right">{formatJPY(inst.price)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
});
