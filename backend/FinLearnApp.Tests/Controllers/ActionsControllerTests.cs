using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FinLearnApp.Api.Controllers;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Mappers;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Application.Actions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinLearnApp.Tests.Controllers;

public class ActionsControllerTests
{
    private static readonly Guid InvestorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TickerGuid   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static (FakeMediator mediator, ActionsController controller) CreateController()
    {
        var store = SeedData.Create();
        var mediator = new FakeMediator();
        var controller = new ActionsController(
            mediator,
            new PortfolioMapper(store),
            NullLogger<ActionsController>.Instance);
        return (mediator, controller);
    }

    [Fact]
    public async Task Buy_WithoutLimitPrice_DispatchesBuyNowCommand()
    {
        var (mediator, controller) = CreateController();
        var request = new ActionBuyRequestDto(InvestorGuid, TickerGuid, 5, null, 0);

        await controller.Buy(request);

        Assert.Equal(typeof(BuyNowCommand), mediator.LastCommandType);
    }

    [Fact]
    public async Task Buy_WithLimitPrice_DispatchesBuyLimitCommand()
    {
        var (mediator, controller) = CreateController();
        var request = new ActionBuyRequestDto(InvestorGuid, TickerGuid, 5, 1200m, 0);

        await controller.Buy(request);

        Assert.Equal(typeof(BuyLimitCommand), mediator.LastCommandType);
    }

    [Fact]
    public async Task Sell_WithoutLimitPrice_DispatchesSellNowCommand()
    {
        var (mediator, controller) = CreateController();
        var request = new ActionSellRequestDto(InvestorGuid, TickerGuid, 5, null, 0);

        await controller.Sell(request);

        Assert.Equal(typeof(SellNowCommand), mediator.LastCommandType);
    }

    [Fact]
    public async Task Sell_WithLimitPrice_DispatchesSellLimitCommand()
    {
        var (mediator, controller) = CreateController();
        var request = new ActionSellRequestDto(InvestorGuid, TickerGuid, 5, 900m, 0);

        await controller.Sell(request);

        Assert.Equal(typeof(SellLimitCommand), mediator.LastCommandType);
    }
}

internal sealed class FakeMediator : IMediator
{
    public Type? LastCommandType { get; private set; }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        LastCommandType = request.GetType();
        var result = ActionExecutionResult.BadRequest("dispatched");
        return Task.FromResult((TResponse)(object)result);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        => Task.CompletedTask;

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(null);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        => Task.CompletedTask;
}
