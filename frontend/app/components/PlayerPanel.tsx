type Props = {
  cash: number;
  totalAssets: number;
  profitLoss: number;
};

export function PlayerPanel({ cash, totalAssets, profitLoss }: Props) {
  const plColor = profitLoss >= 0 ? "text-green-600" : "text-red-600";
  const plSign = profitLoss >= 0 ? "+" : "";

  return (
    <div className="grid grid-cols-3 gap-4">
      <div className="bg-white dark:bg-gray-800 rounded-lg p-4 shadow">
        <p className="text-xs text-gray-500">現金</p>
        <p className="text-xl font-bold">¥{cash.toLocaleString()}</p>
      </div>
      <div className="bg-white dark:bg-gray-800 rounded-lg p-4 shadow">
        <p className="text-xs text-gray-500">総資産</p>
        <p className="text-xl font-bold">¥{totalAssets.toLocaleString()}</p>
      </div>
      <div className="bg-white dark:bg-gray-800 rounded-lg p-4 shadow">
        <p className="text-xs text-gray-500">損益</p>
        <p className={`text-xl font-bold ${plColor}`}>
          {plSign}¥{profitLoss.toLocaleString()}
        </p>
      </div>
    </div>
  );
}
