using System;

namespace FinLearnApp.Api.Models.Api;

public sealed record TickerSummaryDto(
    Guid TickerId,
    string Symbol,
    string CompanyName,
    MoneyDto CurrentPrice);

public sealed record TickerDetailDto(
    Guid TickerId,
    string Symbol,
    string CompanyName,
    int UnitSize,
    MoneyDto CurrentPrice);
