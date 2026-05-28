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

    /// <summary>
    /// ItemConfig type definition. Example: "golden_axe", "wheat", "iron_ore". 
    /// This field is used to determine which Definition to use for rendering itemConfig in the frontend 
    /// and for other purposes. It should be unique for each itemConfig type.
    /// </summary>
    public required string Definition { get; set; }
    public required string Name { get; set; }
    public List<Lot> Lots { get; set; } = [];
    public List<Bid> Bids { get; set; } = [];
}
