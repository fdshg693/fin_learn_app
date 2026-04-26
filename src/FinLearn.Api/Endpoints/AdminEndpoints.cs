using FinLearn.Api.Mappers;
using FinLearn.Api.Services;

namespace FinLearn.Api.Endpoints;

public static class AdminEndpoints
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static RouteGroupBuilder MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin");

        group.MapGet("/games/{id}/orderbook", GetOrderBook);

        return group;
    }

    private static IResult GetOrderBook(
        string id,
        GameStore store,
        int? page,
        int? pageSize)
    {
        var pageValue = page ?? 1;
        var pageSizeValue = pageSize ?? DefaultPageSize;

        if (pageValue < 1)
            return Results.BadRequest("page must be >= 1");
        if (pageSizeValue < 1)
            return Results.BadRequest("pageSize must be >= 1");
        if (pageSizeValue > MaxPageSize)
            return Results.BadRequest($"pageSize must be <= {MaxPageSize}");

        var game = store.GetGame(id);
        if (game is null) return Results.NotFound();

        return Results.Ok(OrderBookMapper.ToResponse(game.OrderBook, pageValue, pageSizeValue));
    }
}
