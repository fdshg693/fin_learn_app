using System;
using System.Threading.Tasks;
using FinLearnApp.Api.Mappers;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Application.Actions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Api.Controllers;

[ApiController]
[Route("api/actions")]
/// <summary>
/// 投資家アクション（BuyNow / SellNow / Wait）を受け付けるコントローラ。
/// </summary>
public sealed class ActionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PortfolioMapper _portfolioMapper;

    public ActionsController(IMediator mediator, PortfolioMapper portfolioMapper)
    {
        _mediator = mediator;
        _portfolioMapper = portfolioMapper;
    }

    /// <summary>
    /// 指定銘柄を現在価格で即時購入する。
    /// 現金不足時は失敗結果を返し、状態は変更しない。
    /// </summary>
    /// <param name="request">投資家ID・銘柄ID・数量。</param>
    /// <returns>実行結果と最新ポートフォリオ。</returns>
    [HttpPost("buy-now")]
    public async Task<ActionResult<ActionResultDto>> BuyNow(ActionTradeRequestDto request)
    {
        var command = new BuyNowCommand(request.InvestorId, request.TickerId, request.Quantity);
        var response = await _mediator.Send(command);
        return ToHttpResult(response);
    }

    /// <summary>
    /// 指定銘柄を現在価格で即時売却する。
    /// 保有なし/数量不足時は失敗結果を返し、状態は変更しない。
    /// </summary>
    /// <param name="request">投資家ID・銘柄ID・数量。</param>
    /// <returns>実行結果と最新ポートフォリオ。</returns>
    [HttpPost("sell-now")]
    public async Task<ActionResult<ActionResultDto>> SellNow(ActionTradeRequestDto request)
    {
        var command = new SellNowCommand(request.InvestorId, request.TickerId, request.Quantity);
        var response = await _mediator.Send(command);
        return ToHttpResult(response);
    }

    /// <summary>
    /// 売買を行わず、最新ポートフォリオを返す（見送り）。
    /// </summary>
    /// <param name="request">投資家ID。</param>
    /// <returns>実行結果と最新ポートフォリオ。</returns>
    [HttpPost("wait")]
    public async Task<ActionResult<ActionResultDto>> Wait(ActionWaitRequestDto request)
    {
        var command = new WaitCommand(request.InvestorId);
        var response = await _mediator.Send(command);
        return ToHttpResult(response);
    }

    private ActionResult<ActionResultDto> ToHttpResult(ActionExecutionResult response)
    {
        if (response.Status == ActionExecutionStatus.BadRequest)
        {
            return BadRequest(new { message = response.Message });
        }

        if (response.Status == ActionExecutionStatus.NotFound)
        {
            return NotFound();
        }

        if (response.Portfolio is null || response.Message is null)
        {
            return NotFound();
        }

        var result = new ActionResultDto(
            response.Success,
            response.Message,
            _portfolioMapper.ToDto(response.Portfolio));

        return Ok(result);
    }
}
