# OrderBook ページング フロント対応 実装計画（後続）

**Goal:** `OrderBookPanel` に前/次ボタンとページ情報表示を追加し、ページング API を UI から操作可能にする

**Architecture:** `OrderBookPanel` を自己完結のページングコンポーネントに拡張する。初期表示は loader が返す 1 ページ目をそのまま使い、ページ切替時のみ `OrderBookPanel` 内から `getOrderBook` を再取得する。ゲームアクション（buy/sell/wait）で clientAction が再取得するとページ 1 にリセットされる（新規注文が発生しうるため妥当な挙動）。

**Prerequisite:** [2026-04-19-orderbook-pagination.md](./2026-04-19-orderbook-pagination.md) の実装・マージが完了していること。API が `totalCount` / `page` / `pageSize` を返す状態であることが前提。

**Tech Stack:** React 19、React Router v7（クライアントサイドフェッチのみ）、Vitest + @testing-library/react、Tailwind CSS v4

---

## File Structure

- **Modify** `frontend/app/types/game.ts` — `OrderBookResponse` に `totalCount` / `page` / `pageSize` を追加
- **Modify** `frontend/app/api/gameApi.ts` — `getOrderBook(id, page?, pageSize?)` シグネチャ変更
- **Modify** `frontend/app/components/OrderBookPanel.tsx` — props 追加・ページング UI・内部フェッチ
- **Modify** `frontend/app/components/OrderBookPanel.test.tsx` — ページングテスト追加
- **Modify** `frontend/app/routes/games.$id.tsx` — `gameId` を panel に渡し、全 `OrderBookResponse` を渡す
- **Modify** `frontend/app/routes/games.$id.test.tsx` — `mockOrderBook` を新形状に更新
- **Modify** `docs/FRONT.md` — OrderBookPanel 節に pagination 仕様を追記

### 設計方針（全タスク共通）

- **ページサイズ**: フロント固定 `20`。`OrderBookPanel.tsx` から `ORDERBOOK_PAGE_SIZE = 20` を export し、loader / clientAction / panel 内部フェッチがすべて同じ値を使う。**API のデフォルト (50) と異なるので loader 側も明示的に 20 を指定する必要がある** — これを怠ると初回 50 件取得・2 ページ目以降 20 件取得で表示範囲と `totalPages` 計算が不整合を起こす
- **状態**: `OrderBookPanel` 内部 `useState` で現在のページ（`OrderBookResponse`）を保持。URL 同期はしない（リロード時は loader が 1 ページ目を返すのでリセットされる前提）
- **初期データ**: props として渡された `OrderBookResponse`（loader / clientAction が 1 ページ目を取得済み）を page=1 として表示
- **再取得トリガー**: ページ切替ボタン押下時のみ `getOrderBook(id, page, ORDERBOOK_PAGE_SIZE)` を fetch。props の `orderBook` が変わった場合（clientAction 後）は props が page=1 の前提で反映
- **エラー処理**: ページ切替中の fetch エラーは既存の `gameApi.handleResponse` が throw する。`OrderBookPanel` 内で try/catch して「取得に失敗しました」と panel 内にインライン表示。既存のルートエラーバウンダリは発火させない（ゲーム全体が落ちないようにする）
- **ローディング**: fetch 中はボタンを `disabled` にし、ページ情報横に小さく「読み込み中...」を表示

---

## Task 1: TypeScript 型定義を API 仕様に合わせる

**Files:**
- Modify: `frontend/app/types/game.ts`

- [ ] **Step 1: `OrderBookResponse` を拡張**

`frontend/app/types/game.ts` の該当部分を以下に置き換える:

```typescript
export type OrderBookResponse = {
  orders: OrderDto[];
  totalCount: number;
  page: number;
  pageSize: number;
};
```

- [ ] **Step 2: 型チェックで影響範囲を確認**

Run: `cd frontend && npm run typecheck`
Expected: エラーは発生しない想定（TS は readonly オブジェクトの追加プロパティに寛容）。ただし `mockOrderBook: OrderBookResponse = { orders: [...] }` の箇所は不足エラーが出る可能性があるため、次タスク以降で修正する。

- [ ] **Step 3: この時点ではコミットしない**

Task 2 以降の変更とまとめてコミットする。

---

## Task 2: API クライアントにページングパラメータを追加

**Files:**
- Modify: `frontend/app/api/gameApi.ts`

