namespace Aucy.Domain.Entities;

public class Lot
{
	public Guid Id { get; set; }
	public Guid OwnerUserId { get; set; }
	public Guid ItemConfigId { get; set; }

    /// <summary>
    /// The config of the item, which is being sold in this lot. 
    /// </summary>
    // TODO: replace with ItemStack? Because lot has access to concrete item instances
    // ItemConfig + ItemQuantity -> ItemStack (Does it has sense?)
    public ItemConfig? ItemConfig { get; set; } 
	public long UnitPrice { get; set; }
	public int ItemQuantity { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public static Lot Create(Guid ownerUserId, ItemConfig itemConfig, 
        long unitPrice, int itemQuantity)
    {
        return new Lot
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            ItemConfigId = itemConfig.Id,
            ItemConfig = itemConfig,
            UnitPrice = unitPrice,
            ItemQuantity = itemQuantity,
        };
    }

    public static Lot Create(Guid ownerUserId, Guid itemConfigId,
        long unitPrice, int itemQuantity)
    {
        return new Lot
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            ItemConfigId = itemConfigId,
            UnitPrice = unitPrice,
            ItemQuantity = itemQuantity,
        };
    }
}
