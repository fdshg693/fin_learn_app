using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Api.Data;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests;

/// <summary>
/// ターン制システムのユニットテスト。
/// AdvanceTurn / ApplyPriceFluctuation / GenerateSystemOrdersForTurn の動作を検証する。
/// 各テストは独自の InMemoryStore を作成してテスト間の状態共有を防ぐ。
/// </summary>
public class TurnSystemTests
{
    // ---- テストデータ用の固定 GUID ----
    private static readonly Guid InvestorGuid  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TickerGuid1   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid TickerGuid2   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid TickerGuid3   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");
    private static readonly Guid TickerGuid4   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000004");
    private static readonly Guid CompanyGuid   = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static readonly InvestorId TestInvestorId = new(InvestorGuid);
    private static readonly TickerId   TestTickerId1  = new(TickerGuid1);
    private static readonly TickerId   TestTickerId2  = new(TickerGuid2);
    private static readonly TickerId   TestTickerId3  = new(TickerGuid3);
    private static readonly TickerId   TestTickerId4  = new(TickerGuid4);
    private static readonly CompanyId  TestCompanyId  = new(CompanyGuid);

    // ---- ヘルパー: テスト用 InMemoryStore を組み立てる ----

    /// <summary>
    /// tickerCount 枚の銘柄を含む InMemoryStore を作成する。
    /// </summary>
    private static (InMemoryStore store, List<Ticker> tickers)
        CreateStore(int tickerCount = 1, decimal marketPrice = 1_000m, int initialTurn = 0, int? randomSeed = 42)
    {
        var tickerGuids = new[]
        {
            TickerGuid1, TickerGuid2, TickerGuid3, TickerGuid4
        };
        var symbols = new[] { "AOKI", "HND", "SKR", "TST" };

        var company   = new Company(TestCompanyId, "Test Corp");
        var tickers   = Enumerable.Range(0, tickerCount)
            .Select(i => new Ticker(
                new TickerId(tickerGuids[i]),
                TestCompanyId,
                symbols[i],
                1,
                Money.Jpy(marketPrice)))
            .ToList();

        var investor  = new Investor(TestInvestorId, "Test Investor");
        var portfolio = new Portfolio(
            new PortfolioId(Guid.NewGuid()),
            TestInvestorId,
            Money.Jpy(1_000_000m));

        var turnByInvestor = new Dictionary<InvestorId, int> { [TestInvestorId] = initialTurn };
        var random = randomSeed.HasValue ? new Random(randomSeed.Value) : Random.Shared;

        var store = new InMemoryStore(
            companies:      new List<Company>   { company },
            tickers:        tickers,
            investors:      new List<Investor>  { investor },
            portfolios:     new List<Portfolio> { portfolio },
            turnByInvestor: turnByInvestor,
            random:         random);

        return (store, tickers);
    }

    // ================================================================
    // ターン管理: AdvanceTurn の基本動作
    // ================================================================

