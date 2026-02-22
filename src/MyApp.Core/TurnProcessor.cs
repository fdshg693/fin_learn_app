namespace MyApp.Core;

/// <summary>
/// ターン進行のワークフローを担うドメインサービス。
/// Game は状態スナップショットに徹し、TurnProcessor がアクション処理を行う。
/// </summary>
public sealed class TurnProcessor
{
    public IOrderPlacer OrderPlacer { get; }
    public IMarket Market { get; }

    public TurnProcessor(IOrderPlacer orderPlacer)
        : this(orderPlacer, new Market())
    {
    }

    public TurnProcessor(IOrderPlacer orderPlacer, IMarket market)
    {
        OrderPlacer = orderPlacer;
        Market = market;
    }

    public (Game Result, string? Warning) Buy(Game game, IExchange exchange, int instrumentId, int quantity, int? price = null)
    {
        if (quantity <= 0)
            return (game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return (game, Messages.PriceMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, exchange, instrument, OrderSide.Buy, quantity, price, Messages.NoMatchingSellOrders);
    }

    public (Game Result, string? Warning) Sell(Game game, IExchange exchange, int instrumentId, int quantity, int? price = null)
    {
        if (quantity <= 0)
            return (game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return (game, Messages.PriceMustBePositive);

        if (game.Player.Portfolio.QuantityOf(instrumentId) < quantity)
            return (game, Messages.InsufficientQuantityToSell);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, exchange, instrument, OrderSide.Sell, quantity, price, Messages.NoMatchingBuyOrders);
    }

    public (Game Result, string? Warning) Wait(Game game, IExchange exchange)
    {
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId);
        return (new Game(game.Player, game.Turn + 1, bookWithOrders, nextId, game.Instruments), null);
    }

    private (Game Result, string? Warning) PlaceOrder(
        Game game, IExchange exchange, Instrument instrument, OrderSide side,
        int quantity, int? price, string noMatchMessage)
    {
        // 1. コンピューター注文を生成
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId);

        // 2. プレイヤーの注文を生成
        var order = game.Player.CreateOrder(nextId, instrument, side, quantity, price);

        // 3. 市場で約定
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        // 4. 成行注文で約定ゼロ → 警告を返す
        if (price is null && matchResult.Trade.FilledQuantity == 0)
            return (game, noMatchMessage);

        // 5. 約定分があればポートフォリオを更新
        var resultPlayer = game.Player;
        if (matchResult.Trade.FilledQuantity > 0)
        {
            var (resultPortfolio, warning) = game.Player.Portfolio.ApplyTrade(matchResult.Trade);
            if (warning is not null)
                return (game, warning);
            resultPlayer = game.Player.WithPortfolio(resultPortfolio);
        }

        // 6. 指値注文の未約定分を板に追加
        var updatedBook = matchResult.UpdatedBook;
        if (price is not null)
        {
            var remainingQty = quantity - matchResult.Trade.FilledQuantity;
            if (remainingQty > 0)
            {
                updatedBook = updatedBook.Add(order.WithQuantity(remainingQty));
            }
        }

        return (new Game(resultPlayer, game.Turn + 1, updatedBook,
            nextId + 1, game.Instruments), null);
    }
}
