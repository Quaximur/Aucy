namespace Aucy.Domain.Entities;

public class Bid
{
    public Guid Id { get; set; }
    public Guid BidderUserId { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    public long BidPrice { get; set; }
    public int BidAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
