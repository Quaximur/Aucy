using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class GetItemConfigDto
    {
        [Key(0)] public Guid ItemConfigId { get; set; }

        public GetItemConfigDto(Guid itemConfigId)
        {
            ItemConfigId = itemConfigId;
        }
    }
}
