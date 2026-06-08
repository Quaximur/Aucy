using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
	/// <summary>
	/// Presented in common list of user bids (no total/bought quantity - only remaining)
	/// </summary>
	/// TODO: mb redundant. Use simple array of structs (Bids and Lots) for displaying 
	[MessagePackObject]
    public class BidDto
    {
        [Key(0)] public Guid Id { get; set; }
        [Key(1)] public Guid ItemConfigId { get; set; }
        [Key(2)] public Guid BidderUserId { get; set; } // mb for displaying bidder in the list of bids
        [Key(3)] public long BidPrice { get; set; }
        [Key(4)] public int RemainingQuantity { get; set; }

        public BidDto(Guid id, Guid itemConfigId, Guid bidderUserId, long bidPrice, int remainingQuantity)
        {
            Id = id;
            ItemConfigId = itemConfigId;
            BidderUserId = bidderUserId;
            BidPrice = bidPrice;
            RemainingQuantity = remainingQuantity;
        }
    }
}