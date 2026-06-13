using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class GetInventoryDto
    {
        [Key(0)] public int Page { get; set; }
        [Key(1)] public int PageSize { get; set; }
    }
}