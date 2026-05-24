using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class LotDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string OwnerUserId { get; set; }
    [Key(2)] public string ItemId { get; set; }
    [Key(3)] public long ItemPrice { get; set; }
    [Key(4)] public int ItemAmount { get; set; }
    [Key(5)] public DateTime CreatedAt { get; set; }

    public LotDto(string id, string ownerUserId, string itemId, long itemPrice, int itemAmount, DateTime createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        ItemId = itemId;
        ItemPrice = itemPrice;
        ItemAmount = itemAmount;
        CreatedAt = createdAt;
    }
}