using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class BidDto
    {
        [Key(0)] public string Id { get; set; }
        [Key(1)] public string ItemConfigId { get; set; }
        [Key(2)] public string BidderUserId { get; set; }
        [Key(3)] public long BidPrice { get; set; }
        [Key(4)] public int BidAmount { get; set; }
        [Key(5)] public DateTime CreatedAt { get; set; }

        public BidDto(string id, string itemConfigId, string bidderUserId, long bidPrice, int bidAmount, DateTime createdAt)
        {
            Id = id;
            ItemConfigId = itemConfigId;
            BidderUserId = bidderUserId;
            BidPrice = bidPrice;
            BidAmount = bidAmount;
            CreatedAt = createdAt;
        }
    }
}