using System;
using System.Collections.Generic;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Api.Data;

public static class SeedData
{
    public static InMemoryStore Create()
    {
        var companyA = new Company(
            new CompanyId(Guid.Parse("1b2a9f6e-9a6f-4a9f-9b91-8e2f4b4a0c01")),
            "Aoki Holdings");
        var companyB = new Company(
            new CompanyId(Guid.Parse("2c8b1c3e-1b3e-4f7a-9c2b-2b3e1d5f2a02")),
            "Hinode Systems");
        var companyC = new Company(
            new CompanyId(Guid.Parse("3d7a2e4f-2c4f-4a9b-8d3c-3c4f2e6a3b03")),
            "Sakura Foods");

        var tickerA = new Ticker(
            new TickerId(Guid.Parse("4e6b3f5a-3d5a-4b9c-8e4d-4d5a3f7b4c04")),
            companyA.Id,
            "AOKI",
            1,
            Money.Jpy(1200m));
        var tickerB = new Ticker(
            new TickerId(Guid.Parse("5f5c4a6b-4e6b-4c9d-9f5e-5e6b4a8c5d05")),
            companyB.Id,
            "HND",
            1,
            Money.Jpy(860m));
        var tickerC = new Ticker(
            new TickerId(Guid.Parse("6a4d5b7c-5f7c-4d9e-9a6f-6f7c5b9d6e06")),
            companyC.Id,
            "SKR",
            1,
            Money.Jpy(540m));

        var investor = new Investor(
            new InvestorId(Guid.Parse("7b3e6c8d-6a8d-4e9f-9b7c-7c8d6c0e7f07")),
            "Demo Investor");

        var portfolio = new Portfolio(
            new PortfolioId(Guid.Parse("8c2f7d9e-7b9e-4f9a-9c8d-8d9e7d1f8008")),
            investor.Id,
            Money.Jpy(1_000_000m));

        portfolio.AddOrUpdateHolding(tickerA.Id, 120);
        portfolio.AddOrUpdateHolding(tickerB.Id, 80);
        portfolio.Withdraw(Money.Jpy(300_000m));

        var companies = new List<Company> { companyA, companyB, companyC };
        var tickers = new List<Ticker> { tickerA, tickerB, tickerC };
        var investors = new List<Investor> { investor };
        var portfolios = new List<Portfolio> { portfolio };

        return new InMemoryStore(companies, tickers, investors, portfolios);
    }
}
