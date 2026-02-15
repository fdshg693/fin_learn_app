using System;

namespace FinLearnApp.Api.Models.Api;

/// <summary>
/// 売買系アクション（BuyNow/SellNow）用のリクエスト。
/// </summary>
public sealed record ActionTradeRequestDto
{
    public Guid InvestorId { get; }
    public Guid TickerId { get; }
    public int Quantity { get; }

    /// <summary>
    /// 売買系アクション（BuyNow/SellNow）用のリクエスト。
    /// </summary>
    /// <param name="investorId">対象投資家ID。</param>
    /// <param name="tickerId">対象銘柄ID。</param>
    /// <param name="quantity">売買数量。</param>
    public ActionTradeRequestDto(Guid investorId, Guid tickerId, int quantity)
    {
        InvestorId = investorId;
        TickerId = tickerId;
        Quantity = quantity;
    }
}

/// <summary>
/// Waitアクション（見送り）用のリクエスト。
/// </summary>
public sealed record ActionWaitRequestDto
{
    public Guid InvestorId { get; }

    /// <summary>
    /// Waitアクション（見送り）用のリクエスト。
    /// </summary>
    /// <param name="investorId">対象投資家ID。</param>
    public ActionWaitRequestDto(Guid investorId)
    {
        InvestorId = investorId;
    }
}

/// <summary>
/// アクション実行結果。
/// </summary>
public sealed record ActionResultDto
{
    public bool Success { get; }
    public string Message { get; }
    public PortfolioDto Portfolio { get; }

    /// <summary>
    /// アクション実行結果。
    /// </summary>
    /// <param name="success">実行成否。</param>
    /// <param name="message">実行メッセージ。</param>
    /// <param name="portfolio">実行後ポートフォリオ。</param>
    public ActionResultDto(bool success, string message, PortfolioDto portfolio)
    {
        Success = success;
        Message = message;
        Portfolio = portfolio;
    }
}
