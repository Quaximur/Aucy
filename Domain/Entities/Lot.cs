namespace Aucy.Domain.Entities;

public class Lot
{
	public Guid Id { get; set; }
	public Guid OwnerUserId { get; set; }
	public Guid ItemConfigId { get; set; }

    /// <summary>
    /// The config of the item, which is being sold in this lot. 
    /// </summary>
    public ItemConfig? ItemConfig { get; set; } // TODO: replace with ItemStack. Because lot has access to concrete item instances
	public long ItemConfigPrice { get; set; }
	public int ItemConfigAmount { get; set; }
	public DateTime CreatedAt { get; set; }
	public List<Bid>? Bids { get; set; }
}
