import { memo } from "react";
import { formatJPY } from "~/utils/format";

type Props = {
  cash: number;
  totalAssets: number;
  profitLoss: number;
};

function StatCard({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg p-4 shadow">
      <p className="text-xs text-gray-500">{label}</p>
      {children}
    </div>
  );
}

export const PlayerPanel = memo(function PlayerPanel({ cash, totalAssets, profitLoss }: Props) {
  const plColor = profitLoss >= 0 ? "text-green-600" : "text-red-600";
  const plSign = profitLoss >= 0 ? "+" : "";

  return (
    <div className="grid grid-cols-3 gap-4">
      <StatCard label="現金">
        <p className="text-xl font-bold">{formatJPY(cash)}</p>
      </StatCard>
      <StatCard label="総資産">
        <p className="text-xl font-bold">{formatJPY(totalAssets)}</p>
      </StatCard>
      <StatCard label="損益">
        <p className={`text-xl font-bold ${plColor}`}>
          {plSign}{formatJPY(profitLoss)}
        </p>
      </StatCard>
    </div>
  );
});
