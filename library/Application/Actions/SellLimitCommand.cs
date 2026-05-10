using System;
using MediatR;

namespace FinLearnApp.Application.Actions;

public sealed record SellLimitCommand : IRequest<ActionExecutionResult>
{
    public Guid InvestorId { get; }
    public Guid TickerId { get; }
    public int Quantity { get; }
    public decimal LimitPriceAmount { get; }
    public int ExpectedTurn { get; }

    public SellLimitCommand(Guid investorId, Guid tickerId, int quantity, decimal limitPriceAmount, int expectedTurn)
    {
        InvestorId = investorId;
        TickerId = tickerId;
        Quantity = quantity;
        LimitPriceAmount = limitPriceAmount;
        ExpectedTurn = expectedTurn;
    }
}
