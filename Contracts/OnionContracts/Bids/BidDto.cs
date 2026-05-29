using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class BidDto
    {
        [Key(0)] public Guid Id { get; set; }
        [Key(1)] public Guid ItemConfigId { get; set; }
        [Key(2)] public Guid BidderUserId { get; set; }
        [Key(3)] public long BidPrice { get; set; }
        [Key(4)] public int BidAmount { get; set; }
        [Key(5)] public DateTime CreatedAt { get; set; }

        public BidDto(Guid id, Guid itemConfigId, Guid bidderUserId, long bidPrice, int bidAmount, DateTime createdAt)
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