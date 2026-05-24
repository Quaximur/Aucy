namespace Aucy.Domain.Entities;

public class Lot
{
	public Guid Id { get; set; }
	public Guid OwnerUserId { get; set; }
	public Guid ItemId { get; set; }
	public Item? Item { get; set; }
	public long ItemPrice { get; set; }
	public int ItemAmount { get; set; }
	public DateTime CreatedAt { get; set; }
	public List<Bid>? Bids { get; set; }
}
