using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class ItemStackDto
    {
        [Key(0)] public Guid ItemConfigId { get; set; }

        /// <summary>
        /// Unary ItemConfig. null if NonUnary
        /// </summary>
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public int Quantity { get; set; }

        public ItemStackDto(Guid itemConfigId, ItemConfigDto itemConfig, int quantity)
        {
            ItemConfigId = itemConfigId;
            ItemConfig = itemConfig;
            Quantity = quantity;
        }
    }
}
