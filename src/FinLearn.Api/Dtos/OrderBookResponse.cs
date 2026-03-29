namespace FinLearn.Api.Dtos;

public sealed record OrderBookResponse(
    IReadOnlyList<OrderDto> Orders);

public sealed record OrderDto(
    int Id,
    string TraderId,
    int InstrumentId,
    string Side,
    string Type,
    int Quantity,
    int? Price,
    int? StopPrice,
    int CreatedAtTurn);
