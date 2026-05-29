using MessagePack;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class OwnLotDto : LotDto
    {
        [IgnoreMember] public int SoldQuantity => TotalQuantity - RemainingQuantity;

        [Key(5)] public int TotalQuantity { get; set; }
        [Key(6)] public DateTime CreatedAt { get; set; }
        [Key(7)] public ItemConfigDto ItemConfig { get; set; }

        public OwnLotDto(Guid id,
            Guid ownerUserId,
            Guid itemConfigId,
            long unitPrice,
            int remainingQuantity,
            int totalQuantity,
            DateTime createdAt,
            ItemConfigDto itemConfig) :

            base(id: id,
                ownerUserId:
                ownerUserId,
                itemConfigId: itemConfigId,
                unitPrice: unitPrice,
                remainingQuantity: remainingQuantity)
        {
            TotalQuantity = totalQuantity;
            CreatedAt = createdAt;
            ItemConfig = itemConfig;
        }
    }
}
