using System;
using FinLearnApp.Api.Controllers;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Mappers;
using FinLearnApp.Api.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Tests.Controllers;

public class PortfoliosControllerTests
{
    private static readonly Guid InvestorGuid = Guid.Parse("7b3e6c8d-6a8d-4e9f-9b7c-7c8d6c0e7f07");
    private static readonly Guid PortfolioGuid = Guid.Parse("8c2f7d9e-7b9e-4f9a-9c8d-8d9e7d1f8008");
    private static readonly Guid AokiTickerGuid = Guid.Parse("4e6b3f5a-3d5a-4b9c-8e4d-4d5a3f7b4c04");
    private static readonly Guid HndTickerGuid = Guid.Parse("5f5c4a6b-4e6b-4c9d-9f5e-5e6b4a8c5d05");

    [Fact]
    public void GetPortfolio_ExistingInvestor_ReturnsOkWithPortfolio()
    {
        var store = SeedData.Create();
        var controller = new PortfoliosController(store, new PortfolioMapper(store));

        var result = controller.GetPortfolio(InvestorGuid);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PortfolioDto>(ok.Value);

        Assert.Equal(PortfolioGuid, dto.PortfolioId);
        Assert.Equal(InvestorGuid, dto.InvestorId);
        Assert.Equal(0, dto.CurrentTurn);
        Assert.Equal(700_000m, dto.Cash.Amount);
        Assert.Equal("JPY", dto.Cash.Currency);
        Assert.Equal(912_800m, dto.Valuation.Amount);
        Assert.Equal(-87_200m, dto.ProfitLoss.Amount);
        Assert.Equal(2, dto.Holdings.Count);

        var aoki = Assert.Single(dto.Holdings, holding => holding.TickerId == AokiTickerGuid);
        Assert.Equal("AOKI", aoki.Symbol);
        Assert.Equal(120, aoki.Quantity);
        Assert.Equal(144_000m, aoki.MarketValue.Amount);

        var hnd = Assert.Single(dto.Holdings, holding => holding.TickerId == HndTickerGuid);
        Assert.Equal("HND", hnd.Symbol);
        Assert.Equal(80, hnd.Quantity);
        Assert.Equal(68_800m, hnd.MarketValue.Amount);
    }

    [Fact]
    public void GetPortfolio_UnknownInvestor_ReturnsNotFound()
    {
        var store = SeedData.Create();
        var controller = new PortfoliosController(store, new PortfolioMapper(store));
        var unknownInvestorId = Guid.Parse("99999999-0000-0000-0000-000000000000");

        var result = controller.GetPortfolio(unknownInvestorId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal("Portfolio was not found for the specified investor.", problem.Detail);
        Assert.Equal("portfolios.not_found", problem.Extensions["code"]);
    }
}
