# 01 — Context

## このファイルは何か

このリファクタを引き継ぐ AI/開発者が**最初に読むべき背景知識**。既存ドメイン構造、現状の `TurnProcessor` の責務、そしてなぜリファクタが必要かをまとめる。新規セッションでこのファイルだけ読めば、後続のファイル (`02-target-design.md` 以降) を理解できる粒度を目指している。

## なぜこのリファクタが必要か

`TurnProcessor.PlaceOrder` + `ExecutePlayerOrder` には、性質の異なる処理が同じレベルで混在している:

| 種類 | 例 | リクエスト依存 | 状態依存 | 順序の必然性 |
|---|---|---|---|---|
| **(a) 状態観察 (read)** | `ExchangeFactory.Create(game.Prices, fee)`, `BuildAllPortfolios(game)`, `portfolios[playerName]` の参照 | 不要 | あり | 観察時点だけ守れば順序自由 |
| **(b) Intent 構築 (pure)** | `Player.CreateOrder(...)`, `Combine(placedOrders, order)` | 必要 | 不要 (player identity 除く) | いつ作っても同じ |
| **(c) 世界遷移 (transition)** | `OrderPlacer.PlaceOrders`, `ReserveBuy/Sell`, `Market.Execute`, `ApplyTrade`/`SettleFills`, `AddRemainingLimitOrder`, `AdvanceTurn` | 注文系は intent 必要、ターン進行は不要 | あり | **ここだけが順序必須** |
| **(d) 制御フロー** | 各失敗時の早期 return (`Failed` ヘルパー) | — | — | — |

これらが**フラットに並んでいる**ため、

- Order 作成 (intent) と portfolios[playerName] 観察 (read) — 互いに独立で並列化可能
- AddRemainingLimitOrder と SettleFills — どちらも matchResult から派生するだけで互いに参照しない

といった**「隣にあるが実は独立な行」**と、

- 限値の Reserve → Match → Settle (available チェックが先、約定がないと反映できない)
- 成行の Match → ApplyTrade (同期トランザクション)
- Match の残量確定 → 板更新

といった**「ドメイン的に順序必須な行」**を、コード構造で区別できない。

### 根本原因

1. **"世界の状態" を表す型がない**。`book / portfolios / nextOrderId / exchange / fee` が裸の引数として 6 個並んで関数を渡り歩く。本来これらは一塊の **World snapshot** であり、transition は `World → World` の関数として定義したい。
2. **`portfolios[playerName]` の参照解決が transition の中に散らばる**。Reserve でも ApplyTrade でも、毎回「player 名から portfolio を引く」を書いている。"プレイヤーという観察軸" が型として存在していないから。
3. **限値・成行の分岐が "注文タイプ" 軸で if/else** されている。Reserve 段階・Settlement 段階でそれぞれ `if (price is null)` が出る。本来この分岐は「注文の受付と反映の戦略」というドメイン概念で、`LimitOrderHandler` / `MarketOrderHandler` のような型レベル分岐として表現できる。

## 既存ドメインモデル (リファクタで触る範囲)

### ドメイン階層

```
TurnProcessor (リファクタ対象 — ターン進行ワークフロー)
    ├─ uses IOrderPlacer (computer 注文発注 + 約定 + settlement)
    │   └─ ComputerTrader (実装)
    ├─ uses IMarket (player 注文の付け合わせ)
    │   └─ Market (OrderBook.Match のラッパ)
    ├─ uses IPriceFluctuator (株価変動)
    ├─ uses IExchangeFactory → IExchange
    └─ orchestrates:
        Game (状態スナップショット: Player + ComputerPortfolios + OrderBook + Prices + ...)
        Player (identity + Portfolio + CreateOrder)
        Portfolio (Reserve / Apply / Settle / Release)
        SettlementProcessor (static — fill 群を Portfolio に統一適用)
        OrderBook (Match / Add / ExpireOrders)
```

### 重要な戻り値型 (リファクタで頻出)

