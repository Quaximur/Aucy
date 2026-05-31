using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class AddItemStackDto
    {
        [Key(0)] public ItemConfigDto ItemConfig { get; set; }
        [Key(1)] public int Quantity { get; set; }
    }
}
