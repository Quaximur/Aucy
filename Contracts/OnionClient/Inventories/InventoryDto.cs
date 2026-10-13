using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class InventoryDto
    {
        [Key(0)] public long Money { get; set; }
        [Key(1)] public ItemStackDto[] ItemStacks { get; set; }
        [Key(2)] public int Capacity { get; set; }
        [Key(3)] public int UsedCapacity { get; set; }
        [Key(4)] public float WeightLimit { get; set; }
        [Key(5)] public float UsedWeight { get; set; }

        public InventoryDto(long money, ItemStackDto[] itemStacks, int capacity, 
            int usedCapacity, float weightLimit, float usedWeight)
        {
            Money = money;
            ItemStacks = itemStacks;
            Capacity = capacity;
            UsedCapacity = usedCapacity;
            WeightLimit = weightLimit;
            UsedWeight = usedWeight;
        }
    }
}