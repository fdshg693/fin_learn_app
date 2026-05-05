namespace FinLearn.Api.Services;

/// <summary>
/// 注文ログ（Serilog）設定。
/// </summary>
public sealed class OrderLogConfig
{
    public int RetainedFileCountLimit { get; init; } = 7;
}
