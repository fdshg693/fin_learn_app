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
    /// 有効期限を超過した注文を除去し、除去した注文リストを併せて返す。
    /// 各注文が持つ <see cref="Order.ExpiresAtTurn"/> を基準に判定する
    /// (currentTurn &gt;= ExpiresAtTurn で期限切れ)。
    /// 失効注文の予約解放を呼び出し側で行うため、Expired リストを公開する。
    /// </summary>
    public (OrderBook Updated, IReadOnlyList<Order> Expired) ExpireOrders(int currentTurn)
    {
        var expired = _orders.Where(o => currentTurn >= o.ExpiresAtTurn).ToList();

        if (expired.Count == 0)
            return (this, Array.Empty<Order>());

        var remaining = _orders.Where(o => currentTurn < o.ExpiresAtTurn).ToImmutableList();
        var remainingIds = remaining.Select(o => o.Id).ToImmutableHashSet();
        return (new OrderBook(remaining, remainingIds), expired);
    }

    /// <summary>
    /// 対称的マッチング — 受注注文を板の反対側注文とマッチングする。
    /// 約定価格は常に待機注文（板にいた注文）の価格。
    /// 自己約定（同一TraderId同士の約定）は行わない。
    /// </summary>
    public FillResult Match(Order incoming)
    {
        var eligible = OppositeSideOrders(incoming)
            .Where(resting => resting.TraderId != incoming.TraderId)
            .Where(resting => IsPriceCompatible(incoming, resting))
            .ToList();

        return Fill(incoming, eligible);
    }

    private IReadOnlyList<Order> OppositeSideOrders(Order incoming) =>
        incoming.Side == OrderSide.Buy
            ? SellOrders(incoming.Instrument.Id)
            : BuyOrders(incoming.Instrument.Id);

    /// <summary>
    /// 受注注文の価格条件を待機注文が満たすかを判定する。
    /// 指値注文は Price、成行注文は StopPrice（無ければ無制限）を上限/下限として用いる。
    /// </summary>
    private static bool IsPriceCompatible(Order incoming, Order resting)
    {
        var limit = incoming.Type == OrderType.Limit ? incoming.Price : incoming.StopPrice;
        if (limit is null) return true;

        return incoming.Side == OrderSide.Buy
            ? resting.Price <= limit.Value
            : resting.Price >= limit.Value;
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
