using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class LotCreatedDto
    {
        // Remove TotalQuantity ItemStacks with ItemConfig.Id, add WeightDiff to UsedWeight and mb increase UsedCapacity
        [Key(0)] public Guid LotId { get; set; }
        [Key(1)] public Guid ItemConfigId { get; set; }

        /// <summary>
        /// Unary ItemConfig. null if NonUnary
        /// </summary>
        [Key(2)] public ItemConfigDto ItemConfig { get; set; }
        [Key(3)] public long UnitPrice { get; set; }
        [Key(4)] public int TotalQuantity { get; set; }

        /// <summary>
        /// Could be either positive or negative. Add it to UsedWeight to apply.
        /// </summary>
        [Key(5)] public float WeightDiff { get; set; }
        [Key(6)] public DateTime CreatedAt { get; set; }

        public LotCreatedDto(Guid lotId, Guid itemConfigId, ItemConfigDto itemConfig, long unitPrice, 
            int totalQuantity, float weightDiff, DateTime createdAt)
        {
            LotId = lotId;
            ItemConfigId = itemConfigId;
            ItemConfig = itemConfig;
            UnitPrice = unitPrice;
            TotalQuantity = totalQuantity;
            WeightDiff = weightDiff;
            CreatedAt = createdAt;
        }
    }
}
