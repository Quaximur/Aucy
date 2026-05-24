using Aucy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Data;

public class AuctionContext : DbContext
{
    public DbSet<Lot> Lots { get; set; }
    public DbSet<Bid> Bids { get; set; }
    public DbSet<Item> Items { get; set; }

    public AuctionContext(DbContextOptions<AuctionContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}