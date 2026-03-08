using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Api.Controllers;

[ApiController]
[Route("api/market")]
public sealed class MarketController : ControllerBase
{
    private readonly InMemoryStore _store;

    public MarketController(InMemoryStore store)
    {
        _store = store;
    }

    [HttpGet("snapshot")]
    public ActionResult<MarketSnapshotDto> GetSnapshot()
    {
        var buyOrders = _store.Exchange.OrderBook.BuyOrders
            .OrderByDescending(order => order.CreatedAt)
            .Select(ToOrderDto)
            .ToList();

        var sellOrders = _store.Exchange.OrderBook.SellOrders
            .OrderByDescending(order => order.CreatedAt)
            .Select(ToOrderDto)
            .ToList();

        var trades = _store.Trades
            .OrderByDescending(trade => trade.ExecutedAt)
            .Select(ToTradeDto)
            .ToList();

        return Ok(new MarketSnapshotDto(buyOrders, sellOrders, trades));
    }

    private MarketOrderDto ToOrderDto(Order order)
    {
        return new MarketOrderDto(
            order.Id.Value,
            order.TickerId.Value,
            _store.FindTicker(order.TickerId)?.Symbol ?? string.Empty,
            order.Side.ToString(),
            order.Origin.ToString(),
            ToMoneyDto(order.Price),
            order.Quantity,
            order.CreatedAt);
    }

    private MarketTradeDto ToTradeDto(Trade trade)
    {
        return new MarketTradeDto(
            trade.Id.Value,
            trade.TickerId.Value,
            _store.FindTicker(trade.TickerId)?.Symbol ?? string.Empty,
            trade.Quantity,
            ToMoneyDto(trade.Price),
            ToMoneyDto(trade.Fee),
            trade.ExecutedAt);
    }

    private static MoneyDto ToMoneyDto(Money money)
    {
        return new MoneyDto(money.Amount, money.Currency.ToString());
    }
}
