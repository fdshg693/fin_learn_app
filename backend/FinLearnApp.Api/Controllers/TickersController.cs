using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Api.Responses;
using FinLearnApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Api.Controllers;

[ApiController]
[Route("api/tickers")]
public sealed class TickersController : ControllerBase
{
    private readonly InMemoryStore _store;

    public TickersController(InMemoryStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<TickerSummaryDto>> GetTickers()
    {
        var results = _store.Tickers
            .Select(ticker => new TickerSummaryDto(
                ticker.Id.Value,
                ticker.Symbol,
                _store.GetCompany(ticker.CompanyId).Name,
                ToMoneyDto(ticker.CurrentPrice)))
            .ToList();

        return Ok(results);
    }

    [HttpGet("{tickerId:guid}")]
    public ActionResult<TickerDetailDto> GetTicker(Guid tickerId)
    {
        var ticker = _store.FindTicker(new TickerId(tickerId));
        if (ticker is null)
        {
            return ApiProblemFactory.NotFound(
                this,
                "Ticker was not found.",
                "tickers.not_found");
        }

        var dto = new TickerDetailDto(
            ticker.Id.Value,
            ticker.Symbol,
            _store.GetCompany(ticker.CompanyId).Name,
            ticker.UnitSize,
            ToMoneyDto(ticker.CurrentPrice));

        return Ok(dto);
    }

    private static MoneyDto ToMoneyDto(Money money)
    {
        return new MoneyDto(money.Amount, money.Currency.ToString());
    }
}
