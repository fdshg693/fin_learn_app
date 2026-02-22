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

    public (Player Result, string? Warning) Buy(int instrumentId, int filledQuantity, int totalCost, int fee)
    {
        var (resultPortfolio, warning) = Portfolio.Buy(instrumentId, filledQuantity, totalCost, fee);
        if (warning is not null)
            return (this, warning);
        return (new Player(resultPortfolio), null);
    }

    public (Player Result, string? Warning) Sell(int instrumentId, int filledQuantity, int totalProceeds, int fee)
    {
        var (resultPortfolio, warning) = Portfolio.Sell(instrumentId, filledQuantity, totalProceeds, fee);
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
