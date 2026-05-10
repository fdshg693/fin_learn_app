using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

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

    public IReadOnlyList<Order> FindByTickerAndSide(TickerId tickerId, OrderSide side)
    {
        return side == OrderSide.Buy
            ? _buyOrders.Where(order => order.TickerId == tickerId).ToList()
            : _sellOrders.Where(order => order.TickerId == tickerId).ToList();
    }

    public void Remove(Order order)
    {
        if (order.Side == OrderSide.Buy)
        {
            _buyOrders.Remove(order);
            return;
        }

        _sellOrders.Remove(order);
    }

    public void ReplaceWithRemaining(Order original, int remainingQuantity)
    {
        Remove(original);

        if (remainingQuantity <= 0)
        {
            return;
        }

        Add(new Order(
            original.Id,
            original.TickerId,
            original.Side,
            original.Price,
            remainingQuantity,
            original.Origin,
            original.CreatedAt));
    }
}
