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
/// SellLimit アクションのユニットテスト。
/// 各テストは独自の InMemoryStore を作成してテスト間の状態共有を防ぐ。
/// </summary>
public class SellLimitTests
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
    /// - 銘柄: AOKI 1,000円
    /// - 投資家: 現金 70万円、AOKI 100株保有
    /// - オーダーブックは空（テストケースで注文を追加する）
    /// </summary>
    private static (InMemoryStore store, Investor investor, Ticker ticker, Portfolio portfolio)
        CreateStore(
            decimal cashAmount = 700_000m,
            decimal marketPrice = 1_000m,
            int initialTurn = 0,
            int holdingQuantity = 100)
    {
        var company  = new Company(TestCompanyId, "Test Corp");
        var ticker   = new Ticker(TestTickerId, TestCompanyId, "AOKI", 1, Money.Jpy(marketPrice));
        var investor = new Investor(TestInvestorId, "Test Investor");

        var portfolio = new Portfolio(
            new PortfolioId(Guid.NewGuid()),
            TestInvestorId,
            Money.Jpy(cashAmount));

        if (holdingQuantity > 0)
        {
            portfolio.AddOrUpdateHolding(TestTickerId, holdingQuantity);
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

    private static SellLimitCommandHandler CreateHandler(InMemoryStore store)
        => new SellLimitCommandHandler(new SellLimitStoreAdapter(store));

    // ================================================================
    // 正常系: シナリオ1 — 全数量約定
    // ================================================================

    [Fact]
    public async Task SellLimit_SufficientBuyOrders_ReturnsSuccessTrueWithFullExecution()
    {
        // Arrange: 指値以上の買い注文が十分に存在する
        var (store, _, ticker, portfolio) = CreateStore(holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("SellLimit を実行しました。", result.Message);
    }

    [Fact]
    public async Task SellLimit_SufficientBuyOrders_IncreasesPortfolioCashByTotalProceeds()
    {
        // Arrange: 1,200円の買い注文、10株売却
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 700_000m, holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 1_200m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,200円 × 10株 = 12,000円 増加
        Assert.Equal(700_000m + 12_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task SellLimit_SufficientBuyOrders_DecreasesHoldingQuantity()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 50 - 10 = 40株になる
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(TestTickerId, holding.TickerId);
        Assert.Equal(40, holding.Quantity);
    }

    [Fact]
    public async Task SellLimit_SellAllHolding_RemovesHoldingFromPortfolio()
    {
        // Arrange: 保有数量と売却数量が一致 → 保有銘柄が削除される
        var (store, _, ticker, portfolio) = CreateStore(holdingQuantity: 10);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 20);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 保有がなくなる
        Assert.Empty(portfolio.Holdings);
    }

    [Fact]
    public async Task SellLimit_SufficientBuyOrders_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ2 — 一部約定（買い注文数量不足）
    // ================================================================

    [Fact]
    public async Task SellLimit_InsufficientBuyOrderQuantity_ReturnsSuccessTrueWithPartialExecution()
    {
        // Arrange: 買い注文 3株しかない、要求は 10株
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("指値売りで 3株を約定（未約定 7株）。", result.Message);
    }

    [Fact]
    public async Task SellLimit_InsufficientBuyOrderQuantity_IncreasesPortfolioCashByPartialProceeds()
    {
        // Arrange: 買い注文 3株のみ
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 700_000m, holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,100円 × 3株 = 3,300円 のみ増加
        Assert.Equal(700_000m + 3_300m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task SellLimit_InsufficientBuyOrderQuantity_DecreasesPartialHolding()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 50 - 3 = 47株
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(47, holding.Quantity);
    }

    [Fact]
    public async Task SellLimit_InsufficientBuyOrderQuantity_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 50, initialTurn: 5);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 5);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(6, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ3 — 保有なし
    // ================================================================

    [Fact]
    public async Task SellLimit_NoHolding_ReturnsSuccessFalseWithNoHoldingMessage()
    {
        // Arrange: 保有なしで作成
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 0);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("保有がありません。", result.Message);
    }

    [Fact]
    public async Task SellLimit_NoHolding_DoesNotChangePortfolioCash()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 700_000m, holdingQuantity: 0);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(700_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task SellLimit_NoHolding_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 0, initialTurn: 2);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 2);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 保有なしでもターンは進む
        Assert.Equal(3, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ4 — 保有数量不足
    // ================================================================

    [Fact]
    public async Task SellLimit_InsufficientHoldingQuantity_ReturnsSuccessFalseWithInsufficientMessage()
    {
        // Arrange: 保有 5株、売却要求 10株
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 5);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("保有数量が不足しています。", result.Message);
    }

    [Fact]
    public async Task SellLimit_InsufficientHoldingQuantity_DoesNotChangePortfolio()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 700_000m, holdingQuantity: 5);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 現金変化なし、保有数量変化なし
        Assert.Equal(700_000m, portfolio.Cash.Amount);
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(5, holding.Quantity);
    }

    [Fact]
    public async Task SellLimit_InsufficientHoldingQuantity_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 5, initialTurn: 1);
        AddBuyOrder(store, ticker.Id, price: 1_100m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ5 — 条件に合う買い注文なし（指値未満の注文のみ）
    // ================================================================

    [Fact]
    public async Task SellLimit_NoBuyOrdersMeetingLimitPrice_ReturnsSuccessFalseWithNoMatchMessage()
    {
        // Arrange: 買い注文価格が指値 1,000円 を下回る（800円）
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 800m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("条件に合う買い注文がありませんでした。", result.Message);
    }

    [Fact]
    public async Task SellLimit_NoBuyOrdersMeetingLimitPrice_DoesNotChangePortfolio()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 700_000m, holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 800m, quantity: 50);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(700_000m, portfolio.Cash.Amount);
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(50, holding.Quantity);
    }

    [Fact]
    public async Task SellLimit_NoBuyOrders_AdvancesTurnByOne()
    {
        // Arrange: オーダーブックは空
        var (store, _, _, _) = CreateStore(holdingQuantity: 50, initialTurn: 3);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 3);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 条件なし注文でもターンは進む
        Assert.Equal(4, result.CurrentTurn);
    }

    // ================================================================
    // 異常系: エラー1 — 数量が 0 以下
    // ================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task SellLimit_QuantityZeroOrNegative_ReturnsBadRequest(int quantity)
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: quantity, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.BadRequest, result.Status);
        Assert.Equal("Quantity must be greater than 0.", result.Message);
    }

    [Fact]
    public async Task SellLimit_QuantityZero_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 0);
        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 0, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンが進んでいない
        Assert.Equal(0, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // 異常系: エラー2 — 指値が 0 以下
    // ================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    public async Task SellLimit_LimitPriceZeroOrNegative_ReturnsBadRequest(decimal limitPrice)
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: limitPrice, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.BadRequest, result.Status);
        Assert.Equal("Limit price must be greater than 0.", result.Message);
    }

    [Fact]
    public async Task SellLimit_LimitPriceZero_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 0);
        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 0m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(0, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // 異常系: エラー3 — 投資家が見つからない
    // ================================================================

    [Fact]
    public async Task SellLimit_InvestorNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var unknownInvestorId = Guid.Parse("99999999-0000-0000-0000-000000000000");
        var command = new SellLimitCommand(unknownInvestorId, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    // ================================================================
    // 異常系: エラー4 — 銘柄が見つからない
    // ================================================================

    [Fact]
    public async Task SellLimit_TickerNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var unknownTickerId = Guid.Parse("99999999-0000-0000-0000-000000000099");
        var command = new SellLimitCommand(InvestorGuid, unknownTickerId, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    // ================================================================
    // 異常系: エラー5 — ターン番号の不一致
    // ================================================================

    [Fact]
    public async Task SellLimit_ExpectedTurnMismatch_ReturnsConflict()
    {
        // Arrange: サーバーのターンは 2
        var (store, _, _, _) = CreateStore(initialTurn: 2);
        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 99);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Conflict, result.Status);
        Assert.Equal("ExpectedTurn mismatch. expected=99, current=2.", result.Message);
    }

    [Fact]
    public async Task SellLimit_ExpectedTurnMismatch_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 2);
        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンは変わっていない
        Assert.Equal(2, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // マッチングルール: 価格優先（高い価格から）
    // ================================================================

    [Fact]
    public async Task SellLimit_MultipleBuyOrders_MatchesHighestPriceFirst()
    {
        // Arrange: 高い価格の買い注文から先にマッチするはず
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 700_000m, holdingQuantity: 50);

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
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 5, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,200円 × 5株 = 6,000円 増加（高い方から約定）
        Assert.Equal(700_000m + 6_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task SellLimit_SamePriceBuyOrders_MatchesEarlierOrderFirst()
    {
        // Arrange: 同価格なら時刻が早い注文が先に約定する
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 700_000m, holdingQuantity: 50);

        var baseTime = DateTimeOffset.UtcNow;
        var firstOrderId = new OrderId(Guid.Parse("dddddddd-0000-0000-0000-000000000001"));
        var secondOrderId = new OrderId(Guid.Parse("dddddddd-0000-0000-0000-000000000002"));

        store.Exchange.OrderBook.Add(new Order(
            firstOrderId, ticker.Id, OrderSide.Buy,
            Money.Jpy(1_100m), 3, OrderOrigin.System, baseTime));
        store.Exchange.OrderBook.Add(new Order(
            secondOrderId, ticker.Id, OrderSide.Buy,
            Money.Jpy(1_100m), 5, OrderOrigin.System, baseTime.AddSeconds(1)));

        var handler = CreateHandler(store);
        // 4株要求 → 先の 3株 + 後の注文から 1株
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 4, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,100円 × 4株 = 4,400円 増加
        Assert.Equal(700_000m + 4_400m, portfolio.Cash.Amount);
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(46, holding.Quantity); // 50 - 4
    }

    [Fact]
    public async Task SellLimit_PartialFillConsumesOrder_RemainingQuantityStaysInOrderBook()
    {
        // Arrange: 買い注文 10株、4株だけ売却
        // marketPrice を 1,200円にして、ターン後のシステム売り注文（1,200円）が
        // 元の買い注文（1,100円）とクロスしないようにする
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 50, marketPrice: 1_200m);
        var originalOrderId = new OrderId(Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"));
        store.Exchange.OrderBook.Add(new Order(
            originalOrderId, ticker.Id, OrderSide.Buy,
            Money.Jpy(1_100m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 4, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 元の注文が残数量 6株で残っている
        var buyOrders = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy);
        var remaining = Assert.Single(buyOrders, o => o.Id == originalOrderId);
        Assert.Equal(6, remaining.Quantity);
    }

    // ================================================================
    // マッチングルール: 指値ちょうどの注文はマッチ対象
    // ================================================================

    [Fact]
    public async Task SellLimit_BuyOrderPriceEqualsLimitPrice_MatchesSuccessfully()
    {
        // Arrange: 買い注文価格 = 指値ちょうど
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 700_000m, holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 指値ちょうどの注文は約定する
        Assert.True(result.Success);
        Assert.Equal("SellLimit を実行しました。", result.Message);
        Assert.Equal(700_000m + 10_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task SellLimit_BuyOrderPriceBelowLimitPrice_DoesNotMatch()
    {
        // Arrange: 買い注文価格 < 指値 → マッチング対象外
        var (store, _, ticker, _) = CreateStore(holdingQuantity: 50);
        AddBuyOrder(store, ticker.Id, price: 999m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new SellLimitCommand(InvestorGuid, TickerGuid, quantity: 10, limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("条件に合う買い注文がありませんでした。", result.Message);
    }
}

/// <summary>
/// InMemoryStore を IActionExecutionStore として SellLimitCommandHandler に渡すためのアダプター。
/// </summary>
internal sealed class SellLimitStoreAdapter : IActionExecutionStore
{
    private readonly InMemoryStore _store;

    public SellLimitStoreAdapter(InMemoryStore store)
    {
        _store = store;
    }

    public Portfolio? FindPortfolioByInvestor(InvestorId investorId)
        => _store.FindPortfolioByInvestor(investorId);

    public Ticker? FindTicker(TickerId tickerId)
        => _store.FindTicker(tickerId);

    public int GetCurrentTurn(InvestorId investorId)
        => _store.GetCurrentTurn(investorId);

    public int AdvanceTurn(InvestorId investorId)
        => _store.AdvanceTurn(investorId);

    public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash)
        => _store.ExecuteBuyNow(tickerId, quantity, availableCash);

    public OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity)
        => _store.ExecuteSellNow(tickerId, quantity);

    public OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash)
        => _store.ExecuteBuyLimit(tickerId, quantity, limitPrice, availableCash);

    public OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice)
        => _store.ExecuteSellLimit(tickerId, quantity, limitPrice);
}
