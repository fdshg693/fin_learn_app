# ログ機能 — ドメイン側のログ構造

> ソース: [`TurnResult`](../../../src/FinLearn.Core/Results/TurnResult.cs) / [`OrderFill`](../../../src/FinLearn.Core/Results/OrderFill.cs) / [`TradeResult`](../../../src/FinLearn.Core/Results/TradeResult.cs) / [`TurnProcessor`](../../../src/FinLearn.Core/TurnProcessor.cs)

## 概要

ログ機能は二層構造になっている:

| 層 | 役割 | ドキュメント |
|---|---|---|
| **ドメインログ** | ターンごとの「何が起きたか」を表す不変オブジェクト群 | 本ドキュメント |
| **API/UI 公開層** | プレイヤー向けに集約・キャッシュして公開 | [API.UI.md](./API.UI.md) |
| **インフラログ** | Serilog による構造化ログ（デバッグ用） | [INFRASTRUCTURE.md](./INFRASTRUCTURE.md) |

本ドキュメントでは、最下層のドメインログ（`TurnResult` を中心とするレコード群）と、それを生成する `TurnProcessor` の振る舞いを扱う。

## TurnResult — ターンごとのログ単位

`TurnProcessor.Buy / Sell / Wait` の戻り値。1ターンに「板に届いた全注文」と「発生した全約定」をひとまとめにしたログレコード。

```csharp
public sealed record TurnResult(
    Game Game,                              // 処理後のゲーム状態
    TradeResult? Trade,                     // プレイヤー注文の取引結果（Wait・失敗時 null）
    string? Warning,                        // エラーメッセージ（成功時 null）
    int ProcessedTurn,                      // この処理が対象としたターン番号
    IReadOnlyList<Order> SubmittedOrders,   // 板に届いた全注文（コンピューター + プレイヤー）
    IReadOnlyList<OrderFill> Fills);        // 発生した全約定明細
```

- `ProcessedTurn` は処理直前の `game.Turn` と等価。ログ行の「いつ」に相当する
- `SubmittedOrders` には自動生成のコンピューター注文も含まれる
- `Fills` はコンピューター同士の約定もすべて含む（[COMPUTER_ORDER/LOGIC.md](../COMPUTER_ORDER/LOGIC.md) 参照）

## OrderFill — 約定明細

```csharp
public sealed record OrderFill(int OrderId, int FilledQuantity, int TotalAmount);
```

`OrderBook.Match` が約定に参加した注文ごとに 1 件ずつ生成する内部レコード。プレイヤー注文・板の待機注文・コンピューター注文を区別せず、すべて同じ形式で記録される。

詳細なマッチングアルゴリズムと並び順は [FillResult/LOGIC.md](../FillResult/LOGIC.md) を参照。

## TradeResult — プレイヤー約定サマリ

```csharp
public sealed record TradeResult(
    int InstrumentId,
    OrderSide Side,
    int FilledQuantity,
    int TotalAmount,
    int Fee);
```

`TurnResult.Trade` に格納される、プレイヤー注文に対する集約結果。複数の `OrderFill` から `Market.Execute` がプレイヤー分のみ抽出して構築する。

`OrderFill` を直接公開する代わりに `TradeResult` を返すことで、「どの待機注文と何株マッチしたか」という板の内部情報を隠蔽している。

## TurnProcessor によるログ生成

```
Buy / Sell の場合:
  1. ComputerTrader.PlaceOrders   → computerOrders
  2. Player.CreateOrder           → playerOrder
  3. SubmittedOrders = computerOrders ∪ {playerOrder}
  4. Market.Execute(playerOrder)  → MatchResult
  5. Fills = MatchResult.Fills
  6. Trade = MatchResult.TradeResult
  7. Portfolio.ApplyTrade で残高更新

Wait の場合:
  1. ComputerTrader.PlaceOrders   → computerOrders
  2. SubmittedOrders = computerOrders
  3. Fills = []
  4. Trade = null
```

### イベントとログの対応

| イベント | `SubmittedOrders` | `Fills` | `Trade` | `Warning` |
|---|---|---|---|---|
| 買い/売り成功 | コンピューター + プレイヤー | 全約定 | 集約結果 | null |
| Wait | コンピューターのみ | 空 | null | null |
| 注文バリデーション失敗（数量0等） | 空 | 空 | null | エラー文 |
| 成行で対当注文なし | コンピューター + プレイヤー | 空 | null | エラー文 |
| 資金不足・保有不足でロールバック | コンピューター + プレイヤー | **空にクリア** | null | エラー文 |

ロールバック時に `Fills` をあえて空にするのは、「ログ = 確定した事実」という不変条件を保つため。板の状態も処理前に巻き戻される。

## 監査トレイル

時刻情報は専用フィールドではなく、複数の場所に分散している:

| フィールド | 場所 | 意味 |
|---|---|---|
| `TurnResult.ProcessedTurn` | `TurnResult` | この `TurnResult` が対象としたターン |
| `Order.CreatedAtTurn` | `Order` | 注文が作成されたターン（過去ターンからの持ち越し判定に使用） |

これにより、板に残っている古い注文と新規注文を区別できる。

## 永続化しないもの

`TurnResult` 自体は API ハンドラ内で消費されるトランジェントなオブジェクト。`Game` には保持されない。ターンを跨いで残るのは:

- `OrderBook` の未約定注文（板の状態）
- `Player.Portfolio`（現金・ポジション）
- `Game.Turn`, `Game.Prices`

の純粋な状態のみ。ログ的情報のうち恒久的に残すのは API 層の直近約定キャッシュ（[API.UI.md](./API.UI.md) 参照）と Serilog のファイル出力（[INFRASTRUCTURE.md](./INFRASTRUCTURE.md) 参照）。

## テスト

[tests/FinLearn.Tests/TurnProcessorLoggingTests.cs](../../../tests/FinLearn.Tests/TurnProcessorLoggingTests.cs) で `TurnResult` の構造を検証している:

- Wait → `SubmittedOrders` がコンピューター注文のみ、`Fills` 空
- 買い成功 → `SubmittedOrders` にプレイヤー含む、`Fills` に約定明細
- バリデーション失敗 → 両方空、`Warning` 設定
- ロールバック → `SubmittedOrders` にプレイヤー含むが `Fills` は空
