using MessagePack;
using System.Collections.Generic;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class ItemConfigWithRawListingDto : ItemConfigDto
    {
        [Key(10)] public RawLotTuple[] RawLots { get; set; }
        [Key(11)] public RawBidTuple[] RawBids { get; set; }

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
            RawBidTuple[] rawBids)

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
            RawLots = rawLots;
            RawBids = rawBids;
        }
    }
}
