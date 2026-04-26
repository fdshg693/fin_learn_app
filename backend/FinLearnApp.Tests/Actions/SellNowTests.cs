using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FinLearnApp.Api.Data;
using FinLearnApp.Application.Actions;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Actions;

/// <summary>
/// SellNow アクションのユニットテスト。
/// 各テストは独自の InMemoryStore を作成してテスト間の状態共有を防ぐ。
/// </summary>
public class SellNowTests
{
    // ---- テストデータ用の固定 GUID ----
    private static readonly Guid InvestorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TickerGuid   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid CompanyGuid  = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static readonly InvestorId TestInvestorId = new(InvestorGuid);
    private static readonly TickerId   TestTickerId   = new(TickerGuid);
    private static readonly CompanyId  TestCompanyId  = new(CompanyGuid);

    // ---- ヘルパー: テスト用 InMemoryStore を組み立てる ----

    /// <summary>
    /// 基本的な InMemoryStore を作る。
    /// - 銘柄: TEST 1,000円
    /// - 投資家: 現金 100万円、指定銘柄の保有数量を初期設定できる
    /// - オーダーブックは空（テストケースで注文を追加する）
    /// </summary>
    private static (InMemoryStore store, Investor investor, Ticker ticker, Portfolio portfolio)
        CreateStore(
            decimal cashAmount = 1_000_000m,
            decimal marketPrice = 1_000m,
            int initialHolding = 0,
            int initialTurn = 0)
    {
        var company  = new Company(TestCompanyId, "Test Corp");
        var ticker   = new Ticker(TestTickerId, TestCompanyId, "TEST", 1, Money.Jpy(marketPrice));
        var investor = new Investor(TestInvestorId, "Test Investor");

        var portfolio = new Portfolio(
            new PortfolioId(Guid.NewGuid()),
            TestInvestorId,
            Money.Jpy(cashAmount));

        if (initialHolding > 0)
        {
            portfolio.AddOrUpdateHolding(TestTickerId, initialHolding);
        }

        var turnByInvestor = new Dictionary<InvestorId, int> { [TestInvestorId] = initialTurn };

        var store = new InMemoryStore(
            companies:      new List<Company>   { company },
            tickers:        new List<Ticker>    { ticker },
            investors:      new List<Investor>  { investor },
            portfolios:     new List<Portfolio> { portfolio },
            turnByInvestor: turnByInvestor,
            random:         new Random(42));   // 乱数固定でテストを安定させる

        return (store, investor, ticker, portfolio);
    }

    /// <summary>
    /// 買い注文をオーダーブックに追加するヘルパー。
    /// </summary>
    private static void AddBuyOrder(InMemoryStore store, TickerId tickerId, decimal price, int quantity,
        DateTimeOffset? createdAt = null)
    {
        var order = new Order(
            id:        new OrderId(Guid.NewGuid()),
            tickerId:  tickerId,
            side:      OrderSide.Buy,
            price:     Money.Jpy(price),
            quantity:  quantity,
            origin:    OrderOrigin.System,
            createdAt: createdAt ?? DateTimeOffset.UtcNow);

        store.Exchange.OrderBook.Add(order);
    }

    private static SellNowCommandHandler CreateHandler(InMemoryStore store)
        => new SellNowCommandHandler(new InMemoryStoreAdapter(store));

    // ================================================================
    // 正常系: シナリオ1 — 全数量約定
    // ================================================================

