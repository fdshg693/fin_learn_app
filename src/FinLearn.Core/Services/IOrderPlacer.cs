namespace FinLearn.Core;

/// <summary>
/// 注文を生成して OrderBook に追加する（注文生成戦略のインターフェース）。
/// 戻り値の <see cref="OrderPlacementResult"/> には発生した全 OrderFill と更新済 trader Portfolio が含まれる。
/// settlement は本インターフェース実装内で完結する責務（player の resting 注文約定もここで反映される）。
/// </summary>
public interface IOrderPlacer
{
    OrderPlacementResult PlaceOrders(
        OrderBook book,
        IExchange exchange,
        IReadOnlyList<Instrument> instruments,
        int startOrderId,
        int currentTurn,
        IReadOnlyDictionary<string, Portfolio> traderPortfolios);
}

/// <summary>
/// 注文発注処理の結果。
/// </summary>
/// <param name="UpdatedBook">注文発注 + 約定後の板</param>
/// <param name="NextOrderId">次に使う注文 ID</param>
/// <param name="PlacedOrders">このターン発注された注文の一覧</param>
/// <param name="Fills">このターン発生した全約定明細（自身の発注分 + resting 約定分の双方）</param>
/// <param name="UpdatedTraderPortfolios">settlement 適用後の traderId → Portfolio</param>
public sealed record OrderPlacementResult(
    OrderBook UpdatedBook,
    int NextOrderId,
    IReadOnlyList<Order> PlacedOrders,
    IReadOnlyList<OrderFill> Fills,
    IReadOnlyDictionary<string, Portfolio> UpdatedTraderPortfolios);
