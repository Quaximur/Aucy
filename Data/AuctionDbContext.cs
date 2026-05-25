using Aucy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Data;

public class AuctionDbContext : DbContext
{
    public DbSet<Lot> Lots { get; set; }
    public DbSet<Bid> Bids { get; set; }
    public DbSet<ItemConfig> ItemConfigs { get; set; }

    public AuctionDbContext(DbContextOptions<AuctionDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Lot>()
            .HasKey(x => x.Id);

        builder.Entity<Lot>()
            .HasOne(x => x.ItemConfig)
            .WithMany()
            .HasForeignKey(x => x.ItemConfigId);

        builder.Entity<Bid>()
            .HasKey(x => x.Id);

        builder.Entity<Bid>()
            .HasOne(x => x.ItemConfig)
            .WithMany()
            .HasForeignKey(x => x.ItemConfigId);

        builder.Entity<ItemConfig>()
            .HasKey(x => x.Id);

        builder.Entity<ItemConfig>()
            .HasMany(x => x.Lots)
            .WithOne()
            .HasForeignKey(x => x.Id);

        builder.Entity<ItemConfig>()
            .HasMany(x => x.Bids)
            .WithOne()
            .HasForeignKey(x => x.Id);
    }
}