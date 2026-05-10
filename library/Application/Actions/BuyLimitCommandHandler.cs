using System.Threading;
using System.Threading.Tasks;
using FinLearnApp.Domain.ValueObjects;
using MediatR;

namespace FinLearnApp.Application.Actions;

public sealed class BuyLimitCommandHandler : IRequestHandler<BuyLimitCommand, ActionExecutionResult>
{
    private readonly IActionExecutionStore _store;

    public BuyLimitCommandHandler(IActionExecutionStore store)
    {
        _store = store;
    }

    public Task<ActionExecutionResult> Handle(BuyLimitCommand command, CancellationToken cancellationToken)
    {
        if (command.Quantity <= 0)
        {
            return Task.FromResult(ActionExecutionResult.BadRequest("Quantity must be greater than 0."));
        }

        if (command.LimitPriceAmount <= 0m)
        {
            return Task.FromResult(ActionExecutionResult.BadRequest("Limit price must be greater than 0."));
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

        var limitPrice = Money.Jpy(command.LimitPriceAmount);
        var maxCost = limitPrice.Multiply(command.Quantity);
        if (portfolio.Cash.Amount < maxCost.Amount)
        {
            var nextTurn = _store.AdvanceTurn(portfolio.InvestorId);
            return Task.FromResult(ActionExecutionResult.Ok(false, "指値注文に必要な現金が不足しています。", portfolio, nextTurn));
        }

        var matchResult = _store.ExecuteBuyLimit(ticker.Id, command.Quantity, limitPrice, portfolio.Cash);
        if (matchResult.ExecutedQuantity <= 0)
        {
            var nextTurn = _store.AdvanceTurn(portfolio.InvestorId);
            return Task.FromResult(ActionExecutionResult.Ok(false, "条件に合う売り注文がありませんでした。", portfolio, nextTurn));
        }

        portfolio.Withdraw(matchResult.TotalAmount);
        portfolio.AddOrUpdateHolding(ticker.Id, matchResult.ExecutedQuantity);
        var advancedTurn = _store.AdvanceTurn(portfolio.InvestorId);

        if (matchResult.RemainingQuantity > 0)
        {
            return Task.FromResult(ActionExecutionResult.Ok(
                true,
                $"指値買いで {matchResult.ExecutedQuantity}株を約定（未約定 {matchResult.RemainingQuantity}株）。",
                portfolio,
                advancedTurn));
        }

        return Task.FromResult(ActionExecutionResult.Ok(true, "BuyLimit を実行しました。", portfolio, advancedTurn));
    }
}