- [ ] **Step 1: `getOrderBook` にオプション引数を追加**

`frontend/app/api/gameApi.ts` の末尾の `getOrderBook` 関数を以下に置き換える:

```typescript
export async function getOrderBook(
  id: string,
  page?: number,
  pageSize?: number,
): Promise<OrderBookResponse> {
  const params = new URLSearchParams();
  if (page !== undefined) params.set("page", String(page));
  if (pageSize !== undefined) params.set("pageSize", String(pageSize));
  const query = params.toString();
  const url = `${BASE}/api/admin/games/${id}/orderbook${query ? `?${query}` : ""}`;
  const res = await fetch(url);
  return handleResponse(res);
}
```

- [ ] **Step 2: 型チェック**

Run: `cd frontend && npm run typecheck`
Expected: エラーなし（既存呼び出し `getOrderBook(id)` は引数省略で後方互換）。

---

## Task 3: OrderBookPanel のテストを書く（TDD）

**Files:**
- Modify: `frontend/app/components/OrderBookPanel.test.tsx`

### 新しい `OrderBookPanel` の props 仕様（次タスクで実装）

```typescript
type Props = {
  gameId: string;
  orderBook: OrderBookResponse;  // loader / action が取得した初期データ（ページ1想定）
  pageSize?: number;              // default 20
};
```

- [ ] **Step 1: 既存テストを新 props 形状に書き換え + ページングテストを追加**

`frontend/app/components/OrderBookPanel.test.tsx` を以下の内容で全面置き換える:

