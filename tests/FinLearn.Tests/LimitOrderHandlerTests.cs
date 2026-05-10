namespace FinLearn.Tests;

using System.Collections.Generic;
using FinLearn.Core;
using Xunit;

public class LimitOrderHandlerTests
{
    private static readonly LimitOrderHandler _handler = new();

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

    // ---------- Receive ----------

    [Fact]
    public void Receive_買い注文_で指値予約される_AvailableCashが減りReservedCashが増える()
    {
        var pf = new Portfolio(cash: 10000, positions: System.Array.Empty<Position>());
        var world = CreateWorld(playerPortfolio: pf, fee: 5);
        var order = new Order(Id: 100, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 10, Price: 100);

        var (next, warn) = _handler.Receive(world, order);

        Assert.Null(warn);
        // amount = 10*100 + 5 = 1005
        Assert.Equal(10000 - 1005, next.PlayerPortfolio.Cash);
        Assert.Equal(1005, next.PlayerPortfolio.ReservedCash);
    }

    [Fact]
    public void Receive_売り注文_で指値予約される_AvailableQtyが減りReservedQtyが増える()
    {
        var position = new Position(TestData.Instrument1, quantity: 10);
        var pf = new Portfolio(cash: 0, positions: new[] { position });
        var world = CreateWorld(playerPortfolio: pf);
        var order = new Order(Id: 100, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Sell, Quantity: 4, Price: 100);

        var (next, warn) = _handler.Receive(world, order);

        Assert.Null(warn);
        Assert.Equal(10, next.PlayerPortfolio.QuantityOf(1));
        Assert.Equal(4, next.PlayerPortfolio.ReservedQuantityOf(1));
        Assert.Equal(6, next.PlayerPortfolio.AvailableQuantityOf(1));
    }

    [Fact]
    public void Receive_残高不足の場合_World不変でWarningが返る()
    {
        var pf = new Portfolio(cash: 100, positions: System.Array.Empty<Position>());
        var world = CreateWorld(playerPortfolio: pf);
        var order = new Order(Id: 100, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 10, Price: 100);

        var (next, warn) = _handler.Receive(world, order);

        Assert.Equal(Messages.InsufficientCashToBuy, warn);
        Assert.Same(world, next);
    }

    [Fact]
    public void Receive_数量不足の場合_World不変でWarningが返る()
    {
        var pf = new Portfolio(cash: 0, positions: System.Array.Empty<Position>());
        var world = CreateWorld(playerPortfolio: pf);
        var order = new Order(Id: 100, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Sell, Quantity: 5, Price: 100);

        var (next, warn) = _handler.Receive(world, order);

        Assert.Equal(Messages.InsufficientQuantityToSell, warn);
        Assert.Same(world, next);
    }

    [Fact]
    public void Receive_World_Bookは変更されない()
    {
        var pf = new Portfolio(cash: 10000, positions: System.Array.Empty<Position>());
        var world = CreateWorld(playerPortfolio: pf);
        var order = new Order(Id: 100, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 5, Price: 100);

        var (next, _) = _handler.Receive(world, order);

        Assert.Same(world.Book, next.Book);
    }

    // ---------- Settle ----------

    [Fact]
    public void Settle_完全約定_予約が消費されAvailableCashが増える()
    {
        // 買い 10@100 を予約。対側に 10@100 売り resting (computer1)。
        var pf = new Portfolio(cash: 2000, positions: System.Array.Empty<Position>());
        var (reserved, _) = pf.ReserveBuy(instrumentId: 1, quantity: 10, price: 100, fee: 0);
        var restingSell = new Order(Id: 50, TraderId: "computer1", Instrument: TestData.Instrument1, Side: OrderSide.Sell, Quantity: 10, Price: 100);
        var book = new OrderBook().Add(restingSell);
        var world = CreateWorld(playerPortfolio: reserved, book: book, nextOrderId: 100, fee: 0);

        var playerOrder = new Order(Id: 100, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 10, Price: 100);
        var match = new Market().Execute(book, playerOrder, world.Exchange);

        var (next, warn) = _handler.Settle(world, playerOrder, match);

        Assert.Null(warn);
        // reservedCash = 1000 → 0、available = 1000 (差額 0)、保有 +10
        Assert.Equal(1000, next.PlayerPortfolio.Cash);
        Assert.Equal(0, next.PlayerPortfolio.ReservedCash);
        Assert.Equal(10, next.PlayerPortfolio.QuantityOf(1));
    }

    [Fact]
    public void Settle_部分約定_部分だけ予約消費される()
    {
        // 買い 10@100 を予約。対側に 4@100 売り resting だけ。
        var pf = new Portfolio(cash: 2000, positions: System.Array.Empty<Position>());
        var (reserved, _) = pf.ReserveBuy(instrumentId: 1, quantity: 10, price: 100, fee: 0);
        var restingSell = new Order(Id: 50, TraderId: "computer1", Instrument: TestData.Instrument1, Side: OrderSide.Sell, Quantity: 4, Price: 100);
        var book = new OrderBook().Add(restingSell);
        var world = CreateWorld(playerPortfolio: reserved, book: book, nextOrderId: 100, fee: 0);

        var playerOrder = new Order(Id: 100, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 10, Price: 100);
        var match = new Market().Execute(book, playerOrder, world.Exchange);

        var (next, warn) = _handler.Settle(world, playerOrder, match);

        Assert.Null(warn);
        // 4 株分の予約が消費 (400)、残予約 600、available 1000 (差額 0、部分約定なので fee=0)
        Assert.Equal(1000, next.PlayerPortfolio.Cash);
        Assert.Equal(600, next.PlayerPortfolio.ReservedCash);
        Assert.Equal(4, next.PlayerPortfolio.QuantityOf(1));
    }

    [Fact]
    public void Settle_約定ゼロ_World不変でWarningはnull()
    {
        // 買い 10@100 を予約。対側に売り resting なし → fill 0。
        var pf = new Portfolio(cash: 2000, positions: System.Array.Empty<Position>());
        var (reserved, _) = pf.ReserveBuy(instrumentId: 1, quantity: 10, price: 100, fee: 0);
        var book = new OrderBook();
        var world = CreateWorld(playerPortfolio: reserved, book: book, nextOrderId: 100, fee: 0);

        var playerOrder = new Order(Id: 100, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 10, Price: 100);
        var match = new Market().Execute(book, playerOrder, world.Exchange);

        var (next, warn) = _handler.Settle(world, playerOrder, match);

        Assert.Null(warn);
        // 予約状態は維持される (settlement に fills が無い)
        Assert.Equal(1000, next.PlayerPortfolio.Cash);
        Assert.Equal(1000, next.PlayerPortfolio.ReservedCash);
        Assert.Equal(0, next.PlayerPortfolio.QuantityOf(1));
    }
}
