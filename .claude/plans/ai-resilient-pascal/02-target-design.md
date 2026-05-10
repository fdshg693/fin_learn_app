# 02 — Target Design

## このファイルは何か

リファクタ完了後の**最終形**を定義する。新型 (`World`, `IPlayerOrderHandler`, `LimitOrderHandler`, `MarketOrderHandler`) と新 Pipeline (`RunTurn`) の型シグネチャ・責務・呼び出し関係を確定する。

実装ステップは [03-migration-steps.md](03-migration-steps.md) を参照。背景は [01-context.md](01-context.md) を参照。

## 確定済み設計判断 (再掲)

- **Handler 境界**: `Receive` / `Settle` 分離 + `Match` は pipeline 共通実行
- **Public API 維持**: `TurnProcessor.Buy / Sell / Wait` のシグネチャは変えない
- **段階的移行**: 旧コードと並走、最後に削除

## 全体図 (リファクタ後)

```
┌────────────────────────────────────────────────────────────┐
│  TurnProcessor.Buy / Sell / Wait  (public, シグネチャ維持) │
│      ├ バリデーション (quantity > 0, price > 0, ...)        │
│      ├ Handler 選択 (Limit / Market / null=Wait)           │
│      └ intent factory 構築 (orderId → Order)                │
└────────────────────────────────────────────────────────────┘
                            ↓
┌────────────────────────────────────────────────────────────┐
│  TurnProcessor.RunTurn (新, private)  — Pipeline 本体        │
└────────────────────────────────────────────────────────────┘
   ↓
[1] World.FromGame(game, fee, exchange) — 観察
   ↓
[2] Computer phase: OrderPlacer.PlaceOrders → World 更新
   ↓
[3] Player Intent 構築 (intentFactory が null なら Wait)
   ↓
[4] Handler.Receive(world, order) → (World, warning?)
       └ warning があれば AdvanceTurn して Wait 化終了
   ↓
[5] Match (pipeline 共通): Market.Execute(world.Book, order, world.Exchange)
   ↓
[6] Handler.Settle(world, order, matchResult) → (World, warning?)
       └ warning があれば AdvanceTurn して Wait 化終了 (成行残高不足 / 成行 fill=0)
   ↓
[7] AddRemainingLimitOrder (限値の未約定分を板に追加)
   ↓
[8] AdvanceTurn (price fluctuate, expire, release reservations) → 次の Game
   ↓
TurnResult 構築
```

## 新型定義

### World (新規 — `src/FinLearn.Core/World.cs`)

世界スナップショット。**immutable**, `record class` で `with`-expression 更新可能。

```csharp
namespace FinLearn.Core;

/// <summary>
/// ターン処理中の世界スナップショット。
/// Pipeline / Handler は World → World の関数として動く。
/// </summary>
internal sealed record World(
    OrderBook Book,
    IReadOnlyDictionary<string, Portfolio> Portfolios,
    int NextOrderId,
    IExchange Exchange,
    int Fee,
    string PlayerName,
    int Turn,
    IReadOnlyList<Instrument> Instruments,
    IReadOnlyDictionary<int, int> Prices)
{
    /// <summary>Player の Portfolio を観察する (dict 直接アクセスを排除)。</summary>
    public Portfolio PlayerPortfolio => Portfolios[PlayerName];

    public World WithBook(OrderBook book) => this with { Book = book };

    public World WithPortfolios(IReadOnlyDictionary<string, Portfolio> portfolios) =>
        this with { Portfolios = portfolios };

    /// <summary>Player の Portfolio だけを差し替える (dict は新インスタンス)。</summary>
    public World WithPlayerPortfolio(Portfolio playerPortfolio)
    {
        var dict = new Dictionary<string, Portfolio>(Portfolios) { [PlayerName] = playerPortfolio };
        return this with { Portfolios = dict };
    }

    public World WithNextOrderId(int nextOrderId) => this with { NextOrderId = nextOrderId };

    /// <summary>
    /// Game から World を構築する。
    /// Player.Portfolio + ComputerPortfolios を統合 dict にする (= 旧 BuildAllPortfolios)。
    /// </summary>
    public static World FromGame(Game game, int fee, IExchange exchange)
    {
        var portfolios = new Dictionary<string, Portfolio>(game.ComputerPortfolios.Count + 1);
        foreach (var (id, pf) in game.ComputerPortfolios)
            portfolios[id] = pf;
        portfolios[game.Player.Name] = game.Player.Portfolio;

        return new World(
            Book: game.OrderBook,
            Portfolios: portfolios,
            NextOrderId: game.NextOrderId,
            Exchange: exchange,
            Fee: fee,
            PlayerName: game.Player.Name,
            Turn: game.Turn,
            Instruments: game.Instruments,
            Prices: game.Prices);
    }
}
```

