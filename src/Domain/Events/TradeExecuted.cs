using System;
using FinLearnApp.Domain.Entities;

namespace FinLearnApp.Domain.Events;

public sealed record TradeExecuted
{
	public Trade Trade { get; }
	public DateTimeOffset OccurredAt { get; }

	public TradeExecuted(Trade trade, DateTimeOffset occurredAt)
	{
		Trade = trade;
		OccurredAt = occurredAt;
	}
}
