using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class RemoveBidDto
    {
        [Key(0)] public Guid BidId { get; set; }

        public RemoveBidDto(Guid bidId)
        {
            BidId = bidId;
        }
    }
}
