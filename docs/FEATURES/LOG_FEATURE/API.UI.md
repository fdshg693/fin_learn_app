# ログ機能 — API・UI 公開層

> ドメインログの構造は [LOGIC.md](./LOGIC.md)、Serilog によるサーバー側ログは [INFRASTRUCTURE.md](./INFRASTRUCTURE.md) を参照。

## 設計方針

ドメイン側の `TurnResult` / `OrderFill` / `SubmittedOrders` はそのままクライアントに返さない。代わりに、プレイヤー視点で意味のある「直近の約定結果」のみをサーバーが短期キャッシュとして保持し、ゲームレスポンスに同梱する。

公開しない情報:

- 個別の `OrderFill`（どの待機注文と何株マッチしたか）
- `SubmittedOrders`（コンピューター注文の生成詳細）
- 約定価格の内訳・板の中身

## サーバー側キャッシュ

[`GameStore`](../../../src/FinLearn.Api/Services/GameStore.cs) がゲームごとに直近の約定履歴を保持する。

```csharp
private readonly ConcurrentDictionary<string, List<TradeResult>> _tradeHistories = new();
private const int MaxRecentTrades = 3;
```

| 項目 | 仕様 |
|---|---|
| 保持件数 | 最大 3 件（`MaxRecentTrades`） |
| 順序 | 時系列昇順（古い順）。リスト末尾が最新 |
| 追加タイミング | `TurnProcessor` が `Trade != null` を返したとき（買い/売りの成功時のみ） |
| 追加されないケース | Wait / 警告ありで `Trade == null` のとき |
| 溢れ時の挙動 | 4 件目を追加するときに先頭（最古）を破棄（FIFO） |
| 永続化 | なし（プロセス内メモリ）。ゲーム終了で消滅 |
| スレッド安全性 | `lock(history)` で追加・取得を保護 |

### 追加フロー

[`GameEndpoints`](../../../src/FinLearn.Api/Endpoints/GameEndpoints.cs) の買い/売りハンドラ:

```
1. processor.Buy/Sell()         → TurnResult
2. if (turn.Trade is not null):
       store.AddTrade(gameId, turn.Trade)
3. GameMapper.ToResponse(...) で recentTrades をレスポンスに同梱
```

Wait は `Trade` が常に `null` のため履歴を変更しない。

## API スキーマ

### `GameResponse`

[`Dtos/GameResponse.cs`](../../../src/FinLearn.Api/Dtos/GameResponse.cs):

```csharp
public sealed record GameResponse(
    string GameId,
    int Turn,
    PlayerDto Player,
    IReadOnlyList<InstrumentDto> Instruments,
    IReadOnlyList<TradeResultDto> RecentTrades,   // ← ログ公開フィールド
    string? Warning = null);
```

### `TradeResultDto`

| フィールド | 型 | 説明 |
|---|---|---|
| `instrumentId` | int | 銘柄 ID |
| `side` | string | `"Buy"` / `"Sell"` |
| `filledQuantity` | int | 約定数量 |
| `totalAmount` | int | 約定金額（手数料を含まない） |
| `fee` | int | 手数料 |

`side` は `OrderSide` enum を `ToString()` した文字列で返す。

### 影響を受けるエンドポイント

| エンドポイント | recentTrades への影響 |
|---|---|
| `POST /api/games/{id}/orders` | 約定成功時のみ末尾に追加（買い・売り共通） |
| `POST /api/games/{id}/wait` | 変更なし（既存の履歴を返す） |
| `GET /api/games/{id}` | 現在の履歴をそのまま返す |

すべてのエンドポイントが `GameResponse` を返すため、クライアントは追加のリクエストなく最新の履歴を取得できる。

## フロントエンド表示

### 型定義

[`frontend/app/types/game.ts`](../../../frontend/app/types/game.ts):

```ts
export type TradeResultDto = {
  instrumentId: number;
  side: string;
  filledQuantity: number;
  totalAmount: number;
  fee: number;
};

export type GameResponse = {
  /* ... */
  recentTrades: TradeResultDto[];
  warning: string | null;
};
```

### TradeHistory コンポーネント

[`frontend/app/components/TradeHistory.tsx`](../../../frontend/app/components/TradeHistory.tsx) が約定履歴テーブルを描画する。

| 列 | 表示内容 | 備考 |
|---|---|---|
| 売買 | `買`（赤） / `売`（青） | `side` を日本語化 |
| 銘柄 ID | `instrumentId` | |
| 約定数量 | `filledQuantity` | |
| 約定金額 | `totalAmount` | JPY フォーマット |
| 手数料 | `fee` | JPY フォーマット |

表示順は **新しい順**: コンポーネント内で `[...trades].reverse()` してから `map` する。サーバーは古い順で返すが、ユーザーには直近を上に見せる。

### 空表示

`trades.length === 0` のときはテーブルごと描画しない（空のヘッダーも表示しない）。ゲーム開始直後は `recentTrades` が空配列になるため自動的に非表示。

### データフロー

[`frontend/app/routes/games.$id.tsx`](../../../frontend/app/routes/games.$id.tsx):

```
clientAction (orders/wait)
  → fetch /api/games/{id}/orders もしくは /api/games/{id}/wait
  → GameResponse （recentTrades 含む）
  → React Router の再描画で TradeHistory に流れ込む
```

クライアント側では履歴をキャッシュせず、毎回サーバーから来た値を表示する。サーバーが正本。

## 警告との関係

`warning` フィールドは約定失敗・部分約定時の理由を伝える独立フィールドであり、`recentTrades` には反映されない（失敗時は履歴に追加されないため）。詳細は [FillResult/API.UI.md](../FillResult/API.UI.md) の「失敗・警告時」セクションを参照。
