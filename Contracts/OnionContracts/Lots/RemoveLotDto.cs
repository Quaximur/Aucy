using MessagePack;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class RemoveLotDto
    {
        [Key(0)] public Guid LotId { get; set; }

        public RemoveLotDto(Guid lotId)
        {
            LotId = lotId;
        }
    }
}
