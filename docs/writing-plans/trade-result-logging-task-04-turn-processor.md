# Task 4: TurnProcessor の戻り値を TurnResult に置換

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Modify: `src/FinLearn.Core/TurnProcessor.cs`
- Modify: `tests/FinLearn.Tests/TurnProcessorTests.cs`（既存テストの分解部分のみ調整）
- Modify: `src/FinLearn.Api/Endpoints/GameEndpoints.cs`（コンパイルエラー回避のための最小変更）

このタスクは破壊的シグネチャ変更を伴うため、既存テストの「分解構文」だけ機械的に書き換えてコンパイルとグリーンを保つ。新規テストケースは Task 5 で追加する。

- [ ] **Step 1: 既存テストを `TurnResult` プロパティアクセスへ移行**

`tests/FinLearn.Tests/TurnProcessorTests.cs` 全体に対して、以下のパターンを適用する。テスト本体のロジックは変えない。

置換ルール:

| 元の式 | 新しい式 |
|---|---|
| `var (result, trade, warning) = processor.Buy(...)` | `var r = processor.Buy(...); var result = r.Game; var trade = r.Trade; var warning = r.Warning;` |
| `var (result, trade, warning) = processor.Sell(...)` | `var r = processor.Sell(...); var result = r.Game; var trade = r.Trade; var warning = r.Warning;` |
| `var (result, trade, warning) = processor.Wait(...)` | `var r = processor.Wait(...); var result = r.Game; var trade = r.Trade; var warning = r.Warning;` |
| `var (result, _, _)` / `var (_, trade, _)` / `var (_, _, warning)` 等の部分破棄 | 同様に named record 経由でアクセス |
| `var (bought, _, _) = processor.Buy(...)` | `var bought = processor.Buy(...).Game;` |
| `var (turn2, _, _) = processor.Wait(...)` | `var turn2 = processor.Wait(...).Game;` |
| `(current, _, _) = processor.Wait(current, fee: 0);` (再代入) | `current = processor.Wait(current, fee: 0).Game;` |

機械的な置換が分かりにくいケースを 1 つだけ完全な形で示す:

元 (`tests/FinLearn.Tests/TurnProcessorTests.cs:33`):

```csharp
var (result, trade, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

Assert.Null(warning);
Assert.Equal(2, result.Turn);
```

新:

```csharp
var r = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);
var result = r.Game;
var warning = r.Warning;

Assert.Null(warning);
Assert.Equal(2, result.Turn);
```

`for` ループ内の再代入 (`tests/FinLearn.Tests/TurnProcessorTests.cs:464`):

```csharp
for (int i = 0; i < 3; i++)
    (current, _, _) = processor.Wait(current, fee: 0);
```

新:

```csharp
for (int i = 0; i < 3; i++)
    current = processor.Wait(current, fee: 0).Game;
```

複数ターン進行 (`tests/FinLearn.Tests/TurnProcessorTests.cs:207-209`):

```csharp
var turn2 = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1).Game;
var turn3 = processor.Wait(turn2, fee: 0).Game;
var turn4 = processor.Sell(turn3, fee: 0, instrumentId: 1, quantity: 1).Game;
```

参照しないフィールドへの破棄構文 (`var (_, trade, _)` 等) は対応プロパティだけ拾う形に書き換える。

- [ ] **Step 2: TurnProcessor を `TurnResult` に書き換え**

`src/FinLearn.Core/TurnProcessor.cs` 全体を以下に置き換え:

