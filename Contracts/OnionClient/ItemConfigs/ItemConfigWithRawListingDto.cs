using MessagePack;
using System.Collections.Generic;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class ItemConfigWithRawListingDto : ItemConfigDto
    {
        [Key(50)] public RawLotTuple[] RawLots { get; set; }
        [Key(51)] public RawBidTuple[] RawBids { get; set; }

        public ItemConfigWithRawListingDto() { }

        public ItemConfigWithRawListingDto(Guid id,
            string name,
            bool isUnary,
            string icon,
            int? grade,
            int? rarity,
            float weight,
            string baseType,
            List<string> extra,
            string categoryPath,
            RawLotTuple[] rawLots,
            RawBidTuple[] rawBids,
            long minPrice,
            long maxPrice,
            int maxSellQuantity,
            int maxBuyQuantity)

            : base(id: id,
                  name: name,
                  isUnary: isUnary,
                  icon: icon,
                  grade: grade,
                  rarity: rarity,
                  weight: weight,
                  baseType: baseType,
                  extra: extra,
                  categoryPath: categoryPath,
                  minPrice: minPrice,
                  maxPrice: maxPrice,
                  maxSellQuantity: maxSellQuantity,
                  maxBuyQuantity: maxBuyQuantity)
        {
            RawLots = rawLots;
            RawBids = rawBids;
        }
    }
}
