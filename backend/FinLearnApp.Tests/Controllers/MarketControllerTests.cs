using System;
using System.Collections.Generic;
using System.Threading;
using FinLearnApp.Api.Controllers;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Tests.Controllers;

public class MarketControllerTests
{
    private static readonly CompanyId CompanyId = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
    private static readonly InvestorId InvestorId = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"));
    private static readonly TickerId SnapshotTickerId = new(Guid.Parse("cccccccc-0000-0000-0000-000000000001"));
    private static readonly TickerId TradeTickerId = new(Guid.Parse("dddddddd-0000-0000-0000-000000000001"));

    [Fact]
    public void GetSnapshot_WithOrdersAndTrades_ReturnsSortedSnapshot()
    {
        var store = CreateStore();
        var snapshotTicker = store.FindTicker(SnapshotTickerId)!;
        var tradeTicker = store.FindTicker(TradeTickerId)!;
        var baseTime = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);

        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            snapshotTicker.Id,
            OrderSide.Buy,
            Money.Jpy(990m),
            5,
            OrderOrigin.Investor,
            baseTime));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.Parse("10000000-0000-0000-0000-000000000002")),
            snapshotTicker.Id,
            OrderSide.Buy,
            Money.Jpy(995m),
            10,
            OrderOrigin.System,
            baseTime.AddMinutes(1)));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            snapshotTicker.Id,
            OrderSide.Sell,
            Money.Jpy(1_010m),
            7,
            OrderOrigin.Investor,
            baseTime.AddMinutes(2)));
        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            snapshotTicker.Id,
            OrderSide.Sell,
            Money.Jpy(1_020m),
            9,
            OrderOrigin.System,
            baseTime.AddMinutes(3)));

        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
            tradeTicker.Id,
            OrderSide.Sell,
            Money.Jpy(1_500m),
            5,
            OrderOrigin.System,
            baseTime.AddMinutes(4)));
        store.ExecuteBuyNow(tradeTicker.Id, quantity: 2, availableCash: Money.Jpy(1_000_000m));

        Thread.Sleep(20);

        store.Exchange.OrderBook.Add(new Order(
            new OrderId(Guid.Parse("30000000-0000-0000-0000-000000000002")),
            tradeTicker.Id,
            OrderSide.Sell,
            Money.Jpy(1_400m),
            5,
            OrderOrigin.Investor,
            baseTime.AddMinutes(5)));
        store.ExecuteBuyNow(tradeTicker.Id, quantity: 3, availableCash: Money.Jpy(1_000_000m));

        var controller = new MarketController(store);

        var result = controller.GetSnapshot();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MarketSnapshotDto>(ok.Value);

        Assert.Equal(2, dto.BuyOrders.Count);
        Assert.Equal(4, dto.SellOrders.Count);
        Assert.Equal(2, dto.Trades.Count);

        Assert.True(dto.BuyOrders[0].CreatedAt >= dto.BuyOrders[1].CreatedAt);
        Assert.True(dto.SellOrders[0].CreatedAt >= dto.SellOrders[1].CreatedAt);
        Assert.True(dto.SellOrders[1].CreatedAt >= dto.SellOrders[2].CreatedAt);
        Assert.True(dto.SellOrders[2].CreatedAt >= dto.SellOrders[3].CreatedAt);
        Assert.True(dto.Trades[0].ExecutedAt >= dto.Trades[1].ExecutedAt);

        Assert.Equal("AOKI", dto.BuyOrders[0].Symbol);
        Assert.Equal("Buy", dto.BuyOrders[0].Side);
        Assert.Equal("System", dto.BuyOrders[0].Origin);
        Assert.Equal(995m, dto.BuyOrders[0].Price.Amount);

        Assert.Equal("HND", dto.SellOrders[0].Symbol);
        Assert.Equal("Sell", dto.SellOrders[0].Side);
        Assert.Equal("Investor", dto.SellOrders[0].Origin);
        Assert.Equal(1_400m, dto.SellOrders[0].Price.Amount);

        Assert.Equal("AOKI", dto.SellOrders[2].Symbol);
        Assert.Equal("System", dto.SellOrders[2].Origin);
        Assert.Equal(1_020m, dto.SellOrders[2].Price.Amount);

        Assert.Equal("HND", dto.Trades[0].Symbol);
        Assert.Equal(500m, dto.Trades[0].Fee.Amount);
        Assert.Equal("JPY", dto.Trades[0].Fee.Currency);
    }

    [Fact]
    public void GetSnapshot_EmptyStore_ReturnsOkWithEmptyLists()
    {
        var store = CreateStore();
        var controller = new MarketController(store);

        var result = controller.GetSnapshot();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MarketSnapshotDto>(ok.Value);
        Assert.Empty(dto.BuyOrders);
        Assert.Empty(dto.SellOrders);
        Assert.Empty(dto.Trades);
    }

    private static InMemoryStore CreateStore()
    {
        var company = new Company(CompanyId, "Test Company");
        var snapshotTicker = new Ticker(SnapshotTickerId, CompanyId, "AOKI", 1, Money.Jpy(1_000m));
        var tradeTicker = new Ticker(TradeTickerId, CompanyId, "HND", 1, Money.Jpy(1_500m));
        var investor = new Investor(InvestorId, "Test Investor");
        var portfolio = new Portfolio(new PortfolioId(Guid.NewGuid()), InvestorId, Money.Jpy(1_000_000m));

        return new InMemoryStore(
            companies: new List<Company> { company },
            tickers: new List<Ticker> { snapshotTicker, tradeTicker },
            investors: new List<Investor> { investor },
            portfolios: new List<Portfolio> { portfolio },
            turnByInvestor: new Dictionary<InvestorId, int> { [InvestorId] = 0 },
            random: new Random(42));
    }
}
