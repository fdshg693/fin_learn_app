using FinLearn.Api.Dtos;
using FinLearn.Core;

namespace FinLearn.Api.Mappers;

public static class OrderBookMapper
{
    public static OrderBookResponse ToResponse(OrderBook book)
    {
        var orders = book.Orders.Select(o => new OrderDto(
            Id: o.Id,
            TraderId: o.TraderId,
            InstrumentId: o.Instrument.Id,
            Side: o.Side.ToString(),
            Type: o.Type.ToString(),
            Quantity: o.Quantity,
            Price: o.Price,
            StopPrice: o.StopPrice,
            CreatedAtTurn: o.CreatedAtTurn
        )).ToList();

        return new OrderBookResponse(orders);
    }
}
