using System;
using FinLearnApp.Domain.Enums;

namespace FinLearnApp.Domain.ValueObjects;

public readonly record struct Money(decimal Amount, Currency Currency)
{
    public static Money Jpy(decimal amount) => new(amount, Currency.JPY);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal multiplier) => new(Amount * multiplier, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException("Currency mismatch.");
        }
    }
}