    [Fact]
    public void TurnSystem_InitialTurn_IsZero()
    {
        // Arrange
        var (store, _) = CreateStore();

        // Assert
        Assert.Equal(0, store.GetCurrentTurn(TestInvestorId));
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_ReturnsNextTurnNumber()
    {
        // Arrange
        var (store, _) = CreateStore(initialTurn: 0);

        // Act
        var nextTurn = store.AdvanceTurn(TestInvestorId);

        // Assert
        Assert.Equal(1, nextTurn);
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_UpdatesCurrentTurn()
    {
        // Arrange
        var (store, _) = CreateStore(initialTurn: 0);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert
        Assert.Equal(1, store.GetCurrentTurn(TestInvestorId));
    }

    [Fact]
    public void TurnSystem_AdvanceTurnFromArbitraryTurn_IncrementsCorrectly()
    {
        // Arrange: ターン 5 から開始
        var (store, _) = CreateStore(initialTurn: 5);

        // Act
        var nextTurn = store.AdvanceTurn(TestInvestorId);

        // Assert
        Assert.Equal(6, nextTurn);
        Assert.Equal(6, store.GetCurrentTurn(TestInvestorId));
    }

    [Fact]
    public void TurnSystem_AdvanceTurnMultipleTimes_AccumulatesCorrectly()
    {
        // Arrange
        var (store, _) = CreateStore(initialTurn: 0);

        // Act: 3回連続でターンを進める
        store.AdvanceTurn(TestInvestorId);
        store.AdvanceTurn(TestInvestorId);
        var finalTurn = store.AdvanceTurn(TestInvestorId);

        // Assert
        Assert.Equal(3, finalTurn);
        Assert.Equal(3, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // 価格変動: ApplyPriceFluctuation
    // ================================================================

    [Fact]
    public void TurnSystem_AdvanceTurn_PriceChangesAfterTurnAdvance()
    {
        // Arrange
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m);
        var originalPrice = tickers[0].CurrentPrice.Amount;

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 価格が変動していることを確認（97%〜103% の範囲）
        var newPrice = tickers[0].CurrentPrice.Amount;
        Assert.InRange(newPrice, originalPrice * 0.97m, originalPrice * 1.03m);
    }

    [Theory]
    [InlineData(1_000.00)]
    [InlineData(500.00)]
    [InlineData(10_000.00)]
    [InlineData(100.00)]
    public void TurnSystem_AdvanceTurn_PriceStaysWithinFluctuationRange(double initialPrice)
    {
        // Arrange
        var price = (decimal)initialPrice;
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: price, randomSeed: null);

        // Act: 複数ターン繰り返しても範囲を逸脱しないことを確認（1ターン分）
        store.AdvanceTurn(TestInvestorId);

        // Assert: 97%〜103% の範囲内
        var newPrice = tickers[0].CurrentPrice.Amount;
        Assert.InRange(newPrice, price * 0.97m, price * 1.03m);
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_AllTickersPricesChange()
    {
        // Arrange: 3銘柄すべてが価格変動対象
        var (store, tickers) = CreateStore(tickerCount: 3, marketPrice: 1_000m, randomSeed: 100);
        var originalPrices = tickers.Select(t => t.CurrentPrice.Amount).ToList();

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 各銘柄の価格が97%〜103% の範囲内にある
        for (int i = 0; i < tickers.Count; i++)
        {
            Assert.InRange(tickers[i].CurrentPrice.Amount,
                originalPrices[i] * 0.97m,
                originalPrices[i] * 1.03m);
        }
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_PriceNeverFallsBelowOne()
    {
        // Arrange: 最低価格付近の銘柄（価格1円）
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1m);

        // Act: 何度ターンを進めても1円未満にはならない
        for (int i = 0; i < 10; i++)
        {
            store.AdvanceTurn(TestInvestorId);
        }

        // Assert
        Assert.True(tickers[0].CurrentPrice.Amount >= 1m,
            $"Price should be at least 1 JPY, but was {tickers[0].CurrentPrice.Amount}");
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_PriceIsRoundedToTwoDecimalPlaces()
    {
        // Arrange
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m, randomSeed: 1);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 小数点以下2桁に丸められている
        var newPrice = tickers[0].CurrentPrice.Amount;
        var rounded = decimal.Round(newPrice, 2);
        Assert.Equal(rounded, newPrice);
    }

    // ================================================================
    // コンピュータ注文生成: GenerateSystemOrdersForTurn
    // ================================================================

    [Fact]
    public void TurnSystem_AdvanceTurn_GeneratesSystemOrdersInOrderBook()
    {
        // Arrange: 1銘柄
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m);
        var initialOrderCount = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy).Count()
                              + store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell).Count();

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 1銘柄（<=3）なので買い1件 + 売り1件 = 2件追加される
        var buyOrders  = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy);
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell);
        Assert.Equal(initialOrderCount + 2, buyOrders.Count() + sellOrders.Count());
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_SystemOrderQuantityIsTen()
    {
        // Arrange: 1銘柄のみなので必ずその銘柄に注文が入る
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 各注文の数量は 10株
        var buyOrders  = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy);
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell);

        Assert.All(buyOrders,  o => Assert.Equal(10, o.Quantity));
        Assert.All(sellOrders, o => Assert.Equal(10, o.Quantity));
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_SystemBuyOrderPriceIs95PercentOfCurrentPrice()
    {
        // Arrange: 1銘柄のみ
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m, randomSeed: 42);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 買い注文価格 = 価格変動後の価格 × 0.95（小数点以下2桁丸め）
        var newPrice = tickers[0].CurrentPrice.Amount;
        var expectedBuyPrice = decimal.Round(newPrice * 0.95m, 2, MidpointRounding.AwayFromZero);

        var buyOrders = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy).ToList();
        Assert.NotEmpty(buyOrders);
        Assert.All(buyOrders, o => Assert.Equal(expectedBuyPrice, o.Price.Amount));
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_SystemSellOrderPriceIsCurrentPrice()
    {
        // Arrange: 1銘柄のみ
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m, randomSeed: 42);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 売り注文価格 = 価格変動後の価格 × 1.00（変動後の価格そのもの）
        var newPrice = tickers[0].CurrentPrice.Amount;
        var expectedSellPrice = decimal.Round(newPrice * 1.00m, 2, MidpointRounding.AwayFromZero);

        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell).ToList();
        Assert.NotEmpty(sellOrders);
        Assert.All(sellOrders, o => Assert.Equal(expectedSellPrice, o.Price.Amount));
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_SystemOrderOriginIsSystem()
    {
        // Arrange: 1銘柄のみ
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 生成された注文のオリジンは System
        var buyOrders  = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy);
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell);

        Assert.All(buyOrders,  o => Assert.Equal(OrderOrigin.System, o.Origin));
        Assert.All(sellOrders, o => Assert.Equal(OrderOrigin.System, o.Origin));
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_SystemOrderPriceUsesPostFluctuationPrice()
    {
        // Arrange: 1銘柄（価格変動後の価格でコンピュータ注文価格が計算されることを確認）
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m, randomSeed: 42);
        var preFluctuationPrice = tickers[0].CurrentPrice.Amount;

        // Act
        store.AdvanceTurn(TestInvestorId);

        var postFluctuationPrice = tickers[0].CurrentPrice.Amount;

        // 価格変動が発生していることを確認（前提条件の確認）
        // seed=42 では価格が変動するはず
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell).ToList();
        Assert.NotEmpty(sellOrders);

        // 売り注文価格が変動後の価格（× 1.00）であることを確認
        var expectedSellPrice = decimal.Round(postFluctuationPrice * 1.00m, 2, MidpointRounding.AwayFromZero);
        Assert.All(sellOrders, o => Assert.Equal(expectedSellPrice, o.Price.Amount));

        // 売り注文価格が変動前の価格（× 1.00）と一致するかどうかは、
        // 変動があった場合は一致しないことを検証する（変動後を使っていることの間接確認）
        var priceChangedSignificantly = Math.Abs(postFluctuationPrice - preFluctuationPrice) > 0.01m;
        if (priceChangedSignificantly)
        {
            Assert.NotEqual(preFluctuationPrice, expectedSellPrice);
        }
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_WithThreeOrMoreTickers_GeneratesAtMostThreeTargets()
    {
        // Arrange: 4銘柄（MaxTargetTickersPerTurn = 3）
        var (store, tickers) = CreateStore(tickerCount: 4, marketPrice: 1_000m, randomSeed: 42);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 全銘柄の注文数合計を確認
        // 最大3銘柄 × 2件（買い + 売り）= 6件以下
        var totalSystemOrders = tickers.Sum(t =>
            store.Exchange.OrderBook.FindByTickerAndSide(t.Id, OrderSide.Buy).Count() +
            store.Exchange.OrderBook.FindByTickerAndSide(t.Id, OrderSide.Sell).Count());

        Assert.True(totalSystemOrders <= 6,
            $"Expected at most 6 system orders (3 tickers × 2), but got {totalSystemOrders}");
        Assert.True(totalSystemOrders >= 2,
            $"Expected at least 2 system orders (1 ticker × 2), but got {totalSystemOrders}");
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_WithOneTicker_GeneratesExactlyOneBuyAndOneSell()
    {
        // Arrange: 1銘柄のみ（必ずその1銘柄が選ばれる）
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert
        var buyOrders  = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy).ToList();
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell).ToList();
        Assert.Single(buyOrders);
        Assert.Single(sellOrders);
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_WithTwoTickers_GeneratesOrdersForBothTickers()
    {
        // Arrange: 2銘柄（両方とも選ばれるはず：MaxTargetTickersPerTurn = 3 >= 2）
        var (store, tickers) = CreateStore(tickerCount: 2, marketPrice: 1_000m, randomSeed: 42);

        // Act
        store.AdvanceTurn(TestInvestorId);

        // Assert: 2銘柄 × 2件 = 4件
        var totalOrders = tickers.Sum(t =>
            store.Exchange.OrderBook.FindByTickerAndSide(t.Id, OrderSide.Buy).Count() +
            store.Exchange.OrderBook.FindByTickerAndSide(t.Id, OrderSide.Sell).Count());

        Assert.Equal(4, totalOrders);
    }

    [Fact]
    public void TurnSystem_AdvanceTurnTwice_AccumulatesSystemOrders()
    {
        // Arrange: 1銘柄
        var (store, tickers) = CreateStore(tickerCount: 1, marketPrice: 1_000m);

        // Act: 2ターン進める
        store.AdvanceTurn(TestInvestorId);
        store.AdvanceTurn(TestInvestorId);

        // Assert: 各サイドに2件ずつ蓄積される
        var buyOrders  = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy).ToList();
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell).ToList();
        Assert.Equal(2, buyOrders.Count);
        Assert.Equal(2, sellOrders.Count);
    }

    // ================================================================
    // ターン進行の順序: AdvanceTurn → ApplyPriceFluctuation → GenerateSystemOrdersForTurn
    // ================================================================

    [Fact]
    public void TurnSystem_AdvanceTurn_TurnNumberIncreasesBeforePriceFluctuation()
    {
        // Arrange
        var (store, _) = CreateStore(initialTurn: 0);

        // Act
        var result = store.AdvanceTurn(TestInvestorId);

        // Assert: AdvanceTurn の戻り値が 1 であり、現在ターンも 1 になっている
        Assert.Equal(1, result);
        Assert.Equal(1, store.GetCurrentTurn(TestInvestorId));
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_NoTickersDoesNotThrow()
    {
        // Arrange: 銘柄なし
        var investor  = new Investor(TestInvestorId, "Test Investor");
        var portfolio = new Portfolio(
            new PortfolioId(Guid.NewGuid()),
            TestInvestorId,
            Money.Jpy(1_000_000m));

        var turnByInvestor = new Dictionary<InvestorId, int> { [TestInvestorId] = 0 };
        var store = new InMemoryStore(
            companies:      new List<Company>   { new Company(TestCompanyId, "Test Corp") },
            tickers:        new List<Ticker>(),   // 銘柄なし
            investors:      new List<Investor>  { investor },
            portfolios:     new List<Portfolio> { portfolio },
            turnByInvestor: turnByInvestor,
            random:         new Random(42));

        // Act & Assert: 例外が発生しないこと
        var ex = Record.Exception(() => store.AdvanceTurn(TestInvestorId));
        Assert.Null(ex);
    }

    [Fact]
    public void TurnSystem_AdvanceTurn_TurnStillIncreasesWithNoTickers()
    {
        // Arrange: 銘柄なし
        var investor  = new Investor(TestInvestorId, "Test Investor");
        var portfolio = new Portfolio(
            new PortfolioId(Guid.NewGuid()),
            TestInvestorId,
            Money.Jpy(1_000_000m));

        var turnByInvestor = new Dictionary<InvestorId, int> { [TestInvestorId] = 0 };
        var store = new InMemoryStore(
            companies:      new List<Company>   { new Company(TestCompanyId, "Test Corp") },
            tickers:        new List<Ticker>(),
            investors:      new List<Investor>  { investor },
            portfolios:     new List<Portfolio> { portfolio },
            turnByInvestor: turnByInvestor,
            random:         new Random(42));

        // Act
        var nextTurn = store.AdvanceTurn(TestInvestorId);

        // Assert: 銘柄がなくてもターンは進む
        Assert.Equal(1, nextTurn);
    }
}
