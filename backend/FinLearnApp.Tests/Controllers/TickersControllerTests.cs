using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Api.Controllers;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Tests.Controllers;

public class TickersControllerTests
{
    private static readonly Guid AokiTickerGuid = Guid.Parse("4e6b3f5a-3d5a-4b9c-8e4d-4d5a3f7b4c04");

    private static InMemoryStore CreateStore() => SeedData.Create();

    [Fact]
    public void GetTickers_ReturnsAllTickers()
    {
        var controller = new TickersController(CreateStore());

        var result = controller.GetTickers();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TickerSummaryDto>>(ok.Value);

        Assert.Equal(3, dtos.Count);
        Assert.Contains(dtos, ticker => ticker.Symbol == "AOKI" && ticker.CompanyName == "Aoki Holdings" && ticker.CurrentPrice.Amount == 1_200m);
        Assert.Contains(dtos, ticker => ticker.Symbol == "HND" && ticker.CompanyName == "Hinode Systems" && ticker.CurrentPrice.Amount == 860m);
        Assert.Contains(dtos, ticker => ticker.Symbol == "SKR" && ticker.CompanyName == "Sakura Foods" && ticker.CurrentPrice.Amount == 540m);
    }

    [Fact]
    public void GetTicker_ExistingTicker_ReturnsDetail()
    {
        var controller = new TickersController(CreateStore());

        var result = controller.GetTicker(AokiTickerGuid);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TickerDetailDto>(ok.Value);

        Assert.Equal(AokiTickerGuid, dto.TickerId);
        Assert.Equal("AOKI", dto.Symbol);
        Assert.Equal("Aoki Holdings", dto.CompanyName);
        Assert.Equal(1, dto.UnitSize);
        Assert.Equal(1_200m, dto.CurrentPrice.Amount);
        Assert.Equal("JPY", dto.CurrentPrice.Currency);
    }

    [Fact]
    public void GetTicker_UnknownTicker_ReturnsNotFound()
    {
        var controller = new TickersController(CreateStore());
        var unknownTickerId = Guid.Parse("99999999-0000-0000-0000-000000000000");

        var result = controller.GetTicker(unknownTickerId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal("Ticker was not found.", problem.Detail);
        Assert.Equal("tickers.not_found", problem.Extensions["code"]);
    }

    [Fact]
    public void GetPriceHistory_ExistingTicker_ReturnsHistory()
    {
        // Arrange
        var store = CreateStore();
        var controller = new TickersController(store);
        var ticker = store.Tickers.First();

        // Act
        var result = controller.GetPriceHistory(ticker.Id.Value, limit: 20);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var history = Assert.IsAssignableFrom<IReadOnlyList<PriceRecordDto>>(ok.Value);
        Assert.NotEmpty(history);
        // 最初のレコードがターン0であること
        var firstRecord = history.First();
        Assert.Equal(0, firstRecord.Turn);
        // 価格が正の値であること（SeedDataの初期価格）
        Assert.True(firstRecord.Price.Amount > 0);
        Assert.Equal(ticker.CurrentPrice.Amount, firstRecord.Price.Amount);
    }

    [Fact]
    public void GetPriceHistory_UnknownTicker_Returns404()
    {
        // Arrange
        var store = CreateStore();
        var controller = new TickersController(store);

        // Act
        var result = controller.GetPriceHistory(Guid.NewGuid(), limit: 20);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
        Assert.Equal("tickers.not_found", problem.Extensions["code"]);
    }

    [Fact]
    public void GetPriceHistory_LimitApplied_ReturnsAtMostLimitRecords()
    {
        // Arrange
        var store = CreateStore();
        var investorId = store.Portfolios.First().InvestorId;
        for (int i = 0; i < 5; i++) store.AdvanceTurn(investorId);

        var controller = new TickersController(store);
        var ticker = store.Tickers.First();

        // Act
        var result = controller.GetPriceHistory(ticker.Id.Value, limit: 3);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var history = Assert.IsAssignableFrom<IReadOnlyList<PriceRecordDto>>(ok.Value);
        Assert.Equal(3, history.Count);
    }
}
