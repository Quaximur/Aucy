using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class LotDto
    {
        [Key(0)] public string Id { get; set; }
        [Key(1)] public string OwnerUserId { get; set; }
        [Key(2)] public string ItemConfigId { get; set; }
        [Key(3)] public long UnitPrice { get; set; }
        [Key(4)] public int ItemQuantity { get; set; }
        [Key(5)] public DateTime CreatedAt { get; set; }

        public LotDto(string id, string ownerUserId, string itemConfigId, long unitPrice, int itemQuantity, DateTime createdAt)
        {
            Id = id;
            OwnerUserId = ownerUserId;
            ItemConfigId = itemConfigId;
            UnitPrice = unitPrice;
            ItemQuantity = itemQuantity;
            CreatedAt = createdAt;
        }
    }
}