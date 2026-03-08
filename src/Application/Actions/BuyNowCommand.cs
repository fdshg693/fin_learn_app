using System;
using MediatR;

namespace FinLearnApp.Application.Actions;

public sealed record BuyNowCommand : IRequest<ActionExecutionResult>
{
    public Guid InvestorId { get; }
    public Guid TickerId { get; }
    public int Quantity { get; }
    public int ExpectedTurn { get; }

    public BuyNowCommand(Guid investorId, Guid tickerId, int quantity, int expectedTurn)
    {
        InvestorId = investorId;
        TickerId = tickerId;
        Quantity = quantity;
        ExpectedTurn = expectedTurn;
    }
}
