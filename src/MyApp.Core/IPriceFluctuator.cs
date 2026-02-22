namespace MyApp.Core;

/// <summary>
/// 株価変動戦略（毎ターンの価格変動ロジック）
/// </summary>
public interface IPriceFluctuator
{
    IReadOnlyDictionary<int, int> Fluctuate(IReadOnlyDictionary<int, int> currentPrices);
}
