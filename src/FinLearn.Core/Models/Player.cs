namespace FinLearn.Core;

public sealed class Player
{
    public Player(string name = "player")
        : this(name, new Portfolio(cash: GameRules.Player.InitialCash, positions: Array.Empty<Position>()))
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

    public Order CreateOrder(int orderId, Instrument instrument, OrderSide side, int quantity, int? price, int? stopPrice, int createdAtTurn, int expiresAtTurn)
    {
        return price is not null
            ? new Order(orderId, Name, instrument, side, quantity, price.Value, createdAtTurn, expiresAtTurn)
            : Order.CreateMarket(orderId, Name, instrument, side, quantity, stopPrice, createdAtTurn, expiresAtTurn);
    }

    public int ProfitLoss(IExchange exchange)
    {
        return Portfolio.TotalAmount(exchange) - GameRules.Player.InitialCash;
    }
}
