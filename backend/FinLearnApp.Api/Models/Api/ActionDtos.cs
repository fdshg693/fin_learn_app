using System;

namespace FinLearnApp.Api.Models.Api;

/// <summary>
/// 買いアクション（BuyNow / BuyLimit）用のリクエスト。
/// </summary>
public sealed record ActionBuyRequestDto
{
    public Guid InvestorId { get; }
    public Guid TickerId { get; }
    public int Quantity { get; }
    public decimal? LimitPrice { get; }
    public int ExpectedTurn { get; }

    /// <summary>
    /// 買いアクション（BuyNow / BuyLimit）用のリクエスト。
    /// </summary>
    /// <param name="investorId">対象投資家ID。</param>
    /// <param name="tickerId">対象銘柄ID。</param>
    /// <param name="quantity">売買数量。</param>
    /// <param name="limitPrice">指値価格。null の場合は成行注文。</param>
    /// <param name="expectedTurn">期待ターン番号。</param>
    public ActionBuyRequestDto(Guid investorId, Guid tickerId, int quantity, decimal? limitPrice, int expectedTurn)
    {
        InvestorId = investorId;
        TickerId = tickerId;
        Quantity = quantity;
        LimitPrice = limitPrice;
        ExpectedTurn = expectedTurn;
    }
}

/// <summary>
/// 売りアクション（SellNow / SellLimit）用のリクエスト。
/// </summary>
public sealed record ActionSellRequestDto
{
    public Guid InvestorId { get; }
    public Guid TickerId { get; }
    public int Quantity { get; }
    public decimal? LimitPrice { get; }
    public int ExpectedTurn { get; }

    /// <summary>
    /// 売りアクション（SellNow / SellLimit）用のリクエスト。
    /// </summary>
    /// <param name="investorId">対象投資家ID。</param>
    /// <param name="tickerId">対象銘柄ID。</param>
    /// <param name="quantity">売買数量。</param>
    /// <param name="limitPrice">指値価格。null の場合は成行注文。</param>
    /// <param name="expectedTurn">期待ターン番号。</param>
    public ActionSellRequestDto(Guid investorId, Guid tickerId, int quantity, decimal? limitPrice, int expectedTurn)
    {
        InvestorId = investorId;
        TickerId = tickerId;
        Quantity = quantity;
        LimitPrice = limitPrice;
        ExpectedTurn = expectedTurn;
    }
}

/// <summary>
/// Waitアクション（見送り）用のリクエスト。
/// </summary>
public sealed record ActionWaitRequestDto
{
    public Guid InvestorId { get; }
    public int ExpectedTurn { get; }

    public ActionWaitRequestDto(Guid investorId, int expectedTurn)
    {
        InvestorId = investorId;
        ExpectedTurn = expectedTurn;
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
    public int CurrentTurn { get; }

    public ActionResultDto(bool success, string message, PortfolioDto portfolio, int currentTurn)
    {
        Success = success;
        Message = message;
        Portfolio = portfolio;
        CurrentTurn = currentTurn;
    }
}
