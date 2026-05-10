using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.Services;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

/// <summary>
/// TurnDomainService のユニットテスト。
/// Exchange と Ticker を直接組み立て、ターン処理の各ステップを検証する。
/// </summary>
public class TurnDomainServiceTests
{
    private static readonly CompanyId TestCompanyId = new(Guid.Parse("cccccccc-0000-0000-0000-000000000001"));

    /// <summary>テスト用 Ticker を指定株数生成するヘルパー。</summary>
    private static List<Ticker> CreateTickers(int count, decimal price = 1_000m)
        => Enumerable.Range(0, count)
            .Select(i => new Ticker(
                new TickerId(Guid.NewGuid()),
                TestCompanyId,
                $"T{i}",
                1,
                Money.Jpy(price)))
            .ToList();

    /// <summary>手数料 500 円の Exchange を生成するヘルパー。</summary>
    private static Exchange CreateExchange() => new(Money.Jpy(500m));

    // ================================================================
    // ApplyPriceFluctuation
    // ================================================================

    [Fact]
    public void TurnDomainService_AdvanceTurn_OneTicker_UpdatesPriceHistoryAndGeneratesOrders()
    {
        // Arrange: 1 銘柄なら必ずシステム注文生成対象になる
        var tickers = CreateTickers(1, price: 1_000m);
        var exchange = CreateExchange();
        var initialHistoryCount = tickers[0].PriceHistory.Count;

        // Act
        TurnDomainService.AdvanceTurn(exchange, tickers, new Random(42), turn: 1);

        // Assert: 価格履歴が増え、買い 1 件・売り 1 件が積まれる
        Assert.Equal(initialHistoryCount + 1, tickers[0].PriceHistory.Count);
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy));
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell));
    }

    [Fact]
    public void TurnDomainService_AdvanceTurn_CrossedOrders_AreResolvedByEndOfTurn()
    {
        // Arrange: 既存のクロス注文を入れておく
        var tickers = CreateTickers(1, price: 1_000m);
        var exchange = CreateExchange();
        exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), tickers[0].Id, OrderSide.Buy,
            Money.Jpy(1_000m), 5, OrderOrigin.System, DateTimeOffset.UtcNow));
        exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), tickers[0].Id, OrderSide.Sell,
            Money.Jpy(950m), 5, OrderOrigin.System, DateTimeOffset.UtcNow));

        // Act
        TurnDomainService.AdvanceTurn(exchange, tickers, new Random(42), turn: 1);

        // Assert: ターン終了時点で最良買い < 最良売り になっている
        var bestBuy = exchange.OrderBook
            .FindByTickerAndSide(tickers[0].Id, OrderSide.Buy)
            .OrderByDescending(order => order.Price.Amount)
            .FirstOrDefault();
        var bestSell = exchange.OrderBook
            .FindByTickerAndSide(tickers[0].Id, OrderSide.Sell)
            .OrderBy(order => order.Price.Amount)
            .FirstOrDefault();

        Assert.NotNull(bestBuy);
        Assert.NotNull(bestSell);
        Assert.True(bestBuy!.Price.Amount < bestSell!.Price.Amount);
    }

    [Fact]
    public void TurnDomainService_ApplyPriceFluctuation_ChangesAllTickerPrices()
    {
        // Arrange: 3 銘柄、初期価格 1,000 円
        var tickers = CreateTickers(3, price: 1_000m);
        var originalPrices = tickers.Select(t => t.CurrentPrice.Amount).ToList();

        // Act: seed 固定の Random を使って価格を変動させる（再現性のある結果）
        TurnDomainService.ApplyPriceFluctuation(tickers, new Random(42), turn: 1);

        // Assert: 全銘柄の価格が 97%〜103% の範囲内に変動している
        for (var i = 0; i < tickers.Count; i++)
        {
            Assert.InRange(tickers[i].CurrentPrice.Amount,
                originalPrices[i] * 0.97m,
                originalPrices[i] * 1.03m);
        }
    }

    [Fact]
    public void TurnDomainService_ApplyPriceFluctuation_PriceNeverFallsBelowOne()
    {
        // Arrange: 最低価格 1 円（下限テスト）
        var tickers = CreateTickers(1, price: 1m);

        // Act: 10 回変動させても 1 円未満にならないことを確認
        for (var i = 1; i <= 10; i++)
        {
            TurnDomainService.ApplyPriceFluctuation(tickers, Random.Shared, turn: i);
        }

        // Assert
        Assert.True(tickers[0].CurrentPrice.Amount >= 1m);
    }

    [Fact]
    public void TurnDomainService_ApplyPriceFluctuation_UpdatesPriceHistory()
    {
        // Arrange
        var tickers = CreateTickers(1, price: 1_000m);
        var initialHistoryCount = tickers[0].PriceHistory.Count;

        // Act
        TurnDomainService.ApplyPriceFluctuation(tickers, new Random(42), turn: 1);

        // Assert: 価格履歴に 1 件追加されている
        Assert.Equal(initialHistoryCount + 1, tickers[0].PriceHistory.Count);
    }

    [Fact]
    public void TurnDomainService_ApplyPriceFluctuation_EmptyTickers_DoesNotThrow()
    {
        // Arrange: 銘柄なし
        var tickers = new List<Ticker>();

        // Act & Assert: 例外が発生しない
        var ex = Record.Exception(
            () => TurnDomainService.ApplyPriceFluctuation(tickers, new Random(42), turn: 1));
        Assert.Null(ex);
    }

    // ================================================================
    // GenerateSystemOrders
    // ================================================================

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_OneTicker_GeneratesOneBuyAndOneSell()
    {
        // Arrange: 1 銘柄のみ（必ずその銘柄が選ばれる）
        var tickers  = CreateTickers(1, price: 1_000m);
        var exchange = CreateExchange();

        // Act
        TurnDomainService.GenerateSystemOrders(exchange, tickers, new Random(42));

        // Assert: 買い 1 件・売り 1 件
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy));
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell));
    }

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_FourTickers_GeneratesAtMostSixOrders()
    {
        // Arrange: 4 銘柄（MaxTargetTickersPerTurn = 3 なので最大 3 銘柄 × 2 件 = 6 件）
        var tickers  = CreateTickers(4, price: 1_000m);
        var exchange = CreateExchange();

        // Act
        TurnDomainService.GenerateSystemOrders(exchange, tickers, new Random(42));

        // Assert: 合計注文数が 2〜6 件の範囲内
        var total = tickers.Sum(t =>
            exchange.OrderBook.FindByTickerAndSide(t.Id, OrderSide.Buy).Count() +
            exchange.OrderBook.FindByTickerAndSide(t.Id, OrderSide.Sell).Count());
        Assert.InRange(total, 2, 6);
    }

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_SystemOrderQuantityIsTen()
    {
        // Arrange: 1 銘柄
        var tickers  = CreateTickers(1, price: 1_000m);
        var exchange = CreateExchange();

        // Act
        TurnDomainService.GenerateSystemOrders(exchange, tickers, new Random(42));

        // Assert: 各注文の数量は 10 株
        var buyOrders  = exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy);
        var sellOrders = exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell);
        Assert.All(buyOrders,  o => Assert.Equal(10, o.Quantity));
        Assert.All(sellOrders, o => Assert.Equal(10, o.Quantity));
    }

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_OrderOriginIsSystem()
    {
        // Arrange
        var tickers  = CreateTickers(1, price: 1_000m);
        var exchange = CreateExchange();

        // Act
        TurnDomainService.GenerateSystemOrders(exchange, tickers, new Random(42));

        // Assert: Origin が System
        var buyOrders  = exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy);
        var sellOrders = exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell);
        Assert.All(buyOrders,  o => Assert.Equal(OrderOrigin.System, o.Origin));
        Assert.All(sellOrders, o => Assert.Equal(OrderOrigin.System, o.Origin));
    }

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_EmptyTickers_DoesNotThrow()
    {
        // Arrange
        var exchange = CreateExchange();

        // Act & Assert
        var ex = Record.Exception(
            () => TurnDomainService.GenerateSystemOrders(exchange, new List<Ticker>(), new Random(42)));
        Assert.Null(ex);
    }

    // ================================================================
    // MatchCrossedOrdersForAllTickers
    // ================================================================

    [Fact]
    public void TurnDomainService_MatchCrossedOrdersForAllTickers_CrossedOrders_AreFilledForAllTickers()
    {
        // Arrange: 2 銘柄それぞれにクロス注文を入れる
        var tickers  = CreateTickers(2, price: 1_000m);
        var exchange = CreateExchange();

        foreach (var ticker in tickers)
        {
            exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
                Money.Jpy(1_000m), 5, OrderOrigin.System, DateTimeOffset.UtcNow));
            exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
                Money.Jpy(950m), 5, OrderOrigin.System, DateTimeOffset.UtcNow));
        }

        // Act
        TurnDomainService.MatchCrossedOrdersForAllTickers(exchange, tickers);

        // Assert: 両銘柄の注文板が空になっている
        foreach (var ticker in tickers)
        {
            Assert.Empty(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy));
            Assert.Empty(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell));
        }
    }
}