| メソッド | 戻り値型 | 失敗パス |
|---|---|---|
| `Portfolio.ReserveBuy(instrumentId, qty, price, fee)` | `(Portfolio, string?)` | あり (残高/数量不足) |
| `Portfolio.ReserveSell(instrumentId, qty)` | `(Portfolio, string?)` | あり |
| `Portfolio.ApplyTrade(trade)` | `(Portfolio, string?)` | あり (成行残高不足) |
| `Portfolio.SettleReservedBuy(...)` | `Portfolio` 直接 | なし |
| `Portfolio.SettleReservedSell(...)` | `Portfolio` 直接 | なし |
| `Portfolio.ReleaseBuyReservation(...)` | `Portfolio` 直接 | なし |
| `Portfolio.ReleaseSellReservation(...)` | `Portfolio` 直接 | なし |
| `SettlementProcessor.SettleFills(...)` | `IReadOnlyDictionary<string, Portfolio>` | なし |
| `SettlementProcessor.ReleaseExpired(...)` | `IReadOnlyDictionary<string, Portfolio>` | なし |
| `OrderBook.ExpireOrders(currentTurn)` | `(OrderBook Updated, IReadOnlyList<Order> Expired)` | — |
| `OrderBook.Match(incoming)` | `FillResult (Fills + UpdatedBook)` | — |
| `IMarket.Execute(book, order, exchange)` | `MatchResult (Trade + UpdatedBook + Fills)` | — |
| `IOrderPlacer.PlaceOrders(...)` | `OrderPlacementResult (UpdatedBook + NextOrderId + PlacedOrders + Fills + UpdatedTraderPortfolios)` | — |

### 予約モデル (Reservation Model) の流れ

```
[Player の Limit 注文]

  Buy/Sell 受付
    │
    ▼
  Portfolio.ReserveBuy / ReserveSell
    │  available cash/positions → reserved に移す
    ├── 失敗 (残高/数量不足) → Wait 化 + warning
    │
    ▼
  Market.Execute (OrderBook.Match)
    │
    ├── 約定 → OrderFill 発生
    └── 未約定 → OrderFill.FilledQuantity = 0 (限値は noMatch でも板に残るので OK)
        │
        ▼
  SettlementProcessor.SettleFills
    │  Limit fill: SettleReservedBuy/Sell (reserved 消費 + 差額返金)
    │  Market fill: ApplyTrade (限値ルートでは使われない)
    │
    ▼
  OrderBook.Add (部分約定なら残量を追加)
    │
    ▼
  AdvanceTurn
    ├── ExpireOrders → ReleaseExpired (失効注文の予約解放)
    ├── Fluctuate (価格変動)
    └── SplitPortfolios (Player / ComputerPortfolios に分解)
```

`Portfolio.CreateInfinite()` (computer 用) では Reserve / Settle / Release / ApplyTrade すべて no-op。

## 現状の TurnProcessor 構造

### Public API (このプランでシグネチャ維持)

```csharp
public TurnResult Buy(Game game, int fee, int instrumentId, int quantity,
    int? price = null, int? stopPrice = null, int expiresInTurns = GameRules.DefaultOrderTtl);

public TurnResult Sell(Game game, int fee, int instrumentId, int quantity,
    int? price = null, int? stopPrice = null, int expiresInTurns = GameRules.DefaultOrderTtl);

public TurnResult Wait(Game game, int fee);
```

### コンストラクタ (このプランでシグネチャ維持)

```csharp
public TurnProcessor(IOrderPlacer orderPlacer, IPriceFluctuator fluctuator);
public TurnProcessor(IOrderPlacer orderPlacer, IMarket market,
    IPriceFluctuator fluctuator, IExchangeFactory exchangeFactory);
```

### Private 内部 (リファクタで置き換え)

| メソッド | 役割 | 最終的な扱い |
|---|---|---|
| `PlaceOrder(...)` | Buy/Sell から呼ばれる本体 | 削除 (新 RunTurn に置き換え) |
| `ExecutePlayerOrder(...)` | 受付→約定→反映→板更新 | 削除 (Handler.Receive / Pipeline.Match / Handler.Settle に分解) |
| `PlayerOrderOutcome` (record struct) | 戻り値タプルのリッチ版 | 削除 (World に統合) |
| `Failed(book, portfolios, warning)` | 失敗時のヘルパー | 削除 (World.WithWarning 等で表現) |
| `BuildAllPortfolios(game)` | Player + ComputerPortfolios 統合 | 削除 (`World.FromGame` に統合) |
| `SplitPortfolios(player, all)` | World 分解 → Game 用 | `AdvanceTurn` 内のヘルパとして残す or 統合 |
| `BuildOrdersByIdSnapshot(book, order)` | settlement 用スナップショット | `LimitOrderHandler` 内に移動 |
| `AddRemainingLimitOrder(book, order, filledQty)` | 残量を板に追加 | Pipeline インラインまたは別ヘルパー |
| `AdvanceTurn(...)` | 価格変動 + 失効処理 + 予約解放 + 状態分解 | 維持 (引数を World 経由に変更) |
| `Combine(placedOrders, playerOrder)` | submittedOrders 構築 | 維持 |
| `Rejected(game, warning)` | 早期バリデーション失敗 (ターン進めない) | 維持 |

