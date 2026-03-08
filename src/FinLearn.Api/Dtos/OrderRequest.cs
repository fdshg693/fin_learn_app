namespace FinLearn.Api.Dtos;

public sealed record OrderRequest(
    int InstrumentId,
    int Quantity,
    int? Price = null,
    int? StopPrice = null);
