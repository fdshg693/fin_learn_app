using System;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Order
{
    public OrderId Id { get; }
    public TickerId TickerId { get; }
    public OrderSide Side { get; }
    public Money Price { get; }
    public int Quantity { get; }
    public OrderOrigin Origin { get; }
    public DateTimeOffset CreatedAt { get; }

    public Order(
        OrderId id,
        TickerId tickerId,
        OrderSide side,
        Money price,
        int quantity,
        OrderOrigin origin,
        DateTimeOffset createdAt)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than 0.");
        }

        Id = id;
        TickerId = tickerId;
        Side = side;
        Price = price;
        Quantity = quantity;
        Origin = origin;
        CreatedAt = createdAt;
    }
}
