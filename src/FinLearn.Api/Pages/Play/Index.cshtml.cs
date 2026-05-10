using FinLearn.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinLearn.Api.Pages.Play;

public class IndexModel : PageModel
{
    private readonly GameStore _store;

    public IndexModel(GameStore store)
    {
        _store = store;
    }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        var (gameId, _) = _store.CreateGame();
        return Redirect($"/play/{gameId}");
    }
}
