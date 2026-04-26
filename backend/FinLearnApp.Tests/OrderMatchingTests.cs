using System;
using System.Collections.Generic;
using FinLearnApp.Api.Data;
using FinLearnApp.Application.Actions;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests;

/// <summary>
/// オーダーマッチングエンジンのユニットテスト。
/// InMemoryStore の ExecuteBuyNow / ExecuteSellNow / ExecuteBuyLimit / ExecuteSellLimit
/// および OrderBook.ReplaceWithRemaining を直接検証する。
/// テスト間の状態共有を防ぐため、各テストで独立した InMemoryStore を作成する。
/// </summary>
public class OrderMatchingTests
{
    // ---- テストデータ用の固定 GUID ----
    private static readonly Guid InvestorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TickerGuid   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid CompanyGuid  = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static readonly InvestorId TestInvestorId = new(InvestorGuid);
    private static readonly TickerId   TestTickerId   = new(TickerGuid);
    private static readonly CompanyId  TestCompanyId  = new(CompanyGuid);

    // ---- ヘルパー: テスト用 InMemoryStore ----

    private static (InMemoryStore store, Ticker ticker)
        CreateStore(decimal marketPrice = 1_000m, decimal cashAmount = 1_000_000m)
    {
        var company  = new Company(TestCompanyId, "Test Corp");
        var ticker   = new Ticker(TestTickerId, TestCompanyId, "AOKI", 1, Money.Jpy(marketPrice));
        var investor = new Investor(TestInvestorId, "Test Investor");
        var portfolio = new Portfolio(
            new PortfolioId(Guid.NewGuid()),
            TestInvestorId,
            Money.Jpy(cashAmount));

        var turnByInvestor = new Dictionary<InvestorId, int> { [TestInvestorId] = 0 };

        var store = new InMemoryStore(
            companies:      new List<Company>   { company },
            tickers:        new List<Ticker>    { ticker },
            investors:      new List<Investor>  { investor },
            portfolios:     new List<Portfolio> { portfolio },
            turnByInvestor: turnByInvestor,
            random:         new Random(42));

        return (store, ticker);
    }

