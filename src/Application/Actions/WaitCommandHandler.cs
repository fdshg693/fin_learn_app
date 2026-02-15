using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Application.Actions;

public sealed class WaitCommandHandler : IRequestHandler<WaitCommand, ActionExecutionResult>
{
    private readonly IActionExecutionStore _store;

    public WaitCommandHandler(IActionExecutionStore store)
    {
        _store = store;
    }

    public Task<ActionExecutionResult> Handle(WaitCommand command, CancellationToken cancellationToken)
    {
        var portfolio = _store.FindPortfolioByInvestor(new InvestorId(command.InvestorId));
        if (portfolio is null)
        {
            return Task.FromResult(ActionExecutionResult.NotFound());
        }

        return Task.FromResult(ActionExecutionResult.Ok(true, "Wait を実行しました。", portfolio));
    }
}
