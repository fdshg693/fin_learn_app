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

    public Order CreateBuyOrder(int orderId, Instrument instrument, int quantity, int price)
    {
        return new Order(orderId, PlayerId, instrument, OrderSide.Buy, quantity, price);
    }

    public Order CreateSellOrder(int orderId, Instrument instrument, int quantity, int price)
    {
        return new Order(orderId, PlayerId, instrument, OrderSide.Sell, quantity, price);
    }

    public (Player Result, string? Warning) ApplyTrade(TradeResult trade)
    {
        var (resultPortfolio, warning) = trade.Side switch
        {
            OrderSide.Buy => Portfolio.Buy(
                trade.InstrumentId, trade.FilledQuantity, trade.TotalAmount, trade.Fee),
            OrderSide.Sell => Portfolio.Sell(
                trade.InstrumentId, trade.FilledQuantity, trade.TotalAmount, trade.Fee),
            _ => throw new ArgumentOutOfRangeException(nameof(trade))
        };

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
