namespace FinLearn.Core;

/// <summary>
/// 当ターンに発生した全 <see cref="OrderFill"/> を traderId 別 <see cref="Portfolio"/> に
/// 統一適用する domain service。注文生成（intent）と settlement の責務を分離するため、
/// <see cref="ComputerTrader"/> や <see cref="TurnProcessor"/> から呼ばれる。
///
/// 指値注文の約定は予約モデル（<see cref="Portfolio.SettleReservedBuy"/> /
/// <see cref="Portfolio.SettleReservedSell"/>）で確定し、成行注文（板に乗らない fill）は
/// 既存の <see cref="Portfolio.ApplyTrade"/> で同期適用する。
/// </summary>
public static class SettlementProcessor
{
    /// <summary>
    /// fills を一度ループで処理し、traderId 別の Portfolio を更新したマップを返す。
    /// fill の重複処理は無し（OrderBook.Match の対称な fills は OrderId 単位で一意）。
    /// </summary>
    /// <param name="fills">当ターン発生した全約定明細</param>
    /// <param name="ordersById">fill.OrderId → Order 逆引き（match 直前の Quantity を保持）</param>
    /// <param name="postFillRemainingQty">注文ID → 当 fill 群適用後の残数量。
    /// 0 になった注文の fill では feeIfFinal=fee を計上する</param>
    /// <param name="portfolios">traderId → Portfolio。computer + player の統合マップ</param>
    /// <param name="fee">手数料（per-order）</param>
    public static IReadOnlyDictionary<string, Portfolio> SettleFills(
        IReadOnlyList<OrderFill> fills,
        IReadOnlyDictionary<int, Order> ordersById,
        IReadOnlyDictionary<int, int> postFillRemainingQty,
        IReadOnlyDictionary<string, Portfolio> portfolios,
        int fee)
    {
        if (fills.Count == 0)
            return portfolios;

        var result = new Dictionary<string, Portfolio>(portfolios);

        foreach (var fill in fills)
        {
            if (fill.FilledQuantity <= 0) continue;
            if (!ordersById.TryGetValue(fill.OrderId, out var order)) continue;
            if (!result.TryGetValue(order.TraderId, out var pf)) continue;

            var actualUnitPrice = fill.TotalAmount / fill.FilledQuantity;
            var isFinal = postFillRemainingQty.TryGetValue(order.Id, out var remaining) && remaining == 0;
            var feeIfFinal = isFinal ? fee : 0;

            if (order.Type == OrderType.Limit && order.Price is not null)
            {
                pf = order.Side == OrderSide.Buy
                    ? pf.SettleReservedBuy(order.Instrument.Id, fill.FilledQuantity, actualUnitPrice, order.Price.Value, feeIfFinal)
                    : pf.SettleReservedSell(order.Instrument.Id, fill.FilledQuantity, actualUnitPrice, feeIfFinal);
            }
            else
            {
                // 成行注文: 予約なし fill。事前 affordability チェックで弾く前提のため warning は無視。
                var trade = new TradeResult(order.Instrument.Id, order.Side, fill.FilledQuantity, fill.TotalAmount, feeIfFinal);
                (pf, _) = pf.ApplyTrade(trade);
            }

            result[order.TraderId] = pf;
        }

        return result;
    }

    /// <summary>
    /// 失効した注文の予約を解放する。指値注文のみ対象（成行は板に乗らない）。
    /// </summary>
    public static IReadOnlyDictionary<string, Portfolio> ReleaseExpired(
        IReadOnlyList<Order> expiredOrders,
        IReadOnlyDictionary<string, Portfolio> portfolios,
        int feePerOrder)
    {
        if (expiredOrders.Count == 0)
            return portfolios;

        var result = new Dictionary<string, Portfolio>(portfolios);

        foreach (var order in expiredOrders)
        {
            if (order.Type != OrderType.Limit || order.Price is null) continue;
            if (!result.TryGetValue(order.TraderId, out var pf)) continue;

            // order.Quantity は失効時点の残数量（部分約定後なら減算済）
            pf = order.Side == OrderSide.Buy
                ? pf.ReleaseBuyReservation(order.Quantity, order.Price.Value, feePerOrder)
                : pf.ReleaseSellReservation(order.Instrument.Id, order.Quantity);

            result[order.TraderId] = pf;
        }

        return result;
    }

    /// <summary>
    /// fill 群から「注文ID → 当 fill 群適用後の残数量」を算出する。
    /// originalQty は match 直前の Quantity（部分約定 resting なら残量）を渡す。
    /// </summary>
    public static IReadOnlyDictionary<int, int> ComputePostFillRemainingQty(
        IReadOnlyList<OrderFill> fills,
        IReadOnlyDictionary<int, Order> ordersById)
    {
        var sumByOrderId = new Dictionary<int, int>();
        foreach (var fill in fills)
        {
            sumByOrderId.TryGetValue(fill.OrderId, out var sum);
            sumByOrderId[fill.OrderId] = sum + fill.FilledQuantity;
        }

        var result = new Dictionary<int, int>();
        foreach (var (orderId, filledSum) in sumByOrderId)
        {
            if (ordersById.TryGetValue(orderId, out var order))
            {
                result[orderId] = order.Quantity - filledSum;
            }
        }
        return result;
    }
}
