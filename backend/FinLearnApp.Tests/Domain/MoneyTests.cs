using System;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

public class MoneyTests
{
    // ================================================================
    // Add
    // ================================================================

    [Fact]
    public void Money_Add_SameCurrency_ReturnsSumAmount()
    {
        var a = Money.Jpy(1_000m);
        var b = Money.Jpy(500m);

        var result = a.Add(b);

        Assert.Equal(1_500m, result.Amount);
        Assert.Equal(Currency.JPY, result.Currency);
    }


    // ================================================================
    // Subtract
    // ================================================================

    [Fact]
    public void Money_Subtract_SameCurrency_ReturnsDifferenceAmount()
    {
        var a = Money.Jpy(1_000m);
        var b = Money.Jpy(300m);

        var result = a.Subtract(b);

        Assert.Equal(700m, result.Amount);
    }

    [Fact]
    public void Money_Subtract_ResultCanBeNegative()
    {
        var a = Money.Jpy(100m);
        var b = Money.Jpy(300m);

        var result = a.Subtract(b);

        Assert.Equal(-200m, result.Amount);
    }

    // ================================================================
    // Multiply
    // ================================================================

    [Fact]
    public void Money_Multiply_ReturnsScaledAmount()
    {
        var money = Money.Jpy(1_000m);

        var result = money.Multiply(3m);

        Assert.Equal(3_000m, result.Amount);
        Assert.Equal(Currency.JPY, result.Currency);
    }

    [Fact]
    public void Money_Multiply_ByZero_ReturnsZero()
    {
        var money = Money.Jpy(1_000m);

        var result = money.Multiply(0m);

        Assert.Equal(0m, result.Amount);
    }

    [Fact]
    public void Money_Multiply_ByFraction_ReturnsCorrectAmount()
    {
        var money = Money.Jpy(1_000m);

        var result = money.Multiply(0.95m);

        Assert.Equal(950m, result.Amount);
    }

    // ================================================================
    // Jpy factory
    // ================================================================

    [Fact]
    public void Money_Jpy_SetsCurrencyToJpy()
    {
        var money = Money.Jpy(500m);

        Assert.Equal(Currency.JPY, money.Currency);
        Assert.Equal(500m, money.Amount);
    }

    // ================================================================
    // Equality (record struct)
    // ================================================================

    [Fact]
    public void Money_EqualAmountAndCurrency_AreEqual()
    {
        var a = Money.Jpy(1_000m);
        var b = Money.Jpy(1_000m);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Money_DifferentAmount_AreNotEqual()
    {
        var a = Money.Jpy(1_000m);
        var b = Money.Jpy(999m);

        Assert.NotEqual(a, b);
    }
}
