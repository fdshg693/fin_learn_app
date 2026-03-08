using System;
using MediatR;

namespace FinLearnApp.Application.Actions;

public sealed record BuyLimitCommand : IRequest<ActionExecutionResult>
{
    public Guid InvestorId { get; }
    public Guid TickerId { get; }
    public int Quantity { get; }
    public decimal LimitPriceAmount { get; }
    public int ExpectedTurn { get; }

    public BuyLimitCommand(Guid investorId, Guid tickerId, int quantity, decimal limitPriceAmount, int expectedTurn)
    {
        InvestorId = investorId;
        TickerId = tickerId;
        Quantity = quantity;
        LimitPriceAmount = limitPriceAmount;
        ExpectedTurn = expectedTurn;
    }
}
