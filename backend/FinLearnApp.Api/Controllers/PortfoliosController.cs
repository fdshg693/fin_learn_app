using System;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Mappers;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Api.Controllers;

[ApiController]
[Route("api/portfolios")]
public sealed class PortfoliosController : ControllerBase
{
    private readonly InMemoryStore _store;
    private readonly PortfolioMapper _portfolioMapper;

    public PortfoliosController(InMemoryStore store, PortfolioMapper portfolioMapper)
    {
        _store = store;
        _portfolioMapper = portfolioMapper;
    }

    [HttpGet("{investorId:guid}")]
    public ActionResult<PortfolioDto> GetPortfolio(Guid investorId)
    {
        var portfolio = _store.FindPortfolioByInvestor(new InvestorId(investorId));
        if (portfolio is null)
        {
            return NotFound();
        }

        return Ok(_portfolioMapper.ToDto(portfolio));
    }
}
