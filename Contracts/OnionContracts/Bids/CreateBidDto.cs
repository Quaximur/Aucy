using MessagePack;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class CreateBidDto
    {
        [Key(0)] public Guid ItemConfigId { get; set; }
        [Key(1)] public Guid BidderUserId { get; set; }
        [Key(2)] public long BidPrice { get; set; }
        [Key(3)] public int TotalQuantity { get; set; }

        public CreateBidDto(Guid itemConfigId, Guid bidderUserId, long bidPrice, int totalAmount)
        {
            ItemConfigId = itemConfigId;
            BidderUserId = bidderUserId;
            BidPrice = bidPrice;
            TotalQuantity = totalAmount;
        }
    }
}