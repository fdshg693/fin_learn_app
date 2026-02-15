using System;
using System.Collections.Generic;

namespace FinLearnApp.Api.Models.Api;

public sealed record HoldingDto(
    Guid TickerId,
    string Symbol,
    int Quantity,
    MoneyDto MarketValue);

public sealed record PortfolioDto(
    Guid PortfolioId,
    Guid InvestorId,
    MoneyDto Cash,
    MoneyDto Valuation,
    MoneyDto ProfitLoss,
    IReadOnlyList<HoldingDto> Holdings);
