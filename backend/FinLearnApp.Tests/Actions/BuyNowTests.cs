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
/// BuyNow アクションのユニットテスト。
/// 各テストは独自の InMemoryStore を作成してテスト間の状態共有を防ぐ。
/// </summary>
public class BuyNowTests
{
    // ---- テストデータ用の固定 GUID ----
    private static readonly Guid InvestorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TickerGuid   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid CompanyGuid  = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static readonly InvestorId  TestInvestorId  = new(InvestorGuid);
    private static readonly TickerId    TestTickerId    = new(TickerGuid);
    private static readonly CompanyId   TestCompanyId   = new(CompanyGuid);

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

        // InitialAssets = cashAmount で作成されるので Cash = cashAmount が初期値。
        // Withdraw は不要。

        var turnByInvestor = new Dictionary<InvestorId, int> { [TestInvestorId] = initialTurn };

        var store = new InMemoryStore(
            companies:       new List<Company>   { company },
            tickers:         new List<Ticker>    { ticker },
            investors:       new List<Investor>  { investor },
            portfolios:      new List<Portfolio> { portfolio },
            turnByInvestor:  turnByInvestor,
            random:          new Random(42));   // 乱数固定でテストを安定させる

        return (store, investor, ticker, portfolio);
    }

    /// <summary>
    /// 売り注文をオーダーブックに追加するヘルパー。
    /// </summary>
    private static void AddSellOrder(InMemoryStore store, TickerId tickerId, decimal price, int quantity)
    {
        var order = new Order(
            id:        new OrderId(Guid.NewGuid()),
            tickerId:  tickerId,
            side:      OrderSide.Sell,
            price:     Money.Jpy(price),
            quantity:  quantity,
            origin:    OrderOrigin.System,
            createdAt: DateTimeOffset.UtcNow);

        store.Exchange.OrderBook.Add(order);
    }

    private static BuyNowCommandHandler CreateHandler(InMemoryStore store)
    {
        // InMemoryStore は IActionExecutionStore を実装していない（直接 handler には渡せない）。
        // そのため StoreAdapter でラップする。
        return new BuyNowCommandHandler(new InMemoryStoreAdapter(store));
    }

    // ================================================================
    // 正常系: シナリオ1 — 全数量約定
    // ================================================================

    [Fact]
    public async Task BuyNow_SufficientSellOrders_ReturnsSuccessTrueWithFullExecution()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("BuyNow を実行しました。", result.Message);
    }

    [Fact]
    public async Task BuyNow_SufficientSellOrders_DecreasesPortfolioCashByTotalCost()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,000円 × 5株 = 5,000円 減少
        Assert.Equal(1_000_000m - 5_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyNow_SufficientSellOrders_AddsHoldingToPortfolio()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 保有株数が 5 株になっている
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(ticker.Id, holding.TickerId);
        Assert.Equal(5, holding.Quantity);
    }

    [Fact]
    public async Task BuyNow_SufficientSellOrders_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 10);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.CurrentTurn);
    }

    // ================================================================
    // 正常系: シナリオ2 — 一部約定（売り注文数量不足）
    // ================================================================

    [Fact]
    public async Task BuyNow_InsufficientSellOrderQuantity_ReturnsSuccessTrueWithPartialExecution()
    {
        // Arrange: 売り注文 3 株しかない、要求は 10 株
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 10, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("3株を約定しました（未約定 7株）。", result.Message);
    }

    [Fact]
    public async Task BuyNow_InsufficientSellOrderQuantity_DecreasesPortfolioCashByPartialCost()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 10, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,000円 × 3株 = 3,000円 のみ減少
        Assert.Equal(1_000_000m - 3_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyNow_InsufficientSellOrderQuantity_AddsPartialHolding()
    {
        // Arrange
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 3);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 10, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(3, holding.Quantity);
    }

    [Fact]
    public async Task BuyNow_InsufficientCash_SingleLargeOrder_SkipsOrderAndReturnsNoMatch()
    {
        // Arrange: 現金 2,500円、1注文あたり 5株（= 5,000円）は買えない
        // ExecuteBuyNow は注文単位で累積チェックするため、1注文全体が超過すると break する。
        // その結果 executedQuantity = 0 → "約定する売り注文がありませんでした。" が返る。
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 2_500m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_000m, quantity: 5);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 現金が足りない1注文全体をスキップ → 約定なし
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("約定する売り注文がありませんでした。", result.Message);
        Assert.Equal(2_500m, portfolio.Cash.Amount); // 変化なし
    }

    [Fact]
    public async Task BuyNow_InsufficientCash_MultipleSmallOrders_StopsAtCashLimit()
    {
        // Arrange: 現金 2,500円、売り注文は 1株ずつ 5件（各 1,000円）
        // 累積チェックで 3株目（3,000円）が超過するため 2株のみ約定する。
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 2_500m, marketPrice: 1_000m);
        var baseTime = DateTimeOffset.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            store.Exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
                Money.Jpy(1_000m), 1, OrderOrigin.System, baseTime.AddSeconds(i)));
        }

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 2株のみ約定
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("2株を約定しました（未約定 3株）。", result.Message);
        Assert.Equal(2_500m - 2_000m, portfolio.Cash.Amount);
    }

    // ================================================================
    // 正常系: シナリオ3 — 売り注文なし
    // ================================================================

    [Fact]
    public async Task BuyNow_NoSellOrders_ReturnsSuccessFalseWithNoMatchMessage()
    {
        // Arrange: オーダーブックは空
        var (store, _, _, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.False(result.Success);
        Assert.Equal("約定する売り注文がありませんでした。", result.Message);
    }

    [Fact]
    public async Task BuyNow_NoSellOrders_DoesNotChangePortfolioCash()
    {
        // Arrange
        var (store, _, _, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1_000_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyNow_NoSellOrders_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m, initialTurn: 3);

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 3);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 売り注文なしでもターンは進む
        Assert.Equal(4, result.CurrentTurn);
    }

    [Fact]
    public async Task BuyNow_SellOrderPriceAboveMarketPrice_ReturnsNoMatchMessage()
    {
        // Arrange: 売り注文の価格が市場価格より高い → マッチング対象外
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        AddSellOrder(store, ticker.Id, price: 1_500m, quantity: 10);  // 市場価格 1,000円より高い

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("約定する売り注文がありませんでした。", result.Message);
    }

    // ================================================================
    // 異常系: エラー1 — 数量が 0 以下
    // ================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task BuyNow_QuantityZeroOrNegative_ReturnsBadRequest(int quantity)
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: quantity, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.BadRequest, result.Status);
        Assert.Equal("Quantity must be greater than 0.", result.Message);
    }

    [Fact]
    public async Task BuyNow_QuantityZero_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 0);
        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 0, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンが進んでいないことを確認（現在ターンは 0 のまま）
        Assert.Equal(0, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // 異常系: エラー2 — 投資家が見つからない
    // ================================================================

    [Fact]
    public async Task BuyNow_InvestorNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var unknownInvestorId = Guid.Parse("99999999-0000-0000-0000-000000000000");
        var command = new BuyNowCommand(unknownInvestorId, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    // ================================================================
    // 異常系: エラー3 — 銘柄が見つからない
    // ================================================================

    [Fact]
    public async Task BuyNow_TickerNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var unknownTickerId = Guid.Parse("99999999-0000-0000-0000-000000000099");
        var command = new BuyNowCommand(InvestorGuid, unknownTickerId, quantity: 5, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    // ================================================================
    // 異常系: エラー4 — ターン番号の不一致
    // ================================================================

    [Fact]
    public async Task BuyNow_ExpectedTurnMismatch_ReturnsConflict()
    {
        // Arrange: サーバーのターンは 2
        var (store, _, _, _) = CreateStore(initialTurn: 2);
        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 99);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Conflict, result.Status);
        Assert.Equal("ExpectedTurn mismatch. expected=99, current=2.", result.Message);
    }

    [Fact]
    public async Task BuyNow_ExpectedTurnMismatch_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 2);
        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンは変わっていない
        Assert.Equal(2, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // マッチングルール: 価格優先・時間優先
    // ================================================================

    [Fact]
    public async Task BuyNow_MultipleSellOrders_MatchesLowestPriceFirst()
    {
        // Arrange: 低価格の注文が先にマッチするはず
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);

        var createdAt = DateTimeOffset.UtcNow;
        // 高い価格を先に追加
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(900m), 5, OrderOrigin.System, createdAt));
        // 低い価格を後から追加
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(800m), 5, OrderOrigin.System, createdAt.AddSeconds(1)));

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 5, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 800円 × 5株 = 4,000円 減少（安い方が先に約定）
        Assert.Equal(1_000_000m - 4_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task BuyNow_SamePriceSellOrders_MatchesEarlierOrderFirst()
    {
        // Arrange: 同価格なら時刻が早い注文が先に約定する
        var (store, _, ticker, portfolio) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);

        var baseTime = DateTimeOffset.UtcNow;
        // 後から追加した order だが時刻は早い
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(1_000m), 3, OrderOrigin.System, baseTime));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
            Money.Jpy(1_000m), 5, OrderOrigin.System, baseTime.AddSeconds(1)));

        var handler = CreateHandler(store);
        // 4株要求 → 先の 3株 + 後の注文から 1株
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 4, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 1,000円 × 4株 = 4,000円 減少
        Assert.Equal(1_000_000m - 4_000m, portfolio.Cash.Amount);
        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(4, holding.Quantity);
    }

    [Fact]
    public async Task BuyNow_PartialFillConsumesOrder_RemainingQuantityStaysInOrderBook()
    {
        // Arrange: 売り注文 10株、4株だけ購入
        // AdvanceTurn がシステム注文を追加するため、注文ID で元の注文を特定する。
        var (store, _, ticker, _) = CreateStore(cashAmount: 1_000_000m, marketPrice: 1_000m);
        var originalOrderId = new OrderId(Guid.Parse("dddddddd-0000-0000-0000-000000000001"));
        store.Exchange.OrderBook.Add(new Order(
            originalOrderId, ticker.Id, OrderSide.Sell,
            Money.Jpy(1_000m), 10, OrderOrigin.System, DateTimeOffset.UtcNow));

        var handler = CreateHandler(store);
        var command = new BuyNowCommand(InvestorGuid, TickerGuid, quantity: 4, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 元の注文が残数量 6株で残っている（ID は ReplaceWithRemaining で引き継がれる）
        var sellOrders = store.Exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell);
        var remaining = Assert.Single(sellOrders, o => o.Id == originalOrderId);
        Assert.Equal(6, remaining.Quantity);
    }
}

/// <summary>
/// InMemoryStore を IActionExecutionStore として BuyNowCommandHandler に渡すためのアダプター。
/// InMemoryStore は IActionExecutionStore インターフェースを実装していないため、
/// このアダプタークラスを介してブリッジする。
/// </summary>
internal sealed class InMemoryStoreAdapter : IActionExecutionStore
{
    private readonly InMemoryStore _store;

    public InMemoryStoreAdapter(InMemoryStore store)
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
