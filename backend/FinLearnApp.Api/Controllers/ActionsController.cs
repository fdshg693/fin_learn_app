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

    [HttpPost("buy")]
    public async Task<ActionResult<ActionResultDto>> Buy(ActionBuyRequestDto request)
    {
        _logger.LogInformation(
            "Execute action={Action} investorId={InvestorId} tickerId={TickerId} quantity={Quantity} limitPrice={LimitPrice} expectedTurn={ExpectedTurn}",
            "Buy",
            request.InvestorId,
            request.TickerId,
            request.Quantity,
            request.LimitPrice,
            request.ExpectedTurn);

        IRequest<ActionExecutionResult> command = request.LimitPrice.HasValue
            ? new BuyLimitCommand(request.InvestorId, request.TickerId, request.Quantity, request.LimitPrice.Value, request.ExpectedTurn)
            : new BuyNowCommand(request.InvestorId, request.TickerId, request.Quantity, request.ExpectedTurn);

        var response = await _mediator.Send(command);

        LogActionResult("Buy", request.InvestorId, request.TickerId, request.Quantity, response);
        return ToHttpResult(response);
    }

    [HttpPost("sell")]
    public async Task<ActionResult<ActionResultDto>> Sell(ActionSellRequestDto request)
    {
        _logger.LogInformation(
            "Execute action={Action} investorId={InvestorId} tickerId={TickerId} quantity={Quantity} limitPrice={LimitPrice} expectedTurn={ExpectedTurn}",
            "Sell",
            request.InvestorId,
            request.TickerId,
            request.Quantity,
            request.LimitPrice,
            request.ExpectedTurn);

        IRequest<ActionExecutionResult> command = request.LimitPrice.HasValue
            ? new SellLimitCommand(request.InvestorId, request.TickerId, request.Quantity, request.LimitPrice.Value, request.ExpectedTurn)
            : new SellNowCommand(request.InvestorId, request.TickerId, request.Quantity, request.ExpectedTurn);

        var response = await _mediator.Send(command);

        LogActionResult("Sell", request.InvestorId, request.TickerId, request.Quantity, response);
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
            "Execute action={Action} investorId={InvestorId} expectedTurn={ExpectedTurn}",
            "Wait",
            request.InvestorId,
            request.ExpectedTurn);

        var command = new WaitCommand(request.InvestorId, request.ExpectedTurn);
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

        if (response.Status == ActionExecutionStatus.Conflict)
        {
            return ApiProblemFactory.Conflict(
                this,
                response.Message ?? "Turn conflict occurred.",
                "actions.turn_conflict");
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
            _portfolioMapper.ToDto(response.Portfolio),
            response.CurrentTurn ?? 0);

        return Ok(result);
    }
}
