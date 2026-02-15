namespace MyApp.Core;

public sealed class Game
{
    public Game()
        : this(new Player(), turn: 1)
    {
    }

    private Game(Player player, int turn)
    {
        Player = player;
        Turn = turn;
    }

    public int Turn { get; }
    public Player Player { get; }

    public (Game Result, string? Warning) Buy(IExchange exchange, int instrumentId, int quantity)
    {
        var (resultPlayer, warning) = Player.Buy(exchange, instrumentId, quantity);
        if (warning is not null)
        {
            return (this, warning);
        }
        return (new Game(resultPlayer, Turn + 1), null);
    }

    public (Game Result, string? Warning) Sell(IExchange exchange, int instrumentId, int quantity)
    {
        var (resultPlayer, warning) = Player.Sell(exchange, instrumentId, quantity);
        if (warning is not null)
        {
            return (this, warning);
        }
        return (new Game(resultPlayer, Turn + 1), null);
    }

    public (Game Result, string? Warning) Wait()
    {
        return (new Game(Player, Turn + 1), null);
    }
}
