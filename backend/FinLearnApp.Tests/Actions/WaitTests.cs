using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FinLearnApp.Api.Data;
using FinLearnApp.Application.Actions;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Actions;

/// <summary>
/// Wait アクションのユニットテスト。
/// 各テストは独自の InMemoryStore を作成してテスト間の状態共有を防ぐ。
/// </summary>
public class WaitTests
{
    // ---- テストデータ用の固定 GUID ----
    private static readonly Guid InvestorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CompanyGuid  = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid TickerGuid   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static readonly InvestorId TestInvestorId = new(InvestorGuid);
    private static readonly TickerId   TestTickerId   = new(TickerGuid);
    private static readonly CompanyId  TestCompanyId  = new(CompanyGuid);

    // ---- ヘルパー: テスト用 InMemoryStore を組み立てる ----

    /// <summary>
    /// 基本的な InMemoryStore を作る。
    /// - 銘柄: AOKI 1,000円
    /// - 投資家: 現金 cashAmount 円
    /// - ターン初期値: initialTurn
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
            random:         new Random(42));   // 乱数固定でテストを安定させる

        return (store, investor, ticker, portfolio);
    }

    private static WaitCommandHandler CreateHandler(InMemoryStore store)
    {
        return new WaitCommandHandler(new WaitStoreAdapter(store));
    }

    // ================================================================
    // 正常系: シナリオ1 — 見送り実行
    // ================================================================

    [Fact]
    public async Task Wait_ValidRequest_ReturnsSuccessTrue()
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Ok, result.Status);
        Assert.True(result.Success);
        Assert.Equal("Wait を実行しました。", result.Message);
    }

    [Fact]
    public async Task Wait_ValidRequest_AdvancesTurnByOne()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 0);
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.CurrentTurn);
    }

    [Fact]
    public async Task Wait_ValidRequest_TurnInStoreIsAdvancedByOne()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 5);
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 5);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: InMemoryStore 内のターンが進んでいる
        Assert.Equal(6, store.GetCurrentTurn(TestInvestorId));
    }

    [Fact]
    public async Task Wait_ValidRequest_DoesNotChangePortfolioCash()
    {
        // Arrange
        var (store, _, _, portfolio) = CreateStore(cashAmount: 500_000m);
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 現金は変化しない
        Assert.Equal(500_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public async Task Wait_ValidRequest_DoesNotChangePortfolioHoldings()
    {
        // Arrange
        var (store, _, _, portfolio) = CreateStore();
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 保有銘柄に変化なし（Wait 前後で空のまま）
        Assert.Empty(portfolio.Holdings);
    }

    [Fact]
    public async Task Wait_ValidRequest_ReturnsPortfolioInResult()
    {
        // Arrange
        var (store, _, _, portfolio) = CreateStore();
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: レスポンスにポートフォリオが含まれる
        Assert.NotNull(result.Portfolio);
        Assert.Equal(portfolio.InvestorId, result.Portfolio!.InvestorId);
    }

    [Fact]
    public async Task Wait_ValidRequestAtHigherTurn_AdvancesToNextTurn()
    {
        // Arrange: ターンが 10 の状態から Wait
        var (store, _, _, _) = CreateStore(initialTurn: 10);
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 10);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(11, result.CurrentTurn);
    }

    // ================================================================
    // 異常系: エラー1 — 投資家が見つからない
    // ================================================================

    [Fact]
    public async Task Wait_InvestorNotFound_ReturnsNotFound()
    {
        // Arrange
        var (store, _, _, _) = CreateStore();
        var handler = CreateHandler(store);
        var unknownInvestorId = Guid.Parse("99999999-0000-0000-0000-000000000000");
        var command = new WaitCommand(unknownInvestorId, expectedTurn: 0);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Wait_InvestorNotFound_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 3);
        var handler = CreateHandler(store);
        var unknownInvestorId = Guid.Parse("99999999-0000-0000-0000-000000000000");
        var command = new WaitCommand(unknownInvestorId, expectedTurn: 3);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: 既存投資家のターンは変わらない
        Assert.Equal(3, store.GetCurrentTurn(TestInvestorId));
    }

    // ================================================================
    // 異常系: エラー2 — ターン番号の不一致
    // ================================================================

    [Fact]
    public async Task Wait_ExpectedTurnMismatch_ReturnsConflict()
    {
        // Arrange: サーバーのターンは 2
        var (store, _, _, _) = CreateStore(initialTurn: 2);
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 99);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ActionExecutionStatus.Conflict, result.Status);
        Assert.Equal("ExpectedTurn mismatch. expected=99, current=2.", result.Message);
    }

    [Fact]
    public async Task Wait_ExpectedTurnMismatch_DoesNotAdvanceTurn()
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: 2);
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: 0);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: ターンは変わっていない
        Assert.Equal(2, store.GetCurrentTurn(TestInvestorId));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 3)]
    [InlineData(10, 0)]
    public async Task Wait_ExpectedTurnMismatch_ReturnsMismatchMessageWithBothTurns(
        int serverTurn, int clientExpectedTurn)
    {
        // Arrange
        var (store, _, _, _) = CreateStore(initialTurn: serverTurn);
        var handler = CreateHandler(store);
        var command = new WaitCommand(InvestorGuid, expectedTurn: clientExpectedTurn);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: メッセージに expected と current の両方が含まれている
        Assert.Equal(ActionExecutionStatus.Conflict, result.Status);
        Assert.Equal(
            $"ExpectedTurn mismatch. expected={clientExpectedTurn}, current={serverTurn}.",
            result.Message);
    }
}

/// <summary>
/// InMemoryStore を IActionExecutionStore として WaitCommandHandler に渡すためのアダプター。
/// </summary>
internal sealed class WaitStoreAdapter : IActionExecutionStore
{
    private readonly InMemoryStore _store;

    public WaitStoreAdapter(InMemoryStore store)
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
