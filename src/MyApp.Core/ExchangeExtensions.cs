namespace MyApp.Core;

using System.Collections.Generic;

public static class ExchangeExtensions
{
    public static bool TryGetPrice(
        this IExchange exchange,
        int instrumentId,
        out int price,
        out string? warning)
    {
        try
        {
            price = exchange.PriceOf(instrumentId);
        }
        catch (KeyNotFoundException)
        {
            price = 0;
            warning = "価格が取得できません";
            return false;
        }

        if (price <= 0)
        {
            warning = "価格は0より大きい必要があります";
            return false;
        }

        warning = null;
        return true;
    }
}
