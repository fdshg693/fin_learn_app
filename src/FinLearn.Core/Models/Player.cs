namespace FinLearn.Core;

public sealed class Player
{
    private const int InitialCash = 10000;

    public Player(string name = "player")
        : this(name, new Portfolio(cash: InitialCash, positions: Array.Empty<Position>()))
    {
    }

    private Player(string name, Portfolio portfolio)
    {
        Name = name;
        Portfolio = portfolio;
    }

    public string Name { get; }
    public Portfolio Portfolio { get; }

    public Player WithPortfolio(Portfolio portfolio)
    {
        return new Player(Name, portfolio);
    }

    public Order CreateOrder(int orderId, Instrument instrument, OrderSide side, int quantity, int? price)
    {
        return price is not null
            ? new Order(orderId, Name, instrument, side, quantity, price.Value)
            : Order.CreateMarket(orderId, Name, instrument, side, quantity);
    }

    public int ProfitLoss(IExchange exchange)
    {
        return Portfolio.TotalAmount(exchange) - InitialCash;
    }
}
