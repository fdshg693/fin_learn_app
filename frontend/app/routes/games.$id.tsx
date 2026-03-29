import { useState } from "react";
import { useLoaderData, useActionData, isRouteErrorResponse, type ClientLoaderFunctionArgs, type ClientActionFunctionArgs } from "react-router";
import { getGame, getOrderBook, buy, sell, wait } from "~/api/gameApi";
import type { Route } from "./+types/games.$id";
import type { GameResponse, OrderRequest, OrderBookResponse } from "~/types/game";
import { GameHeader } from "~/components/GameHeader";
import { PlayerPanel } from "~/components/PlayerPanel";
import { MarketBoard } from "~/components/MarketBoard";
import { PositionList } from "~/components/PositionList";
import { TradeForm } from "~/components/TradeForm";
import { WarningMessage } from "~/components/WarningMessage";
import { TradeHistory } from "~/components/TradeHistory";
import { OrderBookPanel } from "~/components/OrderBookPanel";

type LoaderData = {
  game: GameResponse;
  orderBook: OrderBookResponse;
};

export async function clientLoader({ params }: ClientLoaderFunctionArgs): Promise<LoaderData> {
  const id = params.id!;
  const [game, orderBook] = await Promise.all([
    getGame(id),
    getOrderBook(id),
  ]);
  return { game, orderBook };
}

export async function clientAction({ params, request }: ClientActionFunctionArgs): Promise<LoaderData> {
  const formData = await request.formData();
  const intent = formData.get("intent") as string;
  const id = params.id!;

  let game: GameResponse;

  if (intent === "wait") {
    game = await wait(id);
  } else {
    const instrumentId = Number(formData.get("instrumentId"));
    const quantity = Number(formData.get("quantity"));
    const priceRaw = formData.get("price");
    const price = priceRaw ? Number(priceRaw) : null;

    if (Number.isNaN(instrumentId) || Number.isNaN(quantity) || (price !== null && Number.isNaN(price))) {
      throw new Error("入力値が不正です。数値を入力してください。");
    }

    const order: OrderRequest = { instrumentId, quantity, price };

    if (intent === "buy") {
      game = await buy(id, order);
    } else if (intent === "sell") {
      game = await sell(id, order);
    } else {
      throw new Error(`Invalid intent: ${intent}`);
    }
  }

  const orderBook = await getOrderBook(id);
  return { game, orderBook };
}

export function HydrateFallback() {
  return (
    <main className="flex items-center justify-center min-h-screen">
      <p className="text-gray-500 text-lg animate-pulse">読み込み中...</p>
    </main>
  );
}

export default function GamePage() {
  const loaderData = useLoaderData<LoaderData>();
  const actionData = useActionData<LoaderData>();
  const { game, orderBook } = actionData ?? loaderData;
  const [selectedInstrumentId, setSelectedInstrumentId] = useState<number | null>(null);

  return (
    <main className="max-w-2xl mx-auto p-4 space-y-4">
      <GameHeader turn={game.turn} playerName={game.player.name} />
      <WarningMessage warning={game.warning} />
      <PlayerPanel
        cash={game.player.cash}
        totalAssets={game.player.totalAssets}
        profitLoss={game.player.profitLoss}
      />
      <div className="grid grid-cols-2 gap-4">
        <MarketBoard
          instruments={game.instruments}
          selectedId={selectedInstrumentId}
          onSelect={setSelectedInstrumentId}
        />
        <PositionList positions={game.player.positions} />
      </div>
      <TradeForm
        instruments={game.instruments}
        selectedInstrumentId={selectedInstrumentId}
        onInstrumentChange={setSelectedInstrumentId}
      />
      <TradeHistory trades={game.recentTrades} />
      <OrderBookPanel orders={orderBook.orders} />
    </main>
  );
}

export function ErrorBoundary({ error }: Route.ErrorBoundaryProps) {
  let message: string;
  if (isRouteErrorResponse(error)) {
    message = error.status === 404 ? "ゲームが見つかりません。" : (error.statusText || "エラーが発生しました。");
  } else if (error instanceof Error) {
    message = error.message;
  } else {
    message = "予期しないエラーが発生しました。";
  }

  return (
    <main className="max-w-2xl mx-auto p-4 space-y-4">
      <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-6 text-center">
        <h2 className="text-lg font-bold text-red-700 dark:text-red-400 mb-2">エラー</h2>
        <p className="text-red-600 dark:text-red-300 mb-4">{message}</p>
        <a href="/" className="inline-block bg-gray-500 hover:bg-gray-600 text-white font-bold py-2 px-4 rounded">
          トップに戻る
        </a>
      </div>
    </main>
  );
}
