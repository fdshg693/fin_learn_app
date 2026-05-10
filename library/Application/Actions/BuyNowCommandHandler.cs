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

        var currentTurn = _store.GetCurrentTurn(portfolio.InvestorId);
        if (command.ExpectedTurn != currentTurn)
        {
            return Task.FromResult(ActionExecutionResult.Conflict(
                $"ExpectedTurn mismatch. expected={command.ExpectedTurn}, current={currentTurn}.",
                currentTurn));
        }

        var ticker = _store.FindTicker(new TickerId(command.TickerId));
        if (ticker is null)
        {
            return Task.FromResult(ActionExecutionResult.NotFound());
        }

        var matchResult = _store.ExecuteBuyNow(ticker.Id, command.Quantity, portfolio.Cash);
        if (matchResult.ExecutedQuantity <= 0)
        {
            var nextTurn = _store.AdvanceTurn(portfolio.InvestorId);
            return Task.FromResult(ActionExecutionResult.Ok(false, "約定する売り注文がありませんでした。", portfolio, nextTurn));
        }

        portfolio.Withdraw(matchResult.TotalAmount);
        portfolio.AddOrUpdateHolding(ticker.Id, matchResult.ExecutedQuantity);
        var advancedTurn = _store.AdvanceTurn(portfolio.InvestorId);

        if (matchResult.RemainingQuantity > 0)
        {
            return Task.FromResult(ActionExecutionResult.Ok(
                true,
                $"{matchResult.ExecutedQuantity}株を約定しました（未約定 {matchResult.RemainingQuantity}株）。",
                portfolio,
                advancedTurn));
        }

        return Task.FromResult(ActionExecutionResult.Ok(true, "BuyNow を実行しました。", portfolio, advancedTurn));
    }
}
