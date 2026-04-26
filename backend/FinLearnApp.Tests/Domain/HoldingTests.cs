using System;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

public class HoldingTests
{
    private static readonly TickerId TestTickerId = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"));

    // ================================================================
    // Constructor
    // ================================================================

    [Fact]
    public void Holding_Constructor_SetsQuantity()
    {
        var holding = new Holding(TestTickerId, 10);

        Assert.Equal(10, holding.Quantity);
        Assert.Equal(TestTickerId, holding.TickerId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Holding_Constructor_ZeroOrNegativeQuantity_ThrowsArgumentOutOfRangeException(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Holding(TestTickerId, quantity));
    }

    // ================================================================
    // Increase
    // ================================================================

    [Fact]
    public void Holding_Increase_AddsToQuantity()
    {
        var holding = new Holding(TestTickerId, 5);

        holding.Increase(3);

        Assert.Equal(8, holding.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Holding_Increase_ZeroOrNegative_ThrowsArgumentOutOfRangeException(int quantity)
    {
        var holding = new Holding(TestTickerId, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => holding.Increase(quantity));
    }

    // ================================================================
    // Decrease
    // ================================================================

    [Fact]
    public void Holding_Decrease_SubtractsFromQuantity()
    {
        var holding = new Holding(TestTickerId, 10);

        holding.Decrease(4);

        Assert.Equal(6, holding.Quantity);
    }

    [Fact]
    public void Holding_Decrease_ToExactZero_SetsQuantityToZero()
    {
        var holding = new Holding(TestTickerId, 5);

        holding.Decrease(5);

        Assert.Equal(0, holding.Quantity);
    }

    [Fact]
    public void Holding_Decrease_ExceedsQuantity_ThrowsInvalidOperationException()
    {
        var holding = new Holding(TestTickerId, 3);

        Assert.Throws<InvalidOperationException>(() => holding.Decrease(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Holding_Decrease_ZeroOrNegative_ThrowsArgumentOutOfRangeException(int quantity)
    {
        var holding = new Holding(TestTickerId, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => holding.Decrease(quantity));
    }

    // ================================================================
    // MarketValue
    // ================================================================

    [Fact]
    public void Holding_MarketValue_ReturnsQuantityTimesPrice()
    {
        var holding = new Holding(TestTickerId, 10);

        var value = holding.MarketValue(Money.Jpy(1_500m));

        Assert.Equal(15_000m, value.Amount);
    }

    [Fact]
    public void Holding_MarketValue_QuantityOne_ReturnsPriceItself()
    {
        var holding = new Holding(TestTickerId, 1);

        var value = holding.MarketValue(Money.Jpy(2_000m));

        Assert.Equal(2_000m, value.Amount);
    }
}
