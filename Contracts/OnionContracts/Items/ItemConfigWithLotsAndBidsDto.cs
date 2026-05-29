using MessagePack;
using System.Collections.Generic;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class ItemConfigWithLotsAndBidsDto : ItemConfigDto
    {
        [Key(9)] public List<LotDto> Lots { get; set; }
        [Key(10)] public List<BidDto> Bids { get; set; }

        public ItemConfigWithLotsAndBidsDto(Guid id,
            string name,
            bool isUnary,
            string? icon,
            int? grade,
            int? rarity,
            string baseType,
            List<string> extra,
            string categoryPath,
            List<LotDto> lots,
            List<BidDto> bids)

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
