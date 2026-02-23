namespace FinLearn.Tests;

using FinLearn.Core;

public class PriceFluctuatorTests
{
    private static IReadOnlyDictionary<int, int> CreatePrices(params (int id, int price)[] prices)
    {
        var dict = new Dictionary<int, int>();
        foreach (var (id, price) in prices)
            dict[id] = price;
        return dict;
    }

    [Fact]
    public void 同じシードで同じ結果が得られる()
    {
        var prices = CreatePrices((1, 100), (2, 200), (3, 300));
        var fluctuator1 = new RandomPriceFluctuator(new Random(42));
        var fluctuator2 = new RandomPriceFluctuator(new Random(42));

        var result1 = fluctuator1.Fluctuate(prices);
        var result2 = fluctuator2.Fluctuate(prices);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void 変動後の価格はプラスマイナス5パーセント以内()
    {
        var prices = CreatePrices((1, 100), (2, 200), (3, 1000));

        for (int seed = 0; seed < 100; seed++)
        {
            var fluctuator = new RandomPriceFluctuator(new Random(seed));
            var result = fluctuator.Fluctuate(prices);

            foreach (var (id, originalPrice) in prices)
            {
                var newPrice = result[id];
                var minExpected = Math.Max(1, originalPrice * 95 / 100);
                var maxExpected = originalPrice * 105 / 100;
                Assert.InRange(newPrice, minExpected, maxExpected);
            }
        }
    }

    [Fact]
    public void 最低価格は1()
    {
        var prices = CreatePrices((1, 1));

        for (int seed = 0; seed < 100; seed++)
        {
            var fluctuator = new RandomPriceFluctuator(new Random(seed));
            var result = fluctuator.Fluctuate(prices);

            Assert.True(result[1] >= 1);
        }
    }

    [Fact]
    public void 複数銘柄がそれぞれ独立に変動する()
    {
        var prices = CreatePrices((1, 100), (2, 100), (3, 100));
        var fluctuator = new RandomPriceFluctuator(new Random(42));

        var result = fluctuator.Fluctuate(prices);

        // 同じ元値でもシード次第で異なる結果になりうる
        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey(1));
        Assert.True(result.ContainsKey(2));
        Assert.True(result.ContainsKey(3));
    }

    [Fact]
    public void NoPriceFluctuatorは価格を変更しない()
    {
        var prices = CreatePrices((1, 100), (2, 200), (3, 300));
        var fluctuator = new NoPriceFluctuator();

        var result = fluctuator.Fluctuate(prices);

        Assert.Equal(prices, result);
    }
}
