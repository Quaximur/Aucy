using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Group by BidPrice with SUM of RemainingQuantity.
    /// </summary>
    [MessagePackObject]
    public readonly struct RawBidTuple
    {
        [Key(0)] public long MinBidPrice { get; }
        [Key(1)] public long MaxBidPrice { get; }
        [Key(2)] public long RemainingQuantity { get; }

        public RawBidTuple(long minBidPrice, long maxBidPrice, long remainingQuantity)
        {
            MinBidPrice = minBidPrice;
            MaxBidPrice = maxBidPrice;
            RemainingQuantity = remainingQuantity;
        }
    }
}