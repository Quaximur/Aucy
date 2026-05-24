namespace Aucy.Domain.Entities;

public class Item
{
    public Guid Id { get; set; }
    /// <summary>
    /// Item type definition. Example: "golden_axe", "wheat", "iron_ore". 
    /// This field is used to determine which Definition to use for rendering item in the frontend 
    /// and for other purposes. It should be unique for each item type.
    /// </summary>
    public required string Definition { get; set; }
    public required string Name { get; set; }
    public List<Lot>? Lots { get; set; }
    public List<Bid>? Bids { get; set; }
}
