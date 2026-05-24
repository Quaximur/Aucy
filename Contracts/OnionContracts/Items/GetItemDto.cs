using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class GetItemDto
{
    [Key(0)] public string ItemId { get; set; }

    public GetItemDto(string itemId)
    {
        ItemId = itemId;
    }
}
