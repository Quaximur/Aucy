using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Group by UnitPrice with SUM of RemainingQuantity.
    /// </summary>
    [MessagePackObject]
    public readonly struct RawLotTuple
    {
        [Key(0)] public long UnitPrice { get; }
        [Key(1)] public long RemainingQuantity { get; }
    }
}
