using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Application.Actions;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Api.Data;

public sealed class InMemoryStore
{
    private const int MaxTargetTickersPerTurn = 3;
    private const int SystemOrderQuantity = 10;
    private const decimal SystemBuyPriceRate = 0.95m;
    private const decimal SystemSellPriceRate = 1.00m;
    private const decimal MinPriceFluctuationRate = 0.97m;
    private const decimal MaxPriceFluctuationRate = 1.03m;

    private readonly Dictionary<CompanyId, Company> _companiesById;
    private readonly Dictionary<TickerId, Ticker> _tickersById;
    private readonly Dictionary<InvestorId, int> _turnByInvestor;
    private readonly Random _random;

    public IReadOnlyList<Company> Companies { get; }
    public IReadOnlyList<Ticker> Tickers { get; }
    public IReadOnlyList<Investor> Investors { get; }
    public IReadOnlyList<Portfolio> Portfolios { get; }
    public Exchange Exchange { get; }
    public IReadOnlyList<Trade> Trades => _trades.AsReadOnly();

    private readonly List<Trade> _trades = new();

    public InMemoryStore(
        IReadOnlyList<Company> companies,
        IReadOnlyList<Ticker> tickers,
        IReadOnlyList<Investor> investors,
        IReadOnlyList<Portfolio> portfolios,
        IReadOnlyDictionary<InvestorId, int>? turnByInvestor = null,
        Random? random = null)
    {
        Companies = companies;
        Tickers = tickers;
        Investors = investors;
        Portfolios = portfolios;
        Exchange = new Exchange(Money.Jpy(500m));

        _companiesById = companies.ToDictionary(c => c.Id, c => c);
        _tickersById = tickers.ToDictionary(t => t.Id, t => t);
        _turnByInvestor = turnByInvestor is null
            ? investors.ToDictionary(investor => investor.Id, _ => 0)
            : new Dictionary<InvestorId, int>(turnByInvestor);
        _random = random ?? Random.Shared;
    }

    public Company GetCompany(CompanyId id)
    {
        return _companiesById[id];
    }

    public Ticker? FindTicker(TickerId id)
    {
        return _tickersById.TryGetValue(id, out var ticker) ? ticker : null;
    }

    public Portfolio? FindPortfolioByInvestor(InvestorId investorId)
    {
        return Portfolios.FirstOrDefault(p => p.InvestorId == investorId);
    }

    public int GetCurrentTurn(InvestorId investorId)
    {
        return _turnByInvestor.TryGetValue(investorId, out var turn) ? turn : 0;
    }

    public int AdvanceTurn(InvestorId investorId)
    {
        var currentTurn = GetCurrentTurn(investorId);
        var nextTurn = currentTurn + 1;
        _turnByInvestor[investorId] = nextTurn;

        ApplyPriceFluctuation(nextTurn);
        GenerateSystemOrdersForTurn();

        return nextTurn;
    }

    public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalCost = Money.Jpy(0m);

        var marketPrice = FindTicker(tickerId)!.CurrentPrice.Amount;
        var candidates = FindSellCandidates(tickerId, price => price <= marketPrice);

