using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class CreateInventoryDto
    {
        [Key(0)] public int Capacity { get; set; }
        [Key(1)] public float WeightLimit { get; set; }
    }
}
