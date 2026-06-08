using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class CollectBidDto
    {
        [Key(0)] public Guid BidId { get; set; }
        [Key(1)] public int Quantity { get; set; } // Less or equal than bought quantity

        public CollectBidDto(Guid bidId, int quantity)
        {
            BidId = bidId;
            Quantity = quantity;
        }
    }
}
