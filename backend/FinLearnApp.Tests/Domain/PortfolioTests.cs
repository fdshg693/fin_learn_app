using System;
using System.Collections.Generic;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

public class PortfolioTests
{
    private static readonly InvestorId TestInvestorId = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
    private static readonly TickerId   TestTickerId   = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));
    private static readonly TickerId   OtherTickerId  = new(Guid.Parse("cccccccc-0000-0000-0000-000000000003"));

    private static Portfolio CreatePortfolio(decimal initialCash = 1_000_000m)
        => new(new PortfolioId(Guid.NewGuid()), TestInvestorId, Money.Jpy(initialCash));

    // ================================================================
    // 初期状態
    // ================================================================

    [Fact]
    public void Portfolio_InitialCash_EqualsInitialAssets()
    {
        var portfolio = CreatePortfolio(500_000m);

        Assert.Equal(500_000m, portfolio.Cash.Amount);
        Assert.Equal(500_000m, portfolio.InitialAssets.Amount);
    }

    [Fact]
    public void Portfolio_InitialHoldings_IsEmpty()
    {
        var portfolio = CreatePortfolio();

        Assert.Empty(portfolio.Holdings);
    }

    // ================================================================
    // Deposit / Withdraw
    // ================================================================

    [Fact]
    public void Portfolio_Deposit_IncreasesCash()
    {
        var portfolio = CreatePortfolio(1_000_000m);

        portfolio.Deposit(Money.Jpy(200_000m));

        Assert.Equal(1_200_000m, portfolio.Cash.Amount);
    }

    [Fact]
    public void Portfolio_Withdraw_DecreasesCash()
    {
        var portfolio = CreatePortfolio(1_000_000m);

        portfolio.Withdraw(Money.Jpy(300_000m));

        Assert.Equal(700_000m, portfolio.Cash.Amount);
    }

    // ================================================================
    // AddOrUpdateHolding
    // ================================================================

    [Fact]
    public void Portfolio_AddOrUpdateHolding_NewTicker_AddsHolding()
    {
        var portfolio = CreatePortfolio();

        portfolio.AddOrUpdateHolding(TestTickerId, 10);

        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(TestTickerId, holding.TickerId);
        Assert.Equal(10, holding.Quantity);
    }

    [Fact]
    public void Portfolio_AddOrUpdateHolding_ExistingTicker_IncreasesQuantity()
    {
        var portfolio = CreatePortfolio();
        portfolio.AddOrUpdateHolding(TestTickerId, 5);

        portfolio.AddOrUpdateHolding(TestTickerId, 3);

        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(8, holding.Quantity);
    }

    [Fact]
    public void Portfolio_AddOrUpdateHolding_ZeroQuantity_ThrowsArgumentOutOfRangeException()
    {
        var portfolio = CreatePortfolio();

        Assert.Throws<ArgumentOutOfRangeException>(() => portfolio.AddOrUpdateHolding(TestTickerId, 0));
    }

    [Fact]
    public void Portfolio_AddOrUpdateHolding_NegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        var portfolio = CreatePortfolio();

        Assert.Throws<ArgumentOutOfRangeException>(() => portfolio.AddOrUpdateHolding(TestTickerId, -1));
    }

    // ================================================================
    // ReduceHolding
    // ================================================================

    [Fact]
    public void Portfolio_ReduceHolding_DecreasesQuantity()
    {
        var portfolio = CreatePortfolio();
        portfolio.AddOrUpdateHolding(TestTickerId, 10);

        portfolio.ReduceHolding(TestTickerId, 4);

        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(6, holding.Quantity);
    }

    [Fact]
    public void Portfolio_ReduceHolding_ToZero_RemovesHolding()
    {
        var portfolio = CreatePortfolio();
        portfolio.AddOrUpdateHolding(TestTickerId, 5);

        portfolio.ReduceHolding(TestTickerId, 5);

        Assert.Empty(portfolio.Holdings);
    }

    [Fact]
    public void Portfolio_ReduceHolding_HoldingNotFound_ThrowsInvalidOperationException()
    {
        var portfolio = CreatePortfolio();

        Assert.Throws<InvalidOperationException>(() => portfolio.ReduceHolding(TestTickerId, 1));
    }

    [Fact]
    public void Portfolio_ReduceHolding_ExceedsQuantity_ThrowsInvalidOperationException()
    {
        var portfolio = CreatePortfolio();
        portfolio.AddOrUpdateHolding(TestTickerId, 3);

        Assert.Throws<InvalidOperationException>(() => portfolio.ReduceHolding(TestTickerId, 10));
    }

    // ================================================================
    // CalculateValuation
    // ================================================================

    [Fact]
    public void Portfolio_CalculateValuation_NoHoldings_ReturnsCashOnly()
    {
        var portfolio = CreatePortfolio(1_000_000m);
        var prices = new Dictionary<TickerId, Money>();

        var valuation = portfolio.CalculateValuation(prices);

        Assert.Equal(1_000_000m, valuation.Amount);
    }

    [Fact]
    public void Portfolio_CalculateValuation_WithHoldings_ReturnsCashPlusHoldingsValue()
    {
        var portfolio = CreatePortfolio(500_000m);
        portfolio.AddOrUpdateHolding(TestTickerId, 10);
        var prices = new Dictionary<TickerId, Money>
        {
            [TestTickerId] = Money.Jpy(1_000m)
        };

        var valuation = portfolio.CalculateValuation(prices);

        Assert.Equal(510_000m, valuation.Amount);
    }

    [Fact]
    public void Portfolio_CalculateValuation_MultipleHoldings_SumsAllValues()
    {
        var portfolio = CreatePortfolio(100_000m);
        portfolio.AddOrUpdateHolding(TestTickerId, 5);
        portfolio.AddOrUpdateHolding(OtherTickerId, 3);
        var prices = new Dictionary<TickerId, Money>
        {
            [TestTickerId]  = Money.Jpy(2_000m),
            [OtherTickerId] = Money.Jpy(1_000m),
        };

        var valuation = portfolio.CalculateValuation(prices);

        // 100,000 + (5 * 2,000) + (3 * 1,000) = 113,000
        Assert.Equal(113_000m, valuation.Amount);
    }

    // ================================================================
    // CalculateProfitLoss
    // ================================================================

    [Fact]
    public void Portfolio_CalculateProfitLoss_NoChange_ReturnsZero()
    {
        var portfolio = CreatePortfolio(1_000_000m);
        var prices = new Dictionary<TickerId, Money>();

        var pl = portfolio.CalculateProfitLoss(prices);

        Assert.Equal(0m, pl.Amount);
    }

    [Fact]
    public void Portfolio_CalculateProfitLoss_Profit_ReturnsPositiveAmount()
    {
        var portfolio = CreatePortfolio(1_000_000m);
        portfolio.AddOrUpdateHolding(TestTickerId, 10);
        portfolio.Withdraw(Money.Jpy(10_000m));
        var prices = new Dictionary<TickerId, Money>
        {
            [TestTickerId] = Money.Jpy(2_000m) // 購入1,000円 → 現在2,000円
        };

        var pl = portfolio.CalculateProfitLoss(prices);

        // 保有: 10株 * 2,000 = 20,000、現金: 990,000、合計: 1,010,000
        // P/L = 1,010,000 - 1,000,000 = 10,000
        Assert.Equal(10_000m, pl.Amount);
    }

    [Fact]
    public void Portfolio_CalculateProfitLoss_Loss_ReturnsNegativeAmount()
    {
        var portfolio = CreatePortfolio(1_000_000m);
        portfolio.AddOrUpdateHolding(TestTickerId, 10);
        portfolio.Withdraw(Money.Jpy(10_000m));
        var prices = new Dictionary<TickerId, Money>
        {
            [TestTickerId] = Money.Jpy(500m) // 購入1,000円 → 現在500円
        };

        var pl = portfolio.CalculateProfitLoss(prices);

        // 保有: 10株 * 500 = 5,000、現金: 990,000、合計: 995,000
        // P/L = 995,000 - 1,000,000 = -5,000
        Assert.Equal(-5_000m, pl.Amount);
    }
}
