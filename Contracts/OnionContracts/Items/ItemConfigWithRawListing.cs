using MessagePack;
using System.Collections.Generic;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class ItemConfigWithRawListing : ItemConfigDto
    {
        [Key(9)] public RawLotTuple[] RawLots { get; set; }
        [Key(10)] public RawBidTuple[] RawBids { get; set; }

        public ItemConfigWithRawListing(Guid id,
            string name,
            bool isUnary,
            string icon,
            int? grade,
            int? rarity,
            string baseType,
            List<string> extra,
            string categoryPath,
            RawLotTuple[] rawLots,
            RawBidTuple[] rawBids)

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
            RawLots = rawLots;
            RawBids = rawBids;
        }
    }
}
