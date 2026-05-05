namespace FinLearn.Core;

/// <summary>
/// ゲームルール・バランス調整値の集約定義。
/// 値変更時はゲームバランスへの影響に注意。
/// </summary>
public static class GameRules
{
    public static class Player
    {
        public const int InitialCash = 10000;
    }

    public static class ComputerTraders
    {
        public const int Count = 10;

        // Random.Next(min, maxExclusive) — 上限は exclusive。命名で明示。
        public const int BuyPriceMinPercent = 85;
        public const int BuyPriceMaxPercentExclusive = 106;   // ～105% inclusive
        public const int SellPriceMinPercent = 95;
        public const int SellPriceMaxPercentExclusive = 116;  // ～115% inclusive
    }

    public static class PriceFluctuation
    {
        public const int MinPercent = 95;
        public const int MaxPercentExclusive = 106;  // ±5%
        public const int MinPrice = 1;
    }
}
