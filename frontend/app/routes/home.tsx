import { useState } from "react";
import { useNavigate } from "react-router";
import { createGame } from "~/api/gameApi";

export function meta() {
  return [
    { title: "株売買シミュレーター" },
    { name: "description", content: "株売買シミュレーターゲーム" },
  ];
}

export default function HomePage() {
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(false);

  async function handleStart() {
    setIsLoading(true);
    const game = await createGame();
    navigate(`/games/${game.gameId}`);
  }

  return (
    <main className="flex items-center justify-center min-h-screen">
      <div className="text-center space-y-6">
        <h1 className="text-3xl font-bold">株売買シミュレーター</h1>
        <p className="text-gray-500">株取引を体験してみよう</p>
        <button
          onClick={handleStart}
          disabled={isLoading}
          className="bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white font-bold py-3 px-8 rounded-lg text-lg"
        >
          {isLoading ? "準備中..." : "ゲーム開始"}
        </button>
      </div>
    </main>
  );
}
