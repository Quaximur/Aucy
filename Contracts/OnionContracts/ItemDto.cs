using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class ItemDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string Definition { get; set; }
    [Key(2)] public string Name { get; set; }

    public ItemDto(string id, string definition, string name)
    {
        Id = id;
        Definition = definition;
        Name = name;
    }
}
