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
    // ItemConfig + ItemConfigAmount -> ItemStack (Does it has sense?)
    public ItemConfig? ItemConfig { get; set; } 
	public long ItemConfigPrice { get; set; }
	public int ItemConfigAmount { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	public static Lot Create(Guid ownerUserId, Guid itemConfigId, ItemConfig itemConfig, 
        long itemConfigPrice, int itemConfigAmount)
    {
        return new Lot
        {
            OwnerUserId = ownerUserId,
            ItemConfigId = itemConfigId,
            ItemConfig = itemConfig,
            ItemConfigPrice = itemConfigPrice,
            ItemConfigAmount = itemConfigAmount,
        };
    }
}
