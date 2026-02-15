using System;
using MediatR;

namespace FinLearnApp.Application.Actions;

public sealed record WaitCommand : IRequest<ActionExecutionResult>
{
    public Guid InvestorId { get; }

    public WaitCommand(Guid investorId)
    {
        InvestorId = investorId;
    }
}
