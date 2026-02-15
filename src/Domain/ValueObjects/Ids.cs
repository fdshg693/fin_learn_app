using System;

namespace FinLearnApp.Domain.ValueObjects;

public readonly record struct CompanyId(Guid Value);
public readonly record struct InvestorId(Guid Value);
public readonly record struct TickerId(Guid Value);
public readonly record struct PortfolioId(Guid Value);
public readonly record struct OrderId(Guid Value);
public readonly record struct TradeId(Guid Value);
