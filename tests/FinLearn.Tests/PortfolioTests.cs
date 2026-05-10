namespace FinLearn.Tests;

using FinLearn.Core;

public class PortfolioTests
{
    [Fact]
    public void 現金プロパティを保持できる()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });

        Assert.Equal(1000, portfolio.Cash);
    }

    [Fact]
    public void 現金とポジションの合計を計算できる()
    {
        var exchange = TestData.CreateExchange((1, 10), (2, 20));

        var positionA = new Position(TestData.Instrument1, quantity: 10);
        var positionB = new Position(TestData.Instrument2, quantity: 5);

        var portfolio = new Portfolio(cash: 1000, positions: new[] { positionA, positionB });

        var totalAmount = portfolio.TotalAmount(exchange);

        Assert.Equal(1200, totalAmount);
    }

    [Fact]
    public void 銘柄IDを指定して保有数量を取得できる()
    {
        var positionA1 = new Position(TestData.Instrument1, quantity: 10);
        var positionA2 = new Position(TestData.Instrument1, quantity: 5);
        var positionB = new Position(TestData.Instrument2, quantity: 3);

        var portfolio = new Portfolio(cash: 0, positions: new[] { positionA1, positionA2, positionB });

        var quantity = portfolio.QuantityOf(instrumentId: 1);

        Assert.Equal(15, quantity);
    }

    [Fact]
    public void 保有数量を超える売却は警告して何もしない()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 10, TotalAmount: 100, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.InsufficientQuantityToSell, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 保有数量以内の売却は現金と数量が更新される()
    {
        var position = new Position(TestData.Instrument1, quantity: 8);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        // 約定価格10円 × 3株 = 30円
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 3, TotalAmount: 30, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(1030, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 現金の範囲内の購入は現金と数量が更新される()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 100, positions: new[] { position });

        // 約定価格10円 × 3株 = 30円
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 30, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(70, resultPortfolio.Cash);
        Assert.Equal(8, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 現金の範囲外の購入は警告して何もしない()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 20, positions: new[] { position });

        // 約定価格10円 × 3株 = 30 > 現金20
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 30, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(20, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 未保有銘柄の購入は許可され現金と数量が更新される()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 200, positions: new[] { position });

        // 約定価格20円 × 3株 = 60円
        var trade = new TradeResult(InstrumentId: 2, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 60, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(140, resultPortfolio.Cash);
        Assert.Equal(3, resultPortfolio.QuantityOf(instrumentId: 2));
    }

    [Fact]
    public void Sellの数量が0以下の場合は警告して何もしない()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 0, TotalAmount: 0, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void Buyの数量が0以下の場合は警告して何もしない()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: -1, TotalAmount: 0, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 購入時に手数料が差し引かれる()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });

        // 約定価格10円 × 3株 = 30 + 手数料50 = 80
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 30, Fee: 50);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(920, resultPortfolio.Cash);
        Assert.Equal(3, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 売却時に手数料が差し引かれる()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        // 現金1000 + 約定価格10円 × 3株 = 30 - 手数料50 = 980
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 3, TotalAmount: 30, Fee: 50);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(980, resultPortfolio.Cash);
        Assert.Equal(2, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void Infinite_PortfolioはBuyで現金も保有数量も変わらない()
    {
        var portfolio = Portfolio.CreateInfinite();
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 100, TotalAmount: 10000, Fee: 50);
        var (result, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(int.MaxValue, result.Cash);
        Assert.Equal(0, result.QuantityOf(1));
        Assert.True(result.IsInfinite);
    }

    [Fact]
    public void Infinite_PortfolioはSellで保有数量0でも警告なしで不変()
    {
        var portfolio = Portfolio.CreateInfinite();
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 5, TotalAmount: 500, Fee: 0);
        var (result, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(int.MaxValue, result.Cash);
        Assert.Equal(0, result.QuantityOf(1));
        Assert.True(result.IsInfinite);
    }

    [Fact]
    public void 手数料込みで現金が不足する購入は警告して何もしない()
    {
        var portfolio = new Portfolio(cash: 70, positions: new Position[] { });

        // 約定価格10円 × 3株 = 30 + 手数料50 = 80 > 現金70
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 30, Fee: 50);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(70, resultPortfolio.Cash);
    }

    // -- 予約モデル（指値発注時の資源予約）テスト --

    [Fact]
    public void ReserveBuy_は_available_cash_を_reserved_cash_に移す()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });

        // 100円 × 5株 + 手数料50 = 550
        var (result, warning) = portfolio.ReserveBuy(instrumentId: 1, quantity: 5, price: 100, fee: 50);

        Assert.Null(warning);
        Assert.Equal(450, result.Cash);
        Assert.Equal(550, result.ReservedCash);
    }

    [Fact]
    public void ReserveBuy_は残高不足で警告し状態を変えない()
    {
        var portfolio = new Portfolio(cash: 100, positions: new Position[] { });

        var (result, warning) = portfolio.ReserveBuy(instrumentId: 1, quantity: 5, price: 100, fee: 50);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(100, result.Cash);
        Assert.Equal(0, result.ReservedCash);
    }

    [Fact]
    public void ReserveSell_は保有を_reserved_positions_に移す_全保有数は不変()
    {
        var position = new Position(TestData.Instrument1, quantity: 10);
        var portfolio = new Portfolio(cash: 0, positions: new[] { position });

        var (result, warning) = portfolio.ReserveSell(instrumentId: 1, quantity: 6);

        Assert.Null(warning);
        Assert.Equal(10, result.QuantityOf(1));               // 全保有は不変
        Assert.Equal(6, result.ReservedQuantityOf(1));
        Assert.Equal(4, result.AvailableQuantityOf(1));
    }

    [Fact]
    public void ReserveSell_は数量不足で警告し状態を変えない()
    {
        var position = new Position(TestData.Instrument1, quantity: 3);
        var portfolio = new Portfolio(cash: 0, positions: new[] { position });

        var (result, warning) = portfolio.ReserveSell(instrumentId: 1, quantity: 5);

        Assert.Equal(Messages.InsufficientQuantityToSell, warning);
        Assert.Equal(3, result.QuantityOf(1));
        Assert.Equal(0, result.ReservedQuantityOf(1));
    }

    [Fact]
    public void ReserveSell_は既存予約と合算される()
    {
        var position = new Position(TestData.Instrument1, quantity: 10);
        var portfolio = new Portfolio(cash: 0, positions: new[] { position });

        var (after1, _) = portfolio.ReserveSell(instrumentId: 1, quantity: 3);
        var (after2, _) = after1.ReserveSell(instrumentId: 1, quantity: 4);

        Assert.Equal(7, after2.ReservedQuantityOf(1));
        Assert.Equal(3, after2.AvailableQuantityOf(1));
    }

    [Fact]
    public void SettleReservedBuy_部分約定_feeIfFinal_0_では_fee_は計上されず差額_refund()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });
        // 予約: 100×5 + 50 = 550
        var (reserved, _) = portfolio.ReserveBuy(instrumentId: 1, quantity: 5, price: 100, fee: 50);

        // 部分約定 2 株 @ 90 (resting price)。feeIfFinal=0
        var settled = reserved.SettleReservedBuy(instrumentId: 1, filledQty: 2, actualUnitPrice: 90, reservedUnitPrice: 100, feeIfFinal: 0);

        // consumedReserved = 2*100 + 0 = 200, actualCost = 2*90 + 0 = 180, refund = 20
        // available: 450 + 20 = 470, reserved: 550 - 200 = 350
        Assert.Equal(470, settled.Cash);
        Assert.Equal(350, settled.ReservedCash);
        Assert.Equal(2, settled.QuantityOf(1));
    }

    [Fact]
    public void SettleReservedBuy_全約定_feeIfFinal_fee_で_reserved_全消費()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });
        var (reserved, _) = portfolio.ReserveBuy(instrumentId: 1, quantity: 5, price: 100, fee: 50);

        // 全約定 5 株 @ 90, feeIfFinal=50
        var settled = reserved.SettleReservedBuy(instrumentId: 1, filledQty: 5, actualUnitPrice: 90, reservedUnitPrice: 100, feeIfFinal: 50);

        // consumedReserved = 5*100 + 50 = 550, actualCost = 5*90 + 50 = 500, refund = 50
        // available: 450 + 50 = 500, reserved: 550 - 550 = 0
        Assert.Equal(500, settled.Cash);
        Assert.Equal(0, settled.ReservedCash);
        Assert.Equal(5, settled.QuantityOf(1));
    }

    [Fact]
    public void SettleReservedSell_は保有減_reserved_減_cash増()
    {
        var position = new Position(TestData.Instrument1, quantity: 10);
        var portfolio = new Portfolio(cash: 100, positions: new[] { position });
        var (reserved, _) = portfolio.ReserveSell(instrumentId: 1, quantity: 5);

        // 全約定 5 株 @ 120, feeIfFinal=20
        var settled = reserved.SettleReservedSell(instrumentId: 1, filledQty: 5, actualUnitPrice: 120, feeIfFinal: 20);

        // proceeds = 5*120 - 20 = 580, cash: 100 + 580 = 680
        // 全保有: 10 - 5 = 5, reserved: 5 - 5 = 0
        Assert.Equal(680, settled.Cash);
        Assert.Equal(5, settled.QuantityOf(1));
        Assert.Equal(0, settled.ReservedQuantityOf(1));
    }

    [Fact]
    public void ReleaseBuyReservation_は_reserved_を_available_に戻す()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });
        var (reserved, _) = portfolio.ReserveBuy(instrumentId: 1, quantity: 5, price: 100, fee: 50);

        // 残量 5 株分 + 残 fee 50 を解放
        var released = reserved.ReleaseBuyReservation(remainingQty: 5, reservedUnitPrice: 100, remainingFee: 50);

        Assert.Equal(1000, released.Cash);
        Assert.Equal(0, released.ReservedCash);
    }

    [Fact]
    public void ReleaseSellReservation_は_reserved_positions_のみ消費し全保有は不変()
    {
        var position = new Position(TestData.Instrument1, quantity: 10);
        var portfolio = new Portfolio(cash: 0, positions: new[] { position });
        var (reserved, _) = portfolio.ReserveSell(instrumentId: 1, quantity: 7);

        var released = reserved.ReleaseSellReservation(instrumentId: 1, remainingQty: 7);

        Assert.Equal(10, released.QuantityOf(1));         // 全保有は不変
        Assert.Equal(0, released.ReservedQuantityOf(1));
        Assert.Equal(10, released.AvailableQuantityOf(1));
    }

    [Fact]
    public void Infinite_Portfolio_は予約系メソッドが全て_no_op()
    {
        var portfolio = Portfolio.CreateInfinite();

        var (reserved1, w1) = portfolio.ReserveBuy(instrumentId: 1, quantity: 100, price: 1000, fee: 50);
        var (reserved2, w2) = portfolio.ReserveSell(instrumentId: 1, quantity: 100);
        var settled1 = portfolio.SettleReservedBuy(instrumentId: 1, filledQty: 10, actualUnitPrice: 90, reservedUnitPrice: 100, feeIfFinal: 50);
        var settled2 = portfolio.SettleReservedSell(instrumentId: 1, filledQty: 10, actualUnitPrice: 120, feeIfFinal: 50);
        var released1 = portfolio.ReleaseBuyReservation(remainingQty: 10, reservedUnitPrice: 100, remainingFee: 50);
        var released2 = portfolio.ReleaseSellReservation(instrumentId: 1, remainingQty: 10);

        Assert.Null(w1);
        Assert.Null(w2);
        foreach (var p in new[] { reserved1, reserved2, settled1, settled2, released1, released2 })
        {
            Assert.True(p.IsInfinite);
            Assert.Equal(int.MaxValue, p.Cash);
            Assert.Equal(0, p.ReservedCash);
            Assert.Equal(0, p.QuantityOf(1));
            Assert.Equal(0, p.ReservedQuantityOf(1));
        }
    }

    [Fact]
    public void TotalAmount_は_available_と_reserved_と_全保有評価の合計()
    {
        var exchange = TestData.CreateExchange((1, 50));
        var position = new Position(TestData.Instrument1, quantity: 10);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });
        // 予約後でも合計は同じ（cash 内訳が変わるだけ）
        var (afterReserveBuy, _) = portfolio.ReserveBuy(instrumentId: 1, quantity: 3, price: 100, fee: 20);

        Assert.Equal(1000 + 10 * 50, portfolio.TotalAmount(exchange));
        Assert.Equal(1000 + 10 * 50, afterReserveBuy.TotalAmount(exchange));
    }
}
