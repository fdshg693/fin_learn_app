using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Services;

/// <summary>
/// ターン進行に関わるドメインロジックを集約するドメインサービス。
/// 価格変動・システム注文生成・クロス注文解消の 3 ステップを提供する。
/// Random はメソッド引数で受け取ることで、Domain 層が生成方法に依存しない設計にする。
/// </summary>
public static class TurnDomainService
{
    /// <summary>1 ターンに注文を生成する最大銘柄数。</summary>
    private const int MaxTargetTickersPerTurn = 3;

    /// <summary>1 銘柄あたりのシステム注文株数。</summary>
    private const int SystemOrderQuantity = 10;

    /// <summary>システム買い注文の価格（現在価格 × この倍率）。</summary>
    private const decimal SystemBuyPriceRate = 0.95m;

    /// <summary>システム売り注文の価格（現在価格 × この倍率）。</summary>
    private const decimal SystemSellPriceRate = 1.00m;

    /// <summary>価格変動率の下限（現在価格の 97%）。</summary>
    private const decimal MinPriceFluctuationRate = 0.97m;

    /// <summary>価格変動率の上限（現在価格の 103%）。</summary>
    private const decimal MaxPriceFluctuationRate = 1.03m;

    /// <summary>
    /// 1 ターン分の市場進行を実行する。
    /// 価格変動、システム注文生成、クロス注文解消をこの順で適用する。
    /// </summary>
    /// <param name="exchange">対象の取引所。</param>
    /// <param name="tickers">市場に存在する銘柄一覧。</param>
    /// <param name="random">乱数生成器。</param>
    /// <param name="turn">進行後のターン番号。</param>
    public static void AdvanceTurn(Exchange exchange, IReadOnlyList<Ticker> tickers, Random random, int turn)
    {
        ApplyPriceFluctuation(tickers, random, turn);
        GenerateSystemOrders(exchange, tickers, random);
        MatchCrossedOrdersForAllTickers(exchange, tickers);
    }

    /// <summary>
    /// 全銘柄の価格をランダムに変動させる。
    /// 変動率は MinPriceFluctuationRate 〜 MaxPriceFluctuationRate の一様分布。
    /// 変動後の価格が 1 円未満になる場合は 1 円にクランプする。
    /// </summary>
    /// <param name="tickers">変動対象の銘柄リスト。</param>
    /// <param name="random">乱数生成器（呼び出し元が管理する）。</param>
    /// <param name="turn">現在のターン番号（価格履歴に記録される）。</param>
    public static void ApplyPriceFluctuation(IReadOnlyList<Ticker> tickers, Random random, int turn)
    {
        foreach (var ticker in tickers)
        {
            var rate = NextDecimal(random, MinPriceFluctuationRate, MaxPriceFluctuationRate);
            var newAmount = decimal.Round(ticker.CurrentPrice.Amount * rate, 2, MidpointRounding.AwayFromZero);
            if (newAmount < 1m) newAmount = 1m;
            ticker.UpdatePrice(Money.Jpy(newAmount), turn);
        }
    }

    /// <summary>
    /// ランダムに選んだ最大 MaxTargetTickersPerTurn 銘柄に対して、
    /// システムが自動発注する買い注文・売り注文を Exchange の注文板に追加する。
    /// 買い注文は現在価格の 95%、売り注文は現在価格で発注する。
    /// </summary>
    /// <param name="exchange">注文を追加する取引所。</param>
    /// <param name="tickers">発注対象の銘柄候補リスト。</param>
    /// <param name="random">銘柄ランダム選択に使用する乱数生成器。</param>
    public static void GenerateSystemOrders(Exchange exchange, IReadOnlyList<Ticker> tickers, Random random)
    {
        if (tickers.Count == 0) return;

        var targetTickers = tickers
            .OrderBy(_ => random.Next())
            .Take(MaxTargetTickersPerTurn)
            .ToList();

        foreach (var ticker in targetTickers)
        {
            var createdAt = DateTimeOffset.UtcNow;
            var buyPrice  = Money.Jpy(decimal.Round(
                ticker.CurrentPrice.Amount * SystemBuyPriceRate, 2, MidpointRounding.AwayFromZero));
            var sellPrice = Money.Jpy(decimal.Round(
                ticker.CurrentPrice.Amount * SystemSellPriceRate, 2, MidpointRounding.AwayFromZero));

            exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
                buyPrice, SystemOrderQuantity, OrderOrigin.System, createdAt));
            exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
                sellPrice, SystemOrderQuantity, OrderOrigin.System, createdAt));
        }
    }

    /// <summary>
    /// 全銘柄のクロス注文（買い値 ≥ 売り値の組み合わせ）を自動解消する。
    /// 各銘柄について Exchange.MatchCrossedOrders を呼び出す。
    /// </summary>
    /// <param name="exchange">対象の取引所。</param>
    /// <param name="tickers">解消対象の全銘柄リスト。</param>
    public static void MatchCrossedOrdersForAllTickers(Exchange exchange, IReadOnlyList<Ticker> tickers)
    {
        foreach (var ticker in tickers)
        {
            exchange.MatchCrossedOrders(ticker.Id);
        }
    }

    /// <summary>
    /// minInclusive 〜 maxInclusive の範囲で一様分布の decimal 乱数を生成する。
    /// </summary>
    private static decimal NextDecimal(Random random, decimal minInclusive, decimal maxInclusive)
    {
        var sample = (decimal)random.NextDouble();
        return minInclusive + ((maxInclusive - minInclusive) * sample);
    }
}