### IPlayerOrderHandler (新規 — `src/FinLearn.Core/Services/IPlayerOrderHandler.cs`)

```csharp
namespace FinLearn.Core;

/// <summary>
/// プレイヤー注文の受付と反映を担う戦略インターフェース。
/// Match は pipeline 共通で実行されるため、handler の責務外。
/// </summary>
internal interface IPlayerOrderHandler
{
    /// <summary>
    /// 注文の受付段階。
    /// 限値: ReserveBuy/Sell で available → reserved に移す。失敗時は warning を返す (World 不変)。
    /// 成行: no-op (World と null を返す)。
    /// </summary>
    (World World, string? Warning) Receive(World world, Order order);

    /// <summary>
    /// 約定結果を世界に反映する段階。
    /// 限値: SettlementProcessor.SettleFills 経由で予約消費 + 差額返金。失敗パスなし。
    /// 成行: ApplyTrade で同期適用。fill=0 や残高不足は warning を返す (World 不変)。
    /// </summary>
    (World World, string? Warning) Settle(World world, Order order, MatchResult match);
}
```

### LimitOrderHandler (新規 — `src/FinLearn.Core/Services/LimitOrderHandler.cs`)

```csharp
namespace FinLearn.Core;

internal sealed class LimitOrderHandler : IPlayerOrderHandler
{
    public (World World, string? Warning) Receive(World world, Order order)
    {
        var price = order.Price!.Value; // 限値前提
        var pf = world.PlayerPortfolio;
        var (reserved, warn) = order.Side == OrderSide.Buy
            ? pf.ReserveBuy(order.Instrument.Id, order.Quantity, price, world.Fee)
            : pf.ReserveSell(order.Instrument.Id, order.Quantity);

        if (warn is not null)
            return (world, warn);

        return (world.WithPlayerPortfolio(reserved), null);
    }

    public (World World, string? Warning) Settle(World world, Order order, MatchResult match)
    {
        // 限値は noMatch でも板に残るので fill=0 でも warning は返さない。
        var ordersById = BuildOrdersByIdSnapshot(world.Book, order);
        var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(match.Fills, ordersById);
        var settled = SettlementProcessor.SettleFills(match.Fills, ordersById, postFillRemaining, world.Portfolios, world.Fee);
        return (world.WithPortfolios(settled), null);
    }

    /// <summary>
    /// fill 逆引き用スナップショット。
    /// 旧 TurnProcessor.BuildOrdersByIdSnapshot を移設。
    /// </summary>
    private static IReadOnlyDictionary<int, Order> BuildOrdersByIdSnapshot(OrderBook bookBeforePlayerMatch, Order playerOrder)
    {
        var dict = new Dictionary<int, Order>();
        foreach (var o in bookBeforePlayerMatch.Orders)
            dict[o.Id] = o;
        dict[playerOrder.Id] = playerOrder;
        return dict;
    }
}
```

**注**: `Settle` に渡される `world.Book` は **player match 前** の状態でなければならない。Pipeline は match 結果を反映する前に Settle を呼ぶ必要がある (詳細は Pipeline 節を参照)。

### MarketOrderHandler (新規 — `src/FinLearn.Core/Services/MarketOrderHandler.cs`)

