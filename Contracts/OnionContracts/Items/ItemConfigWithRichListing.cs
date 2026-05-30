using MessagePack;
using System.Collections.Generic;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class ItemConfigWithRichListing : ItemConfigDto
    {
        [Key(9)] public LotDto[] Lots { get; set; }
        [Key(10)] public BidDto[] Bids { get; set; }

        public ItemConfigWithRichListing(Guid id,
            string name,
            bool isUnary,
            string icon,
            int? grade,
            int? rarity,
            string baseType,
            List<string> extra,
            string categoryPath,
            LotDto[] lots,
            BidDto[] bids)

            : base(id,
                  name,
                  isUnary,
                  icon,
                  grade,
                  rarity,
                  baseType,
                  extra,
                  categoryPath)
        {
            Lots = lots;
            Bids = bids;
        }
    }
}
