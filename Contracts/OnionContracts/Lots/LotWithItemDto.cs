using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class LotWithItemDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string OwnerUserId { get; set; }
    [Key(2)] public long ItemPrice { get; set; }
    [Key(3)] public int ItemAmount { get; set; }
    [Key(4)] public DateTime CreatedAt { get; set; }
    [Key(5)] public ItemDto Item { get; set; }

    public LotWithItemDto(string id, string ownerUserId, long itemPrice, int itemAmount, DateTime createdAt,
        ItemDto item)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        ItemPrice = itemPrice;
        ItemAmount = itemAmount;
        CreatedAt = createdAt;
        Item = item;
    }
}
