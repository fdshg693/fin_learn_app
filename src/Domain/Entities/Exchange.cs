using System;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Exchange
{
    public Money Fee { get; }
    public OrderBook OrderBook { get; }

    public Exchange(Money fee)
    {
        Fee = fee;
        OrderBook = new OrderBook();
    }
}
