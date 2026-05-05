# フロントエンド画面設計

## 技術構成

- **React 19** + **React Router v7**（フレームワークモード、SSR 有効）
- **TypeScript** + **Tailwind CSS v4**
- バックエンド API（.NET Minimal API）とは別プロセス（CORS で接続）

---

## ルーティング

| パス | ルートファイル | 画面 | 説明 |
|---|---|---|---|
| `/` | `routes/home.tsx` | HomePage | ゲーム開始画面 |
| `/games/:id` | `routes/games.$id.tsx` | GamePage | メインゲーム画面 |

---

## コンポーネントツリー

```
App (root.tsx)
├── HomePage (/)
│   └── StartButton → POST /api/games → navigate(/games/:id)
│
└── GamePage (/games/:id)
    ├── GameHeader         ← ターン数・プレイヤー名
    ├── PlayerPanel        ← 現金・総資産・損益
    ├── MarketBoard        ← 銘柄一覧と現在価格
    ├── PositionList       ← 保有ポジション一覧
    ├── TradeForm          ← 注文入力（銘柄・数量・価格）+ アクションボタン
    ├── TradeHistory       ← 直近3件の約定結果テーブル
    ├── OrderBookPanel     ← 注文板（ページング対応、初期はloaderの1ページ目）
    └── WarningMessage     ← API からの警告表示
```

---

## データフロー

ゲーム状態はすべてサーバー駆動。クライアントは `GameResponse` を受け取って描画するだけ。

```
[ユーザー操作] → clientAction (POST orders/wait)
                      ↓
              API が更新後の GameResponse を返却
                      ↓
              React Router が自動で画面を再描画
```

React Router のデータ API との対応:

| React Router | API | 用途 |
|---|---|---|
| `clientLoader` | `GET /api/games/:id` | 画面初期表示・リロード |
| `clientAction` | `POST orders/wait` | プレイヤーアクション |

---

## コンポーネント詳細

### HomePage

- 「ゲーム開始」ボタン1つ
- クリック → `POST /api/games` → `navigate(/games/${gameId})`

### GamePage

React Router の `clientLoader` / `clientAction` でデータ取得・アクション実行を行うルートコンポーネント。子コンポーネントに `GameResponse` を分配する。

### GameHeader

| 表示項目 | データソース |
|---|---|
| ターン数 | `GameResponse.turn` |
| プレイヤー名 | `GameResponse.player.name` |

### PlayerPanel

| 表示項目 | データソース | 備考 |
|---|---|---|
| 現金 | `player.cash` | JPY 表記 |
| 総資産 | `player.totalAssets` | 現金 + 全評価額 |
| 損益 | `player.profitLoss` | 正負で色分け |

### MarketBoard

`GameResponse.instruments` をテーブル表示。

| 列 | データソース |
|---|---|
| 銘柄 ID | `instrument.id` |
| 現在価格 | `instrument.price` |

行クリックで TradeForm の銘柄を選択する連携あり。

### PositionList

`GameResponse.player.positions` をテーブル表示。保有なしの場合は空表示。

| 列 | データソース |
|---|---|
| 銘柄 ID | `position.instrumentId` |
| 数量 | `position.quantity` |
| 現在価格 | `position.currentPrice` |
| 評価額 | `position.amount` |

### TradeForm

注文入力フォーム + 3 つのアクションボタン。

| 入力 | 型 | 説明 |
|---|---|---|
| 銘柄 | select | `instruments` からプルダウン or MarketBoard 連携 |
| 数量 | number | 1 以上の整数 |
| 価格 | number? | 空欄 = 成行注文 |

| ボタン | API | 備考 |
|---|---|---|
| 買う | `POST /games/:id/orders` (`side: "Buy"`) | フォーム入力値を送信 |
| 売る | `POST /games/:id/orders` (`side: "Sell"`) | フォーム入力値を送信 |
| 待つ | `POST /games/:id/wait` | フォーム入力不要 |

3 つのボタンはすべて React Router の `<Form>` を使い、hidden フィールド `intent` (`buy` / `sell` / `wait`) で区別する。`clientAction` 境界で `intent` を `side` (`"Buy"` / `"Sell"`) に変換してから API を呼ぶ。

### TradeHistory

`GameResponse.recentTrades` が空でない場合、直近の約定結果をテーブルで表示する（新しい順）。
約定がまだ一度もない場合はコンポーネント自体を非表示にする。

| 列 | データソース | 備考 |
|---|---|---|
| 売買 | `trade.side` | 買=赤、売=青 |
| 銘柄 ID | `trade.instrumentId` | |
| 約定数量 | `trade.filledQuantity` | |
| 約定金額 | `trade.totalAmount` | JPY 表記 |
| 手数料 | `trade.fee` | JPY 表記 |

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

### WarningMessage

- `GameResponse.warning` が non-null のとき表示
- 次のアクション成功時に自動消去（`warning: null` になるため）

---

## API クライアント

`app/api/gameApi.ts` — fetch ベースの薄いラッパー。

```ts
const BASE = import.meta.env.VITE_API_URL ?? "http://localhost:5088";

createGame()             → POST /api/games
getGame(id)              → GET  /api/games/:id
placeOrder(id, order)    → POST /api/games/:id/orders   // order.side: "Buy" | "Sell"
wait(id)                 → POST /api/games/:id/wait
getOrderBook(id, page?, pageSize?)  → GET  /api/admin/games/:id/orderbook
```

---

## ファイル構成

```
frontend/app/
├── root.tsx                    # 共通レイアウト（既存）
├── routes.ts                   # ルート定義
├── routes/
│   ├── home.tsx                # HomePage
│   └── games.$id.tsx           # GamePage (clientLoader + clientAction)
├── components/
│   ├── GameHeader.tsx
│   ├── PlayerPanel.tsx
│   ├── MarketBoard.tsx
│   ├── PositionList.tsx
│   ├── TradeForm.tsx
│   ├── TradeHistory.tsx
│   └── WarningMessage.tsx
├── api/
│   └── gameApi.ts              # API クライアント
└── types/
    └── game.ts                 # 型定義（API DTO ミラー）
```