```csharp
namespace FinLearn.Core;

/// <summary>
/// ターン進行のワークフローを担うドメインサービス。
/// Game は状態スナップショットに徹し、TurnProcessor がアクション処理を行う。
/// </summary>
public sealed class TurnProcessor
{
    public IOrderPlacer OrderPlacer { get; }
    public IMarket Market { get; }
    public IPriceFluctuator Fluctuator { get; }
    public IExchangeFactory ExchangeFactory { get; }
    public int ComputerTtl { get; }
    public int PlayerTtl { get; }

    public TurnProcessor(IOrderPlacer orderPlacer, IPriceFluctuator fluctuator,
        int computerTtl = int.MaxValue, int playerTtl = int.MaxValue)
        : this(orderPlacer, new Market(), fluctuator, new SimpleExchangeFactory(), computerTtl, playerTtl)
    {
    }

    public TurnProcessor(IOrderPlacer orderPlacer, IMarket market,
        IPriceFluctuator fluctuator, IExchangeFactory exchangeFactory,
        int computerTtl = int.MaxValue, int playerTtl = int.MaxValue)
    {
        OrderPlacer = orderPlacer;
        Market = market;
        Fluctuator = fluctuator;
        ExchangeFactory = exchangeFactory;
        ComputerTtl = computerTtl;
        PlayerTtl = playerTtl;
    }

    public TurnResult Buy(Game game, int fee, int instrumentId, int quantity, int? price = null, int? stopPrice = null)
    {
        if (quantity <= 0)
            return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return Rejected(game, Messages.PriceMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Buy, quantity, price, stopPrice, Messages.NoMatchingSellOrders);
    }

    public TurnResult Sell(Game game, int fee, int instrumentId, int quantity, int? price = null, int? stopPrice = null)
    {
        if (quantity <= 0)
            return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return Rejected(game, Messages.PriceMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Sell, quantity, price, stopPrice, Messages.NoMatchingBuyOrders);
    }

    public TurnResult Wait(Game game, int fee)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);
        var (bookWithOrders, nextId, placedOrders) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn);

        var nextGame = AdvanceTurn(game, game.Player, bookWithOrders, nextId);
        return new TurnResult(
            Game: nextGame,
            Trade: null,
            Warning: null,
            ProcessedTurn: game.Turn,
            SubmittedOrders: placedOrders,
            Fills: Array.Empty<OrderFill>());
    }

    private TurnResult PlaceOrder(
        Game game, int fee, Instrument instrument, OrderSide side,
        int quantity, int? price, int? stopPrice, string noMatchMessage)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);

        // 1. コンピューター注文を生成 → プレイヤー注文を生成 → 市場で約定
        var (bookWithOrders, nextId, placedOrders) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn);
        var order = game.Player.CreateOrder(nextId, instrument, side, quantity, price, stopPrice, game.Turn);
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        var submittedOrders = Combine(placedOrders, order);

        // 2. 成行注文で約定ゼロ → コンピューター注文は板に残し、Waitと同じ挙動でターンを進める
        if (price is null && matchResult.Trade.FilledQuantity == 0)
        {
            var nextGameNoMatch = AdvanceTurn(game, game.Player, bookWithOrders, nextId);
            return new TurnResult(nextGameNoMatch, null, noMatchMessage, game.Turn, submittedOrders, Array.Empty<OrderFill>());
        }

        // 3. 約定分があればポートフォリオを更新
        var (resultPlayer, warning) = ApplyTradeToPlayer(game.Player, matchResult.Trade);
        if (warning is not null)
        {
            // 残高不足等で約定をロールバック → Fills は空にしてログ＝確定事実の対応関係を保つ
            var rolledBack = AdvanceTurn(game, game.Player, bookWithOrders, nextId);
            return new TurnResult(rolledBack, null, warning, game.Turn, submittedOrders, Array.Empty<OrderFill>());
        }

        // 4. 指値注文の未約定分を板に追加
        var updatedBook = AddRemainingLimitOrder(matchResult.UpdatedBook, order, quantity, matchResult.Trade.FilledQuantity, price);

        // 5. 株価変動を適用して新しいゲーム状態を返す
        var nextGame = AdvanceTurn(game, resultPlayer, updatedBook, nextId + 1);
        return new TurnResult(nextGame, matchResult.Trade, null, game.Turn, submittedOrders, matchResult.Fills);
    }

    private static TurnResult Rejected(Game game, string warning) =>
        new(game, null, warning, game.Turn, Array.Empty<Order>(), Array.Empty<OrderFill>());

    private static IReadOnlyList<Order> Combine(IReadOnlyList<Order> placedOrders, Order playerOrder)
    {
        var combined = new List<Order>(placedOrders.Count + 1);
        combined.AddRange(placedOrders);
        combined.Add(playerOrder);
        return combined;
    }

    private static (Player Result, string? Warning) ApplyTradeToPlayer(Player player, TradeResult trade)
    {
        if (trade.FilledQuantity <= 0)
            return (player, null);

        var (resultPortfolio, warning) = player.Portfolio.ApplyTrade(trade);
        if (warning is not null)
            return (player, warning);

        return (player.WithPortfolio(resultPortfolio), null);
    }

    private static OrderBook AddRemainingLimitOrder(OrderBook book, Order order, int requestedQty, int filledQty, int? price)
    {
        if (price is null)
            return book;

        var remainingQty = requestedQty - filledQty;
        if (remainingQty <= 0)
            return book;

        return book.Add(order.WithQuantity(remainingQty));
    }

    private Game AdvanceTurn(Game game, Player player, OrderBook book, int nextOrderId)
    {
        var newPrices = Fluctuator.Fluctuate(game.Prices);
        var expiredBook = book.ExpireOrders(game.Turn + 1, ComputerTtl, PlayerTtl);
        return new Game(player, game.Turn + 1, expiredBook, nextOrderId, game.Instruments, newPrices);
    }
}
```

