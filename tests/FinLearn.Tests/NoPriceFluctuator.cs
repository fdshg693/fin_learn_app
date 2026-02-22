namespace FinLearn.Tests;

using FinLearn.Core;

/// <summary>
/// 価格変動なし（既存テスト互換用）
/// </summary>
public sealed class NoPriceFluctuator : IPriceFluctuator
{
    public IReadOnlyDictionary<int, int> Fluctuate(IReadOnlyDictionary<int, int> currentPrices)
        => currentPrices;
}
