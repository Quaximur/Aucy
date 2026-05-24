using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class CreateLotDto
{
    [Key(0)] public string OwnerUserId { get; set; }
    [Key(1)] public ItemDto Item { get; set; }
    [Key(2)] public long ItemPrice { get; set; }
    [Key(3)] public int ItemAmount { get; set; }

    public CreateLotDto(string ownerUserId, ItemDto item, long itemPrice, int itemAmount)
    {
        OwnerUserId = ownerUserId;
        Item = item;
        ItemPrice = itemPrice;
        ItemAmount = itemAmount;
    }
}
