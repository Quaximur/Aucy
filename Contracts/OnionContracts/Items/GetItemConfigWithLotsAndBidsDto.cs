using MessagePack;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class GetItemConfigWithLotsAndBidsDto
    {
        [Key(0)] public Guid ItemConfigId { get; set; }

        public GetItemConfigWithLotsAndBidsDto(Guid itemConfigId)
        {
            ItemConfigId = itemConfigId;
        }
    }
}