### 現状フロー (リファクタ前)

```
Buy/Sell/Wait
  ↓
1. exchange = ExchangeFactory.Create(game.Prices, fee)
2. allPortfolios = BuildAllPortfolios(game)
3. placement = OrderPlacer.PlaceOrders(...)
   └ portfolios = new Dictionary<>(placement.UpdatedTraderPortfolios)
4. order = game.Player.CreateOrder(placement.NextOrderId, ...)
5. submittedOrders = Combine(placement.PlacedOrders, order)
6. ExecutePlayerOrder:
   a. (限値) ReserveBuy/Sell → 失敗時 Failed return
   b. Market.Execute → matchResult
   c. (成行 fill=0) Failed return
   d. (成行) ApplyTrade → 失敗時 Failed return
   e. (限値) BuildOrdersByIdSnapshot → SettlementProcessor.SettleFills
   f. AddRemainingLimitOrder
7. AdvanceTurn (price fluctuate, expire, release)
8. return TurnResult
```

## 参考ファイル一覧

### コア型 (リファクタで触る or 観察する)

| ファイル | 役割 |
|---|---|
| [src/FinLearn.Core/TurnProcessor.cs](../../../src/FinLearn.Core/TurnProcessor.cs) | リファクタ対象本体 |
| [src/FinLearn.Core/Game.cs](../../../src/FinLearn.Core/Game.cs) | 状態スナップショット (Turn / Player / OrderBook / NextOrderId / Instruments / Prices / ComputerPortfolios) |
| [src/FinLearn.Core/Models/Portfolio.cs](../../../src/FinLearn.Core/Models/Portfolio.cs) | Reserve / Settle / Apply / Release メソッド群 |
| [src/FinLearn.Core/Models/Player.cs](../../../src/FinLearn.Core/Models/Player.cs) | identity (Name) + Portfolio + `CreateOrder` |
| [src/FinLearn.Core/Models/Order.cs](../../../src/FinLearn.Core/Models/Order.cs) | 注文 entity。`Type`, `Side`, `Price`(int?), `Quantity`, etc. |
| [src/FinLearn.Core/Models/OrderBook.cs](../../../src/FinLearn.Core/Models/OrderBook.cs) | `Add` / `Match` / `ExpireOrders` |
| [src/FinLearn.Core/Services/IOrderPlacer.cs](../../../src/FinLearn.Core/Services/IOrderPlacer.cs) | + `OrderPlacementResult` (UpdatedBook / NextOrderId / PlacedOrders / Fills / UpdatedTraderPortfolios) |
| [src/FinLearn.Core/Services/ComputerTrader.cs](../../../src/FinLearn.Core/Services/ComputerTrader.cs) | computer1〜10 注文発注 + 内部で SettlementProcessor 利用 |
| [src/FinLearn.Core/Services/SettlementProcessor.cs](../../../src/FinLearn.Core/Services/SettlementProcessor.cs) | `SettleFills` / `ReleaseExpired` / `ComputePostFillRemainingQty` |
| [src/FinLearn.Core/Services/IMarket.cs](../../../src/FinLearn.Core/Services/IMarket.cs), [Market.cs](../../../src/FinLearn.Core/Services/Market.cs) | OrderBook.Match のラッパ。`MatchResult (Trade + UpdatedBook + Fills)` |
| [src/FinLearn.Core/Services/IExchange.cs](../../../src/FinLearn.Core/Services/IExchange.cs), [IExchangeFactory.cs](../../../src/FinLearn.Core/Services/IExchangeFactory.cs) | 価格取得 + fee |
| [src/FinLearn.Core/Services/IPriceFluctuator.cs](../../../src/FinLearn.Core/Services/IPriceFluctuator.cs) | 価格変動 |
| [src/FinLearn.Core/Results/TurnResult.cs](../../../src/FinLearn.Core/Results/TurnResult.cs) | ターン結果 (Game, Trade?, Warning?, ProcessedTurn, SubmittedOrders, Fills) |
| [src/FinLearn.Core/Results/MatchResult.cs](../../../src/FinLearn.Core/Results/MatchResult.cs) | Trade + UpdatedBook + Fills |
| [src/FinLearn.Core/Messages.cs](../../../src/FinLearn.Core/Messages.cs) | エラーメッセージ定数 |

