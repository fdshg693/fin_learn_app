namespace MyApp.Core;

public static class Messages
{
    public const string QuantityMustBePositive = "数量は0より大きい必要があります";
    public const string InsufficientQuantityToSell = "保有数量を超えて売却できません";
    public const string InsufficientCashToBuy = "現金が不足して購入できません";
    public const string NoMatchingSellOrders = "約定できる売り注文がありません";
    public const string NoMatchingBuyOrders = "約定できる買い注文がありません";
    public const string PriceMustBePositive = "価格は0より大きい必要があります";
}
