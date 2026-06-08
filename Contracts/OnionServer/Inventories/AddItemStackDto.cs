using Aucy.Contracts.OnionClient;
using MessagePack;

namespace Aucy.Contracts.OnionServer
{
    [MessagePackObject]
    public class AddItemStackDto
    {
        [Key(0)] public ItemConfigDto ItemConfig { get; set; }
        [Key(1)] public int Quantity { get; set; }
    }
}
