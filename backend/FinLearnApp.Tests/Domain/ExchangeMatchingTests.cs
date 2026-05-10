using System;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

/// <summary>
/// Exchange エンティティのマッチングロジックを直接検証するテスト。
/// InMemoryStore を介さず Exchange を単体でテストする。
/// </summary>
public class ExchangeMatchingTests
{
    private static readonly CompanyId TestCompanyId = new(Guid.Parse("cccccccc-0000-0000-0000-000000000001"));
    private static readonly TickerId  TestTickerId  = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"));

    /// <summary>手数料 500 円の Exchange を生成するヘルパー。</summary>
    private static Exchange CreateExchange() => new(Money.Jpy(500m));

    /// <summary>テスト用 Ticker を生成するヘルパー。</summary>
    private static Ticker CreateTicker(decimal price = 1_000m)
        => new(TestTickerId, TestCompanyId, "AOKI", 1, Money.Jpy(price));

    private static void AddSellOrder(Exchange exchange, TickerId tickerId, decimal price, int quantity)
        => exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), tickerId, OrderSide.Sell,
            Money.Jpy(price), quantity, OrderOrigin.System, DateTimeOffset.UtcNow));

    private static void AddBuyOrder(Exchange exchange, TickerId tickerId, decimal price, int quantity)
        => exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), tickerId, OrderSide.Buy,
            Money.Jpy(price), quantity, OrderOrigin.System, DateTimeOffset.UtcNow));

    // ================================================================
    // ExecuteBuyNow
    // ================================================================

    [Fact]
    public void Exchange_ExecuteBuyNow_MatchingOrder_ReturnsCorrectResult()
    {
        // Arrange: 売り板に 1,000 円 × 10 株がある状態で 5 株成行買い
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddSellOrder(exchange, ticker.Id, price: 1_000m, quantity: 10);

        // Act
        var result = exchange.ExecuteBuyNow(ticker.Id, quantity: 5,
            availableCash: Money.Jpy(1_000_000m), marketPrice: ticker.CurrentPrice);

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(1_000m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteBuyNow_NoMatchingOrder_ReturnsZeroExecution()
    {
        // Arrange: 売り板が空
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);

        // Act
        var result = exchange.ExecuteBuyNow(ticker.Id, quantity: 5,
            availableCash: Money.Jpy(1_000_000m), marketPrice: ticker.CurrentPrice);

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
        Assert.Equal(0m, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteBuyNow_MatchingOrder_RecordsTrade()
    {
        // Arrange
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddSellOrder(exchange, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        exchange.ExecuteBuyNow(ticker.Id, quantity: 5,
            availableCash: Money.Jpy(1_000_000m), marketPrice: ticker.CurrentPrice);

        // Assert: Trades に 1 件記録されている
        Assert.Single(exchange.Trades);
        Assert.Equal(1_000m, exchange.Trades[0].Price.Amount);
        Assert.Equal(500m, exchange.Trades[0].Fee.Amount);
    }

    // ================================================================
    // ExecuteSellNow
    // ================================================================

    [Fact]
    public void Exchange_ExecuteSellNow_MatchingOrder_ReturnsCorrectResult()
    {
        // Arrange: 買い板に 1,000 円 × 10 株がある状態で 5 株成行売り
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddBuyOrder(exchange, ticker.Id, price: 1_000m, quantity: 10);

        // Act
        var result = exchange.ExecuteSellNow(ticker.Id, quantity: 5,
            marketPrice: ticker.CurrentPrice);

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(1_000m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteSellNow_NoMatchingOrder_ReturnsZeroExecution()
    {
        // Arrange: 買い板が空
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);

        // Act
        var result = exchange.ExecuteSellNow(ticker.Id, quantity: 5,
            marketPrice: ticker.CurrentPrice);

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    // ================================================================
    // ExecuteBuyLimit
    // ================================================================

    [Fact]
    public void Exchange_ExecuteBuyLimit_SellPriceBelowLimit_Matches()
    {
        // Arrange: 指値 1,000 円、売り注文 900 円 → マッチする
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 500m);
        AddSellOrder(exchange, ticker.Id, price: 900m, quantity: 5);

        // Act
        var result = exchange.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(900m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteBuyLimit_SellPriceAboveLimit_NoMatch()
    {
        // Arrange: 指値 1,000 円、売り注文 1,001 円 → マッチしない
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 500m);
        AddSellOrder(exchange, ticker.Id, price: 1_001m, quantity: 5);

        // Act
        var result = exchange.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    // ================================================================
    // ExecuteSellLimit
    // ================================================================

    [Fact]
    public void Exchange_ExecuteSellLimit_BuyPriceAboveLimit_Matches()
    {
        // Arrange: 指値 900 円、買い注文 1,000 円 → マッチする
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 500m);
        AddBuyOrder(exchange, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        var result = exchange.ExecuteSellLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(900m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(1_000m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteSellLimit_BuyPriceBelowLimit_NoMatch()
    {
        // Arrange: 指値 900 円、買い注文 899 円 → マッチしない
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 500m);
        AddBuyOrder(exchange, ticker.Id, price: 899m, quantity: 5);

        // Act
        var result = exchange.ExecuteSellLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(900m));

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    // ================================================================
    // MatchCrossedOrders
    // ================================================================

    [Fact]
    public void Exchange_MatchCrossedOrders_CrossedOrders_AreFilledAndRecordedAsTrade()
    {
        // Arrange: 買い 1,000 円・売り 950 円 → クロスしているので自動約定
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddBuyOrder(exchange,  ticker.Id, price: 1_000m, quantity: 5);
        AddSellOrder(exchange, ticker.Id, price: 950m,   quantity: 5);

        // Act
        exchange.MatchCrossedOrders(ticker.Id);

        // Assert: 両注文が消え、Trade が 1 件記録される
        Assert.Empty(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy));
        Assert.Empty(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell));
        Assert.Single(exchange.Trades);
    }

    [Fact]
    public void Exchange_MatchCrossedOrders_NonCrossedOrders_StayInOrderBook()
    {
        // Arrange: 買い 900 円・売り 1,000 円 → クロスしていないので注文は残る
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddBuyOrder(exchange,  ticker.Id, price: 900m,   quantity: 5);
        AddSellOrder(exchange, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        exchange.MatchCrossedOrders(ticker.Id);

        // Assert: 注文はそのまま残る
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy));
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell));
        Assert.Empty(exchange.Trades);
    }
}
