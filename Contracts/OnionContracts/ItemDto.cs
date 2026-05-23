using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class ItemDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string ItemType { get; set; }
    [Key(2)] public string ItemName { get; set; }

    public ItemDto(string id, string itemType, string itemName)
    {
        Id = id;
        ItemType = itemType;
        ItemName = itemName;
    }
}
