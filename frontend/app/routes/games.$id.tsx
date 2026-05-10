import { useState } from "react";
import {
  useLoaderData,
  useActionData,
  useNavigation,
  useSubmit,
  isRouteErrorResponse,
  type ClientLoaderFunctionArgs,
  type ClientActionFunctionArgs,
  type ShouldRevalidateFunctionArgs,
} from "react-router";
import { getGame, getOrderBook, placeOrder, wait } from "~/api/gameApi";
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
import { ORDERBOOK_PAGE_SIZE } from "~/config";
import { TradeIntent, OrderSide } from "~/lib/enums";

type LoaderData = {
  game: GameResponse;
  orderBook: OrderBookResponse;
};

type ActionData =
  | { game: GameResponse; orderBook: OrderBookResponse; error?: undefined; lastSubmission?: undefined }
  | { game?: undefined; orderBook?: undefined; error: string; lastSubmission: Record<string, string> };

function formDataToRecord(fd: FormData): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of fd.entries()) {
    if (typeof value === "string") out[key] = value;
  }
  return out;
}

export async function clientLoader({ params }: ClientLoaderFunctionArgs): Promise<LoaderData> {
  const id = params.id!;
  const [game, orderBook] = await Promise.all([
    getGame(id),
    getOrderBook(id, 1, ORDERBOOK_PAGE_SIZE),
  ]);
  return { game, orderBook };
}

export async function clientAction({ params, request }: ClientActionFunctionArgs): Promise<ActionData> {
  const formData = await request.formData();
  const intent = formData.get("intent") as string;
  const id = params.id!;
  const submission = formDataToRecord(formData);

  try {
    let game: GameResponse;

    if (intent === TradeIntent.Wait) {
      game = await wait(id);
    } else {
      const instrumentId = Number(formData.get("instrumentId"));
      const quantity = Number(formData.get("quantity"));
      const priceRaw = formData.get("price");
      const price = priceRaw ? Number(priceRaw) : null;
      const expiresInTurns = Number(formData.get("expiresInTurns"));

      if (Number.isNaN(instrumentId) || Number.isNaN(quantity) || (price !== null && Number.isNaN(price)) || Number.isNaN(expiresInTurns)) {
        return { error: "入力値が不正です。数値を入力してください。", lastSubmission: submission };
      }
      if (expiresInTurns < 1) {
        return { error: "有効期限は1ターン以上を指定してください。", lastSubmission: submission };
      }

      const side =
        intent === TradeIntent.Buy ? OrderSide.Buy
        : intent === TradeIntent.Sell ? OrderSide.Sell
        : null;
      if (side === null) {
        return { error: `Invalid intent: ${intent}`, lastSubmission: submission };
      }

      const order: OrderRequest = { side, instrumentId, quantity, price, expiresInTurns };
      game = await placeOrder(id, order);
    }

    const orderBook = await getOrderBook(id, 1, ORDERBOOK_PAGE_SIZE);
    return { game, orderBook };
  } catch (e) {
    return {
      error: e instanceof Error ? e.message : "予期しないエラーが発生しました。",
      lastSubmission: submission,
    };
  }
}

export function shouldRevalidate({ actionResult, defaultShouldRevalidate }: ShouldRevalidateFunctionArgs): boolean {
  if (actionResult && typeof actionResult === "object" && "error" in actionResult && actionResult.error) {
    return false;
  }
  return defaultShouldRevalidate;
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
  const actionData = useActionData<ActionData>();
  const navigation = useNavigation();
  const submit = useSubmit();
  const game = actionData?.game ?? loaderData.game;
  const orderBook = actionData?.orderBook ?? loaderData.orderBook;
  const actionError = actionData?.error;
  const lastSubmission = actionData?.lastSubmission;
  const isSubmitting = navigation.state !== "idle";
  const [selectedInstrumentId, setSelectedInstrumentId] = useState<number | null>(null);

  return (
    <main className="max-w-2xl mx-auto p-4 space-y-4">
      <GameHeader turn={game.turn} playerName={game.player.name} />
      {actionError && (
        <div
          role="alert"
          className="bg-red-50 dark:bg-red-900/20 border border-red-300 dark:border-red-800 text-red-700 dark:text-red-300 px-4 py-3 rounded-lg flex items-center justify-between gap-3"
        >
          <span className="flex-1">{actionError}</span>
          {lastSubmission && (
            <button
              type="button"
              onClick={() => submit(lastSubmission, { method: "post" })}
              disabled={isSubmitting}
              className="bg-red-600 hover:bg-red-700 disabled:bg-red-400 disabled:cursor-not-allowed text-white text-sm font-bold py-1 px-3 rounded whitespace-nowrap"
            >
              {isSubmitting ? "再試行中..." : "再試行"}
            </button>
          )}
        </div>
      )}
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
      <OrderBookPanel gameId={game.gameId} orderBook={orderBook} currentTurn={game.turn} />
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
