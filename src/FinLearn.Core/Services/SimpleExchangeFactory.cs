namespace FinLearn.Core;

/// <summary>
/// SimpleExchangeを生成するファクトリ
/// </summary>
public sealed class SimpleExchangeFactory : IExchangeFactory
{
    public IExchange Create(IReadOnlyDictionary<int, int> prices, int fee)
        => new SimpleExchange(prices, fee);
}