```typescript
import { describe, test, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OrderBookPanel } from "./OrderBookPanel";
import type { OrderBookResponse, OrderDto } from "~/types/game";
import { getOrderBook } from "~/api/gameApi";

vi.mock("~/api/gameApi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("~/api/gameApi")>();
  return { ...actual, getOrderBook: vi.fn() };
});

const getOrderBookMock = vi.mocked(getOrderBook);

function makeOrder(id: number): OrderDto {
  return {
    id,
    traderId: `trader-${id}`,
    instrumentId: 1,
    side: "Buy",
    type: "Limit",
    quantity: 1,
    price: 100 + id,
    stopPrice: null,
    createdAtTurn: 1,
  };
}

function makeBook(orders: OrderDto[], totalCount: number, page = 1, pageSize = 20): OrderBookResponse {
  return { orders, totalCount, page, pageSize };
}

describe("OrderBookPanel", () => {
  beforeEach(() => {
    getOrderBookMock.mockReset();
  });

  test("注文がない場合はメッセージを表示", () => {
    render(<OrderBookPanel gameId="g1" orderBook={makeBook([], 0)} />);
    expect(screen.getByText("注文なし")).toBeInTheDocument();
  });

  test("注文一覧を表示する", () => {
    const orders: OrderDto[] = [
      {
        id: 1,
        traderId: "player-1",
        instrumentId: 1,
        side: "Buy",
        type: "Limit",
        quantity: 10,
        price: 1500,
        stopPrice: null,
        createdAtTurn: 1,
      },
    ];
    render(<OrderBookPanel gameId="g1" orderBook={makeBook(orders, 1)} />);

    expect(screen.getByText("買")).toBeInTheDocument();
    expect(screen.getByText("指値")).toBeInTheDocument();
    expect(screen.getByText("10")).toBeInTheDocument();
    expect(screen.getByText("¥1,500")).toBeInTheDocument();
    expect(screen.getByText("player-1")).toBeInTheDocument();
  });

  test("成行注文の価格は「-」と表示する", () => {
    const orders: OrderDto[] = [
      {
        id: 2,
        traderId: "cpu-1",
        instrumentId: 2,
        side: "Sell",
        type: "Market",
        quantity: 5,
        price: null,
        stopPrice: null,
        createdAtTurn: 2,
      },
    ];
    render(<OrderBookPanel gameId="g1" orderBook={makeBook(orders, 1)} />);

    expect(screen.getByText("売")).toBeInTheDocument();
    expect(screen.getByText("成行")).toBeInTheDocument();
  });

  test("totalCountが1ページに収まるときは前/次ボタンが両方disabled", () => {
    const orders = [makeOrder(1), makeOrder(2)];
    render(<OrderBookPanel gameId="g1" orderBook={makeBook(orders, 2)} pageSize={20} />);

    expect(screen.getByRole("button", { name: "前へ" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "次へ" })).toBeDisabled();
  });

  test("複数ページある場合の初期表示で「1–20 / 45」のようなページ情報が出る", () => {
    const orders = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));
    render(<OrderBookPanel gameId="g1" orderBook={makeBook(orders, 45, 1, 20)} pageSize={20} />);

    expect(screen.getByText("1–20 / 45")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "前へ" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "次へ" })).toBeEnabled();
  });

  test("「次へ」クリックで getOrderBook(id, 2, pageSize) が呼ばれ、結果で再描画", async () => {
    const user = userEvent.setup();
    const page1 = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));
    const page2 = [makeOrder(21), makeOrder(22)];
    getOrderBookMock.mockResolvedValueOnce(makeBook(page2, 22, 2, 20));

    render(<OrderBookPanel gameId="g1" orderBook={makeBook(page1, 22, 1, 20)} pageSize={20} />);

    await user.click(screen.getByRole("button", { name: "次へ" }));

    await waitFor(() => {
      expect(getOrderBookMock).toHaveBeenCalledWith("g1", 2, 20);
    });
    await waitFor(() => {
      expect(screen.getByText("21–22 / 22")).toBeInTheDocument();
    });
    // ページ 1 の注文は消えている
    expect(screen.queryByText("trader-1")).not.toBeInTheDocument();
    // ページ 2 の注文が見える
    expect(screen.getByText("trader-21")).toBeInTheDocument();
  });

  test("「前へ」クリックで getOrderBook(id, 1, pageSize) が呼ばれる", async () => {
    const user = userEvent.setup();
    const page2 = [makeOrder(21), makeOrder(22)];
    const page1 = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));

    // 初期 props は page 2 の状態を模擬するため、1 度「次へ」→「前へ」の流れ
    getOrderBookMock
      .mockResolvedValueOnce(makeBook(page2, 22, 2, 20))
      .mockResolvedValueOnce(makeBook(page1, 22, 1, 20));

    render(<OrderBookPanel gameId="g1" orderBook={makeBook(page1, 22, 1, 20)} pageSize={20} />);

    await user.click(screen.getByRole("button", { name: "次へ" }));
    await waitFor(() => expect(screen.getByText("21–22 / 22")).toBeInTheDocument());

    await user.click(screen.getByRole("button", { name: "前へ" }));
    await waitFor(() => {
      expect(getOrderBookMock).toHaveBeenLastCalledWith("g1", 1, 20);
    });
    await waitFor(() => expect(screen.getByText("1–20 / 22")).toBeInTheDocument());
  });

  test("fetchが失敗した場合はエラーメッセージをpanel内に表示する", async () => {
    const user = userEvent.setup();
    const page1 = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));
    getOrderBookMock.mockRejectedValueOnce(new Error("boom"));

    render(<OrderBookPanel gameId="g1" orderBook={makeBook(page1, 45, 1, 20)} pageSize={20} />);

    await user.click(screen.getByRole("button", { name: "次へ" }));

    await waitFor(() => {
      expect(screen.getByText("取得に失敗しました")).toBeInTheDocument();
    });
    // 元のページ 1 の表示は維持される
    expect(screen.getByText("trader-1")).toBeInTheDocument();
  });

  test("props.orderBook が新しく差し替わったらページ1にリセットされる", async () => {
    const user = userEvent.setup();
    const page1 = Array.from({ length: 20 }, (_, i) => makeOrder(i + 1));
    const page2 = [makeOrder(21), makeOrder(22)];
    getOrderBookMock.mockResolvedValueOnce(makeBook(page2, 22, 2, 20));

    const { rerender } = render(
      <OrderBookPanel gameId="g1" orderBook={makeBook(page1, 22, 1, 20)} pageSize={20} />,
    );

    // ページ 2 に移動
    await user.click(screen.getByRole("button", { name: "次へ" }));
    await waitFor(() => expect(screen.getByText("21–22 / 22")).toBeInTheDocument());

    // clientAction 後を模擬: 新しい orderBook が props で降ってくる
    const newFirstPage = [makeOrder(100)];
    rerender(
      <OrderBookPanel gameId="g1" orderBook={makeBook(newFirstPage, 1, 1, 20)} pageSize={20} />,
    );

    await waitFor(() => {
      expect(screen.getByText("1–1 / 1")).toBeInTheDocument();
    });
    expect(screen.getByText("trader-100")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: テストを実行して FAIL を確認**

Run: `cd frontend && npm test -- --run app/components/OrderBookPanel.test.tsx`
Expected: 全テスト FAIL または TypeScript エラー。`OrderBookPanel` がまだ `orders` props のみを受け取るため、`gameId` / `orderBook` / `pageSize` props や「前へ」「次へ」ボタンが存在しない。

---

## Task 4: OrderBookPanel を実装する

**Files:**
- Modify: `frontend/app/components/OrderBookPanel.tsx`

- [ ] **Step 1: コンポーネントを書き換える**

`frontend/app/components/OrderBookPanel.tsx` を以下の内容に置き換える:

```typescript
import { memo, useCallback, useEffect, useState } from "react";
import type { OrderBookResponse, OrderDto } from "~/types/game";
import { getOrderBook } from "~/api/gameApi";
import { formatJPY } from "~/utils/format";

