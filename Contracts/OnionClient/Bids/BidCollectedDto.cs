using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Add ItemStacks (CollectedQuantity).
    /// Add UsedWeight.
    /// Mb add +1 UsedCapacity if ItemStack is not presented in the Inventory.
    /// Substract CollectedQuantity from Total quantity or delete the Bid if it will be 0/0 quantity.
    /// </summary>
    [MessagePackObject]
    public class BidCollectedDto
    {
        [Key(0)] public Guid BidId { get; set; }
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public int CollectedQuantity { get; set; }

        public BidCollectedDto(Guid bidId, ItemConfigDto itemConfig, int collectedQuantity)
        {
            BidId = bidId;
            ItemConfig = itemConfig;
            CollectedQuantity = collectedQuantity;
        }
    }
}
