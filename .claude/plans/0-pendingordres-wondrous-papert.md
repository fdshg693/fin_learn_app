# 取引履歴のゼロ件記録修正 + 未約定注文（PendingOrders）表示

## Context

ユーザーが指値注文を出してマッチしなかった場合、現状のフロント「直近の約定結果」に `約定数量=0` の行が表示されてしまう。約定していない注文を「取引履歴」として記録するのは意味的に誤りで、取引履歴は実際に約定したもの（`FilledQuantity > 0`）のみを残すべき。

加えて、マッチせず板に残ったユーザー自身の注文は、現在 `OrderBookPanel`（全トレーダーの板を表示）の中に埋もれており、ユーザーが自分の未約定注文を一目で確認できない。プレイヤー自身の情報として「未約定注文」一覧を露出する。

### バグの根本原因

[src/FinLearn.Core/TurnProcessor.cs:99-119](src/FinLearn.Core/TurnProcessor.cs#L99-L119): 指値注文（`price is not null`）で `FilledQuantity == 0` の場合、ステップ4 で未約定分を板に追加し、`TurnResult.Trade` には `FilledQuantity=0` の `TradeResult` が入ったまま返される。

[src/FinLearn.Api/Endpoints/GameEndpoints.cs:102](src/FinLearn.Api/Endpoints/GameEndpoints.cs#L102): `if (turn.Trade is not null) store.AddTrade(id, turn.Trade);` の判定では `FilledQuantity == 0` の `TradeResult` も履歴に積まれてしまう。

> 補足: 成行注文の約定ゼロは [TurnProcessor.cs:99-103](src/FinLearn.Core/TurnProcessor.cs#L99-L103) で `Trade: null` を返すため履歴には積まれない。バグは指値注文だけ。

## 変更方針

### 1. 取引履歴のゼロ件記録を排除

**修正箇所**: [src/FinLearn.Api/Endpoints/GameEndpoints.cs:102](src/FinLearn.Api/Endpoints/GameEndpoints.cs#L102)

```csharp
if (turn.Trade is not null && turn.Trade.FilledQuantity > 0)
    store.AddTrade(id, turn.Trade);
```

ドメイン層は触らない（`TurnResult.Trade` は「マッチング処理が走った事実」を表すので `FilledQuantity=0` の値が返るのは正当）。Trade history は API 層 (`GameStore`) のキャッシュなので、そこでフィルタするのが責務として適切。

### 2. PlayerDto に PendingOrders を追加

ユーザー回答に基づき `PlayerDto.pendingOrders` として埋め込む（プレイヤー自身が持つ情報という意味付け）。

**ドメイン側**: 変更不要。`Game.OrderBook.Orders` には `TraderId` 付きの全注文があり、`game.Player.Name` で絞り込める。`Order.ExpiresAtTurn` で残ターン数も計算可。

**API側**:

- 新DTO [src/FinLearn.Api/Dtos/GameResponse.cs](src/FinLearn.Api/Dtos/GameResponse.cs) に追加:
  ```csharp
  public sealed record PendingOrderDto(
      int Id,
      int InstrumentId,
      string Side,           // "Buy" / "Sell"
      string Type,           // "Limit" / "Market"
      int Quantity,
      int? Price,
      int? StopPrice,
      int CreatedAtTurn,
      int ExpiresAtTurn);
  ```
  既存 `OrderDto` から `TraderId` を除いた形（プレイヤー本人のものなので冗長）。
- `PlayerDto` に `IReadOnlyList<PendingOrderDto> PendingOrders` を追加。

- マッパー [src/FinLearn.Api/Mappers/GameMapper.cs](src/FinLearn.Api/Mappers/GameMapper.cs) 内で:
  ```csharp
  var pendingOrders = game.OrderBook.Orders
      .Where(o => o.TraderId == game.Player.Name)
      .Select(o => new PendingOrderDto(
          o.Id, o.Instrument.Id, o.Side.ToString(), o.Type.ToString(),
          o.Quantity, o.Price, o.StopPrice, o.CreatedAtTurn, o.ExpiresAtTurn))
      .ToList();
  ```
  `OrderBook.ExpireOrders` がターン進行時に既に呼ばれているので有効期限切れフィルタは不要。

**フロント側**:

- 型定義 [frontend/app/types/game.ts](frontend/app/types/game.ts):
  - `PlayerDto` に `pendingOrders: PendingOrderDto[]` を追加
  - 新型 `PendingOrderDto` を追加（API 側と同じフィールド、camelCase）

- 新コンポーネント `frontend/app/components/PendingOrders.tsx`:
  - `TradeHistory.tsx` と同形式の memo 化テーブル
  - 列: 売買 / 銘柄ID / 種類（指値・成行）/ 数量 / 価格 / 残ターン
  - 残ターン = `expiresAtTurn - currentTurn`
  - `pendingOrders.length === 0` のときは `<p>未約定注文はありません</p>` を表示（`null` を返さない。`TradeHistory` と違って常設パネルなのでユーザーが「機能が存在する」ことを認識できる方が良い）
  - 見出し: `<h2>未約定注文</h2>`

- レイアウト [frontend/app/routes/games.$id.tsx:148-152](frontend/app/routes/games.$id.tsx#L148-L152):
  `<PlayerPanel ... />` の直下に `<PendingOrders orders={game.player.pendingOrders} currentTurn={game.turn} />` を追加。

- フロントの `TradeHistory` 自体には防御的フィルタを足さない（API がもう 0 件を返さないので不要、コードを増やさない）。

## 影響を受けるファイル

| ファイル | 変更内容 |
|---|---|
| [src/FinLearn.Api/Endpoints/GameEndpoints.cs](src/FinLearn.Api/Endpoints/GameEndpoints.cs) | `AddTrade` 条件に `FilledQuantity > 0` を追加 |
| [src/FinLearn.Api/Dtos/GameResponse.cs](src/FinLearn.Api/Dtos/GameResponse.cs) | `PendingOrderDto` 追加、`PlayerDto.PendingOrders` 追加 |
| [src/FinLearn.Api/Mappers/GameMapper.cs](src/FinLearn.Api/Mappers/GameMapper.cs) | プレイヤー注文を抽出して `PlayerDto` に詰める |
| [tests/FinLearn.Api.Tests/](tests/FinLearn.Api.Tests/) | (新規/既存修正) 約定ゼロ→履歴に積まれない・部分約定→積まれる・PendingOrders 露出のテスト |
| [frontend/app/types/game.ts](frontend/app/types/game.ts) | 型追加 |
| [frontend/app/components/PendingOrders.tsx](frontend/app/components/PendingOrders.tsx) | 新規コンポーネント |
| [frontend/app/components/PendingOrders.test.tsx](frontend/app/components/PendingOrders.test.tsx) | 新規テスト |
| [frontend/app/routes/games.$id.tsx](frontend/app/routes/games.$id.tsx) | `PlayerPanel` 直下に挿入 |

ドキュメント整合: [docs/API/RESPONSE_DTO.md](docs/API/RESPONSE_DTO.md), [docs/API/DESIGN.md](docs/API/DESIGN.md), [docs/FRONT.md](docs/FRONT.md) は実装後に `update-docs` skill で追従更新。

## 検証

### バックエンド
1. `dotnet test tests/FinLearn.Api.Tests` 実行。
2. 新規/拡張する API テスト:
   - **マッチしない指値買い** (高すぎる Sell 価格 / 板に対向注文なし) を出した直後の `recentTrades` に新注文が含まれない。
   - **部分約定指値** で `recentTrades` に約定済み数量で1件追加される。
   - 同レスポンスの `player.pendingOrders` に未約定分が出現する（`quantity` は残数量、`price`/`expiresAtTurn` 一致）。
   - 成行で約定ゼロのケースで `pendingOrders` は空（成行は板に残らない）。
3. ドメインテスト [TurnProcessorTests.cs:280](tests/FinLearn.Tests/TurnProcessorTests.cs#L280) 既存 partial-fill テストはそのまま通ること。

### フロント
1. `cd frontend && npm test` で `PendingOrders.test.tsx` パス（空状態 / 1件以上 / 残ターン計算）。
2. `npm run typecheck` パス。
3. `npm run dev` + バックエンド起動でブラウザ確認:
   - マッチしない高値指値を出す → 取引履歴は変化なし、未約定注文セクションに表示。
   - 板に対向注文がある時に成行を出す → 約定が取引履歴に出る、未約定注文は変わらず。
   - ターン経過で `expiresAtTurn` 到達したら未約定注文セクションから消えること。

## 既存資産の再利用

- `Order.TraderId` / `Player.Name` による所有判定はテスト [TurnProcessorTests.cs:296](tests/FinLearn.Tests/TurnProcessorTests.cs#L296) で確認済み。
- `OrderBook.ExpireOrders` が `AdvanceTurn` 内で呼ばれるため期限切れフィルタを再実装する必要なし [TurnProcessor.cs:160](src/FinLearn.Core/TurnProcessor.cs#L160)。
- 既存 `OrderDto` の構造が PendingOrderDto のテンプレ。
- 既存 `TradeHistory.tsx` のテーブル構造をベースに `PendingOrders.tsx` を作成。