export const ORDERBOOK_PAGE_SIZE = 20;

type Props = {
  gameId: string;
  orderBook: OrderBookResponse;
  pageSize?: number;
};

export const OrderBookPanel = memo(function OrderBookPanel({
  gameId,
  orderBook,
  pageSize = ORDERBOOK_PAGE_SIZE,
}: Props) {
  const [currentBook, setCurrentBook] = useState<OrderBookResponse>(orderBook);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // props.orderBook が差し替わったらページ 1 にリセット
  useEffect(() => {
    setCurrentBook(orderBook);
    setError(null);
  }, [orderBook]);

  const totalPages = Math.max(1, Math.ceil(currentBook.totalCount / pageSize));
  const currentPage = currentBook.page;
  const canPrev = currentPage > 1 && !isLoading;
  const canNext = currentPage < totalPages && !isLoading;

  const rangeStart = currentBook.orders.length === 0
    ? 0
    : (currentPage - 1) * pageSize + 1;
  const rangeEnd = (currentPage - 1) * pageSize + currentBook.orders.length;

  const goToPage = useCallback(
    async (nextPage: number) => {
      setIsLoading(true);
      setError(null);
      try {
        const book = await getOrderBook(gameId, nextPage, pageSize);
        setCurrentBook(book);
      } catch {
        setError("取得に失敗しました");
      } finally {
        setIsLoading(false);
      }
    },
    [gameId, pageSize],
  );

  const onPrev = useCallback(() => goToPage(currentPage - 1), [goToPage, currentPage]);
  const onNext = useCallback(() => goToPage(currentPage + 1), [goToPage, currentPage]);

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h2 className="text-sm font-semibold text-gray-500">注文板</h2>
        {currentBook.totalCount > 0 && (
          <div className="flex items-center gap-2 text-sm text-gray-500">
            <span>
              {rangeStart}–{rangeEnd} / {currentBook.totalCount}
            </span>
            {isLoading && <span className="text-xs">読み込み中...</span>}
            <button
              type="button"
              onClick={onPrev}
              disabled={!canPrev}
              className="px-2 py-1 border rounded disabled:opacity-40 disabled:cursor-not-allowed"
            >
              前へ
            </button>
            <button
              type="button"
              onClick={onNext}
              disabled={!canNext}
              className="px-2 py-1 border rounded disabled:opacity-40 disabled:cursor-not-allowed"
            >
              次へ
            </button>
          </div>
        )}
      </div>
      {error && <p className="text-sm text-red-600 mb-2">{error}</p>}
      {currentBook.orders.length === 0 && !error ? (
        <p className="text-sm text-gray-400">注文なし</p>
      ) : currentBook.orders.length === 0 ? null : (
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-gray-500">
              <th className="py-2">ID</th>
              <th className="py-2">銘柄</th>
              <th className="py-2">売買</th>
              <th className="py-2">種類</th>
              <th className="py-2 text-right">数量</th>
              <th className="py-2 text-right">価格</th>
              <th className="py-2 text-right">ストップ</th>
              <th className="py-2 text-right">ターン</th>
              <th className="py-2">トレーダー</th>
            </tr>
          </thead>
          <tbody>
            {currentBook.orders.map((order: OrderDto) => (
              <tr
                key={order.id}
                className={`border-b ${
                  order.side === "Buy"
                    ? "text-red-600 dark:text-red-400"
                    : "text-blue-600 dark:text-blue-400"
                }`}
              >
                <td className="py-2">{order.id}</td>
                <td className="py-2">{order.instrumentId}</td>
                <td className="py-2">{order.side === "Buy" ? "買" : "売"}</td>
                <td className="py-2">{order.type === "Limit" ? "指値" : "成行"}</td>
                <td className="py-2 text-right">{order.quantity}</td>
                <td className="py-2 text-right">{order.price != null ? formatJPY(order.price) : "-"}</td>
                <td className="py-2 text-right">{order.stopPrice != null ? formatJPY(order.stopPrice) : "-"}</td>
                <td className="py-2 text-right">{order.createdAtTurn}</td>
                <td className="py-2">{order.traderId}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
});
```

- [ ] **Step 2: OrderBookPanel テストを実行して PASS を確認**

Run: `cd frontend && npm test -- --run app/components/OrderBookPanel.test.tsx`
Expected: 全 9 テスト PASS。

---

## Task 5: ルートから gameId を渡し、loader/action で同一 pageSize を使う

**Files:**
- Modify: `frontend/app/routes/games.$id.tsx`
- Modify: `frontend/app/routes/games.$id.test.tsx`

### 重要: pageSize の整合性

API のデフォルト `pageSize = 50`、フロントは `20` を使う。loader / clientAction が `getOrderBook(id)` を引数なしで呼ぶと 50 件返り、`OrderBookPanel` の 20 件ページングと不整合になる。loader / action も `ORDERBOOK_PAGE_SIZE` を明示的に渡すこと。

- [ ] **Step 1: ルートテストの mockOrderBook を新形状に更新**

`frontend/app/routes/games.$id.test.tsx` の `mockOrderBook` 定義を以下に置き換える:

```typescript
const mockOrderBook: OrderBookResponse = {
  orders: [
    {
      id: 1,
      traderId: "cpu-1",
      instrumentId: 1,
      side: "Sell",
      type: "Limit",
      quantity: 5,
      price: 1600,
      stopPrice: null,
      createdAtTurn: 4,
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 20,
};
```

- [ ] **Step 2: ルートテストを実行して失敗状況を確認**

Run: `cd frontend && npm test -- --run app/routes/games.$id.test.tsx`
Expected: この段階ではルート側はまだ `OrderBookPanel` に `orders={orderBook.orders}` しか渡していないので、OrderBookPanel の新 props 要件により型/実行時エラーが発生する。

- [ ] **Step 3: ルートコンポーネントで loader/action/JSX を更新**

`frontend/app/routes/games.$id.tsx` に対して 3 箇所の修正を行う:

**修正 1: import 文を更新（`OrderBookPanel` import 行を置き換え）**

```tsx
import { OrderBookPanel, ORDERBOOK_PAGE_SIZE } from "~/components/OrderBookPanel";
```

**修正 2: `clientLoader` 内の `getOrderBook(id)` を置き換え**

既存:

```ts
  const [game, orderBook] = await Promise.all([
    getGame(id),
    getOrderBook(id),
  ]);
```

新:

```ts
  const [game, orderBook] = await Promise.all([
    getGame(id),
    getOrderBook(id, 1, ORDERBOOK_PAGE_SIZE),
  ]);
```

**修正 3: `clientAction` 内の `getOrderBook(id)` を置き換え**

既存:

```ts
  const orderBook = await getOrderBook(id);
```

新:

```ts
  const orderBook = await getOrderBook(id, 1, ORDERBOOK_PAGE_SIZE);
```

**修正 4: `OrderBookPanel` 呼び出し行を置き換え**

既存:

```tsx
      <OrderBookPanel orders={orderBook.orders} />
```

新:

```tsx
      <OrderBookPanel gameId={game.gameId} orderBook={orderBook} />
```

- [ ] **Step 4: 型チェック + テストを実行**

Run: `cd frontend && npm run typecheck && npm test -- --run`
Expected: すべて PASS。

- [ ] **Step 5: コミット**

```bash
git add frontend/app/types/game.ts frontend/app/api/gameApi.ts frontend/app/components/OrderBookPanel.tsx frontend/app/components/OrderBookPanel.test.tsx frontend/app/routes/games.$id.tsx frontend/app/routes/games.$id.test.tsx
git commit -m "feat(frontend): add pagination UI to OrderBookPanel"
```

---

## Task 6: ブラウザで動作確認（CLAUDE.md の UI 検証ルール準拠）

**Files:** なし（目視確認）

- [ ] **Step 1: バックエンドを起動**

別ターミナルで: `dotnet run --project src/FinLearn.Api`
Expected: `http://localhost:5088` で起動。

- [ ] **Step 2: フロントの dev server を起動**

別ターミナルで: `cd frontend && npm run dev`
Expected: `http://localhost:5173` で起動。

- [ ] **Step 3: golden path を確認**

手順:
1. `http://localhost:5173/` で「ゲーム開始」をクリック
2. `/games/:id` 画面に遷移
3. `TradeForm` で安い指値買い注文（例: 銘柄 1、数量 1、価格 1）を 25 回繰り返して約定しない注文を積む
4. `OrderBookPanel` に「1–20 / N」のような表示が出て、「次へ」ボタンが有効になることを確認
5. 「次へ」をクリックしてページ 2 が表示されることを確認
6. 「前へ」でページ 1 に戻れることを確認

- [ ] **Step 4: エッジケースを確認**

1. 新規ゲーム直後（注文 0 件）で「注文なし」が表示され、前/次ボタンが出ていないことを確認
2. 注文 5 件程度（1 ページに収まる）状態で、「1–5 / 5」表示 + 両ボタン disabled を確認
3. ページ 2 にいる状態で `buy/sell/wait` を押すと自動でページ 1 に戻ることを確認（clientAction で orderBook が差し替わるため）

- [ ] **Step 5: 他画面に回帰影響がないことを確認**

プレイヤー情報・市況・ポジション・トレード履歴・警告表示が従来通り動作することを確認。

---

## Task 7: 画面設計ドキュメントを更新

**Files:**
- Modify: `docs/FRONT.md`

- [ ] **Step 1: `write-docs` スキルを起動**

プロジェクト規約（`.claude/CLAUDE.md`）に従う。

- [ ] **Step 2: コンポーネントツリー図の OrderBookPanel 行を更新**

`docs/FRONT.md` のコンポーネントツリー内 `├── TradeHistory ...` の直後に既に `OrderBookPanel` が無い場合は追加、ある場合は注記を付与する。現状 `OrderBookPanel` が記載されていない場合は以下を `└── WarningMessage` の前に挿入:

```
    ├── OrderBookPanel     ← 注文板（ページング対応、初期はloaderの1ページ目）
```

- [ ] **Step 3: コンポーネント詳細セクションに OrderBookPanel を追記**

`docs/FRONT.md` の `### WarningMessage` の直前に以下のセクションを追加:

```markdown
### OrderBookPanel

`/api/admin/games/:id/orderbook` の内容を表示するデバッグ用パネル。ページング UI 付き。

| 要素 | データソース | 備考 |
|---|---|---|
| 注文行 | `orderBook.orders` | 空なら「注文なし」 |
| ページ情報 | `orderBook.totalCount` / `page` / `pageSize` | 「1–20 / N」形式 |
| 前へ / 次へ | — | 範囲外は disabled、fetch 中も disabled |

- ページサイズは固定 20（panel の `pageSize` prop で上書き可）
- 初期表示は loader が取得した 1 ページ目をそのまま利用
- 「前へ」「次へ」押下時のみ `getOrderBook(gameId, page, pageSize)` を再取得
- clientAction 後は props が差し替わるため自動的にページ 1 にリセット
- 取得失敗時はパネル内にインラインでエラー表示（ルートエラーバウンダリは発火させない）
```

- [ ] **Step 4: API クライアント節を更新**

`docs/FRONT.md` の API クライアント節にある関数一覧に以下の行を追加（既存にない場合）:

```
getOrderBook(id, page?, pageSize?)  → GET  /api/admin/games/:id/orderbook
```

- [ ] **Step 5: コミット**

```bash
git add docs/FRONT.md
git commit -m "docs(front): document OrderBookPanel pagination"
```

---

## 完了チェック

- [ ] `cd frontend && npm run typecheck` がエラーなし
- [ ] `cd frontend && npm test -- --run` が全 PASS
- [ ] `dotnet test` が全 PASS（後続プランなので API 側に影響していないことの確認）
- [ ] ブラウザで Task 6 の golden path / edge cases がすべて確認済み
- [ ] `docs/FRONT.md` に OrderBookPanel のページング仕様が記載済み
- [ ] 2 つのコミットが作成されている（実装 + ドキュメント）
