using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class ItemStackDto
    {
        [Key(0)] public ItemConfigDto ItemConfig { get; set; }
        [Key(1)] public int Quantity { get; set; }
    }
}
