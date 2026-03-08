import { useState } from "react";
import { useLoaderData, useActionData, useNavigation, type ClientLoaderFunctionArgs, type ClientActionFunctionArgs } from "react-router";
import { getGame, buy, sell, wait } from "~/api/gameApi";
import type { GameResponse, OrderRequest } from "~/types/game";
import { GameHeader } from "~/components/GameHeader";
import { PlayerPanel } from "~/components/PlayerPanel";
import { MarketBoard } from "~/components/MarketBoard";
import { PositionList } from "~/components/PositionList";
import { TradeForm } from "~/components/TradeForm";
import { WarningMessage } from "~/components/WarningMessage";

export async function clientLoader({ params }: ClientLoaderFunctionArgs): Promise<GameResponse> {
  return getGame(params.id!);
}

export async function clientAction({ params, request }: ClientActionFunctionArgs): Promise<GameResponse> {
  const formData = await request.formData();
  const intent = formData.get("intent") as string;
  const id = params.id!;

  if (intent === "wait") {
    return wait(id);
  }

  const instrumentId = Number(formData.get("instrumentId"));
  const quantity = Number(formData.get("quantity"));
  const priceRaw = formData.get("price");
  const price = priceRaw ? Number(priceRaw) : null;

  if (Number.isNaN(instrumentId) || Number.isNaN(quantity) || (price !== null && Number.isNaN(price))) {
    throw new Error("入力値が不正です。数値を入力してください。");
  }

  const order: OrderRequest = { instrumentId, quantity, price };

  if (intent === "buy") {
    return buy(id, order);
  }
  if (intent === "sell") {
    return sell(id, order);
  }
  throw new Error(`Invalid intent: ${intent}`);
}

export function HydrateFallback() {
  return (
    <main className="flex items-center justify-center min-h-screen">
      <p className="text-gray-500 text-lg animate-pulse">読み込み中...</p>
    </main>
  );
}

export default function GamePage() {
  const loaderData = useLoaderData<GameResponse>();
  const actionData = useActionData<GameResponse>();
  const game = actionData ?? loaderData;
  const navigation = useNavigation();
  const isSubmitting = navigation.state === "submitting" || navigation.state === "loading";

  const [selectedInstrumentId, setSelectedInstrumentId] = useState<number | null>(null);

  return (
    <main className={`max-w-2xl mx-auto p-4 space-y-4 ${isSubmitting ? "opacity-60 pointer-events-none" : ""}`}>
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
    </main>
  );
}
