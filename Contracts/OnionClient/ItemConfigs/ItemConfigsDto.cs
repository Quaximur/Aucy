using MessagePack;
using System.Collections.Generic;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class ItemConfigsDto
    {
        [Key(0)] public List<ItemConfigDto> ItemConfigs { get; set; }

        public ItemConfigsDto(List<ItemConfigDto> itemConfigs)
        {
            ItemConfigs = itemConfigs;
        }
    }
}