### テスト (リファクタで影響を受ける)

| ファイル | テスト数 | 影響 |
|---|---|---|
| [tests/FinLearn.Tests/TurnProcessorTests.cs](../../../tests/FinLearn.Tests/TurnProcessorTests.cs) | 42 | 公開 API 維持なので**書き換え不要のはず** |
| [tests/FinLearn.Tests/TurnProcessorLoggingTests.cs](../../../tests/FinLearn.Tests/TurnProcessorLoggingTests.cs) | 12 | 同上 |
| [tests/FinLearn.Api.Tests/GameApiTests.cs](../../../tests/FinLearn.Api.Tests/GameApiTests.cs) | 40 | 公開 API 維持なので影響なし |

### テストヘルパー

| ファイル | 役割 |
|---|---|
| [tests/FinLearn.Tests/TestData.cs](../../../tests/FinLearn.Tests/TestData.cs) | `Instrument1/2`, `CreateExchange`, `CreateInfiniteComputerPortfolios` |
| [tests/FinLearn.Tests/TestExchange.cs](../../../tests/FinLearn.Tests/TestExchange.cs) | `IExchange` 固定価格 |
| [tests/FinLearn.Tests/NoOpOrderPlacer.cs](../../../tests/FinLearn.Tests/NoOpOrderPlacer.cs) | `IOrderPlacer` 何もしない |
| [tests/FinLearn.Tests/NoPriceFluctuator.cs](../../../tests/FinLearn.Tests/NoPriceFluctuator.cs) | `IPriceFluctuator` 価格不変 |

### 関連ドキュメント・ルール

| ファイル | 内容 |
|---|---|
| [docs/DDD/MAIN.md](../../../docs/DDD/MAIN.md) | ドメイン用語集 |
| [.claude/rules/src/core-domain.md](../../rules/src/core-domain.md) | コアドメインの責務とコンベンション (このリファクタの完了時に更新が必要) |
| [.claude/rules/always/architecture.md](../../rules/always/architecture.md) | 全体構造・設計パターン |
| [docs/FEATURES/VALIDATION/LOGIC.md](../../../docs/FEATURES/VALIDATION/LOGIC.md) | 3 層バリデーション |

### API 層 (シグネチャ維持の確認用)

| ファイル | 役割 |
|---|---|
| [src/FinLearn.Api/Program.cs](../../../src/FinLearn.Api/Program.cs) | `TurnProcessor` の DI 登録 |
| [src/FinLearn.Api/Endpoints/GameEndpoints.cs](../../../src/FinLearn.Api/Endpoints/GameEndpoints.cs) | `Buy/Sell/Wait` の呼び出し元 |

## 直前のリファクタコンテキスト

このプランの前段で、`TurnProcessor.PlaceOrder` を `PlayerOrderOutcome` 型 + `Failed` ヘルパーで整理する**表面的なリファクタ**を実施した (1 段階目)。これは早期 return の重複を消したが、**ドメイン軸の責務分離にはなっていない**ことが指摘され、本プラン (2 段階目: World + Handler 化) を立てることになった。

1 段階目の成果物 (`PlayerOrderOutcome`, `Failed` ヘルパー) は、本プランの Step 6 で削除される予定。

## このリファクタで解決される具体的な問題

リファクタ完了後:

- **`World` → `World` の関数型シグネチャ**で transition 操作の境界が明確になる
- **`world.PlayerPortfolio` の 1 つのプロパティ**で player portfolio 観察が表現され、`portfolios[playerName]` の散在が消える
- **`LimitOrderHandler` / `MarketOrderHandler` の戦略分離**で、`if (price is null)` の散在が消える
- **`Match` が Pipeline 共通**になり、Limit/Market のどちらでも同じ呼び方
- **Wait と PlaceOrder が統一 Pipeline** に集約され、フローの違いが "intent / handler の有無" だけで表現される
