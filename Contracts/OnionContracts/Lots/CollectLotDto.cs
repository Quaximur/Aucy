using MessagePack;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class CollectLotDto
    {
        [Key(0)] public Guid LotId { get; set; }
        [Key(1)] public int Quantity { get; set; } // Less or equal than sold quantity

        public CollectLotDto(Guid lotId, int quantity)
        {
            LotId = lotId;
            Quantity = quantity;
        }
    }
}
