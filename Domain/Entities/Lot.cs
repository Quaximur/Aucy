namespace Aucy.Domain.Entities;

public class Lot
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid ItemConfigId { get; set; }

    /// <summary>
    /// The config of the item, which is being sold in this lot. 
    /// </summary>
    public ItemConfig? ItemConfig { get; set; }
    public long UnitPrice { get; set; }
    public int TotalQuantity { get; set; }
    public int SoldQuantity => TotalQuantity - RemainingQuantity;
    public int RemainingQuantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Create lot with an existing ItemConfig.
    /// ItemConfig field is not included in constructoring.
    /// </summary>
    public static Lot Create(Guid ownerUserId, Guid itemConfigId,
        long unitPrice, int totalQuantity)
    {
        return new Lot
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            ItemConfigId = itemConfigId,
            UnitPrice = unitPrice,
            TotalQuantity = totalQuantity,
            RemainingQuantity = totalQuantity
        };
    }
}
