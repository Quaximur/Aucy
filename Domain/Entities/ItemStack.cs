namespace Aucy.Domain.Entities;

/// <summary>
/// Group of item instances. 
/// Items with the same <see cref="Config"/> (and <see cref="ConfigId"/> belongs to the same <see cref="ItemStack"/>.
/// All items are treated as groups, so atomic items don`t have unique id, but <see cref="ItemStack"/>s do.
/// </summary>
public class ItemStack
{
    public Guid Id { get; set; }
    public Guid ConfigId { get; set; }
    public ItemConfig? Config { get; set; }
    public int Quantity { get; set; }
    public Guid InventoryId { get; set; } // TODO: UNIQUE (ConfigId, InventoryId) for grouping

    /// <summary>
    /// If the item is put up for sale, it is removed from the ItemStack table, 
    /// and a new Lot is placed in the Lots table. So ItemStack won`t exist without Inventory.
    /// </summary>
    public Inventory? Inventory { get; set; } 
}
