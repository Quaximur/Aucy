using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class LotDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string OwnerUserId { get; set; }
    [Key(2)] public ItemDto Item { get; set; }
    [Key(3)] public long ItemPrice { get; set; }
    [Key(4)] public int ItemAmount { get; set; }
    [Key(5)] public DateTime CreatedAt { get; set; }

    public LotDto(string id, string ownerUserId, ItemDto item, long itemPrice, int itemAmount, DateTime createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Item = item;
        ItemPrice = itemPrice;
        ItemAmount = itemAmount;
        CreatedAt = createdAt;
    }
}