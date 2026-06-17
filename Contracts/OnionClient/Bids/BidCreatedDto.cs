using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Substract TotalQuantity * BidPrice from the Money.
    /// Create Bid for the ItemConfig.
    /// </summary>
    [MessagePackObject]
    public class BidCreatedDto
    {
        [Key(0)] public Guid BidId { get; set; }
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public long BidPrice { get; set; }
        [Key(3)] public int TotalQuantity { get; set; }
        [Key(4)] public DateTime CreatedAt { get; set; }

        public BidCreatedDto(Guid bidId, ItemConfigDto itemConfig, long bidPrice, int totalQuantity, DateTime createdAt)
        {
            BidId = bidId;
            ItemConfig = itemConfig;
            BidPrice = bidPrice;
            TotalQuantity = totalQuantity;
            CreatedAt = createdAt;
        }
    }
}
