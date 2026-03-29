using System.Collections.Immutable;

namespace FinLearn.Core;

/// <summary>
/// 注文帳（不変）— 売り注文・買い注文を銘柄別に管理し、対称的な約定を行う
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

    public IReadOnlyList<Order> Orders => _orders;

    public OrderBook Add(Order order)
    {
        if (_ids.Contains(order.Id))
            return this;

        return new OrderBook(_orders.Add(order), _ids.Add(order.Id));
    }

    public IReadOnlyList<Order> SellOrders(int instrumentId) =>
        _orders
            .Where(o => o.Side == OrderSide.Sell && o.Instrument.Id == instrumentId)
            .OrderBy(o => o.Price ?? 0)
            .ToList();

    public IReadOnlyList<Order> BuyOrders(int instrumentId) =>
        _orders
            .Where(o => o.Side == OrderSide.Buy && o.Instrument.Id == instrumentId)
            .OrderByDescending(o => o.Price ?? 0)
            .ToList();

    /// <summary>
    /// 有効期限を超過した注文を除去する。
    /// コンピューター注文とプレイヤー注文で異なるTTLを適用する。
    /// </summary>
    public OrderBook ExpireOrders(int currentTurn, int computerTtl, int playerTtl)
    {
        var remaining = _orders.Where(o =>
        {
            var ttl = o.TraderId == "computer" ? computerTtl : playerTtl;
            return currentTurn - o.CreatedAtTurn < ttl;
        }).ToImmutableList();

        if (remaining.Count == _orders.Count)
            return this;

        var remainingIds = remaining.Select(o => o.Id).ToImmutableHashSet();
        return new OrderBook(remaining, remainingIds);
    }

    /// <summary>
    /// 対称的マッチング — 受注注文を板の反対側注文とマッチングする。
    /// 約定価格は常に待機注文（板にいた注文）の価格。
    /// </summary>
    public FillResult Match(Order incoming)
    {
        var eligible = incoming.Type == OrderType.Market
            ? (incoming.Side == OrderSide.Buy
                ? SellOrders(incoming.Instrument.Id)
                    .TakeWhile(o => incoming.StopPrice is null || o.Price <= incoming.StopPrice.Value).ToList()
                : BuyOrders(incoming.Instrument.Id)
                    .TakeWhile(o => incoming.StopPrice is null || o.Price >= incoming.StopPrice.Value).ToList())
            : (incoming.Side == OrderSide.Buy
                ? SellOrders(incoming.Instrument.Id)
                    .TakeWhile(o => o.Price <= incoming.Price!.Value).ToList()
                : BuyOrders(incoming.Instrument.Id)
                    .TakeWhile(o => o.Price >= incoming.Price!.Value).ToList());

        return Fill(incoming, eligible);
    }

    private FillResult Fill(Order incoming, IReadOnlyList<Order> matchingOrders)
    {
        var remaining = incoming.Quantity;
        var incomingTotalAmount = 0;
        var updatedOrders = _orders;
        var fills = new List<OrderFill>();

        foreach (var order in matchingOrders)
        {
            if (remaining <= 0) break;

            var fill = Math.Min(remaining, order.Quantity);
            var amount = fill * order.Price!.Value;
            incomingTotalAmount += amount;
            remaining -= fill;

            fills.Add(new OrderFill(order.Id, fill, amount));

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

        fills.Add(new OrderFill(incoming.Id, incoming.Quantity - remaining, incomingTotalAmount));

        return new FillResult(fills, new OrderBook(updatedOrders, _ids));
    }
}
