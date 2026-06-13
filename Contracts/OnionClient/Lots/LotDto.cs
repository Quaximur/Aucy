using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class LotDto
    {
        [Key(0)] public Guid Id { get; set; }
        [Key(1)] public Guid OwnerUserId { get; set; }
        [Key(2)] public Guid ItemConfigId { get; set; }
        [Key(3)] public long UnitPrice { get; set; }
        [Key(4)] public int RemainingQuantity { get; set; }

        public LotDto(Guid id, Guid ownerUserId, Guid itemConfigId, long unitPrice, int remainingQuantity)
        {
            Id = id;
            OwnerUserId = ownerUserId;
            ItemConfigId = itemConfigId;
            UnitPrice = unitPrice;
            RemainingQuantity = remainingQuantity;
        }
    }
}