    private static void AddSellOrder(InMemoryStore store, TickerId tickerId, decimal price, int quantity,
        DateTimeOffset? createdAt = null)
    {
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()),
            tickerId,
            OrderSide.Sell,
            Money.Jpy(price),
            quantity,
            OrderOrigin.System,
            createdAt ?? DateTimeOffset.UtcNow));
    }

    private static void AddBuyOrder(InMemoryStore store, TickerId tickerId, decimal price, int quantity,
        DateTimeOffset? createdAt = null)
    {
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()),
            tickerId,
            OrderSide.Buy,
            Money.Jpy(price),
            quantity,
            OrderOrigin.System,
            createdAt ?? DateTimeOffset.UtcNow));
    }

    // ================================================================
    // ExecuteBuyNow — 正常系
    // ================================================================

    [Fact]
    public void OrderMatching_BuyNow_FullFill_ReturnsCorrectExecutedQuantity()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(5, result.RequestedQuantity);
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(0, result.RemainingQuantity);
    }

    [Fact]
    public void OrderMatching_BuyNow_FullFill_ReturnsTotalAmountAsQuantityTimesPrice()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 10);

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert: 約定価格は相手注文の価格（900円）
        Assert.Equal(900m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_BuyNow_PartialFill_ReturnsCorrectRemainingQuantity()
    {
        // Arrange: 売り注文 3 株しかない
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 10, availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(10, result.RequestedQuantity);
        Assert.Equal(3, result.ExecutedQuantity);
        Assert.Equal(7, result.RemainingQuantity);
    }

    [Fact]
    public void OrderMatching_BuyNow_NoSellOrders_ReturnsZeroExecution()
    {
        // Arrange: オーダーブックは空
        var (store, ticker) = CreateStore(marketPrice: 1_000m);

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
        Assert.Equal(5, result.RemainingQuantity);
        Assert.Equal(0m, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_BuyNow_SellPriceAboveMarketPrice_FiltersOutHighPriceOrders()
    {
        // Arrange: 売り注文の価格が市場価格を超えている → BuyNow のフィルタで除外される
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_001m, quantity: 10);

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_BuyNow_SellPriceEqualToMarketPrice_Matches()
    {
        // Arrange: 売り注文の価格が市場価格と同値 → マッチング対象
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
    }

    // ================================================================
    // ExecuteBuyNow — 価格優先・時間優先マッチング
    // ================================================================

    [Fact]
    public void OrderMatching_BuyNow_MultipleSellOrders_MatchesLowestPriceFirst()
    {
        // Arrange: 安い注文(800円)と高い注文(900円)があるとき、安い方が先に約定する
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var t = DateTimeOffset.UtcNow;
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(900m), 5, OrderOrigin.System, t));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(800m), 5, OrderOrigin.System, t.AddSeconds(1)));

        // Act: 5株だけ買う → 安い 800円注文から消費される
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert: 800円 × 5株
        Assert.Equal(800m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_BuyNow_SamePriceSellOrders_MatchesEarlierCreatedAtFirst()
    {
        // Arrange: 同価格で時刻の異なる2注文 → 早い方が先に消費される
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var t = DateTimeOffset.UtcNow;
        var earlyOrderId = new OrderId(Guid.Parse("dddddddd-0000-0000-0000-000000000001"));
        var lateOrderId  = new OrderId(Guid.Parse("dddddddd-0000-0000-0000-000000000002"));

        store.Exchange.OrderBook.Add(new Order(earlyOrderId, ticker.Id, OrderSide.Sell,
            Money.Jpy(1_000m), 3, OrderOrigin.System, t));
        store.Exchange.OrderBook.Add(new Order(lateOrderId, ticker.Id, OrderSide.Sell,
            Money.Jpy(1_000m), 5, OrderOrigin.System, t.AddSeconds(10)));

        // Act: 4株購入 → 早い注文の 3株 + 遅い注文の 1株
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 4, availableCash: Money.Jpy(1_000_000m));

        // Assert: 早い注文は全部消費 → オーダーブックに残らない
        var remaining = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell);
        Assert.DoesNotContain(remaining, o => o.Id == earlyOrderId);
        // 遅い注文は 4株分消費後 4株残る
        Assert.Contains(remaining, o => o.Id == lateOrderId && o.Quantity == 4);
        Assert.Equal(4, result.ExecutedQuantity);
    }

    // ================================================================
    // ExecuteBuyNow — 現金チェック
    // ================================================================

    [Fact]
    public void OrderMatching_BuyNow_InsufficientCash_SingleLargeOrder_SkipsEntireOrder()
    {
        // Arrange: 現金 2,500円、1注文 5株(5,000円)は超過 → 注文全体スキップ
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(2_500m));

        // Assert: 現金不足で約定なし
        Assert.Equal(0, result.ExecutedQuantity);
        Assert.Equal(0m, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_BuyNow_InsufficientCash_MultipleSmallOrders_StopsAtCashLimit()
    {
        // Arrange: 現金 2,500円、1株ずつ5注文(各1,000円) → 2株で打ち切り
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var t = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            store.Exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
                Money.Jpy(1_000m), 1, OrderOrigin.System, t.AddSeconds(i)));
        }

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(2_500m));

        // Assert: 2株のみ約定（3株目で 3,000円 > 2,500円 となり打ち切り）
        Assert.Equal(2, result.ExecutedQuantity);
        Assert.Equal(2_000m, result.TotalAmount.Amount);
    }

    // ================================================================
    // ExecuteBuyNow — オーダーブック更新
    // ================================================================

    [Fact]
    public void OrderMatching_BuyNow_PartialFill_RemainingQuantityStaysInOrderBook()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var orderId = new OrderId(Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"));
        store.Exchange.OrderBook.Add(new Order(
            orderId, ticker.Id, OrderSide.Sell,
            Money.Jpy(1_000m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        // Act
        store.ExecuteBuyNow(ticker.Id, quantity: 4, availableCash: Money.Jpy(1_000_000m));

        // Assert: 元の ID で残数量 6 株が残っている（ReplaceWithRemaining）
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell);
        var remaining = Assert.Single(sellOrders, o => o.Id == orderId);
        Assert.Equal(6, remaining.Quantity);
    }

    [Fact]
    public void OrderMatching_BuyNow_FullFill_OrderRemovedFromOrderBook()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var orderId = new OrderId(Guid.Parse("eeeeeeee-0000-0000-0000-000000000002"));
        store.Exchange.OrderBook.Add(new Order(
            orderId, ticker.Id, OrderSide.Sell,
            Money.Jpy(1_000m), 5, OrderOrigin.System, DateTimeOffset.UtcNow));

        // Act: ちょうど全数量を購入
        store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert: 注文はオーダーブックから消えている
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell);
        Assert.DoesNotContain(sellOrders, o => o.Id == orderId);
    }

    // ================================================================
    // ExecuteBuyNow — Trade 生成
    // ================================================================

    [Fact]
    public void OrderMatching_BuyNow_FullFill_GeneratesOneTrade()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Single(store.Trades);
    }

    [Fact]
    public void OrderMatching_BuyNow_MultipleOrdersFilledPartially_GeneratesTradePerOrder()
    {
        // Arrange: 2つの注文から3株ずつ購入 → 2回の Trade
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var t = DateTimeOffset.UtcNow;
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 3, createdAt: t);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 3, createdAt: t.AddSeconds(1));

        // Act
        store.ExecuteBuyNow(ticker.Id, quantity: 6, availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(2, store.Trades.Count);
    }

    [Fact]
    public void OrderMatching_BuyNow_Trade_PriceIsSellerOrderPrice()
    {
        // Arrange: 売り注文は 850円
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 850m, quantity: 5);

        // Act
        store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert: Trade の価格は売り注文の 850円
        var trade = Assert.Single(store.Trades);
        Assert.Equal(850m, trade.Price.Amount);
    }

    [Fact]
    public void OrderMatching_BuyNow_Trade_FeeIs500Yen()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert
        var trade = Assert.Single(store.Trades);
        Assert.Equal(500m, trade.Fee.Amount);
    }

    // ================================================================
    // ExecuteSellNow — 正常系
    // ================================================================

    [Fact]
    public void OrderMatching_SellNow_FullFill_ReturnsCorrectExecutedQuantity()
    {
        // Arrange: 買い注文が十分にある
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        // Act
        var result = store.ExecuteSellNow(ticker.Id, quantity: 5);

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(0, result.RemainingQuantity);
    }

    [Fact]
    public void OrderMatching_SellNow_FullFill_ReturnsTotalAmountAsBuyerPrice()
    {
        // Arrange: 買い注文の価格で約定する
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 5);

        // Act
        var result = store.ExecuteSellNow(ticker.Id, quantity: 5);

        // Assert: 約定価格は買い注文の価格（1,100円）
        Assert.Equal(1_100m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_SellNow_PartialFill_ReturnsCorrectRemainingQuantity()
    {
        // Arrange: 買い注文 3 株しかない
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        // Act
        var result = store.ExecuteSellNow(ticker.Id, quantity: 10);

        // Assert
        Assert.Equal(3, result.ExecutedQuantity);
        Assert.Equal(7, result.RemainingQuantity);
    }

    [Fact]
    public void OrderMatching_SellNow_NoBuyOrders_ReturnsZeroExecution()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);

        // Act
        var result = store.ExecuteSellNow(ticker.Id, quantity: 5);

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
        Assert.Equal(0m, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_SellNow_BuyPriceBelowMarketPrice_FiltersOutLowPriceOrders()
    {
        // Arrange: 買い注文の価格が市場価格を下回る → SellNow のフィルタで除外
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddBuyOrder(store, ticker.Id, price: 999m, quantity: 10);

        // Act
        var result = store.ExecuteSellNow(ticker.Id, quantity: 5);

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_SellNow_BuyPriceEqualToMarketPrice_Matches()
    {
        // Arrange: 買い注文価格が市場価格と同値 → マッチング対象
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        var result = store.ExecuteSellNow(ticker.Id, quantity: 5);

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
    }

    // ================================================================
    // ExecuteSellNow — 価格優先・時間優先マッチング
    // ================================================================

    [Fact]
    public void OrderMatching_SellNow_MultipleBuyOrders_MatchesHighestPriceFirst()
    {
        // Arrange: 高い買い注文(1,100円)と安い買い注文(1,000円) → 高い方が先に約定
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var t = DateTimeOffset.UtcNow;
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 5, OrderOrigin.System, t));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
            Money.Jpy(1_100m), 5, OrderOrigin.System, t.AddSeconds(1)));

        // Act: 5株だけ売る → 高い 1,100円注文から消費
        var result = store.ExecuteSellNow(ticker.Id, quantity: 5);

        // Assert
        Assert.Equal(1_100m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_SellNow_SamePriceBuyOrders_MatchesEarlierCreatedAtFirst()
    {
        // Arrange: 同価格で時刻の異なる2注文 → 早い方が先に消費される
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var t = DateTimeOffset.UtcNow;
        var earlyOrderId = new OrderId(Guid.Parse("ffff0000-0000-0000-0000-000000000001"));
        var lateOrderId  = new OrderId(Guid.Parse("ffff0000-0000-0000-0000-000000000002"));

        store.Exchange.OrderBook.Add(new Order(earlyOrderId, ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 3, OrderOrigin.System, t));
        store.Exchange.OrderBook.Add(new Order(lateOrderId, ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 5, OrderOrigin.System, t.AddSeconds(10)));

        // Act: 4株売却 → 早い注文 3株 + 遅い注文 1株
        var result = store.ExecuteSellNow(ticker.Id, quantity: 4);

        // Assert
        var remaining = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy);
        Assert.DoesNotContain(remaining, o => o.Id == earlyOrderId);
        Assert.Contains(remaining, o => o.Id == lateOrderId && o.Quantity == 4);
        Assert.Equal(4, result.ExecutedQuantity);
    }

    // ================================================================
    // ExecuteSellNow — オーダーブック更新
    // ================================================================

    [Fact]
    public void OrderMatching_SellNow_PartialFill_RemainingQuantityStaysInOrderBook()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        var orderId = new OrderId(Guid.Parse("cccc0000-0000-0000-0000-000000000001"));
        store.Exchange.OrderBook.Add(new Order(
            orderId, ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        // Act
        store.ExecuteSellNow(ticker.Id, quantity: 3);

        // Assert
        var buyOrders = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy);
        var remaining = Assert.Single(buyOrders, o => o.Id == orderId);
        Assert.Equal(7, remaining.Quantity);
    }

    [Fact]
    public void OrderMatching_SellNow_Trade_PriceIsBuyerOrderPrice()
    {
        // Arrange: 買い注文は 1,050円
        var (store, ticker) = CreateStore(marketPrice: 1_000m);
        AddBuyOrder(store, ticker.Id, price: 1_050m, quantity: 5);

        // Act
        store.ExecuteSellNow(ticker.Id, quantity: 5);

        // Assert: Trade の価格は買い注文の価格
        var trade = Assert.Single(store.Trades);
        Assert.Equal(1_050m, trade.Price.Amount);
    }

    // ================================================================
    // ExecuteBuyLimit — 正常系
    // ================================================================

    [Fact]
    public void OrderMatching_BuyLimit_SellPriceBelowLimit_Matches()
    {
        // Arrange: 指値 1,000円、売り注文 900円 → 条件を満たす
        var (store, ticker) = CreateStore(marketPrice: 500m); // 市場価格は無関係
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 5);

        // Act
        var result = store.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(900m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_BuyLimit_SellPriceEqualToLimit_Matches()
    {
        // Arrange: 指値と売り注文価格が同値 → 条件を満たす
        var (store, ticker) = CreateStore(marketPrice: 500m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        var result = store.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_BuyLimit_SellPriceAboveLimit_NoMatch()
    {
        // Arrange: 指値 1,000円、売り注文 1,001円 → 条件を満たさない
        var (store, ticker) = CreateStore(marketPrice: 500m);
        AddSellOrder(store, ticker.Id, price: 1_001m, quantity: 5);

        // Act
        var result = store.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_BuyLimit_UsesLimitPriceNotMarketPrice_IgnoresMarketPrice()
    {
        // Arrange: 市場価格 500円だが、指値 1,000円 → 売り注文 800円はマッチ
        var (store, ticker) = CreateStore(marketPrice: 500m);
        AddSellOrder(store, ticker.Id, price: 800m, quantity: 5);

        // Act: 市場価格ベースの BuyNow ならマッチしないが、BuyLimit なら指値で判断
        var result = store.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_BuyLimit_InsufficientCash_StopsAtCashLimit()
    {
        // Arrange: 現金 2,500円、1株ずつ5注文(各1,000円) → 2株で打ち切り
        var (store, ticker) = CreateStore(marketPrice: 500m);
        var t = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            store.Exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
                Money.Jpy(1_000m), 1, OrderOrigin.System, t.AddSeconds(i)));
        }

        // Act
        var result = store.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(2_500m));

        // Assert
        Assert.Equal(2, result.ExecutedQuantity);
        Assert.Equal(2_000m, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_BuyLimit_MultipleSellOrders_MatchesLowestPriceFirst()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 500m);
        var t = DateTimeOffset.UtcNow;
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(900m), 5, OrderOrigin.System, t));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(800m), 5, OrderOrigin.System, t.AddSeconds(1)));

        // Act: 5株、指値 1,000円
        var result = store.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert: 800円の安い注文が優先される
        Assert.Equal(800m * 5, result.TotalAmount.Amount);
    }

    // ================================================================
    // ExecuteSellLimit — 正常系
    // ================================================================

    [Fact]
    public void OrderMatching_SellLimit_BuyPriceAboveLimit_Matches()
    {
        // Arrange: 指値 900円、買い注文 1,000円 → 条件を満たす
        var (store, ticker) = CreateStore(marketPrice: 500m);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        var result = store.ExecuteSellLimit(ticker.Id, quantity: 5, limitPrice: Money.Jpy(900m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(1_000m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_SellLimit_BuyPriceEqualToLimit_Matches()
    {
        // Arrange: 指値と買い注文価格が同値 → 条件を満たす
        var (store, ticker) = CreateStore(marketPrice: 500m);
        AddBuyOrder(store, ticker.Id, price: 900m, quantity: 5);

        // Act
        var result = store.ExecuteSellLimit(ticker.Id, quantity: 5, limitPrice: Money.Jpy(900m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_SellLimit_BuyPriceBelowLimit_NoMatch()
    {
        // Arrange: 指値 900円、買い注文 899円 → 条件を満たさない
        var (store, ticker) = CreateStore(marketPrice: 500m);
        AddBuyOrder(store, ticker.Id, price: 899m, quantity: 5);

        // Act
        var result = store.ExecuteSellLimit(ticker.Id, quantity: 5, limitPrice: Money.Jpy(900m));

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_SellLimit_UsesLimitPriceNotMarketPrice_IgnoresMarketPrice()
    {
        // Arrange: 市場価格 2,000円だが、指値 900円 → 買い注文 1,000円はマッチ
        var (store, ticker) = CreateStore(marketPrice: 2_000m);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        // Act: SellNow(市場価格基準)ならマッチしないが SellLimit(指値基準)はマッチ
        var result = store.ExecuteSellLimit(ticker.Id, quantity: 5, limitPrice: Money.Jpy(900m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_SellLimit_MultipleBuyOrders_MatchesHighestPriceFirst()
    {
        // Arrange: 高い買い注文(1,100円)と低い注文(1,000円) → 高い方が先に約定
        var (store, ticker) = CreateStore(marketPrice: 500m);
        var t = DateTimeOffset.UtcNow;
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 5, OrderOrigin.System, t));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
            Money.Jpy(1_100m), 5, OrderOrigin.System, t.AddSeconds(1)));

        // Act: 5株、指値 900円
        var result = store.ExecuteSellLimit(ticker.Id, quantity: 5, limitPrice: Money.Jpy(900m));

        // Assert: 1,100円の高い注文が優先される
        Assert.Equal(1_100m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void OrderMatching_SellLimit_PartialFill_RemainingQuantityStaysInOrderBook()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 500m);
        var orderId = new OrderId(Guid.Parse("bbbb0000-0000-0000-0000-000000000001"));
        store.Exchange.OrderBook.Add(new Order(
            orderId, ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        // Act
        store.ExecuteSellLimit(ticker.Id, quantity: 4, limitPrice: Money.Jpy(900m));

        // Assert
        var buyOrders = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy);
        var remaining = Assert.Single(buyOrders, o => o.Id == orderId);
        Assert.Equal(6, remaining.Quantity);
    }

    // ================================================================
    // OrderBook — ReplaceWithRemaining の直接テスト
    // ================================================================

    [Fact]
    public void OrderMatching_ReplaceWithRemaining_ZeroRemaining_RemovesOrder()
    {
        // Arrange
        var orderBook = new OrderBook();
        var orderId = new OrderId(Guid.NewGuid());
        var order = new Order(orderId, TestTickerId, OrderSide.Sell,
            Money.Jpy(1_000m), 5, OrderOrigin.System, DateTimeOffset.UtcNow);
        orderBook.Add(order);

        // Act
        orderBook.ReplaceWithRemaining(order, remainingQuantity: 0);

        // Assert
        Assert.Empty(orderBook.FindByTickerAndSide(TestTickerId, OrderSide.Sell));
    }

    [Fact]
    public void OrderMatching_ReplaceWithRemaining_PositiveRemaining_PreservesIdAndCreatedAt()
    {
        // Arrange
        var orderBook = new OrderBook();
        var orderId = new OrderId(Guid.Parse("aaaa0000-0000-0000-0000-000000000001"));
        var createdAt = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var order = new Order(orderId, TestTickerId, OrderSide.Sell,
            Money.Jpy(1_000m), 10, OrderOrigin.System, createdAt);
        orderBook.Add(order);

        // Act
        orderBook.ReplaceWithRemaining(order, remainingQuantity: 7);

        // Assert: ID と CreatedAt が引き継がれる
        var remainingOrders = orderBook.FindByTickerAndSide(TestTickerId, OrderSide.Sell);
        var remaining = Assert.Single(remainingOrders);
        Assert.Equal(orderId, remaining.Id);
        Assert.Equal(createdAt, remaining.CreatedAt);
        Assert.Equal(7, remaining.Quantity);
    }

    [Fact]
    public void OrderMatching_ReplaceWithRemaining_PositiveRemaining_UpdatesQuantityOnly()
    {
        // Arrange
        var orderBook = new OrderBook();
        var orderId = new OrderId(Guid.NewGuid());
        var order = new Order(orderId, TestTickerId, OrderSide.Buy,
            Money.Jpy(1_200m), 8, OrderOrigin.System, DateTimeOffset.UtcNow);
        orderBook.Add(order);

        // Act
        orderBook.ReplaceWithRemaining(order, remainingQuantity: 3);

        // Assert
        var buyOrders = orderBook.FindByTickerAndSide(TestTickerId, OrderSide.Buy);
        var remaining = Assert.Single(buyOrders);
        Assert.Equal(3, remaining.Quantity);
        Assert.Equal(1_200m, remaining.Price.Amount);
    }

    // ================================================================
    // クロス銘柄・異銘柄注文の分離
    // ================================================================

    [Fact]
    public void OrderMatching_BuyNow_OnlyMatchesOrdersOfSameTicker()
    {
        // Arrange: 別銘柄の売り注文はマッチしない
        var (store, ticker) = CreateStore(marketPrice: 1_000m);

        var otherTickerGuid   = Guid.Parse("11111111-0000-0000-0000-000000000099");
        var otherCompanyGuid  = Guid.Parse("22222222-0000-0000-0000-000000000099");
        var otherTickerId     = new TickerId(otherTickerGuid);
        var otherCompanyId    = new CompanyId(otherCompanyGuid);

        // 別銘柄の売り注文を追加
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), otherTickerId, OrderSide.Sell,
            Money.Jpy(1_000m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        // Act
        var result = store.ExecuteBuyNow(ticker.Id, quantity: 5, availableCash: Money.Jpy(1_000_000m));

        // Assert: 別銘柄の注文はマッチしない
        Assert.Equal(0, result.ExecutedQuantity);
    }

    [Fact]
    public void OrderMatching_SellNow_OnlyMatchesOrdersOfSameTicker()
    {
        // Arrange
        var (store, ticker) = CreateStore(marketPrice: 1_000m);

        var otherTickerId = new TickerId(Guid.Parse("11111111-0000-0000-0000-000000000098"));

        // 別銘柄の買い注文を追加
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), otherTickerId, OrderSide.Buy,
            Money.Jpy(1_000m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        // Act
        var result = store.ExecuteSellNow(ticker.Id, quantity: 5);

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }
}
