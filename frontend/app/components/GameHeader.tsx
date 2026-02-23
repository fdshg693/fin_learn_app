type Props = {
  turn: number;
  playerName: string;
};

export function GameHeader({ turn, playerName }: Props) {
  return (
    <header className="flex items-center justify-between bg-gray-800 text-white px-6 py-3 rounded-lg">
      <h1 className="text-lg font-bold">ターン {turn}</h1>
      <span className="text-sm">{playerName}</span>
    </header>
  );
}
