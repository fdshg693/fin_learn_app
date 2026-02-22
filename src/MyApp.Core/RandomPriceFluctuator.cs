namespace MyApp.Core;

/// <summary>
/// ランダム株価変動（-5% ~ +5%、最低価格1）
/// </summary>
public sealed class RandomPriceFluctuator : IPriceFluctuator
{
    private readonly Random _random;

    public RandomPriceFluctuator(Random random)
    {
        _random = random;
    }

    public IReadOnlyDictionary<int, int> Fluctuate(IReadOnlyDictionary<int, int> currentPrices)
    {
        var result = new Dictionary<int, int>();
        foreach (var (instrumentId, price) in currentPrices)
        {
            // -5% ~ +5%: factor は 95..105
            var factor = _random.Next(95, 106);
            var newPrice = Math.Max(1, price * factor / 100);
            result[instrumentId] = newPrice;
        }
        return result;
    }
}
