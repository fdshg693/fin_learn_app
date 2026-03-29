using FinLearn.Api.Mappers;
using FinLearn.Api.Services;

namespace FinLearn.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin");

        group.MapGet("/games/{id}/orderbook", GetOrderBook);

        return group;
    }

    private static IResult GetOrderBook(string id, GameStore store)
    {
        var game = store.GetGame(id);
        if (game is null) return Results.NotFound();
        return Results.Ok(OrderBookMapper.ToResponse(game.OrderBook));
    }
}
