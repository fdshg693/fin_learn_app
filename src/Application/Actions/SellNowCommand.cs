using System;
using MediatR;

namespace FinLearnApp.Application.Actions;

public sealed record SellNowCommand : IRequest<ActionExecutionResult>
{
    public Guid InvestorId { get; }
    public Guid TickerId { get; }
    public int Quantity { get; }

    public SellNowCommand(Guid investorId, Guid tickerId, int quantity)
    {
        InvestorId = investorId;
        TickerId = tickerId;
        Quantity = quantity;
    }
}
