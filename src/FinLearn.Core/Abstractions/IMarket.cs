namespace FinLearn.Core;

/// <summary>
/// 市場（注文のマッチングと約定を仲介する）
/// </summary>
public interface IMarket
{
    MatchResult Execute(OrderBook book, Order order, IExchange exchange);
}
