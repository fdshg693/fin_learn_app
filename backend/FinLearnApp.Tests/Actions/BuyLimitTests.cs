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
/// BuyLimit アクションのユニットテスト。
/// 各テストは独自の InMemoryStore を作成してテスト間の状態共有を防ぐ。
/// </summary>
public class BuyLimitTests
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
    /// - 投資家: 現金 100万円
    /// - オーダーブックは空（テストケースで注文を追加する）
    /// </summary>
    private static (InMemoryStore store, Investor investor, Ticker ticker, Portfolio portfolio)
        CreateStore(decimal cashAmount = 1_000_000m, decimal marketPrice = 1_000m, int initialTurn = 0)
    {
        var company  = new Company(TestCompanyId, "Test Corp");
        var ticker   = new Ticker(TestTickerId, TestCompanyId, "AOKI", 1, Money.Jpy(marketPrice));
        var investor = new Investor(TestInvestorId, "Test Investor");

        var portfolio = new Portfolio(
            new PortfolioId(Guid.NewGuid()),
            TestInvestorId,
            Money.Jpy(cashAmount));

        var turnByInvestor = new Dictionary<InvestorId, int> { [TestInvestorId] = initialTurn };

        var store = new InMemoryStore(
            companies:      new List<Company>   { company },
            tickers:        new List<Ticker>    { ticker },
            investors:      new List<Investor>  { investor },
            portfolios:     new List<Portfolio> { portfolio },
            turnByInvestor: turnByInvestor,
            random:         new Random(42));

        return (store, investor, ticker, portfolio);
    }

    /// <summary>
    /// 売り注文をオーダーブックに追加するヘルパー。
    /// </summary>
    private static void AddSellOrder(InMemoryStore store, TickerId tickerId, decimal price, int quantity,
        DateTimeOffset? createdAt = null)
    {
        var order = new Order(
            id:        new OrderId(Guid.NewGuid()),
            tickerId:  tickerId,
            side:      OrderSide.Sell,
            price:     Money.Jpy(price),
            quantity:  quantity,
            origin:    OrderOrigin.System,
            createdAt: createdAt ?? DateTimeOffset.UtcNow);

        store.Exchange.OrderBook.Add(order);
    }

    private static BuyLimitCommandHandler CreateHandler(InMemoryStore store)
        => new BuyLimitCommandHandler(new InMemoryStoreAdapter(store));

    // ================================================================
    // 正常系: シナリオ1 — 全数量約定
    // ================================================================

    [Fact]
    public async Task BuyLimit_SufficientSellOrdersBelowLimitPrice_ReturnsSuccessTrueWithFullExecution()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("BuyLimit を実行しました。", result.Message);
    }

    [Fact]
    public async Task BuyLimit_SufficientSellOrders_DecreasesPortfolioCashByActualTradePrice()
    {
        // Arrange: 指値 1,000円、売り注文は 900円 → 約定価格は 900円（相手の注文価格）
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 約定価格 900円 × 5株 = 4,500円 減少
        Assert.Equal(1_000_000m - 4_500m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyLimit_SufficientSellOrders_AddsHoldingToPortfolio()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 保有株数が 5 株になっている
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(ticker.Id, holding.TickerId);
        Assert.Equal(5, holding.Quantity);
    }

    [Fact]
    public async Task BuyLimit_SufficientSellOrders_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ2 — 一部約定（売り注文数量不足）
    // ================================================================

    [Fact]
    public async Task BuyLimit_InsufficientSellOrderQuantity_ReturnsSuccessTrueWithPartialExecution()
    {
        // Arrange: 指値以下の売り注文が 3 株しかない、要求は 10 株
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 10,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("指値買いで 3株を約定（未約定 7株）。", result.Message);
    }

    [Fact]
    public async Task BuyLimit_InsufficientSellOrderQuantity_DecreasesPortfolioCashByPartialCost()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 10,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 900円 × 3株 = 2,700円 のみ減少
        Assert.Equal(1_000_000m - 2_700m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyLimit_InsufficientSellOrderQuantity_AddsPartialHolding()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 10,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(3, holding.Quantity);
    }

    [Fact]
    public async Task BuyLimit_InsufficientSellOrderQuantity_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 10,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ2（別パターン）— 指値ちょうどの現金で全数量約定
    // ================================================================

    [Fact]
    public async Task BuyLimit_CashExactlyEqualsLimitPriceTimesQuantity_ExecutesSuccessfully()
    {
        // Arrange: 現金 = 指値 × 数量 ちょうど（事前チェックの境界値）
        // 指値 1,000円 × 5株 = 5,000円、現金 5,000円 → 事前チェックOK
        // 売り注文は 900円（指値以下）→ 実際のコストは 4,500円
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 5_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 5);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 全数量約定
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("BuyLimit を実行しました。", result.Message);
        Assert.Equal(5_000m - 4_500m, portfolio.Cash.Amount);
    }

    // ================================================================
    // 正常系: シナリオ3 — 条件に合う売り注文なし
    // ================================================================

    [Fact]
    public async Task BuyLimit_NoSellOrdersBelowLimitPrice_ReturnsSuccessFalseWithNoMatchMessage()
    {
        // Arrange: オーダーブックは空
        var (store, _, _, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("条件に合う売り注文がありませんでした。", result.Message);
    }

    [Fact]
    public async Task BuyLimit_SellOrderPriceAboveLimitPrice_ReturnsNoMatchMessage()
    {
        // Arrange: 売り注文は 1,200円（指値 1,000円より高い）→ マッチング対象外
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_200m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("条件に合う売り注文がありませんでした。", result.Message);
    }

    [Fact]
    public async Task BuyLimit_NoSellOrders_DoesNotChangePortfolioCash()
    {
        // Arrange
        var (store, _, _, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1_000_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyLimit_NoSellOrders_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialTurn: 3);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 3);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 売り注文なしでもターンは進む
        Assert.Equal(4, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ4 — 現金不足（指値 × 数量 > 保有現金）
    // ================================================================

    [Fact]
    public async Task BuyLimit_InsufficientCashForLimitPriceTimesQuantity_ReturnsSuccessFalseWithCashMessage()
    {
        // Arrange: 指値 1,000円 × 5株 = 5,000円、現金 3,000円 → 不足
        var (store, _, ticker, _) = CreateStore(cashAmount: 3_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("指値注文に必要な現金が不足しています。", result.Message);
    }

    [Fact]
    public async Task BuyLimit_InsufficientCash_DoesNotChangePortfolioCash()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 3_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ポートフォリオは変更されない
        Assert.Equal(3_000m, portfolio.Cash.Amount);
        Assert.Empty(portfolio.Holdings);
    }

    [Fact]
    public async Task BuyLimit_InsufficientCash_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 3_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 900m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 現金不足でもターンは進む
        Assert.Equal(1, result.CurrentTurn);
    }

    // ================================================================
    // 異常系: エラー1 — 数量が 0 以下
    // ================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task BuyLimit_QuantityZeroOrNegative_ReturnsBadRequest(int quantity)
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: quantity,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.BadRequest, result.Status);
        Assert.Equal("Quantity must be greater than 0.", result.Message);
    }

    [Fact]
    public async Task BuyLimit_QuantityZero_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 0);
        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 0,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンが進んでいないことを確認（現在ターンは 0 のまま）
        Assert.Equal(0, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // 異常系: エラー2 — 指値が 0 以下
    // ================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    public async Task BuyLimit_LimitPriceZeroOrNegative_ReturnsBadRequest(decimal limitPrice)
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: limitPrice, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.BadRequest, result.Status);
        Assert.Equal("Limit price must be greater than 0.", result.Message);
    }

    [Fact]
    public async Task BuyLimit_LimitPriceZero_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 0);
        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 0m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンが進んでいない
        Assert.Equal(0, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // 異常系: エラー3 — 投資家が見つからない
    // ================================================================

    [Fact]
    public async Task BuyLimit_InvestorNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var unknownInvestorId = Guid.Parse("99999999-0000-0000-0000-000000000000");
        var command = new BuyLimitCommand(unknownInvestorId, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    // ================================================================
    // 異常系: エラー4 — 銘柄が見つからない
    // ================================================================

    [Fact]
    public async Task BuyLimit_TickerNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var unknownTickerId = Guid.Parse("99999999-0000-0000-0000-000000000099");
        var command = new BuyLimitCommand(InvestorGuid, unknownTickerId, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    // ================================================================
    // 異常系: エラー5 — ターン番号の不一致
    // ================================================================

    [Fact]
    public async Task BuyLimit_ExpectedTurnMismatch_ReturnsConflict()
    {
        // Arrange: サーバーのターンは 2
        var (store, _, _, _) = CreateStore(initialTurn: 2);
        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 99);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Conflict, result.Status);
        Assert.Equal("ExpectedTurn mismatch. expected=99, current=2.", result.Message);
    }

    [Fact]
    public async Task BuyLimit_ExpectedTurnMismatch_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 2);
        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンは変わっていない
        Assert.Equal(2, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // マッチングルール: 価格優先・時間優先
    // ================================================================

    [Fact]
    public async Task BuyLimit_MultipleSellOrders_MatchesLowestPriceFirst()
    {
        // Arrange: 指値以下の売り注文が複数ある → 低い価格から約定する
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);

        var createdAt = DateTimeOffset.UtcNow;
        // 高い価格（指値以下）を先に追加
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(950m), 5, OrderOrigin.System, createdAt));
        // 低い価格を後から追加
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(800m), 5, OrderOrigin.System, createdAt.AddSeconds(1)));

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 800円 × 5株 = 4,000円 減少（安い方が先に約定）
        Assert.Equal(1_000_000m - 4_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyLimit_SamePriceSellOrders_MatchesEarlierOrderFirst()
    {
        // Arrange: 同価格なら時刻が早い注文が先に約定する
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);

        var baseTime = DateTimeOffset.UtcNow;
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(900m), 3, OrderOrigin.System, baseTime));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(900m), 5, OrderOrigin.System, baseTime.AddSeconds(1)));

        var handler = CreateHandler(store);
        // 4株要求 → 先の 3株 + 後の注文から 1株
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 4,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 900円 × 4株 = 3,600円 減少
        Assert.Equal(1_000_000m - 3_600m, portfolio.Cash.Amount);
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(4, holding.Quantity);
    }

    [Fact]
    public async Task BuyLimit_SellOrderPriceEqualToLimitPrice_IsMatched()
    {
        // Arrange: 売り注文の価格が指値と同じ → 約定対象（limitPrice 以下）
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 5,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 指値と同価格でも約定する
        Assert.True(result.Success);
        Assert.Equal("BuyLimit を実行しました。", result.Message);
        Assert.Equal(1_000_000m - 5_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyLimit_PartialFillConsumesOrder_RemainingQuantityStaysInOrderBook()
    {
        // Arrange: 売り注文 10株、4株だけ購入
        // marketPrice を 800円にして、ターン後のシステム買い注文（760円）が
        // 元の売り注文（900円）とクロスしないようにする
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 800m);
        var originalOrderId = new OrderId(Guid.Parse("dddddddd-0000-0000-0000-000000000001"));
        store.Exchange.OrderBook.Add(new Order(
            originalOrderId, ticker.Id, OrderSide.Sell,
            Money.Jpy(900m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        var handler = CreateHandler(store);
        var command = new BuyLimitCommand(InvestorGuid, TickerGuid, quantity: 4,
            limitPriceAmount: 1_000m, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 元の注文が残数量 6株で残っている
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell);
        var remaining = Assert.Single(sellOrders, o => o.Id == originalOrderId);
        Assert.Equal(6, remaining.Quantity);
    }
}