```csharp
namespace FinLearn.Core;

internal sealed class MarketOrderHandler : IPlayerOrderHandler
{
    public (World World, string? Warning) Receive(World world, Order order) => (world, null);

    public (World World, string? Warning) Settle(World world, Order order, MatchResult match)
    {
        if (match.Trade.FilledQuantity == 0)
        {
            var warn = order.Side == OrderSide.Buy
                ? Messages.NoMatchingSellOrders
                : Messages.NoMatchingBuyOrders;
            return (world, warn);
        }

        var (after, applyWarn) = world.PlayerPortfolio.ApplyTrade(match.Trade);
        if (applyWarn is not null)
            return (world, applyWarn);

        return (world.WithPlayerPortfolio(after), null);
    }
}
```

**設計上の重要ポイント**:

- 旧コードでは「成行 fill=0」と「成行 ApplyTrade 失敗」が別々の早期 return 地点だったが、新設計では **両方とも `Settle` 内に集約**。
- Match 自体は pipeline 共通なので、Receive 段階で world.Book を更新せず、Settle 段階で受け取った matchResult から判断する。
- ロールバック条件 (matchResult.UpdatedBook を捨てる) は Pipeline 側の **「Settle が warning を返したら matchResult を捨てる」** という単純なルールに集約される。

### Pipeline (TurnProcessor.RunTurn — リファクタ後)

