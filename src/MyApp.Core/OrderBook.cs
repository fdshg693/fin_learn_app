using System.Collections.Immutable;

namespace MyApp.Core;

/// <summary>
/// 注文帳（不変）— 売り注文・買い注文を銘柄別に管理し、約定を行う
/// </summary>
public sealed class OrderBook
{
    private readonly ImmutableList<Order> _orders;
    private readonly ImmutableHashSet<int> _ids;

    public OrderBook()
        : this(ImmutableList<Order>.Empty, ImmutableHashSet<int>.Empty) { }

    private OrderBook(ImmutableList<Order> orders, ImmutableHashSet<int> ids)
    {
        _orders = orders;
        _ids = ids;
    }

    public OrderBook Add(Order order)
    {
        if (_ids.Contains(order.Id))
            return this;

        return new OrderBook(_orders.Add(order), _ids.Add(order.Id));
    }

    public IReadOnlyList<Order> SellOrders(int instrumentId) =>
        _orders
            .Where(o => o.Side == OrderSide.Sell && o.Instrument.Id == instrumentId)
            .OrderBy(o => o.Price)
            .ToList();

    public IReadOnlyList<Order> BuyOrders(int instrumentId) =>
        _orders
            .Where(o => o.Side == OrderSide.Buy && o.Instrument.Id == instrumentId)
            .OrderByDescending(o => o.Price)
            .ToList();

    public FillResult FillBuy(int instrumentId, int quantity) =>
        Fill(SellOrders(instrumentId), quantity, OrderSide.Sell);

    public FillResult FillSell(int instrumentId, int quantity) =>
        Fill(BuyOrders(instrumentId), quantity, OrderSide.Buy);

    private FillResult Fill(IReadOnlyList<Order> matchingOrders, int quantity, OrderSide side)
    {
        var remaining = quantity;
        var totalAmount = 0;
        var updatedOrders = _orders;

        foreach (var order in matchingOrders)
        {
            if (remaining <= 0) break;

            var fill = Math.Min(remaining, order.Quantity);
            totalAmount += fill * order.Price;
            remaining -= fill;

            if (fill == order.Quantity)
            {
                updatedOrders = updatedOrders.Remove(order);
            }
            else
            {
                var index = updatedOrders.IndexOf(order);
                updatedOrders = updatedOrders.SetItem(index, order.WithQuantity(order.Quantity - fill));
            }
        }

        return new FillResult(
            FilledQuantity: quantity - remaining,
            TotalAmount: totalAmount,
            UpdatedBook: new OrderBook(updatedOrders, _ids));
    }
}
