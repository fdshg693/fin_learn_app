using System;
using System.Collections.Generic;

namespace FinLearnApp.Api.Models.Api;

public sealed record MarketOrderDto
{
    public Guid OrderId { get; }
    public Guid TickerId { get; }
    public string Symbol { get; }
    public string Side { get; }
    public string Origin { get; }
    public MoneyDto Price { get; }
    public int Quantity { get; }
    public DateTimeOffset CreatedAt { get; }

    public MarketOrderDto(
        Guid orderId,
        Guid tickerId,
        string symbol,
        string side,
        string origin,
        MoneyDto price,
        int quantity,
        DateTimeOffset createdAt)
    {
        OrderId = orderId;
        TickerId = tickerId;
        Symbol = symbol;
        Side = side;
        Origin = origin;
        Price = price;
        Quantity = quantity;
        CreatedAt = createdAt;
    }
}

public sealed record MarketTradeDto
{
    public Guid TradeId { get; }
    public Guid TickerId { get; }
    public string Symbol { get; }
    public int Quantity { get; }
    public MoneyDto Price { get; }
    public MoneyDto Fee { get; }
    public DateTimeOffset ExecutedAt { get; }

    public MarketTradeDto(
        Guid tradeId,
        Guid tickerId,
        string symbol,
        int quantity,
        MoneyDto price,
        MoneyDto fee,
        DateTimeOffset executedAt)
    {
        TradeId = tradeId;
        TickerId = tickerId;
        Symbol = symbol;
        Quantity = quantity;
        Price = price;
        Fee = fee;
        ExecutedAt = executedAt;
    }
}

public sealed record MarketSnapshotDto
{
    public IReadOnlyList<MarketOrderDto> BuyOrders { get; }
    public IReadOnlyList<MarketOrderDto> SellOrders { get; }
    public IReadOnlyList<MarketTradeDto> Trades { get; }

    public MarketSnapshotDto(
        IReadOnlyList<MarketOrderDto> buyOrders,
        IReadOnlyList<MarketOrderDto> sellOrders,
        IReadOnlyList<MarketTradeDto> trades)
    {
        BuyOrders = buyOrders;
        SellOrders = sellOrders;
        Trades = trades;
    }
}
