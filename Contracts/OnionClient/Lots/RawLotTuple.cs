using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Group by UnitPrice with SUM of RemainingQuantity.
    /// </summary>
    [MessagePackObject]
    public readonly struct RawLotTuple
    {
        [Key(0)] public long MinUnitPrice { get; }
        [Key(1)] public long MaxUnitPrice { get; }
        [Key(2)] public long RemainingQuantity { get; }

        public RawLotTuple(long minUnitPrice, long maxUnitPrice, long remainingQuantity)
        {
            MinUnitPrice = minUnitPrice;
            MaxUnitPrice = maxUnitPrice;
            RemainingQuantity = remainingQuantity;
        }
    }
}
