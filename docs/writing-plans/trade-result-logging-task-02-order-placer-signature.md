# Task 2: IOrderPlacer の戻り値に PlacedOrders を追加

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Modify: `src/FinLearn.Core/Services/IOrderPlacer.cs`
- Modify: `src/FinLearn.Core/Services/ComputerTrader.cs`
- Modify: `tests/FinLearn.Tests/NoOpOrderPlacer.cs`
- Modify: `src/FinLearn.Core/TurnProcessor.cs`（暫定対応）
- Test: `tests/FinLearn.Tests/ComputerTraderTests.cs`

- [ ] **Step 1: 失敗するテストを書く**

`tests/FinLearn.Tests/ComputerTraderTests.cs` の末尾（クラス内）に追加:

```csharp
[Fact]
public void PlaceOrders_は実際に板に追加した注文をPlacedOrdersで返す()
{
    var trader = new ComputerTrader(new Random(42));
    var instruments = new[] { TestData.Instrument1, TestData.Instrument2 };
    var exchange = TestData.CreateExchange(price: 100, fee: 0);

    var (book, nextId, placed) = trader.PlaceOrders(
        new OrderBook(), exchange, instruments, startOrderId: 100, currentTurn: 5);

    // 20 件（買い10 + 売り10）すべての注文が PlacedOrders に含まれる
    Assert.Equal(20, placed.Count);
    Assert.All(placed, o => Assert.Equal("computer", o.TraderId));
    Assert.All(placed, o => Assert.Equal(5, o.CreatedAtTurn));
    // ID は startOrderId 以降の連番
    Assert.Equal(100, placed[0].Id);
    Assert.Equal(119, placed[^1].Id);
    Assert.Equal(120, nextId);
}
```

- [ ] **Step 2: テスト実行で失敗を確認**

Run: `dotnet test tests/FinLearn.Tests/FinLearn.Tests.csproj --filter "FullyQualifiedName~PlaceOrders_は実際に板に追加した注文をPlacedOrdersで返す"`
Expected: FAIL（コンパイルエラー: タプル要素数不一致）

- [ ] **Step 3: IOrderPlacer インターフェースを拡張**

`src/FinLearn.Core/Services/IOrderPlacer.cs` を置き換え:

```csharp
namespace FinLearn.Core;

/// <summary>
/// 注文を生成してOrderBookに追加する（注文生成戦略のインターフェース）
/// </summary>
public interface IOrderPlacer
{
    (OrderBook UpdatedBook, int NextOrderId, IReadOnlyList<Order> PlacedOrders) PlaceOrders(
        OrderBook book,
        IExchange exchange,
        IReadOnlyList<Instrument> instruments,
        int startOrderId,
        int currentTurn);
}
```

- [ ] **Step 4: ComputerTrader を新シグネチャに対応**

`src/FinLearn.Core/Services/ComputerTrader.cs` の `PlaceOrders` メソッドを以下で置き換え:

```csharp
public (OrderBook UpdatedBook, int NextOrderId, IReadOnlyList<Order> PlacedOrders) PlaceOrders(
    OrderBook book,
    IExchange exchange,
    IReadOnlyList<Instrument> instruments,
    int startOrderId,
    int currentTurn)
{
    var currentId = startOrderId;
    var updatedBook = book;
    var placed = new List<Order>(BuyOrderCount + SellOrderCount);

    // 買い注文: 株価の85〜105%
    for (int i = 0; i < BuyOrderCount; i++)
    {
        var instrument = instruments[_random.Next(instruments.Count)];
        if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
            continue;
        var percent = _random.Next(85, 106);
        var price = Math.Max(1, marketPrice * percent / 100);
        var order = new Order(currentId++, TraderId, instrument, OrderSide.Buy, 1, price, currentTurn);
        placed.Add(order);
        updatedBook = PlaceWithMatching(updatedBook, order);
    }

    // 売り注文: 株価の95〜115%
    for (int i = 0; i < SellOrderCount; i++)
    {
        var instrument = instruments[_random.Next(instruments.Count)];
        if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
            continue;
        var percent = _random.Next(95, 116);
        var price = Math.Max(1, marketPrice * percent / 100);
        var order = new Order(currentId++, TraderId, instrument, OrderSide.Sell, 1, price, currentTurn);
        placed.Add(order);
        updatedBook = PlaceWithMatching(updatedBook, order);
    }

    return (updatedBook, currentId, placed);
}
```

`PlaceWithMatching` プライベートメソッドはそのまま残す（変更不要）。

- [ ] **Step 5: NoOpOrderPlacer を新シグネチャに対応**

`tests/FinLearn.Tests/NoOpOrderPlacer.cs` を置き換え:

```csharp
namespace FinLearn.Tests;

using FinLearn.Core;

/// <summary>
/// 注文を一切生成しないテストダブル。板を空のままにしたいテスト用。
/// </summary>
public sealed class NoOpOrderPlacer : IOrderPlacer
{
    public (OrderBook UpdatedBook, int NextOrderId, IReadOnlyList<Order> PlacedOrders) PlaceOrders(
        OrderBook book, IExchange exchange,
        IReadOnlyList<Instrument> instruments, int startOrderId, int currentTurn)
    {
        return (book, startOrderId, Array.Empty<Order>());
    }
}
```

- [ ] **Step 6: TurnProcessor の OrderPlacer 呼び出しを暫定対応**

`src/FinLearn.Core/TurnProcessor.cs` には `OrderPlacer.PlaceOrders` 呼び出しが 2 箇所あり、現在は 2 タプルへ分解している。3 タプルに合わせるため `_` で無視する暫定対応を入れる。Task 4 で本対応する。

`Wait` メソッド:

```csharp
public (Game Result, TradeResult? Trade, string? Warning) Wait(Game game, int fee)
{
    var exchange = ExchangeFactory.Create(game.Prices, fee);
    var (bookWithOrders, nextId, _) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn);
    return (AdvanceTurn(game, game.Player, bookWithOrders, nextId), null, null);
}
```

`PlaceOrder` プライベートメソッド内の該当行:

```csharp
var (bookWithOrders, nextId, _) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn);
```

- [ ] **Step 7: 全テスト実行でパスを確認**

Run: `dotnet test`
Expected: PASS（既存テスト + 新規 ComputerTrader テストすべてグリーン）

- [ ] **Step 8: コミット**

```bash
git add src/FinLearn.Core/Services/IOrderPlacer.cs src/FinLearn.Core/Services/ComputerTrader.cs src/FinLearn.Core/TurnProcessor.cs tests/FinLearn.Tests/NoOpOrderPlacer.cs tests/FinLearn.Tests/ComputerTraderTests.cs
git commit -m "feat(core): IOrderPlacer returns the orders it placed"
```
