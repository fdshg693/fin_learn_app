namespace FinLearn.Tests;

using FinLearn.Core;

public class SettlementProcessorTests
{
    private static IReadOnlyDictionary<int, Order> Index(params Order[] orders)
    {
        var dict = new Dictionary<int, Order>();
        foreach (var o in orders) dict[o.Id] = o;
        return dict;
    }

    [Fact]
    public void Limit_Buy_の全約定_fill_は予約から確定し差額が_available_に戻る()
    {
        // 買い指値 100×5 + fee 50 を予約済の player
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });
        var (reserved, _) = portfolio.ReserveBuy(instrumentId: 1, quantity: 5, price: 100, fee: 50);

        var order = new Order(Id: 1, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 5, Price: 100);
        // 5株すべて 90 円で約定 (resting 価格)
        var fills = new List<OrderFill> { new(OrderId: 1, FilledQuantity: 5, TotalAmount: 450) };
        var portfolios = new Dictionary<string, Portfolio> { ["player"] = reserved };
        var ordersById = Index(order);
        var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(fills, ordersById);

        var result = SettlementProcessor.SettleFills(fills, ordersById, postFillRemaining, portfolios, fee: 50);

        // consumedReserved = 5*100 + 50 = 550, actualCost = 5*90 + 50 = 500, refund = 50
        Assert.Equal(500, result["player"].Cash);
        Assert.Equal(0, result["player"].ReservedCash);
        Assert.Equal(5, result["player"].QuantityOf(1));
    }

    [Fact]
    public void Limit_Buy_の部分約定では_feeIfFinal_0_となり_fee_は計上されない()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });
        var (reserved, _) = portfolio.ReserveBuy(instrumentId: 1, quantity: 5, price: 100, fee: 50);

        var order = new Order(Id: 1, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 5, Price: 100);
        var fills = new List<OrderFill> { new(OrderId: 1, FilledQuantity: 2, TotalAmount: 180) };  // 2株 @ 90
        var portfolios = new Dictionary<string, Portfolio> { ["player"] = reserved };
        var ordersById = Index(order);
        var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(fills, ordersById);

        var result = SettlementProcessor.SettleFills(fills, ordersById, postFillRemaining, portfolios, fee: 50);

        // postFillRemaining[1] = 5 - 2 = 3 ≠ 0 → feeIfFinal = 0
        // consumedReserved = 2*100 + 0 = 200, actualCost = 2*90 + 0 = 180, refund = 20
        Assert.Equal(450 + 20, result["player"].Cash);
        Assert.Equal(550 - 200, result["player"].ReservedCash);
        Assert.Equal(2, result["player"].QuantityOf(1));
    }

    [Fact]
    public void Limit_Sell_の_fill_は_reserved_positions_を消費し_cash_が増える()
    {
        var position = new Position(TestData.Instrument1, quantity: 10);
        var portfolio = new Portfolio(cash: 100, positions: new[] { position });
        var (reserved, _) = portfolio.ReserveSell(instrumentId: 1, quantity: 5);

        var order = new Order(Id: 1, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Sell, Quantity: 5, Price: 100);
        var fills = new List<OrderFill> { new(OrderId: 1, FilledQuantity: 5, TotalAmount: 600) };  // 5株 @ 120
        var portfolios = new Dictionary<string, Portfolio> { ["player"] = reserved };
        var ordersById = Index(order);
        var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(fills, ordersById);

        var result = SettlementProcessor.SettleFills(fills, ordersById, postFillRemaining, portfolios, fee: 20);

        // proceeds = 5*120 - 20 = 580, cash: 100 + 580 = 680, reserved: 0, 全保有: 5
        Assert.Equal(680, result["player"].Cash);
        Assert.Equal(5, result["player"].QuantityOf(1));
        Assert.Equal(0, result["player"].ReservedQuantityOf(1));
    }

    [Fact]
    public void Market_fill_は_ApplyTrade_で適用される()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });
        var marketOrder = Order.CreateMarket(id: 1, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Buy, quantity: 5);
        var fills = new List<OrderFill> { new(OrderId: 1, FilledQuantity: 5, TotalAmount: 450) };
        var portfolios = new Dictionary<string, Portfolio> { ["player"] = portfolio };
        var ordersById = Index(marketOrder);
        var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(fills, ordersById);

        var result = SettlementProcessor.SettleFills(fills, ordersById, postFillRemaining, portfolios, fee: 50);

        // 成行: ApplyTrade で 1000 - 450 - 50 = 500
        Assert.Equal(500, result["player"].Cash);
        Assert.Equal(5, result["player"].QuantityOf(1));
    }

    [Fact]
    public void ReleaseExpired_は_player_予約を_available_に戻す()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });
        var (reserved, _) = portfolio.ReserveBuy(instrumentId: 1, quantity: 5, price: 100, fee: 50);
        var portfolios = new Dictionary<string, Portfolio> { ["player"] = reserved };

        // 失効した買い指値（残量 5）
        var expiredOrder = new Order(Id: 1, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 5, Price: 100);

        var result = SettlementProcessor.ReleaseExpired(new[] { expiredOrder }, portfolios, feePerOrder: 50);

        Assert.Equal(1000, result["player"].Cash);
        Assert.Equal(0, result["player"].ReservedCash);
    }

    [Fact]
    public void ReleaseExpired_は_computer_の_Infinite_Portfolio_に対し_no_op()
    {
        var portfolio = Portfolio.CreateInfinite();
        var portfolios = new Dictionary<string, Portfolio> { ["computer1"] = portfolio };
        var expired = new Order(Id: 1, TraderId: "computer1", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 5, Price: 100);

        var result = SettlementProcessor.ReleaseExpired(new[] { expired }, portfolios, feePerOrder: 50);

        Assert.True(result["computer1"].IsInfinite);
        Assert.Equal(int.MaxValue, result["computer1"].Cash);
    }

    [Fact]
    public void ReleaseExpired_は成行注文を対象外として無視する()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });
        var portfolios = new Dictionary<string, Portfolio> { ["player"] = portfolio };
        var marketOrder = Order.CreateMarket(id: 1, traderId: "player", instrument: TestData.Instrument1, side: OrderSide.Buy, quantity: 5);

        var result = SettlementProcessor.ReleaseExpired(new[] { marketOrder }, portfolios, feePerOrder: 50);

        Assert.Equal(1000, result["player"].Cash);
        Assert.Equal(0, result["player"].ReservedCash);
    }

    [Fact]
    public void SettleFills_は両側_fill_を統一適用する()
    {
        // player の resting 売り指値が computer の買いと約定するシナリオ
        var playerPosition = new Position(TestData.Instrument1, quantity: 10);
        var (playerReserved, _) = new Portfolio(cash: 0, positions: new[] { playerPosition }).ReserveSell(instrumentId: 1, quantity: 5);

        var playerSell = new Order(Id: 1, TraderId: "player", Instrument: TestData.Instrument1, Side: OrderSide.Sell, Quantity: 5, Price: 100);
        var computerBuy = new Order(Id: 2, TraderId: "computer1", Instrument: TestData.Instrument1, Side: OrderSide.Buy, Quantity: 5, Price: 110);

        // 約定: 5 株 @ 100 (resting price = player の指値)
        var fills = new List<OrderFill>
        {
            new(OrderId: 1, FilledQuantity: 5, TotalAmount: 500),  // resting (player sell)
            new(OrderId: 2, FilledQuantity: 5, TotalAmount: 500),  // incoming (computer buy)
        };
        var portfolios = new Dictionary<string, Portfolio>
        {
            ["player"] = playerReserved,
            ["computer1"] = Portfolio.CreateInfinite(),
        };
        var ordersById = Index(playerSell, computerBuy);
        var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(fills, ordersById);

        var result = SettlementProcessor.SettleFills(fills, ordersById, postFillRemaining, portfolios, fee: 0);

        // player: 全保有 10→5, reserved 5→0, cash 0+500=500
        Assert.Equal(500, result["player"].Cash);
        Assert.Equal(5, result["player"].QuantityOf(1));
        Assert.Equal(0, result["player"].ReservedQuantityOf(1));
        // computer: Infinite なので不変
        Assert.True(result["computer1"].IsInfinite);
    }
}