    [Fact]
    public async Task SellNow_SufficientBuyOrders_ReturnsSuccessTrueWithFullExecution()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("SellNow を実行しました。", result.Message);
    }

    [Fact]
    public async Task SellNow_SufficientBuyOrders_IncreasesPortfolioCashByTotalProceeds()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,000円 × 5株 = 5,000円 増加
        Assert.Equal(1_000_000m + 5_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task SellNow_SufficientBuyOrders_DecreasesHoldingQuantity()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 保有株数が 5 株に減少
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(ticker.Id, holding.TickerId);
        Assert.Equal(5, holding.Quantity);
    }

    [Fact]
    public async Task SellNow_SufficientBuyOrders_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.CurrentTurn);
    }

    [Fact]
    public async Task SellNow_SellAllHoldings_RemovesHoldingFromPortfolio()
    {
        // Arrange: 保有 5株を全て売る
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 5);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 保有が0になったので保有銘柄が削除される
        Assert.Empty(portfolio.Holdings);
    }

    // ================================================================
    // 正常系: シナリオ2 — 一部約定（買い注文数量不足）
    // ================================================================

    [Fact]
    public async Task SellNow_InsufficientBuyOrderQuantity_ReturnsSuccessTrueWithPartialExecution()
    {
        // Arrange: 買い注文 3株しかない、要求は 10株
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 10, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("3株を約定しました（未約定 7株）。", result.Message);
    }

    [Fact]
    public async Task SellNow_InsufficientBuyOrderQuantity_IncreasesPortfolioCashByPartialProceeds()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 10, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,000円 × 3株 = 3,000円 のみ増加
        Assert.Equal(1_000_000m + 3_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task SellNow_InsufficientBuyOrderQuantity_DecreasesHoldingByPartialQuantity()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 10, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 10株 - 3株約定 = 7株残
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(7, holding.Quantity);
    }

    [Fact]
    public async Task SellNow_InsufficientBuyOrderQuantity_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 10, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ3 — 保有なし
    // ================================================================

    [Fact]
    public async Task SellNow_NoHolding_ReturnsSuccessFalseWithNoHoldingMessage()
    {
        // Arrange: 保有なし
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 0);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("保有がありません。", result.Message);
    }

    [Fact]
    public async Task SellNow_NoHolding_DoesNotChangePortfolio()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 0);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 現金も保有も変化なし
        Assert.Equal(1_000_000m, portfolio.Cash.Amount);
        Assert.Empty(portfolio.Holdings);
    }

    [Fact]
    public async Task SellNow_NoHolding_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 0, initialTurn: 3);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 3);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 保有なしでもターンは進む
        Assert.Equal(4, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ4 — 保有数量不足
    // ================================================================

    [Fact]
    public async Task SellNow_InsufficientHolding_ReturnsSuccessFalseWithInsufficientMessage()
    {
        // Arrange: 保有 3株、要求 5株
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 3);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("保有数量が不足しています。", result.Message);
    }

    [Fact]
    public async Task SellNow_InsufficientHolding_DoesNotChangePortfolio()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 3);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 現金も保有も変化なし
        Assert.Equal(1_000_000m, portfolio.Cash.Amount);
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(3, holding.Quantity);
    }

    [Fact]
    public async Task SellNow_InsufficientHolding_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 3);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 保有不足でもターンは進む
        Assert.Equal(1, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ5 — 買い注文なし
    // ================================================================

    [Fact]
    public async Task SellNow_NoBuyOrders_ReturnsSuccessFalseWithNoMatchMessage()
    {
        // Arrange: オーダーブックに買い注文なし
        var (store, _, _, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("約定する買い注文がありませんでした。", result.Message);
    }

    [Fact]
    public async Task SellNow_NoBuyOrders_DoesNotChangePortfolioCashOrHolding()
    {
        // Arrange
        var (store, _, _, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1_000_000m, portfolio.Cash.Amount);
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(10, holding.Quantity);
    }

    [Fact]
    public async Task SellNow_NoBuyOrders_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10, initialTurn: 2);

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 2);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 買い注文なしでもターンは進む
        Assert.Equal(3, result.CurrentTurn);
    }

    [Fact]
    public async Task SellNow_BuyOrderPriceBelowMarketPrice_ReturnsNoMatchMessage()
    {
        // Arrange: 買い注文の価格が市場価格より低い → マッチング対象外
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        AddBuyOrder(store, ticker.Id, price: 500m, quantity: 10);  // 市場価格 1,000円より低い

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("約定する買い注文がありませんでした。", result.Message);
    }

    // ================================================================
    // 異常系: エラー1 — 数量が 0 以下
    // ================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task SellNow_QuantityZeroOrNegative_ReturnsBadRequest(int quantity)
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialHolding: 10);
        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: quantity, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.BadRequest, result.Status);
        Assert.Equal("Quantity must be greater than 0.", result.Message);
    }

    [Fact]
    public async Task SellNow_QuantityZero_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 0, initialHolding: 10);
        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 0, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンが進んでいないことを確認
        Assert.Equal(0, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // 異常系: エラー2 — 投資家が見つからない
    // ================================================================

    [Fact]
    public async Task SellNow_InvestorNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialHolding: 10);
        var handler = CreateHandler(store);
        var unknownInvestorId = Guid.Parse("99999999-0000-0000-0000-000000000000");
        var command = new SellNowCommand(unknownInvestorId, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    // ================================================================
    // 異常系: エラー3 — 銘柄が見つからない
    // ================================================================

    [Fact]
    public async Task SellNow_TickerNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialHolding: 10);
        var handler = CreateHandler(store);
        var unknownTickerId = Guid.Parse("99999999-0000-0000-0000-000000000099");
        var command = new SellNowCommand(InvestorGuid, unknownTickerId, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    // ================================================================
    // 異常系: エラー4 — ターン番号の不一致
    // ================================================================

    [Fact]
    public async Task SellNow_ExpectedTurnMismatch_ReturnsConflict()
    {
        // Arrange: サーバーのターンは 2
        var (store, _, _, _) = CreateStore(initialTurn: 2, initialHolding: 10);
        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 99);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Conflict, result.Status);
        Assert.Equal("ExpectedTurn mismatch. expected=99, current=2.", result.Message);
    }

    [Fact]
    public async Task SellNow_ExpectedTurnMismatch_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 2, initialHolding: 10);
        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンは変わっていない
        Assert.Equal(2, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // マッチングルール: 価格優先・時間優先
    // ================================================================

    [Fact]
    public async Task SellNow_MultipleBuyOrders_MatchesHighestPriceFirst()
    {
        // Arrange: 価格の高い注文が先にマッチするはず
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);

        var createdAt = DateTimeOffset.UtcNow;
        // 低い価格を先に追加
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 5, OrderOrigin.System, createdAt));
        // 高い価格を後から追加
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
            Money.Jpy(1_200m), 5, OrderOrigin.System, createdAt.AddSeconds(1)));

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,200円 × 5株 = 6,000円 増加（高い方が先に約定）
        Assert.Equal(1_000_000m + 6_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task SellNow_SamePriceBuyOrders_MatchesEarlierOrderFirst()
    {
        // Arrange: 同価格なら時刻が早い注文が先に約定する
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);

        var baseTime = DateTimeOffset.UtcNow;
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 3, OrderOrigin.System, baseTime));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 5, OrderOrigin.System, baseTime.AddSeconds(1)));

        var handler = CreateHandler(store);
        // 4株要求 → 先の 3株 + 後の注文から 1株
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 4, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,000円 × 4株 = 4,000円 増加
        Assert.Equal(1_000_000m + 4_000m, portfolio.Cash.Amount);
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(6, holding.Quantity); // 10株 - 4株 = 6株残
    }

    [Fact]
    public async Task SellNow_PartialFillConsumesOrder_RemainingQuantityStaysInOrderBook()
    {
        // Arrange: 買い注文 10株、4株だけ売却
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialHolding: 10);
        var originalOrderId = new OrderId(Guid.Parse("dddddddd-0000-0000-0000-000000000001"));
        store.Exchange.OrderBook.Add(new Order(
            originalOrderId, ticker.Id, OrderSide.Buy,
            Money.Jpy(1_000m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        var handler = CreateHandler(store);
        var command = new SellNowCommand(InvestorGuid, TickerGuid, quantity: 4, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 元の注文が残数量 6株で残っている
        var buyOrders = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy);
        var remaining = Assert.Single(buyOrders, o => o.Id == originalOrderId);
        Assert.Equal(6, remaining.Quantity);
    }
}
