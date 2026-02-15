using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Api.Controllers;

[ApiController]
[Route("api/actions")]
public sealed class ActionsController : ControllerBase
{
    private static readonly HashSet<string> SupportedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "BuyNow",
        "SellNow",
        "Wait"
    };

    private readonly InMemoryStore _store;

    public ActionsController(InMemoryStore store)
    {
        _store = store;
    }

    [HttpPost]
    public ActionResult<ActionResultDto> Execute(ActionRequestDto request)
    {
        if (!SupportedActions.Contains(request.Action))
        {
            return BadRequest(new { message = "Unsupported action." });
        }

        if (!string.Equals(request.Action, "Wait", StringComparison.OrdinalIgnoreCase) && request.Quantity <= 0)
        {
            return BadRequest(new { message = "Quantity must be greater than 0." });
        }

        var portfolio = _store.FindPortfolioByInvestor(new InvestorId(request.InvestorId));
        if (portfolio is null)
        {
            return NotFound();
        }

        if (string.Equals(request.Action, "Wait", StringComparison.OrdinalIgnoreCase))
        {
            var waitResult = new ActionResultDto(
                true,
                "Wait を実行しました。",
                ToPortfolioDto(portfolio));

            return Ok(waitResult);
        }

        var ticker = _store.FindTicker(new TickerId(request.TickerId));
        if (ticker is null)
        {
            return NotFound();
        }

        if (string.Equals(request.Action, "BuyNow", StringComparison.OrdinalIgnoreCase))
        {
            var totalCost = ticker.CurrentPrice.Multiply(request.Quantity);
            if (portfolio.Cash.Amount < totalCost.Amount)
            {
                var insufficientResult = new ActionResultDto(
                    false,
                    "現金が不足しています。",
                    ToPortfolioDto(portfolio));

                return Ok(insufficientResult);
            }

            portfolio.Withdraw(totalCost);
            portfolio.AddOrUpdateHolding(ticker.Id, request.Quantity);

            var buyResult = new ActionResultDto(
                true,
                "BuyNow を実行しました。",
                ToPortfolioDto(portfolio));

            return Ok(buyResult);
        }

        if (string.Equals(request.Action, "SellNow", StringComparison.OrdinalIgnoreCase))
        {
            var holding = portfolio.Holdings.FirstOrDefault(h => h.TickerId == ticker.Id);
            if (holding is null)
            {
                var noHoldingResult = new ActionResultDto(
                    false,
                    "保有がありません。",
                    ToPortfolioDto(portfolio));

                return Ok(noHoldingResult);
            }

            if (request.Quantity > holding.Quantity)
            {
                var insufficientHoldingResult = new ActionResultDto(
                    false,
                    "保有数量が不足しています。",
                    ToPortfolioDto(portfolio));

                return Ok(insufficientHoldingResult);
            }

            var proceeds = ticker.CurrentPrice.Multiply(request.Quantity);
            portfolio.ReduceHolding(ticker.Id, request.Quantity);
            portfolio.Deposit(proceeds);

            var sellResult = new ActionResultDto(
                true,
                "SellNow を実行しました。",
                ToPortfolioDto(portfolio));

            return Ok(sellResult);
        }

        return BadRequest(new { message = "Unsupported action." });
    }

    private PortfolioDto ToPortfolioDto(FinLearnApp.Domain.Entities.Portfolio portfolio)
    {
        var prices = _store.Tickers.ToDictionary(t => t.Id, t => t.CurrentPrice);

        var holdings = portfolio.Holdings
            .Select(holding =>
            {
                var ticker = _store.FindTicker(holding.TickerId);
                var price = prices[holding.TickerId];
                return new HoldingDto(
                    holding.TickerId.Value,
                    ticker?.Symbol ?? string.Empty,
                    holding.Quantity,
                    ToMoneyDto(holding.MarketValue(price)));
            })
            .ToList();

        return new PortfolioDto(
            portfolio.Id.Value,
            portfolio.InvestorId.Value,
            ToMoneyDto(portfolio.Cash),
            ToMoneyDto(portfolio.CalculateValuation(prices)),
            ToMoneyDto(portfolio.CalculateProfitLoss(prices)),
            holdings);
    }

    private static MoneyDto ToMoneyDto(Money money)
        => new(money.Amount, money.Currency.ToString());
}
