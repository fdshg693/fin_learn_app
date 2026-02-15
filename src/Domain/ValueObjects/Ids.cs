using System;

namespace FinLearnApp.Domain.ValueObjects;

public readonly record struct CompanyId
{
	public Guid Value { get; }

	public CompanyId(Guid value)
	{
		Value = value;
	}
}

public readonly record struct InvestorId
{
	public Guid Value { get; }

	public InvestorId(Guid value)
	{
		Value = value;
	}
}

public readonly record struct TickerId
{
	public Guid Value { get; }

	public TickerId(Guid value)
	{
		Value = value;
	}
}

public readonly record struct PortfolioId
{
	public Guid Value { get; }

	public PortfolioId(Guid value)
	{
		Value = value;
	}
}

public readonly record struct OrderId
{
	public Guid Value { get; }

	public OrderId(Guid value)
	{
		Value = value;
	}
}

public readonly record struct TradeId
{
	public Guid Value { get; }

	public TradeId(Guid value)
	{
		Value = value;
	}
}
