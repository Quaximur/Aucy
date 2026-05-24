using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class GetItemWithLotsAndBidsDto
{
    [Key(0)] public string ItemId { get; set; }

    public GetItemWithLotsAndBidsDto(string itemId)
    {
        ItemId = itemId;
    }
}
