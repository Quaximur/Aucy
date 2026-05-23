using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class LotDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string OwnerUserId { get; set; }
    [Key(2)] public string ItemId { get; set; }
    [Key(3)] public string ItemType { get; set; }
    [Key(4)] public string ItemName { get; set; }
    [Key(5)] public ulong ItemPrice { get; set; }
    [Key(6)] public int ItemAmount { get; set; }
    [Key(7)] public DateTime CreatedAt { get; set; }


    public LotDto(string id, string ownerUserId, string itemId, string itemType, string itemName, 
        ulong itemPrice, int itemAmount, DateTime createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        ItemId = itemId;
        ItemType = itemType;
        ItemName = itemName;
        ItemPrice = itemPrice;
        ItemAmount = itemAmount;
        CreatedAt = createdAt;
    }
}