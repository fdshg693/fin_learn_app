using FinLearn.Api.Dtos;

namespace FinLearn.Api.Pages.Shared;

public sealed record OrderBookPanelViewModel(
    string GameId,
    OrderBookResponse Book);
