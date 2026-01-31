namespace MyApp.Core;

/// <summary>
/// 銘柄の保有（銘柄IDと数量）
/// </summary>
public class Position
{
    public Position(int instrumentId, int quantity)
    {
        InstrumentId = instrumentId;
        Quantity = quantity;
    }

    public int InstrumentId { get; }
    public int Quantity { get; }

    /// <summary>
    /// 同じIDの銘柄を足し算する。異なるIDの場合は未対応。
    /// </summary>
    public Position Add(Position other)
    {
        if (InstrumentId != other.InstrumentId)
        {
            throw new InvalidOperationException("同じIDの銘柄を足し算することはできません。");
        }
        return new Position(InstrumentId, Quantity + other.Quantity);
    }
}
