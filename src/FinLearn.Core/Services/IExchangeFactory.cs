namespace FinLearn.Core;

/// <summary>
/// IExchange生成ファクトリ（DI用）
/// </summary>
public interface IExchangeFactory
{
    IExchange Create(IReadOnlyDictionary<int, int> prices, int fee);
}
