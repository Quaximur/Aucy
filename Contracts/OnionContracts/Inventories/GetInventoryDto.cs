using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class GetInventoryDto
    {
        [Key(0)] public int Page { get; set; }
        [Key(1)] public int PageSize { get; set; }
    }
}