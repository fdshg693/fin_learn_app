using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Application.Actions;

public sealed class BuyNowCommandHandler : IRequestHandler<BuyNowCommand, ActionExecutionResult>
{
    private readonly IActionExecutionStore _store;

    public BuyNowCommandHandler(IActionExecutionStore store)
    {
        _store = store;
    }

    public Task<ActionExecutionResult> Handle(BuyNowCommand command, CancellationToken cancellationToken)
    {
        if (command.Quantity <= 0)
        {
            return Task.FromResult(ActionExecutionResult.BadRequest("Quantity must be greater than 0."));
        }

        var portfolio = _store.FindPortfolioByInvestor(new InvestorId(command.InvestorId));
        if (portfolio is null)
        {
            return Task.FromResult(ActionExecutionResult.NotFound());
        }

        var ticker = _store.FindTicker(new TickerId(command.TickerId));
        if (ticker is null)
        {
            return Task.FromResult(ActionExecutionResult.NotFound());
        }

        var totalCost = ticker.CurrentPrice.Multiply(command.Quantity);
        if (portfolio.Cash.Amount < totalCost.Amount)
        {
            return Task.FromResult(ActionExecutionResult.Ok(false, "現金が不足しています。", portfolio));
        }

        portfolio.Withdraw(totalCost);
        portfolio.AddOrUpdateHolding(ticker.Id, command.Quantity);

        return Task.FromResult(ActionExecutionResult.Ok(true, "BuyNow を実行しました。", portfolio));
    }
}
