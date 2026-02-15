namespace MyApp.Tests;

using MyApp.Core;

/// <summary>
/// テスト共通のヘルパーメソッドとフィクスチャ
/// </summary>
internal static class TestData
{
    public static Instrument Instrument1 => new(Id: 1);
    public static Instrument Instrument2 => new(Id: 2);

    public static TestExchange CreateExchange(params (int id, int price)[] prices)
    {
        return CreateExchange(fee: 0, prices);
    }

    public static TestExchange CreateExchange(int fee, params (int id, int price)[] prices)
    {
        var dict = new Dictionary<int, int>();
        foreach (var (id, price) in prices)
        {
            dict[id] = price;
        }
        return new TestExchange(dict, fee);
    }
}
