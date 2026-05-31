using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class InventoryDto
    {
        [Key(0)] public ItemStackDto[] ItemStacks { get; set; }
        [Key(1)] public int Capacity { get; set; }
        [Key(2)] public int UsedCapacity { get; set; }
        [Key(3)] public float WeightLimit { get; set; }
    }
}