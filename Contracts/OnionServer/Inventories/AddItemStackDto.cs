using Aucy.Contracts.OnionClient;
using MessagePack;
using System;

namespace Aucy.Contracts.OnionServer
{
    [MessagePackObject]
    public class AddItemStackDto
    {
        [Key(0)] public Guid ItemConfigId { get; set; }

        /// <summary>
        /// Sometimes it could be optional. Server can already have a specified Config.
        /// Use it with Unary items or items that don`t persist in the DataBase.
        /// </summary>
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public int Quantity { get; set; }

        public AddItemStackDto(Guid itemConfigId, ItemConfigDto itemConfig, int quantity)
        {
            ItemConfigId = itemConfigId;
            ItemConfig = itemConfig;
            Quantity = quantity;
        }
    }
}
