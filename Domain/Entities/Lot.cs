using System.ComponentModel.DataAnnotations;

namespace Aucy.Domain.Entities;

public class Lot
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid ItemConfigId { get; set; }

    /// <summary>
    /// The config of the item, which is being sold in this lot. 
    /// </summary>
    public ItemConfig ItemConfig { get; set; } = null!;

    [Range(1, long.MaxValue)]
    public long UnitPrice { get; set; }

    [Range(1, int.MaxValue)]
    public int TotalQuantity { get; set; }
    public int SoldQuantity => TotalQuantity - RemainingQuantity;

    [Range(0, int.MaxValue)]
    public int RemainingQuantity { get; set; }

    /// <summary>
    /// Item can be sold with higher price than specified in <see cref="UnitPrice"/> at the moment of lot creation. 
    /// In this case, user will receive more money for the item. That difference will be stored here.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long ExtraSoldoutMoney { get; set; }

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
