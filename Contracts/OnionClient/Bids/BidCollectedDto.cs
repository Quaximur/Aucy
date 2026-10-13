using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Add ItemStacks (CollectedQuantity).
    /// Add WeightDiff to UsedWeight.
    /// Mb add +1 UsedCapacity if ItemStack is not presented in the Inventory.
    /// Substract CollectedQuantity from Total quantity or delete the Bid if it will be 0/0 quantity.
    /// </summary>
    [MessagePackObject]
    public class BidCollectedDto
    {
        [Key(0)] public Guid BidId { get; set; }
        [Key(1)] public Guid ItemConfigId { get; set; }

        /// <summary>
        /// Unary ItemConfig. null if NonUnary
        /// </summary>
        [Key(2)] public ItemConfigDto ItemConfig { get; set; }
        [Key(3)] public int CollectedQuantity { get; set; }

        /// <summary>
        /// Could be either positive or negative. Add it to UsedWeight to apply.
        /// </summary>
        [Key(4)] public float WeightDiff { get; set; }

        public BidCollectedDto(Guid bidId, Guid itemConfigId, ItemConfigDto itemConfig, 
            int collectedQuantity, float weightDiff)
        {
            BidId = bidId;
            ItemConfigId = itemConfigId;
            ItemConfig = itemConfig;
            CollectedQuantity = collectedQuantity;
            WeightDiff = weightDiff;
        }
    }
}
