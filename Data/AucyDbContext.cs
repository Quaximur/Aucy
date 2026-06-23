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

        builder.Entity<Lot>()
            .HasIndex(x => new // Should be grouped
            {
                x.ItemConfigId,
                x.OwnerUserId
            }).IsUnique();

        // Bid configuration
        builder.Entity<Bid>()
            .HasKey(x => x.Id);

        builder.Entity<Bid>()
            .HasOne(x => x.ItemConfig)
            .WithMany(x => x.Bids)
            .HasForeignKey(x => x.ItemConfigId);

        builder.Entity<Bid>()
            .HasIndex(x => new // Should be grouped
            {
                x.ItemConfigId,
                x.BidderUserId
            }).IsUnique();

        // ItemConfig configuration
        builder.Entity<ItemConfig>()
            .HasKey(x => x.Id);

        // ItemStack
        builder.Entity<ItemStack>()
            .HasKey(x => x.Id);

        builder.Entity<ItemStack>()
            .HasIndex(x => new // Should be grouped
            {
                x.ConfigId,
                x.InventoryId
            })
            .IsUnique();

        builder.Entity<ItemStack>()
            .HasOne(x => x.Config)
            .WithMany()
            .HasForeignKey(x => x.ConfigId);

        // Inventory
        builder.Entity<Inventory>()
             .HasKey(x => x.Id);

        builder.Entity<Inventory>()
            .HasMany(x => x.ItemStacks)
            .WithOne(x => x.Inventory)
            .HasForeignKey(x => x.InventoryId);

        // Check constraints
        builder.HasCheckConstraint<Lot>("UnitPrice", ">= 1");
        builder.HasCheckConstraint<Lot>("TotalQuantity", ">= 1");
        builder.HasCheckConstraint<Lot>("RemainingQuantity", ">= 0");
        builder.HasCheckConstraint<Lot>("ExtraSoldoutMoney", ">= 0");

        builder.HasCheckConstraint<Bid>("BidPrice", ">= 1");
        builder.HasCheckConstraint<Bid>("TotalQuantity", ">= 1");
        builder.HasCheckConstraint<Bid>("RemainingQuantity", ">= 0");

        builder.HasCheckConstraint<ItemConfig>("MinPrice", ">= 1");
        builder.HasCheckConstraint<ItemConfig>("MinPrice", "<= [MaxPrice]");
        builder.HasCheckConstraint<ItemConfig>("MaxPrice", ">= 1");
        builder.HasCheckConstraint<ItemConfig>("MaxPrice", $"<= {1_000_000_000_000_000}");

        builder.HasCheckConstraint<ItemStack>("Quantity", ">= 1");

        builder.HasCheckConstraint<Inventory>("Money", ">= 0");
        builder.HasCheckConstraint<Inventory>("Capacity", ">= 0");
        builder.HasCheckConstraint<Inventory>("UsedCapacity", ">= 0");
        builder.HasCheckConstraint<Inventory>("UsedCapacity", "<= [Capacity]");
        builder.HasCheckConstraint<Inventory>("WeightLimit", ">= 0");
        builder.HasCheckConstraint<Inventory>("UsedWeight", ">= 0");
        builder.HasCheckConstraint<Inventory>("UsedWeight", "<= [WeightLimit]");
    }
}