        foreach (var order in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            var fillQuantity = Math.Min(remaining, order.Quantity);
            var tradeCost = order.Price.Multiply(fillQuantity);
            if (totalCost.Add(tradeCost).Amount > availableCash.Amount)
            {
                break;
            }

            totalCost = totalCost.Add(tradeCost);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            RegisterTrade(tickerId, buyOrderId: new OrderId(Guid.NewGuid()), sellOrderId: order.Id, order.Price, fillQuantity);
            Exchange.OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalCost);
    }

    public OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalProceeds = Money.Jpy(0m);

        var marketPrice = FindTicker(tickerId)!.CurrentPrice.Amount;
        var candidates = FindBuyCandidates(tickerId, price => price >= marketPrice);

        foreach (var order in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            var fillQuantity = Math.Min(remaining, order.Quantity);
            var proceeds = order.Price.Multiply(fillQuantity);
            totalProceeds = totalProceeds.Add(proceeds);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            RegisterTrade(tickerId, buyOrderId: order.Id, sellOrderId: new OrderId(Guid.NewGuid()), order.Price, fillQuantity);
            Exchange.OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalProceeds);
    }

    public OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalCost = Money.Jpy(0m);

        var candidates = FindSellCandidates(tickerId, price => price <= limitPrice.Amount);
        foreach (var order in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            var fillQuantity = Math.Min(remaining, order.Quantity);
            var tradeCost = order.Price.Multiply(fillQuantity);
            if (totalCost.Add(tradeCost).Amount > availableCash.Amount)
            {
                break;
            }

            totalCost = totalCost.Add(tradeCost);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            RegisterTrade(tickerId, buyOrderId: new OrderId(Guid.NewGuid()), sellOrderId: order.Id, order.Price, fillQuantity);
            Exchange.OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalCost);
    }

    public OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalProceeds = Money.Jpy(0m);

        var candidates = FindBuyCandidates(tickerId, price => price >= limitPrice.Amount);
        foreach (var order in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            var fillQuantity = Math.Min(remaining, order.Quantity);
            var proceeds = order.Price.Multiply(fillQuantity);
            totalProceeds = totalProceeds.Add(proceeds);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            RegisterTrade(tickerId, buyOrderId: order.Id, sellOrderId: new OrderId(Guid.NewGuid()), order.Price, fillQuantity);
            Exchange.OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalProceeds);
    }

    private void ApplyPriceFluctuation(int turn)
    {
        foreach (var ticker in Tickers)
        {
            var rate = NextDecimal(MinPriceFluctuationRate, MaxPriceFluctuationRate);
            var newAmount = decimal.Round(ticker.CurrentPrice.Amount * rate, 2, MidpointRounding.AwayFromZero);
            if (newAmount < 1m)
            {
                newAmount = 1m;
            }

            ticker.UpdatePrice(Money.Jpy(newAmount), turn);
        }
    }

    private void GenerateSystemOrdersForTurn()
    {
        if (Tickers.Count == 0)
        {
            return;
        }

        var targetTickers = Tickers
            .OrderBy(_ => _random.Next())
            .Take(MaxTargetTickersPerTurn)
            .ToList();

        foreach (var ticker in targetTickers)
        {
            var createdAt = DateTimeOffset.UtcNow;
            var buyPrice = Money.Jpy(decimal.Round(
                ticker.CurrentPrice.Amount * SystemBuyPriceRate,
                2,
                MidpointRounding.AwayFromZero));
            var sellPrice = Money.Jpy(decimal.Round(
                ticker.CurrentPrice.Amount * SystemSellPriceRate,
                2,
                MidpointRounding.AwayFromZero));

            Exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()),
                ticker.Id,
                OrderSide.Buy,
                buyPrice,
                SystemOrderQuantity,
                OrderOrigin.System,
                createdAt));

            Exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()),
                ticker.Id,
                OrderSide.Sell,
                sellPrice,
                SystemOrderQuantity,
                OrderOrigin.System,
                createdAt));
        }
    }

    private decimal NextDecimal(decimal minInclusive, decimal maxInclusive)
    {
        var sample = (decimal)_random.NextDouble();
        return minInclusive + ((maxInclusive - minInclusive) * sample);
    }

    private List<Order> FindSellCandidates(TickerId tickerId, Func<decimal, bool> pricePredicate)
    {
        return Exchange.OrderBook
            .FindByTickerAndSide(tickerId, OrderSide.Sell)
            .Where(order => pricePredicate(order.Price.Amount))
            .OrderBy(order => order.Price.Amount)
            .ThenBy(order => order.CreatedAt)
            .ToList();
    }

    private List<Order> FindBuyCandidates(TickerId tickerId, Func<decimal, bool> pricePredicate)
    {
        return Exchange.OrderBook
            .FindByTickerAndSide(tickerId, OrderSide.Buy)
            .Where(order => pricePredicate(order.Price.Amount))
            .OrderByDescending(order => order.Price.Amount)
            .ThenBy(order => order.CreatedAt)
            .ToList();
    }

    private void RegisterTrade(TickerId tickerId, OrderId buyOrderId, OrderId sellOrderId, Money price, int quantity)
    {
        _trades.Add(new Trade(
            new TradeId(Guid.NewGuid()),
            tickerId,
            buyOrderId,
            sellOrderId,
            price,
            quantity,
            Exchange.Fee,
            DateTimeOffset.UtcNow));
    }
}
