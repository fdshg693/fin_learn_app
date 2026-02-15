using System;
using System.Threading.Tasks;
using FinLearnApp.Api.Mappers;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Api.Responses;
using FinLearnApp.Application.Actions;
using MediatR;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ActionsController> _logger;

    public ActionsController(IMediator mediator, PortfolioMapper portfolioMapper, ILogger<ActionsController> logger)
    {
        _mediator = mediator;
        _portfolioMapper = portfolioMapper;
        _logger = logger;
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
        _logger.LogInformation(
            "Execute action={Action} investorId={InvestorId} tickerId={TickerId} quantity={Quantity}",
            "BuyNow",
            request.InvestorId,
            request.TickerId,
            request.Quantity);

        var command = new BuyNowCommand(request.InvestorId, request.TickerId, request.Quantity);
        var response = await _mediator.Send(command);

        LogActionResult("BuyNow", request.InvestorId, request.TickerId, request.Quantity, response);

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
        _logger.LogInformation(
            "Execute action={Action} investorId={InvestorId} tickerId={TickerId} quantity={Quantity}",
            "SellNow",
            request.InvestorId,
            request.TickerId,
            request.Quantity);

        var command = new SellNowCommand(request.InvestorId, request.TickerId, request.Quantity);
        var response = await _mediator.Send(command);

        LogActionResult("SellNow", request.InvestorId, request.TickerId, request.Quantity, response);

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
        _logger.LogInformation(
            "Execute action={Action} investorId={InvestorId}",
            "Wait",
            request.InvestorId);

        var command = new WaitCommand(request.InvestorId);
        var response = await _mediator.Send(command);

        LogActionResult("Wait", request.InvestorId, null, null, response);

        return ToHttpResult(response);
    }

    private void LogActionResult(
        string action,
        Guid investorId,
        Guid? tickerId,
        int? quantity,
        ActionExecutionResult response)
    {
        if (response.Status == ActionExecutionStatus.Ok)
        {
            _logger.LogInformation(
                "Action completed action={Action} investorId={InvestorId} tickerId={TickerId} quantity={Quantity} success={Success} message={Message}",
                action,
                investorId,
                tickerId,
                quantity,
                response.Success,
                response.Message);

            return;
        }

        _logger.LogWarning(
            "Action failed action={Action} investorId={InvestorId} tickerId={TickerId} quantity={Quantity} status={Status} message={Message}",
            action,
            investorId,
            tickerId,
            quantity,
            response.Status,
            response.Message);
    }

    private ActionResult<ActionResultDto> ToHttpResult(ActionExecutionResult response)
    {
        if (response.Status == ActionExecutionStatus.BadRequest)
        {
            return ApiProblemFactory.BadRequest(
                this,
                response.Message ?? "Invalid request.",
                "actions.bad_request");
        }

        if (response.Status == ActionExecutionStatus.NotFound)
        {
            return ApiProblemFactory.NotFound(
                this,
                "Requested resource was not found.",
                "actions.not_found");
        }

        if (response.Portfolio is null || response.Message is null)
        {
            return ApiProblemFactory.NotFound(
                this,
                "Action result was incomplete.",
                "actions.result_incomplete");
        }

        var result = new ActionResultDto(
            response.Success,
            response.Message,
            _portfolioMapper.ToDto(response.Portfolio));

        return Ok(result);
    }
}
