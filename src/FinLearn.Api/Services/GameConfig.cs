namespace FinLearn.Api.Services;

/// <summary>
/// ゲーム初期設定（銘柄数・初期株価・手数料）
/// </summary>
public sealed class GameConfig
{
    public int InstrumentCount { get; init; } = 3;
    public int InitialPrice { get; init; } = 100;
    public int Fee { get; init; } = 10;
}
