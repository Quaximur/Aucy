using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class RawItemListingDto
    {
        [Key(0)] public RawPriceLevelTuple[] Levels { get; set; }
    }

    [MessagePackObject]
    public struct RawPriceLevelTuple
    {
        [Key(0)] public long MinPrice { get; set; }
        [Key(1)] public long MaxPrice { get; set; }
        [Key(2)] public long LotQuantity { get; set; }
        [Key(3)] public long BidQuantity { get; set; }

        public RawPriceLevelTuple(long minPrice, long maxPrice,
            long lotQuantity, long bidQuantity)
        {
            MinPrice = minPrice;
            MaxPrice = maxPrice;
            LotQuantity = lotQuantity;
            BidQuantity = bidQuantity;
        }
    }

}
