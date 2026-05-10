namespace FinLearn.Tests;

using System.Collections.Generic;
using FinLearn.Core;
using Xunit;

public class WorldTests
{
    [Fact]
    public void FromGame_でPlayerPortfolioとComputerPortfoliosが統合されたPortfoliosが構築される()
    {
        var game = CreateGame();
        var exchange = TestData.CreateExchange((1, 100));

        var world = World.FromGame(game, fee: 10, exchange);

        Assert.Equal(game.ComputerPortfolios.Count + 1, world.Portfolios.Count);
        Assert.True(world.Portfolios.ContainsKey(game.Player.Name));
        foreach (var id in game.ComputerPortfolios.Keys)
        {
            Assert.True(world.Portfolios.ContainsKey(id));
        }
    }

    [Fact]
    public void FromGame_でWorld_PlayerNameはGame_Player_Nameと一致する()
    {
        var game = CreateGame();
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());

        Assert.Equal(game.Player.Name, world.PlayerName);
    }

    [Fact]
    public void FromGame_でWorld_TurnはGame_Turnと一致する()
    {
        var game = CreateGame(turn: 7);
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());

        Assert.Equal(7, world.Turn);
    }

    [Fact]
    public void FromGame_でWorld_NextOrderIdはGame_NextOrderIdと一致する()
    {
        var game = CreateGame(nextOrderId: 42);
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());

        Assert.Equal(42, world.NextOrderId);
    }

    [Fact]
    public void FromGame_でWorld_BookはGame_OrderBookと一致する()
    {
        var game = CreateGame();
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());

        Assert.Same(game.OrderBook, world.Book);
    }

    [Fact]
    public void FromGame_でWorld_PricesはGame_Pricesと一致する()
    {
        var game = CreateGame();
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());

        Assert.Same(game.Prices, world.Prices);
    }

    [Fact]
    public void PlayerPortfolio_でPlayerNameに対応するPortfolioが取得できる()
    {
        var game = CreateGame();
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());

        Assert.Same(game.Player.Portfolio, world.PlayerPortfolio);
    }

    [Fact]
    public void WithPlayerPortfolio_でPlayerだけ更新されComputer分は変わらない()
    {
        var game = CreateGame();
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());
        var newPortfolio = Portfolio.CreateInfinite();

        var updated = world.WithPlayerPortfolio(newPortfolio);

        Assert.Same(newPortfolio, updated.PlayerPortfolio);
        foreach (var (id, pf) in game.ComputerPortfolios)
        {
            Assert.Same(pf, updated.Portfolios[id]);
        }
    }

    [Fact]
    public void WithPlayerPortfolio_は元のWorldを変更しない()
    {
        var game = CreateGame();
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());
        var originalPlayerPortfolio = world.PlayerPortfolio;
        var newPortfolio = Portfolio.CreateInfinite();

        var updated = world.WithPlayerPortfolio(newPortfolio);

        Assert.Same(originalPlayerPortfolio, world.PlayerPortfolio);
        Assert.NotSame(world.Portfolios, updated.Portfolios);
    }

    [Fact]
    public void WithBook_でBookだけ更新される()
    {
        var game = CreateGame();
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());
        var newBook = new OrderBook();

        var updated = world.WithBook(newBook);

        Assert.Same(newBook, updated.Book);
        Assert.Same(world.Portfolios, updated.Portfolios);
        Assert.Equal(world.NextOrderId, updated.NextOrderId);
    }

    [Fact]
    public void WithBook_は元のWorldを変更しない()
    {
        var game = CreateGame();
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());
        var originalBook = world.Book;
        var newBook = new OrderBook();

        var updated = world.WithBook(newBook);

        Assert.Same(originalBook, world.Book);
        Assert.NotSame(world.Book, updated.Book);
    }

    [Fact]
    public void WithNextOrderId_でNextOrderIdだけ更新される()
    {
        var game = CreateGame(nextOrderId: 1);
        var world = World.FromGame(game, fee: 0, TestData.CreateExchange());

        var updated = world.WithNextOrderId(99);

        Assert.Equal(99, updated.NextOrderId);
        Assert.Equal(1, world.NextOrderId);
        Assert.Same(world.Book, updated.Book);
        Assert.Same(world.Portfolios, updated.Portfolios);
    }

    private static Game CreateGame(int turn = 1, int nextOrderId = 1)
    {
        var player = new Player();
        var prices = new Dictionary<int, int> { [1] = 100 };
        var instruments = new[] { TestData.Instrument1 };
        var computers = TestData.CreateInfiniteComputerPortfolios();
        return new Game(player, turn, new OrderBook(), nextOrderId, instruments, prices, computers);
    }
}
