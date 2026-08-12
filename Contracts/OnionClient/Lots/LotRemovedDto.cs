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
        [Key(1)] public Guid ItemConfigId { get; set; }

        /// <summary>
        /// Unary ItemConfig. null if NonUnary
        /// </summary>
        [Key(2)] public ItemConfigDto ItemConfig { get; set; }
        [Key(3)] public int RemovedQuantity { get; set; }

        /// <summary>
        /// Could be either positive or negative. Add it to UsedWeight to apply.
        /// </summary>
        [Key(4)] public float WeightDiff { get; set; }
        
        public LotRemovedDto(Guid lotId, Guid itemConfigId, ItemConfigDto itemConfig, int removedQuantity, float weightDiff)
        {
            LotId = lotId;
            ItemConfigId = itemConfigId;
            ItemConfig = itemConfig;
            RemovedQuantity = removedQuantity;
            WeightDiff = weightDiff;
        }
    }
}
