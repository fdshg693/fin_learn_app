using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Application.Actions;

public sealed class SellNowCommandHandler : IRequestHandler<SellNowCommand, ActionExecutionResult>
{
    private readonly IActionExecutionStore _store;

    public SellNowCommandHandler(IActionExecutionStore store)
    {
        _store = store;
    }

    public Task<ActionExecutionResult> Handle(SellNowCommand command, CancellationToken cancellationToken)
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

        var holding = portfolio.Holdings.FirstOrDefault(holdingItem => holdingItem.TickerId == ticker.Id);
        if (holding is null)
        {
            var nextTurn = _store.AdvanceTurn(portfolio.InvestorId);
            return Task.FromResult(ActionExecutionResult.Ok(false, "保有がありません。", portfolio, nextTurn));
        }

        if (command.Quantity > holding.Quantity)
        {
            var nextTurn = _store.AdvanceTurn(portfolio.InvestorId);
            return Task.FromResult(ActionExecutionResult.Ok(false, "保有数量が不足しています。", portfolio, nextTurn));
        }

        var matchResult = _store.ExecuteSellNow(ticker.Id, command.Quantity);
        if (matchResult.ExecutedQuantity <= 0)
        {
            var nextTurn = _store.AdvanceTurn(portfolio.InvestorId);
            return Task.FromResult(ActionExecutionResult.Ok(false, "約定する買い注文がありませんでした。", portfolio, nextTurn));
        }

        portfolio.ReduceHolding(ticker.Id, matchResult.ExecutedQuantity);
        portfolio.Deposit(matchResult.TotalAmount);
        var advancedTurn = _store.AdvanceTurn(portfolio.InvestorId);

        if (matchResult.RemainingQuantity > 0)
        {
            return Task.FromResult(ActionExecutionResult.Ok(
                true,
                $"{matchResult.ExecutedQuantity}株を約定しました（未約定 {matchResult.RemainingQuantity}株）。",
                portfolio,
                advancedTurn));
        }

        return Task.FromResult(ActionExecutionResult.Ok(true, "SellNow を実行しました。", portfolio, advancedTurn));
    }
}
