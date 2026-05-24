using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class GetLotsDto
{
    [Key(0)] public string ItemDefinition { get; set; }
    [Key(1)] public string ItemName { get; set; }
    [Key(2)] public int Page { get; set; } = 1;
    [Key(3)] public int PageSize { get; set; } = 10;

    public GetLotsDto(string itemDefinition, string itemName, int page, int pageSize)
    {
        ItemDefinition = itemDefinition;
        ItemName = itemName;
        Page = page;
        PageSize = pageSize;
    }
}