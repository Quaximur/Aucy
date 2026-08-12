using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class CreateBidDto
    {
        [Key(0)] public Guid ItemConfigId { get; set; }
        [Key(1)] public long BidPrice { get; set; }
        [Key(2)] public int TotalQuantity { get; set; }

        public CreateBidDto(Guid itemConfigId, long bidPrice, int totalQuantity)
        {
            ItemConfigId = itemConfigId;
            BidPrice = bidPrice;
            TotalQuantity = totalQuantity;
        }
    }
}