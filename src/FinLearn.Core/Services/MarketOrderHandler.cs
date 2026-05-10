namespace FinLearn.Core;

internal sealed class MarketOrderHandler : IPlayerOrderHandler
{
    public (World World, string? Warning) Receive(World world, Order order) => (world, null);

    public (World World, string? Warning) Settle(World world, Order order, MatchResult match)
    {
        if (match.Trade.FilledQuantity == 0)
        {
            var warn = order.Side == OrderSide.Buy
                ? Messages.NoMatchingSellOrders
                : Messages.NoMatchingBuyOrders;
            return (world, warn);
        }

        var (after, applyWarn) = world.PlayerPortfolio.ApplyTrade(match.Trade);
        if (applyWarn is not null)
            return (world, applyWarn);

        return (world.WithPlayerPortfolio(after), null);
    }
}
