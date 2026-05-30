using MessagePack;
using System.Collections.Generic;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class ItemConfigWithRichListing : ItemConfigDto
    {
        [Key(10)] public LotDto[] Lots { get; set; }
        [Key(11)] public BidDto[] Bids { get; set; }

        public ItemConfigWithRichListing(Guid id,
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
            BidDto[] bids)

            : base(id: id,
                  name: name,
                  isUnary: isUnary,
                  icon: icon,
                  grade: grade,
                  rarity: rarity,
                  weight: weight,
                  baseType: baseType,
                  extra: extra,
                  categoryPath: categoryPath)
        {
            Lots = lots;
            Bids = bids;
        }
    }
}