```csharp
public sealed class TurnProcessor
{
    // (コンストラクタとプロパティは現状維持)

    public TurnResult Buy(Game game, int fee, int instrumentId, int quantity,
        int? price = null, int? stopPrice = null, int expiresInTurns = GameRules.DefaultOrderTtl)
    {
        if (quantity <= 0) return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0) return Rejected(game, Messages.PriceMustBePositive);
        if (expiresInTurns <= 0) return Rejected(game, Messages.ExpiresInTurnsMustBePositive);

        return RunTurn(game, fee,
            handler: SelectHandler(price),
            intentFactory: nextOrderId => game.Player.CreateOrder(
                nextOrderId, new Instrument(instrumentId), OrderSide.Buy,
                quantity, price, stopPrice, game.Turn, game.Turn + expiresInTurns));
    }

    public TurnResult Sell(...) // Buy と対称

    public TurnResult Wait(Game game, int fee) =>
        RunTurn(game, fee, handler: null, intentFactory: null);

    private static IPlayerOrderHandler SelectHandler(int? price) =>
        price is null ? new MarketOrderHandler() : new LimitOrderHandler();

    /// <summary>
    /// ターン処理 pipeline。
    /// intentFactory が null = Wait、それ以外は Buy/Sell。
    /// </summary>
    private TurnResult RunTurn(Game game, int fee,
        IPlayerOrderHandler? handler, Func<int, Order>? intentFactory)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);
        var world = World.FromGame(game, fee, exchange);

        // [Phase: Computer] computer 注文発注 + 約定 + settlement
        var placement = OrderPlacer.PlaceOrders(
            world.Book, exchange, world.Instruments, world.NextOrderId, world.Turn, world.Portfolios);
        world = world
            .WithBook(placement.UpdatedBook)
            .WithPortfolios(placement.UpdatedTraderPortfolios)
            .WithNextOrderId(placement.NextOrderId);

        // [Phase: Player Intent] Order 作成 (Wait なら null)
        var order = intentFactory?.Invoke(world.NextOrderId);
        var submittedOrders = order is null
            ? placement.PlacedOrders
            : Combine(placement.PlacedOrders, order);

        if (order is null || handler is null)
            return BuildTurnResult(game, world, trade: null, warning: null,
                submittedOrders, fills: Array.Empty<OrderFill>());

        world = world.WithNextOrderId(world.NextOrderId + 1);

        // [Phase: Receive] 限値: 予約 / 成行: no-op
        var (afterReceive, receiveWarn) = handler.Receive(world, order);
        if (receiveWarn is not null)
            return BuildTurnResult(game, world, trade: null, warning: receiveWarn,
                submittedOrders, fills: Array.Empty<OrderFill>());
        world = afterReceive;

        // [Phase: Match] pipeline 共通
        var match = Market.Execute(world.Book, order, exchange);

        // [Phase: Settle] 結果反映 (失敗時は match を捨てる = world.Book を変えない)
        var (afterSettle, settleWarn) = handler.Settle(world, order, match);
        if (settleWarn is not null)
            return BuildTurnResult(game, world, trade: null, warning: settleWarn,
                submittedOrders, fills: Array.Empty<OrderFill>());
        world = afterSettle;

        // [Phase: BookUpdate] match 結果と限値残量で板を確定
        var finalBook = AddRemainingLimitOrder(match.UpdatedBook, order, match.Trade.FilledQuantity);
        world = world.WithBook(finalBook);

        return BuildTurnResult(game, world, trade: match.Trade, warning: null,
            submittedOrders, fills: match.Fills);
    }

    /// <summary>
    /// World を Game に書き戻し、AdvanceTurn してから TurnResult を組み立てる。
    /// </summary>
    private TurnResult BuildTurnResult(Game inputGame, World world,
        TradeResult? trade, string? warning,
        IReadOnlyList<Order> submittedOrders, IReadOnlyList<OrderFill> fills)
    {
        var nextGame = AdvanceTurn(inputGame, world);
        return new TurnResult(
            Game: nextGame,
            Trade: trade,
            Warning: warning,
            ProcessedTurn: inputGame.Turn,
            SubmittedOrders: submittedOrders,
            Fills: fills);
    }

    /// <summary>
    /// 価格変動 + 失効処理 + 失効注文の予約解放 + Player/ComputerPortfolios 分解。
    /// 旧 AdvanceTurn の引数 (book / nextOrderId / portfolios) を World 経由に変更。
    /// </summary>
    private Game AdvanceTurn(Game inputGame, World world)
    {
        var newPrices = Fluctuator.Fluctuate(world.Prices);
        var (expiredBook, expired) = world.Book.ExpireOrders(world.Turn + 1);
        var afterRelease = SettlementProcessor.ReleaseExpired(expired, world.Portfolios, world.Fee);

        var (player, computers) = SplitPortfolios(inputGame.Player, afterRelease, world.PlayerName);
        return new Game(
            player, world.Turn + 1, expiredBook, world.NextOrderId,
            world.Instruments, newPrices, computers);
    }

    private static (Player Player, IReadOnlyDictionary<string, Portfolio> ComputerPortfolios)
        SplitPortfolios(Player original, IReadOnlyDictionary<string, Portfolio> all, string playerName)
    {
        var newPlayer = original.WithPortfolio(all[playerName]);
        var computers = new Dictionary<string, Portfolio>(all.Count - 1);
        foreach (var (id, pf) in all)
            if (id != playerName) computers[id] = pf;
        return (newPlayer, computers);
    }

    private static OrderBook AddRemainingLimitOrder(OrderBook book, Order order, int filledQty)
    {
        if (order.Price is null) return book;
        var remaining = order.Quantity - filledQty;
        if (remaining <= 0) return book;
        return book.Add(order.WithQuantity(remaining));
    }

    private static IReadOnlyList<Order> Combine(IReadOnlyList<Order> placedOrders, Order playerOrder)
    {
        var combined = new List<Order>(placedOrders.Count + 1);
        combined.AddRange(placedOrders);
        combined.Add(playerOrder);
        return combined;
    }

    private static TurnResult Rejected(Game game, string warning) =>
        new(game, null, warning, game.Turn, Array.Empty<Order>(), Array.Empty<OrderFill>());
}
```

## 旧型/メソッドの削除対象 (Step 6 で実施)

| 項目 | 理由 |
|---|---|
| `TurnProcessor.PlaceOrder(...)` | 新 `RunTurn` に置き換え |
| `TurnProcessor.ExecutePlayerOrder(...)` | Handler.Receive / Pipeline.Match / Handler.Settle に分解 |
| `TurnProcessor.PlayerOrderOutcome` (record struct) | World に統合 |
| `TurnProcessor.Failed(...)` | World 経由の return で不要に |
| `TurnProcessor.BuildAllPortfolios(...)` | `World.FromGame` に統合 |
| `TurnProcessor.BuildOrdersByIdSnapshot(...)` | `LimitOrderHandler` 内に移設 |

## 設計上の注意点

