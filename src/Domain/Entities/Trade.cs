using System;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Trade
{
    public TradeId Id { get; }
    public TickerId TickerId { get; }
    public OrderId BuyOrderId { get; }
    public OrderId SellOrderId { get; }
    public Money Price { get; }
    public int Quantity { get; }
    public Money Fee { get; }
    public DateTimeOffset ExecutedAt { get; }

    public Trade(
        TradeId id,
        TickerId tickerId,
        OrderId buyOrderId,
        OrderId sellOrderId,
        Money price,
        int quantity,
        Money fee,
        DateTimeOffset executedAt)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than 0.");
        }

        Id = id;
        TickerId = tickerId;
        BuyOrderId = buyOrderId;
        SellOrderId = sellOrderId;
        Price = price;
        Quantity = quantity;
        Fee = fee;
        ExecutedAt = executedAt;
    }
}
