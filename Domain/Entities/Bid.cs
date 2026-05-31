namespace Aucy.Domain.Entities;

public class Bid
{
    public Guid Id { get; set; }
    public Guid BidderUserId { get; set; }
    public Guid ItemConfigId { get; set; }

    /// <summary>
    /// User makes a bid on some item config (which describes it`s properties), 
    /// not an instance or lot. User can buy many items of the same config.
    /// User can`t determine direct lot or item instance, 
    /// because it is determined by the system based on the availability of items and lots.
    /// </summary>
    public ItemConfig ItemConfig { get; set; } = null!;

    /// <summary>
    /// Per item Price.
    /// </summary>
    public long BidPrice { get; set; }

    /// <summary>
    /// Amount of items of the same config, which user wants to buy for the price specified in <see cref="BidPrice"/>.
    /// </summary>
    public int TotalQuantity { get; set; }
    public int BoughtQuantity => TotalQuantity - RemainingQuantity;
    public int RemainingQuantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