### Order intent の作成タイミング

**問題**: `Order.Id` は `placement.NextOrderId` (computer phase 後) が必要なので、Buy/Sell の入口で Order を作れない。

**解決**: `Func<int, Order>` (intentFactory) を Buy/Sell から RunTurn に渡し、computer phase 完了後に `intentFactory(world.NextOrderId)` で構築。これにより:

- Player という観察 (`game.Player.CreateOrder` の呼び出し) が遅延される
- "観察と Intent 構築は遅延可能で、Pipeline の進行に従属しない" という設計意図が型に出る
- Wait は intentFactory = null として自然に表現される

### Receive 失敗時の扱い

旧コードでは "予約失敗 → Wait 化、computer settlement は確定維持" だった。新コードでも同じ:

- Receive で warning が返ったら、world は **computer phase 直後の状態のまま** (Receive 内部で `WithPlayerPortfolio` していないので)
- そのまま `BuildTurnResult` で AdvanceTurn → 次 Game 構築
- **不変条件**: `Failed` 系は computer settlement 結果を保持したまま AdvanceTurn する

### Settle 失敗時のロールバック

旧コードでは "matchResult.UpdatedBook を捨てて placement.UpdatedBook を使う" だった。新コードでは:

- Settle が warning を返したら、Pipeline は `world.WithBook(match.UpdatedBook)` を呼ばない
- `world` は依然として **Receive 後の状態** (Receive で予約済みなら reserved 入り)
- ⚠️ **要検証**: 限値で Settle が失敗するケースはない (LimitOrderHandler.Settle は warning 返さない設計) ので、実質これは成行のみの話
- 成行は Receive で予約しないので、`world` は computer phase 直後相当
- 板も `match.UpdatedBook` を反映しないので player match 前のまま

→ 旧コードと挙動が一致することを確認すること (Step 5 のテストで)。

### Limit ハンドラの Settle で `world.Book` の状態

`LimitOrderHandler.Settle` は `BuildOrdersByIdSnapshot(world.Book, order)` を呼ぶが、`world.Book` は **Receive 後 / Match 前** の状態 (= 旧コードの `placement.UpdatedBook`) でなければならない。Pipeline は Match の結果 (`match.UpdatedBook`) を world に書き戻さずに Settle を呼ぶので、これは満たされる。

### BuildOrdersByIdSnapshot の引数

旧コードでは `BuildOrdersByIdSnapshot(placement.UpdatedBook, order)` を呼んでおり、これは player match **前** の板。`SettlementProcessor.ComputePostFillRemainingQty` の計算で `Order.Quantity` (元の発注数量) が必要なため。

新設計では `LimitOrderHandler.Settle` が `world.Book` (= player match 前) と `order` (= 元の Order) を直接受け取るので問題なし。

### `Wait` の挙動

旧 `Wait` は computer phase + AdvanceTurn のみで、player intent はなし。新 `RunTurn` で `intentFactory == null` のとき:

- Receive / Match / Settle はスキップ
- submittedOrders は placement.PlacedOrders のみ
- Fills は空
- nextOrderId は `placement.NextOrderId` のまま (player order 採番してないので +1 しない)

これは旧 `Wait` と等価。

## 暗黙の不変条件

リファクタ後も維持すべき不変条件 (失敗時の挙動含めて):

1. **限値の予約成功 → 約定の settlement は決して失敗しない** (LimitOrderHandler.Settle は warning 返さない)
2. **per-order fee** (注文単位で1回。完全消化の fill で計上、部分約定は fee=0) — `SettlementProcessor.SettleFills` 内のロジックを変えないこと
3. **`Portfolio.Cash` は available のみ** — World 経由でも変わらない
4. **成行ロールバックの対象は player の market fill のみ** — computer settlement は確定維持
5. **失効注文の予約解放は AdvanceTurn 内** — Receive / Settle で予約した分も、ターンを跨いで失効するなら `ReleaseExpired` で解放される

リファクタ後は `World` 型のおかげで「どの段階でどの portfolio が更新されているか」が明示されるので、これらの不変条件をコードで読みやすくなる。
