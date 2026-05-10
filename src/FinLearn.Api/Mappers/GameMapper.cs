using FinLearn.Api.Dtos;
using FinLearn.Core;

namespace FinLearn.Api.Mappers;

public static class GameMapper
{
    public static GameResponse ToResponse(string gameId, Game game, IExchange exchange,
        string? warning = null, IReadOnlyList<TradeResult>? recentTrades = null)
    {
        var positions = game.Instruments
            .Select(i =>
            {
                var qty = game.Player.Portfolio.QuantityOf(i.Id);
                if (qty <= 0) return null;
                exchange.TryGetPrice(i.Id, out var price);
                return new PositionDto(i.Id, qty, price, qty * price);
            })
            .Where(p => p is not null)
            .Cast<PositionDto>()
            .ToList();

        var pendingOrders = game.OrderBook.Orders
            .Where(o => o.TraderId == game.Player.Name)
            .Select(o => new PendingOrderDto(
                Id: o.Id,
                InstrumentId: o.Instrument.Id,
                Side: o.Side.ToString(),
                Type: o.Type.ToString(),
                Quantity: o.Quantity,
                Price: o.Price,
                StopPrice: o.StopPrice,
                CreatedAtTurn: o.CreatedAtTurn,
                ExpiresAtTurn: o.ExpiresAtTurn))
            .ToList();

        var playerDto = new PlayerDto(
            Name: game.Player.Name,
            Cash: game.Player.Portfolio.Cash,
            Positions: positions,
            TotalAssets: game.Player.Portfolio.TotalAmount(exchange),
            ProfitLoss: game.Player.ProfitLoss(exchange),
            PendingOrders: pendingOrders);

        var instruments = game.Instruments
            .Select(i =>
            {
                exchange.TryGetPrice(i.Id, out var price);
                return new InstrumentDto(i.Id, price);
            })
            .ToList();

        var tradeDtos = (recentTrades ?? Array.Empty<TradeResult>())
            .Select(t => new TradeResultDto(t.InstrumentId, t.Side.ToString(), t.FilledQuantity, t.TotalAmount, t.Fee))
            .ToList();

        return new GameResponse(gameId, game.Turn, playerDto, instruments, tradeDtos, warning);
    }
}
