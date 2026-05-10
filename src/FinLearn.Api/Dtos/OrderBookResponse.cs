namespace FinLearn.Api.Dtos;

public sealed record OrderBookResponse(
    IReadOnlyList<OrderDto> Orders,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record OrderDto(
    int Id,
    string TraderId,
    int InstrumentId,
    string Side,
    string Type,
    int Quantity,
    int? Price,
    int? StopPrice,
    int CreatedAtTurn,
    int ExpiresAtTurn);
