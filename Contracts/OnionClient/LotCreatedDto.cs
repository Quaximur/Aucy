using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class LotCreatedDto
    {
        // Remove TotalQuantity ItemStacks with ItemConfig.Id, decrease Weight and mb UsedCapacity
        [Key(0)] public Guid LotId { get; set; }
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public long UnitPrice { get; set; }
        [Key(3)] public int TotalQuantity { get; set; }

        public LotCreatedDto(Guid lotId, ItemConfigDto itemConfig, long unitPrice, int totalQuantity)
        {
            LotId = lotId;
            ItemConfig = itemConfig;
            UnitPrice = unitPrice;
            TotalQuantity = totalQuantity;
        }
    }
}
