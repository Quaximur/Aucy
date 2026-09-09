using System.ComponentModel.DataAnnotations;

namespace Aucy.Domain.Entities;

/// <summary>
/// Item configuration. 
/// It is used to store common information, which can be used by multiple item instances.
/// It is bound with Lots and Bids in auction scope for searching and filtering purposes.
/// </summary>
public class ItemConfig
{
    /// <summary>
    /// ItemConfig identification for aggregation of it`s template and possible unique properties. 
    /// It is unique for each config, but one ItemConfig.Id can belong to many itemConfig instances.
    /// </summary>
    public Guid Id { get; set; }
    public required string Name { get; set; }

    /// <summary>
    /// Should item be presented in stack? If it is unary it always has mo more than 1 lot and can be buyout without bids.
    /// </summary>
    public bool IsUnary { get; set; }
    public required string Icon { get; set; }
    public required int Grade { get; set; }
    public required int Rarity { get; set; }

    [Range(0, float.MaxValue)]
    public float Weight { get; set; }

    /// <summary>
    /// ItemConfig type baseType. Example: "golden_axe", "wheat", "iron_ore". 
    /// This field is used to determine which BaseType to use for rendering itemConfig in the frontend 
    /// and for other purposes.
    /// </summary>
    public required string BaseType { get; set; }

    /// <summary>
    /// Stats, modifiers, marks, curses, or any other properties that could differ based on domain.
    /// </summary>
    public List<string> Extra { get; set; } = [];
    public required string CategoryPath { get; set; }

    [Range(1, 1_000_000_000_000_000)]
    public long MinPrice { get; set; }

    [Range(1, 1_000_000_000_000_000)]
    public long MaxPrice { get; set; }

    [Range(1, 5_000)]
    public int MaxSellQuantity { get; set; }

    [Range(1, 5_000)]
    public int MaxBuyQuantity { get; set; }

    [Range(1, 5_000)]
    public int PriceTierCount { get; set; }

    public List<Lot> Lots { get; set; } = [];
    public List<Bid> Bids { get; set; } = [];
    public List<ItemStack> ItemStacks { get; set; } = [];

    public static ItemConfig Create(Guid id,
        string name,
        bool isUnary,
        string icon,
        int grade,
        int rarity,
        float weight,
        string baseType,
        List<string>? extra,
        string categoryPath,
        long minPrice,
        long maxPrice,
        int maxSellQuantity,
        int maxBuyQuantity,
        int priceTierCount)
    {
        return new ItemConfig
        {
            Id = id,
            Name = name,
            IsUnary = isUnary,
            Icon = icon,
            Grade = grade,
            Rarity = rarity,
            Weight = weight,
            BaseType = baseType,
            Extra = extra ?? [],
            CategoryPath = categoryPath,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            MaxSellQuantity = maxSellQuantity,
            MaxBuyQuantity = maxBuyQuantity,
            PriceTierCount = priceTierCount
        };
    }
}
