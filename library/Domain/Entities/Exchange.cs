using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

/// <summary>
/// 株式市場の取引所。注文板（OrderBook）の管理と売買マッチングを担う。
/// </summary>
public sealed class Exchange
{
    private readonly List<Trade> _trades = new();

    /// <summary>約定ごとに徴収する固定手数料。</summary>
    public Money Fee { get; }

    /// <summary>売買注文を管理する注文板。</summary>
    public OrderBook OrderBook { get; }

    /// <summary>これまでの約定履歴。</summary>
    public IReadOnlyList<Trade> Trades => _trades.AsReadOnly();

    /// <summary>取引所を初期化する。</summary>
    /// <param name="fee">1 約定あたりの手数料。</param>
    public Exchange(Money fee)
    {
        Fee = fee;
        OrderBook = new OrderBook();
    }

    /// <summary>
    /// 成行買い注文を執行する。
    /// 市場価格以下の売り注文を価格優先・時間優先で照合し、約定可能な分を即時約定させる。
    /// 現金残高を超える約定は行わない。
    /// 現金残高チェックは最初にオーバーする注文で打ち切る（より安い後続注文があっても続行しない）。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    /// <param name="quantity">購入希望株数。</param>
    /// <param name="availableCash">使用可能な現金残高。</param>
    /// <param name="marketPrice">現在の市場価格（この価格以下の売り注文のみ照合対象）。</param>
    /// <returns>マッチング結果（約定株数・約定総額）。</returns>
    public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash, Money marketPrice)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalCost = Money.Jpy(0m);

        var candidates = FindSellCandidates(tickerId, price => price <= marketPrice.Amount);

        foreach (var order in candidates)
        {
            if (remaining <= 0) break;
            var fillQuantity = Math.Min(remaining, order.Quantity);
            var tradeCost = order.Price.Multiply(fillQuantity);
            if (totalCost.Add(tradeCost).Amount > availableCash.Amount) break;

            totalCost = totalCost.Add(tradeCost);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            // この場で受け付けた執行要求は OrderBook に登録されないため、約定記録用に仮 ID を振る
            RegisterTrade(tickerId, new OrderId(Guid.NewGuid()), order.Id, order.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalCost);
    }

    /// <summary>
    /// 成行売り注文を執行する。
    /// 市場価格以上の買い注文を価格優先・時間優先で照合し、約定可能な分を即時約定させる。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    /// <param name="quantity">売却希望株数。</param>
    /// <param name="marketPrice">現在の市場価格（この価格以上の買い注文のみ照合対象）。</param>
    /// <returns>マッチング結果（約定株数・約定総額）。</returns>
    public OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity, Money marketPrice)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalProceeds = Money.Jpy(0m);

        var candidates = FindBuyCandidates(tickerId, price => price >= marketPrice.Amount);

        foreach (var order in candidates)
        {
            if (remaining <= 0) break;
            var fillQuantity = Math.Min(remaining, order.Quantity);
            var proceeds = order.Price.Multiply(fillQuantity);
            totalProceeds = totalProceeds.Add(proceeds);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            // この場で受け付けた執行要求は OrderBook に登録されないため、約定記録用に仮 ID を振る
            RegisterTrade(tickerId, order.Id, new OrderId(Guid.NewGuid()), order.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalProceeds);
    }

    /// <summary>
    /// 指値買い注文を執行する。
    /// 指値以下の売り注文を価格優先・時間優先で照合し、約定可能な分を即時約定させる。
    /// 現金残高を超える約定は行わない。
    /// 現金残高チェックは最初にオーバーする注文で打ち切る（より安い後続注文があっても続行しない）。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    /// <param name="quantity">購入希望株数。</param>
    /// <param name="limitPrice">指値価格（この価格以下の売り注文のみ照合対象）。</param>
    /// <param name="availableCash">使用可能な現金残高。</param>
    /// <returns>マッチング結果（約定株数・約定総額）。</returns>
    public OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalCost = Money.Jpy(0m);

        var candidates = FindSellCandidates(tickerId, price => price <= limitPrice.Amount);

        foreach (var order in candidates)
        {
            if (remaining <= 0) break;
            var fillQuantity = Math.Min(remaining, order.Quantity);
            var tradeCost = order.Price.Multiply(fillQuantity);
            if (totalCost.Add(tradeCost).Amount > availableCash.Amount) break;

            totalCost = totalCost.Add(tradeCost);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            // 成行注文は OrderBook に登録されないため、約定記録用に仮 ID を振る
            RegisterTrade(tickerId, new OrderId(Guid.NewGuid()), order.Id, order.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalCost);
    }

    /// <summary>
    /// 指値売り注文を執行する。
    /// 指値以上の買い注文を価格優先・時間優先で照合し、約定可能な分を即時約定させる。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    /// <param name="quantity">売却希望株数。</param>
    /// <param name="limitPrice">指値価格（この価格以上の買い注文のみ照合対象）。</param>
    /// <returns>マッチング結果（約定株数・約定総額）。</returns>
    public OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalProceeds = Money.Jpy(0m);

        var candidates = FindBuyCandidates(tickerId, price => price >= limitPrice.Amount);

        foreach (var order in candidates)
        {
            if (remaining <= 0) break;
            var fillQuantity = Math.Min(remaining, order.Quantity);
            var proceeds = order.Price.Multiply(fillQuantity);
            totalProceeds = totalProceeds.Add(proceeds);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            // 成行注文は OrderBook に登録されないため、約定記録用に仮 ID を振る
            RegisterTrade(tickerId, order.Id, new OrderId(Guid.NewGuid()), order.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalProceeds);
    }

    /// <summary>
    /// 指定銘柄のクロス注文（買い値 ≥ 売り値）を自動解消する。
    /// 価格優先・時間優先で照合し、クロスがなくなるまで繰り返す。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    public void MatchCrossedOrders(TickerId tickerId)
    {
        while (true)
        {
            var bestBuy = OrderBook
                .FindByTickerAndSide(tickerId, OrderSide.Buy)
                .OrderByDescending(o => o.Price.Amount)
                .ThenBy(o => o.CreatedAt)
                .FirstOrDefault();

            var bestSell = OrderBook
                .FindByTickerAndSide(tickerId, OrderSide.Sell)
                .OrderBy(o => o.Price.Amount)
                .ThenBy(o => o.CreatedAt)
                .FirstOrDefault();

            if (bestBuy is null || bestSell is null || bestBuy.Price.Amount < bestSell.Price.Amount)
                break;

            var fillQuantity = Math.Min(bestBuy.Quantity, bestSell.Quantity);
            RegisterTrade(tickerId, bestBuy.Id, bestSell.Id, bestSell.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(bestBuy, bestBuy.Quantity - fillQuantity);
            OrderBook.ReplaceWithRemaining(bestSell, bestSell.Quantity - fillQuantity);
        }
    }

    /// <summary>
    /// 指定銘柄の売り注文から照合候補を抽出する。
    /// pricePredicate を満たす注文のみ対象とし、価格昇順・時刻昇順（価格優先・時間優先）でソートして返す。
    /// </summary>
    private List<Order> FindSellCandidates(TickerId tickerId, Func<decimal, bool> pricePredicate)
        => OrderBook.FindByTickerAndSide(tickerId, OrderSide.Sell)
            .Where(o => pricePredicate(o.Price.Amount))
            .OrderBy(o => o.Price.Amount)
            .ThenBy(o => o.CreatedAt)
            .ToList();

    /// <summary>
    /// 指定銘柄の買い注文から照合候補を抽出する。
    /// pricePredicate を満たす注文のみ対象とし、価格降順・時刻昇順（価格優先・時間優先）でソートして返す。
    /// </summary>
    private List<Order> FindBuyCandidates(TickerId tickerId, Func<decimal, bool> pricePredicate)
        => OrderBook.FindByTickerAndSide(tickerId, OrderSide.Buy)
            .Where(o => pricePredicate(o.Price.Amount))
            .OrderByDescending(o => o.Price.Amount)
            .ThenBy(o => o.CreatedAt)
            .ToList();

    /// <summary>
    /// 約定を1件記録する。Trade オブジェクトを生成して内部リストに追加する。
    /// </summary>
    private void RegisterTrade(TickerId tickerId, OrderId buyOrderId, OrderId sellOrderId, Money price, int quantity)
        => _trades.Add(new Trade(
            new TradeId(Guid.NewGuid()),
            tickerId,
            buyOrderId,
            sellOrderId,
            price,
            quantity,
            Fee,
            DateTimeOffset.UtcNow));
}
