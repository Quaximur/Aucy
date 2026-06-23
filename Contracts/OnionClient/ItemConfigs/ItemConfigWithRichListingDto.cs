using MessagePack;
using System.Collections.Generic;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class ItemConfigWithRichListingDto : ItemConfigDto
    {
        [Key(50)] public LotDto[] Lots { get; set; }
        [Key(51)] public BidDto[] Bids { get; set; }

        public ItemConfigWithRichListingDto() { }

        public ItemConfigWithRichListingDto(Guid id,
            string name,
            bool isUnary,
            string icon,
            int? grade,
            int? rarity,
            float weight,
            string baseType,
            List<string> extra,
            string categoryPath,
            LotDto[] lots,
            BidDto[] bids,
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
            Lots = lots;
            Bids = bids;
        }
    }
}
