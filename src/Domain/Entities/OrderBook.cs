using System;
using System.Collections.Generic;
using FinLearnApp.Domain.Enums;

namespace FinLearnApp.Domain.Entities;

public sealed class OrderBook
{
    private readonly List<Order> _buyOrders = new();
    private readonly List<Order> _sellOrders = new();

    public IReadOnlyCollection<Order> BuyOrders => _buyOrders.AsReadOnly();
    public IReadOnlyCollection<Order> SellOrders => _sellOrders.AsReadOnly();

    public void Add(Order order)
    {
        if (order is null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        if (order.Side == OrderSide.Buy)
        {
            _buyOrders.Add(order);
            return;
        }

        _sellOrders.Add(order);
    }
}
