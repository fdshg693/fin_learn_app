namespace FinLearn.Core;

internal sealed class LimitOrderHandler : IPlayerOrderHandler
{
    public (World World, string? Warning) Receive(World world, Order order)
    {
        var price = order.Price!.Value; // 限値前提
        var pf = world.PlayerPortfolio;
        var (reserved, warn) = order.Side == OrderSide.Buy
            ? pf.ReserveBuy(order.Instrument.Id, order.Quantity, price, world.Fee)
            : pf.ReserveSell(order.Instrument.Id, order.Quantity);

        if (warn is not null)
            return (world, warn);

        return (world.WithPlayerPortfolio(reserved), null);
    }

    public (World World, string? Warning) Settle(World world, Order order, MatchResult match)
    {
        // 限値は noMatch でも板に残るので fill=0 でも warning は返さない。
        var ordersById = BuildOrdersByIdSnapshot(world.Book, order);
        var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(match.Fills, ordersById);
        var settled = SettlementProcessor.SettleFills(match.Fills, ordersById, postFillRemaining, world.Portfolios, world.Fee);
        return (world.WithPortfolios(settled), null);
    }

    /// <summary>
    /// fill 逆引き用スナップショット。
    /// world.Book は player match 前の状態 (= computer phase 直後) でなければならない。
    /// </summary>
    private static IReadOnlyDictionary<int, Order> BuildOrdersByIdSnapshot(OrderBook bookBeforePlayerMatch, Order playerOrder)
    {
        var dict = new Dictionary<int, Order>();
        foreach (var o in bookBeforePlayerMatch.Orders)
            dict[o.Id] = o;
        dict[playerOrder.Id] = playerOrder;
        return dict;
    }
}
