using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Add or Create RemovedQuantity of ItemStacks with ItemConfig.Id.
    /// Add ItemConfig.Weight * RemovedQuantity to Inventory.UsedWeight.
    /// Mb add +1 UsedCapacity if ItemStack is not presented in the Inventory.
    /// </summary>
    [MessagePackObject]
    public class LotRemovedDto
    {
        [Key(0)] public Guid LotId { get; set; }
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public int RemovedQuantity { get; set; } 

        public LotRemovedDto(Guid lotId, ItemConfigDto itemConfig, int removedQuantity)
        {
            LotId = lotId;
            ItemConfig = itemConfig;
            RemovedQuantity = removedQuantity;
        }
    }
}
