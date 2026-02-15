using System;

namespace FinLearnApp.Api.Models.Api;

public sealed record ActionRequestDto(
    Guid InvestorId,
    Guid TickerId,
    string Action,
    int Quantity);

public sealed record ActionResultDto(
    bool Success,
    string Message,
    PortfolioDto Portfolio);
