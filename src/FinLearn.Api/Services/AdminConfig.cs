namespace FinLearn.Api.Services;

/// <summary>
/// 管理エンドポイント設定（ページネーション）。
/// </summary>
public sealed class AdminConfig
{
    public int DefaultPageSize { get; init; } = 50;
    public int MaxPageSize { get; init; } = 200;
}
