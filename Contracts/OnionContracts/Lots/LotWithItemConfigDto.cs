using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class LotWithItemConfigDto
    {
        [Key(0)] public string Id { get; set; }
        [Key(1)] public string OwnerUserId { get; set; }
        [Key(2)] public long UnitPrice { get; set; }
        [Key(3)] public int ItemQuantity { get; set; }
        [Key(4)] public DateTime CreatedAt { get; set; }
        [Key(5)] public ItemConfigDto ItemConfig { get; set; }

        public LotWithItemConfigDto(string id, string ownerUserId, long unitPrice, int itemQuantity, DateTime createdAt,
            ItemConfigDto itemConfig)
        {
            Id = id;
            OwnerUserId = ownerUserId;
            UnitPrice = unitPrice;
            ItemQuantity = itemQuantity;
            CreatedAt = createdAt;
            ItemConfig = itemConfig;
        }
    }
}
