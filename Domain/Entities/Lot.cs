namespace Aucy.Domain.Entities;

public class Lot
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public ulong Price { get; set; }
    public required string ItemName { get; set; }
    public required string ItemType { get; set; }
    public DateTime CreatedAt { get; set; }
}
