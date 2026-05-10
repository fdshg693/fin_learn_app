namespace FinLearn.Core;

/// <summary>
/// プレイヤー注文の受付と反映を担う戦略インターフェース。
/// Match は pipeline 共通で実行されるため、handler の責務外。
/// </summary>
internal interface IPlayerOrderHandler
{
    /// <summary>
    /// 注文の受付段階。
    /// 限値: ReserveBuy/Sell で available → reserved に移す。失敗時は warning を返す (World 不変)。
    /// 成行: no-op (World と null を返す)。
    /// </summary>
    (World World, string? Warning) Receive(World world, Order order);

    /// <summary>
    /// 約定結果を世界に反映する段階。
    /// 限値: SettlementProcessor.SettleFills 経由で予約消費 + 差額返金。失敗パスなし。
    /// 成行: ApplyTrade で同期適用。fill=0 や残高不足は warning を返す (World 不変)。
    /// </summary>
    (World World, string? Warning) Settle(World world, Order order, MatchResult match);
}
