using System;
using MediatR;

namespace FinLearnApp.Application.Actions;

public sealed record WaitCommand : IRequest<ActionExecutionResult>
{
    public Guid InvestorId { get; }
    public int ExpectedTurn { get; }

    public WaitCommand(Guid investorId, int expectedTurn)
    {
        InvestorId = investorId;
        ExpectedTurn = expectedTurn;
    }
}
