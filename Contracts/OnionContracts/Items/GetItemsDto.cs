using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class GetItemsDto
{
    [Key(0)] public string DefinitionFilter { get; set; }
    [Key(1)] public string NameFilter { get; set; }
    [Key(2)] public int Page { get; set; }
    [Key(3)] public int PageSize { get; set; }

    public GetItemsDto(string definitionFilter, string nameFilter, int page, int pageSize)
    {
        DefinitionFilter = definitionFilter;
        NameFilter = nameFilter;
        Page = page;
        PageSize = pageSize;
    }
}