仕様表との対応:

| シナリオ | SubmittedOrders | Fills | Warning |
|---|---|---|---|
| Wait | 全コンピューター注文 | 空 | null |
| 引数バリデーション失敗 (qty<=0 / price<=0) | 空 | 空 | エラーメッセージ |
| 成行・約定ゼロ | コンピューター + プレイヤー | 空 | NoMatching... |
| 残高不足ロールバック | コンピューター + プレイヤー | **空** | エラーメッセージ |
| 通常約定 | コンピューター + プレイヤー | matchResult.Fills | null |

仕様の「ProcessedTurn = 処理対象ターン」は **入力 game の Turn** を採用する（成功時の出力 game.Turn は +1 されているため、ここを取ると失敗時とずれる）。

- [ ] **Step 3: GameEndpoints を最小限だけ修正してコンパイルを通す**

`src/FinLearn.Api/Endpoints/GameEndpoints.cs` の変更箇所:

(a) `Wait` ハンドラ (`src/FinLearn.Api/Endpoints/GameEndpoints.cs:50-60`):

```csharp
private static IResult Wait(string id, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config)
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();

    var turn = processor.Wait(game, config.Fee);
    store.UpdateGame(id, turn.Game);
    var exchange = exchangeFactory.Create(turn.Game.Prices, config.Fee);
    var recentTrades = store.GetRecentTrades(id);
    return Results.Ok(GameMapper.ToResponse(id, turn.Game, exchange, recentTrades: recentTrades));
}
```

(b) `Buy` / `Sell` の `Func` シグネチャと `ProcessOrder` (`src/FinLearn.Api/Endpoints/GameEndpoints.cs:38-79`):

```csharp
private static IResult Buy(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config)
{
    return ProcessOrder(id, request, store, processor, exchangeFactory, config,
        (g, fee, req) => processor.Buy(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
}

private static IResult Sell(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config)
{
    return ProcessOrder(id, request, store, processor, exchangeFactory, config,
        (g, fee, req) => processor.Sell(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
}

private static IResult ProcessOrder(
    string id, OrderRequest request, GameStore store, TurnProcessor processor,
    IExchangeFactory exchangeFactory, GameConfig config,
    Func<Game, int, OrderRequest, TurnResult> action)
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();

    var turn = action(game, config.Fee, request);
    if (turn.Warning is null)
    {
        store.UpdateGame(id, turn.Game);
        if (turn.Trade is not null) store.AddTrade(id, turn.Trade);
    }
    var exchange = exchangeFactory.Create(turn.Game.Prices, config.Fee);
    var recentTrades = store.GetRecentTrades(id);
    return Results.Ok(GameMapper.ToResponse(id, turn.Game, exchange, turn.Warning, recentTrades));
}
```

ロギング呼び出しはまだ追加しない（Task 8 で追加する）。

- [ ] **Step 4: 全ビルド + 全テスト実行でグリーンを確認**

Run: `dotnet build && dotnet test`
Expected: PASS（既存テストが新シグネチャでもグリーン、API 統合テストも無修正で通る）

- [ ] **Step 5: コミット**

```bash
git add src/FinLearn.Core/TurnProcessor.cs src/FinLearn.Api/Endpoints/GameEndpoints.cs tests/FinLearn.Tests/TurnProcessorTests.cs
git commit -m "feat(core): TurnProcessor returns TurnResult with submitted orders and fills"
```
