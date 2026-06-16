using System;

namespace Aucy.Contracts.OnionClient
{
    public class LotCreatedDto
    {
        // Remove TotalQuantity ItemStacks with ItemConfig.Id, decrease Weight and mb UsedCapacity
        public Guid LotId { get; set; }
        public ItemConfigDto ItemConfig { get; set; }
        public long UnitPrice { get; set; }
        public int TotalQuantity { get; set; }

        public LotCreatedDto(Guid lotId, ItemConfigDto itemConfig, long unitPrice, int totalQuantity)
        {
            LotId = lotId;
            ItemConfig = itemConfig;
            UnitPrice = unitPrice;
            TotalQuantity = totalQuantity;
        }
    }
}
