using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Group by BidPrice with SUM of RemainingQuantity.
    /// </summary>
    [MessagePackObject]
    public readonly struct RawBidTuple
    {
        [Key(0)] public long BidPrice { get; }
        [Key(1)] public long RemainingQuantity { get; }

        public RawBidTuple(long bidPrice, long remainingQuantity)
        {
            BidPrice = bidPrice;
            RemainingQuantity = remainingQuantity;
        }
    }
}