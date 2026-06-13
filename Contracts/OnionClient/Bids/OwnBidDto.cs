using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class OwnBidDto : BidDto
    {
        [IgnoreMember] public int BoughtQuantity => TotalQuantity - RemainingQuantity;

        [Key(5)] public int TotalQuantity { get; set; }
        [Key(6)] public DateTime CreatedAt { get; set; }
        [Key(7)] public ItemConfigDto ItemConfig { get; set; }

        public OwnBidDto(Guid id,
            Guid itemConfigId,
            Guid bidderUserId,
            long bidPrice,
            int remainingQuantity,
            int totalQuantity,
            DateTime createdAt,
            ItemConfigDto itemConfig) :

            base(id, 
                itemConfigId,
                bidderUserId,
                bidPrice,
                remainingQuantity)
        {
            CreatedAt = createdAt;
            TotalQuantity = totalQuantity;
            ItemConfig = itemConfig;
        }
    }
}

