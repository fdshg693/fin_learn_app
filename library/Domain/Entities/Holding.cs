using System;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Holding
{
    public TickerId TickerId { get; }
    public int Quantity { get; private set; }

    public Holding(TickerId tickerId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than 0.");
        }

        TickerId = tickerId;
        Quantity = quantity;
    }

    public Money MarketValue(Money price)
    {
        return price.Multiply(Quantity);
    }

    public void Increase(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than 0.");
        }

        Quantity += quantity;
    }

    public void Decrease(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than 0.");
        }

        if (quantity > Quantity)
        {
            throw new InvalidOperationException("Insufficient shares.");
        }

        Quantity -= quantity;
    }
}
