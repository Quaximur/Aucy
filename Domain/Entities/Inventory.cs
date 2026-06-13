using System.ComponentModel.DataAnnotations;

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

    [Range(0, long.MaxValue)]
    public long Money { get; set; }

    /// <summary>
    /// Simple disordered collection of items, grouped by configs
    /// </summary>
    public List<ItemStack> ItemStacks { get; set; } = [];

    [Range(0, int.MaxValue)]
    public int Capacity { get; set; }

    [Range(0, int.MaxValue)]
    public int UsedCapacity { get; set; }

    [Range(0, float.MaxValue)]
    public float WeightLimit { get; set; }

    [Range(0, float.MaxValue)]
    public float UsedWeight { get; set; }
}
