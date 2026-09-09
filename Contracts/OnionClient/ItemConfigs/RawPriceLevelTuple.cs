using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public struct RawPriceLevelTuple
    {
        // Representative price for the tier (one price per level)
        [Key(0)] public long Price { get; set; }
        [Key(1)] public long LotQuantity { get; set; }
        [Key(2)] public long BidQuantity { get; set; }

        public RawPriceLevelTuple(long price, long lotQuantity, long bidQuantity)
        {
            Price = price;
            LotQuantity = lotQuantity;
            BidQuantity = bidQuantity;
        }
    }
}
