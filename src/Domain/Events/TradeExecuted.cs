using System;
using FinLearnApp.Domain.Entities;

namespace FinLearnApp.Domain.Events;

public sealed record TradeExecuted(Trade Trade, DateTimeOffset OccurredAt);
