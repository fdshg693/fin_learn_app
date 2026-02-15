namespace MyApp.Tests;

using MyApp.Core;

/// <summary>
/// テスト共通のヘルパーメソッドとフィクスチャ
/// </summary>
internal static class TestData
{
    public static Instrument Instrument1 => new(id: 1);
    public static Instrument Instrument2 => new(id: 2);

    public static TestExchange CreateExchange(params (int id, int price)[] prices)
    {
        var dict = new Dictionary<int, int>();
        foreach (var (id, price) in prices)
        {
            dict[id] = price;
        }
        return new TestExchange(dict);
    }
}
