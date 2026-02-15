namespace MyApp.Core;

public sealed class Player
{
    public Player()
    {
        Portfolio = new Portfolio(cash: 10000, positions: Array.Empty<Position>());
    }

    public Portfolio Portfolio { get; private set; }

    public (Player Result, string? Warning) Buy(IExchange exchange, int instrumentId, int quantity)
    {
        var (resultPortfolio, warning) = Portfolio.Buy(exchange, instrumentId, quantity);
        if (warning is null)
        {
            Portfolio = resultPortfolio;
        }
        return (this, warning);
    }
}
