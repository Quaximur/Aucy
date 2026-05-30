using Aucy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Data;

public class AucyDbContext : DbContext
{
    public DbSet<Lot> Lots { get; set; }
    public DbSet<Bid> Bids { get; set; }
    public DbSet<ItemConfig> ItemConfigs { get; set; }
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<ItemStack> ItemStacks { get; set; }

    public AucyDbContext(DbContextOptions<AucyDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Lot configuration
        builder.Entity<Lot>()
            .HasKey(x => x.Id);

        builder.Entity<Lot>()
            .HasOne(x => x.ItemConfig)
            .WithMany(x => x.Lots)
            .HasForeignKey(x => x.ItemConfigId);

        // Bid configuration
        builder.Entity<Bid>()
            .HasKey(x => x.Id);

        builder.Entity<Bid>()
            .HasOne(x => x.ItemConfig)
            .WithMany(x => x.Bids)
            .HasForeignKey(x => x.ItemConfigId);

        // ItemConfig configuration
        builder.Entity<ItemConfig>()
            .HasKey(x => x.Id);

        // ItemStack
        builder.Entity<ItemStack>()
            .HasKey(x => x.Id);

        // Inventory
        builder.Entity<Inventory>()
             .HasKey(x => x.Id);

        builder.Entity<Inventory>()
            .HasMany(x => x.ItemStacks)
            .WithOne()
            .HasForeignKey(x => x.InventoryId);
    }
}