namespace Aucy.Domain.Entities;

/// <summary>
/// Player inventory.
/// Items from inventory can be sold (by making lots).
/// </summary>
public class Inventory
{
    /// <summary>
    /// Equals UserId, one-to-one relationship with User.
    /// </summary>
    public Guid Id { get; set; }
    public long Money { get; set; }

    /// <summary>
    /// Simple disordered collection of items, grouped by configs
    /// </summary>
    public List<ItemStack> ItemStacks { get; set; } = [];

    public int Capacity { get; set; }
    public int UsedCapacity { get; set; }
    public float WeightLimit { get; set; }
    public float UsedWeight { get; set; }
}
