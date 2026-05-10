namespace FinLearn.Api.Dtos;

public sealed record GameResponse(
    string GameId,
    int Turn,
    PlayerDto Player,
    IReadOnlyList<InstrumentDto> Instruments,
    IReadOnlyList<TradeResultDto> RecentTrades,
    string? Warning = null);

public sealed record TradeResultDto(
    int InstrumentId,
    string Side,
    int FilledQuantity,
    int TotalAmount,
    int Fee);

public sealed record PlayerDto(
    string Name,
    int Cash,
    IReadOnlyList<PositionDto> Positions,
    int TotalAssets,
    int ProfitLoss,
    IReadOnlyList<PendingOrderDto> PendingOrders);

public sealed record PositionDto(
    int InstrumentId,
    int Quantity,
    int CurrentPrice,
    int Amount);

public sealed record PendingOrderDto(
    int Id,
    int InstrumentId,
    string Side,
    string Type,
    int Quantity,
    int? Price,
    int? StopPrice,
    int CreatedAtTurn,
    int ExpiresAtTurn);

public sealed record InstrumentDto(
    int Id,
    int Price);
