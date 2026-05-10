using FinLearn.Core;

namespace FinLearn.Api.Dtos;

public sealed record OrderRequest(
    OrderSide? Side,
    int InstrumentId,
    int Quantity,
    int? Price = null,
    int? StopPrice = null,
    int? ExpiresInTurns = null);
