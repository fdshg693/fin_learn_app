namespace MyApp.Tests;

using MyApp.Core;

public class OrderFillTests
{
    [Fact]
    public void 注文IDと約定数量と合計金額を保持する()
    {
        var fill = new OrderFill(OrderId: 1, FilledQuantity: 3, TotalAmount: 300);

        Assert.Equal(1, fill.OrderId);
        Assert.Equal(3, fill.FilledQuantity);
        Assert.Equal(300, fill.TotalAmount);
    }

    [Fact]
    public void 同じ値のOrderFillは等しい()
    {
        var fill1 = new OrderFill(1, 3, 300);
        var fill2 = new OrderFill(1, 3, 300);

        Assert.Equal(fill1, fill2);
    }
}
