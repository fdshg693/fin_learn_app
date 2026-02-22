namespace MyApp.Core;

public sealed class Player
{
    private const int InitialCash = 10000;

    public Player()
        : this(new Portfolio(cash: InitialCash, positions: Array.Empty<Position>()))
    {
    }

    private Player(Portfolio portfolio)
    {
        Portfolio = portfolio;
    }

    public Portfolio Portfolio { get; }

    private const string PlayerId = "player";

    public Order CreateOrder(int orderId, Instrument instrument, OrderSide side, int quantity, int? price)
    {
        return price is not null
            ? new Order(orderId, PlayerId, instrument, side, quantity, price.Value)
            : Order.CreateMarket(orderId, PlayerId, instrument, side, quantity);
    }

    public (Player Result, string? Warning) ApplyTrade(TradeResult trade)
    {
        var (resultPortfolio, warning) = Portfolio.ApplyTrade(trade);

        if (warning is not null)
            return (this, warning);
        return (new Player(resultPortfolio), null);
    }

    public (Player Result, string? Warning) Wait()
    {
        return (this, null);
    }

    public int ProfitLoss(IExchange exchange)
    {
        return Portfolio.TotalAmount(exchange) - InitialCash;
    }
}
