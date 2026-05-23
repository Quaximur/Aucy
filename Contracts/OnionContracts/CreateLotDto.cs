using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class CreateLotDto
{
    [Key(0)] public Guid OwnerUserId { get; set; }
    [Key(1)] public Guid ItemId { get; set; }
    [Key(2)] public string ItemType { get; set; }
    [Key(3)] public string ItemName { get; set; }
    [Key(4)] public ulong ItemPrice { get; set; }
    [Key(5)] public int ItemAmount { get; set; }

    public CreateLotDto(Guid ownerUserId, Guid itemId, string itemType, string itemName, ulong itemPrice, int itemAmount)
    {
        OwnerUserId = ownerUserId;
        ItemId = itemId;
        ItemType = itemType;
        ItemName = itemName;
        ItemPrice = itemPrice;
        ItemAmount = itemAmount;
    }
}
