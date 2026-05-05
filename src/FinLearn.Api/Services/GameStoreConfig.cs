namespace FinLearn.Api.Services;

/// <summary>
/// GameStore 設定（取引履歴キャッシュサイズ等）。
/// </summary>
public sealed class GameStoreConfig
{
    public int MaxRecentTrades { get; init; } = 3;
}
