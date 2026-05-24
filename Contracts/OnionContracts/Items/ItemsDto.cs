using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class ItemsDto
{
    [Key(0)] public List<ItemDto> Items { get; set; }

    public ItemsDto(List<ItemDto> items)
    {
        Items = items;
    }
}
