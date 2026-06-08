using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class CreateLotDto
    {
        [Key(0)] public Guid ItemConfigId { get; set; }
        [Key(1)] public long UnitPrice { get; set; }
        [Key(2)] public int TotalQuantity { get; set; }

        public CreateLotDto(Guid itemConfigId, long unitPrice, int totalQuantity)
        {
            ItemConfigId = itemConfigId;
            UnitPrice = unitPrice;
            TotalQuantity = totalQuantity;
        }
    }
}
