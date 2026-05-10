namespace FinLearn.Tests;

using System;
using System.Collections.Generic;
using FinLearn.Core;
using Xunit;

public class MarketOrderHandlerTests
{
    private static readonly MarketOrderHandler _handler = new();

    private static World CreateWorld(
        Portfolio? playerPortfolio = null,
        OrderBook? book = null,
        int nextOrderId = 100,
        int fee = 0,
        int turn = 1)
    {
        var player = new Player();
        if (playerPortfolio is not null)
            player = player.WithPortfolio(playerPortfolio);

        var prices = new Dictionary<int, int> { [1] = 100 };
        var instruments = new[] { TestData.Instrument1 };
        var computers = TestData.CreateInfiniteComputerPortfolios();
        var game = new Game(player, turn, book ?? new OrderBook(), nextOrderId, instruments, prices, computers);
        return World.FromGame(game, fee, TestData.CreateExchange((1, 100)));
    }

    // ---------- Receive (常に no-op) ----------

    [Fact]
    public void Receive_買い_は常にWorld不変でWarning_null()
    {
        var pf = new Portfolio(cash: 1000, positions: Array.Empty<Position>());
        var world = CreateWorld(playerPortfolio: pf);
        var order = Order.CreateMarket(id: 100, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Buy, quantity: 5, stopPrice: null);

        var (next, warn) = _handler.Receive(world, order);

        Assert.Null(warn);
        Assert.Same(world, next);
    }

    [Fact]
    public void Receive_売り_は常にWorld不変でWarning_null()
    {
        var pf = new Portfolio(cash: 0, positions: new[] { new Position(TestData.Instrument1, quantity: 5) });
        var world = CreateWorld(playerPortfolio: pf);
        var order = Order.CreateMarket(id: 100, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Sell, quantity: 5, stopPrice: null);

        var (next, warn) = _handler.Receive(world, order);

        Assert.Null(warn);
        Assert.Same(world, next);
    }

    // ---------- Settle ----------

    [Fact]
    public void Settle_成行買い_約定するとPortfolio_Cashが減りPositionが増える()
    {
        var pf = new Portfolio(cash: 1000, positions: Array.Empty<Position>());
        var world = CreateWorld(playerPortfolio: pf, fee: 10);
        var order = Order.CreateMarket(id: 100, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Buy, quantity: 5, stopPrice: null);
        // 約定: 5株、合計 500、fee 10
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 5, TotalAmount: 500, Fee: 10);
        var match = new MatchResult(trade, world.Book, Array.Empty<OrderFill>());

        var (next, warn) = _handler.Settle(world, order, match);

        Assert.Null(warn);
        Assert.Equal(1000 - 500 - 10, next.PlayerPortfolio.Cash);
        Assert.Equal(5, next.PlayerPortfolio.QuantityOf(1));
    }

    [Fact]
    public void Settle_成行売り_約定するとPortfolio_Cashが増えPositionが減る()
    {
        var pf = new Portfolio(cash: 100, positions: new[] { new Position(TestData.Instrument1, quantity: 10) });
        var world = CreateWorld(playerPortfolio: pf, fee: 10);
        var order = Order.CreateMarket(id: 100, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Sell, quantity: 5, stopPrice: null);
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 5, TotalAmount: 500, Fee: 10);
        var match = new MatchResult(trade, world.Book, Array.Empty<OrderFill>());

        var (next, warn) = _handler.Settle(world, order, match);

        Assert.Null(warn);
        Assert.Equal(100 + 500 - 10, next.PlayerPortfolio.Cash);
        Assert.Equal(5, next.PlayerPortfolio.QuantityOf(1));
    }

    [Fact]
    public void Settle_約定ゼロ_買い_NoMatchingSellOrdersのWarning_World不変()
    {
        var pf = new Portfolio(cash: 1000, positions: Array.Empty<Position>());
        var world = CreateWorld(playerPortfolio: pf);
        var order = Order.CreateMarket(id: 100, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Buy, quantity: 5, stopPrice: null);
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 0, TotalAmount: 0, Fee: 0);
        var match = new MatchResult(trade, world.Book, Array.Empty<OrderFill>());

        var (next, warn) = _handler.Settle(world, order, match);

        Assert.Equal(Messages.NoMatchingSellOrders, warn);
        Assert.Same(world, next);
    }

    [Fact]
    public void Settle_約定ゼロ_売り_NoMatchingBuyOrdersのWarning_World不変()
    {
        var pf = new Portfolio(cash: 0, positions: new[] { new Position(TestData.Instrument1, quantity: 5) });
        var world = CreateWorld(playerPortfolio: pf);
        var order = Order.CreateMarket(id: 100, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Sell, quantity: 5, stopPrice: null);
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 0, TotalAmount: 0, Fee: 0);
        var match = new MatchResult(trade, world.Book, Array.Empty<OrderFill>());

        var (next, warn) = _handler.Settle(world, order, match);

        Assert.Equal(Messages.NoMatchingBuyOrders, warn);
        Assert.Same(world, next);
    }

    [Fact]
    public void Settle_残高不足の場合_買い_World不変でWarning()
    {
        // available cash 100 しかないが、約定額が 500
        var pf = new Portfolio(cash: 100, positions: Array.Empty<Position>());
        var world = CreateWorld(playerPortfolio: pf);
        var order = Order.CreateMarket(id: 100, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Buy, quantity: 5, stopPrice: null);
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 5, TotalAmount: 500, Fee: 0);
        var match = new MatchResult(trade, world.Book, Array.Empty<OrderFill>());

        var (next, warn) = _handler.Settle(world, order, match);

        Assert.Equal(Messages.InsufficientCashToBuy, warn);
        Assert.Same(world, next);
    }
}
