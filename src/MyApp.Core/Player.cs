namespace MyApp.Core;

public sealed class Player
{
    public Player()
        : this(new Portfolio(cash: 10000, positions: Array.Empty<Position>()))
    {
    }

    private Player(Portfolio portfolio)
    {
        Portfolio = portfolio;
    }

    public Portfolio Portfolio { get; }

    public (Player Result, string? Warning) Buy(IExchange exchange, int instrumentId, int quantity)
    {
        var (resultPortfolio, warning) = Portfolio.Buy(exchange, instrumentId, quantity);
        if (warning is not null)
        {
            return (this, warning);
        }
        return (new Player(resultPortfolio), null);
    }
}
