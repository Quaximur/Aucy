using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aucy.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConstraintsFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_ExtraSoldoutMoney",
                table: "Lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_RemainingQuantity",
                table: "Lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_TotalQuantity",
                table: "Lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_UnitPrice",
                table: "Lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_Quantity",
                table: "ItemStacks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_MaxBuyQuantity",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_MaxPrice",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_MaxSellQuantity",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_MinPrice",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_Capacity",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_Money",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_UsedCapacity",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_UsedWeight",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_WeightLimit",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_BidPrice",
                table: "Bids");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_RemainingQuantity",
                table: "Bids");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TEntity_TotalQuantity",
                table: "Bids");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Lot_ExtraSoldoutMoney",
                table: "Lots",
                sql: "\"ExtraSoldoutMoney\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Lot_RemainingQuantity",
                table: "Lots",
                sql: "\"RemainingQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Lot_TotalQuantity",
                table: "Lots",
                sql: "\"TotalQuantity\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Lot_UnitPrice",
                table: "Lots",
                sql: "\"UnitPrice\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemStack_Quantity",
                table: "ItemStacks",
                sql: "\"Quantity\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemConfig_MaxBuyQuantity_bottom",
                table: "ItemConfigs",
                sql: "\"MaxBuyQuantity\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemConfig_MaxBuyQuantity_top",
                table: "ItemConfigs",
                sql: "\"MaxBuyQuantity\" <= 5000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemConfig_MaxPrice_bottom",
                table: "ItemConfigs",
                sql: "\"MaxPrice\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemConfig_MaxPrice_top",
                table: "ItemConfigs",
                sql: "\"MaxPrice\" <= 1000000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemConfig_MaxSellQuantity_bottom",
                table: "ItemConfigs",
                sql: "\"MaxSellQuantity\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemConfig_MaxSellQuantity_top",
                table: "ItemConfigs",
                sql: "\"MaxSellQuantity\" <= 5000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemConfig_MinPrice_bottom",
                table: "ItemConfigs",
                sql: "\"MinPrice\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemConfig_MinPrice_top",
                table: "ItemConfigs",
                sql: "\"MinPrice\" <= \"MaxPrice\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_Capacity",
                table: "Inventories",
                sql: "\"Capacity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_Money",
                table: "Inventories",
                sql: "\"Money\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_UsedCapacity_bottom",
                table: "Inventories",
                sql: "\"UsedCapacity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_UsedCapacity_top",
                table: "Inventories",
                sql: "\"UsedCapacity\" <= \"Capacity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_UsedWeight_bottom",
                table: "Inventories",
                sql: "\"UsedWeight\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_UsedWeight_top",
                table: "Inventories",
                sql: "\"UsedWeight\" <= \"WeightLimit\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_WeightLimit",
                table: "Inventories",
                sql: "\"WeightLimit\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bid_BidPrice",
                table: "Bids",
                sql: "\"BidPrice\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bid_RemainingQuantity",
                table: "Bids",
                sql: "\"RemainingQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bid_TotalQuantity",
                table: "Bids",
                sql: "\"TotalQuantity\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Lot_ExtraSoldoutMoney",
                table: "Lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Lot_RemainingQuantity",
                table: "Lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Lot_TotalQuantity",
                table: "Lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Lot_UnitPrice",
                table: "Lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemStack_Quantity",
                table: "ItemStacks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemConfig_MaxBuyQuantity_bottom",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemConfig_MaxBuyQuantity_top",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemConfig_MaxPrice_bottom",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemConfig_MaxPrice_top",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemConfig_MaxSellQuantity_bottom",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemConfig_MaxSellQuantity_top",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemConfig_MinPrice_bottom",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemConfig_MinPrice_top",
                table: "ItemConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_Capacity",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_Money",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_UsedCapacity_bottom",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_UsedCapacity_top",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_UsedWeight_bottom",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_UsedWeight_top",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_WeightLimit",
                table: "Inventories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bid_BidPrice",
                table: "Bids");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bid_RemainingQuantity",
                table: "Bids");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bid_TotalQuantity",
                table: "Bids");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_ExtraSoldoutMoney",
                table: "Lots",
                sql: "\"ExtraSoldoutMoney\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_RemainingQuantity",
                table: "Lots",
                sql: "\"RemainingQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_TotalQuantity",
                table: "Lots",
                sql: "\"TotalQuantity\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_UnitPrice",
                table: "Lots",
                sql: "\"UnitPrice\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_Quantity",
                table: "ItemStacks",
                sql: "\"Quantity\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_MaxBuyQuantity",
                table: "ItemConfigs",
                sql: "\"MaxBuyQuantity\" <= 5000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_MaxPrice",
                table: "ItemConfigs",
                sql: "\"MaxPrice\" <= 1000000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_MaxSellQuantity",
                table: "ItemConfigs",
                sql: "\"MaxSellQuantity\" <= 5000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_MinPrice",
                table: "ItemConfigs",
                sql: "\"MinPrice\" <= \"MaxPrice\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_Capacity",
                table: "Inventories",
                sql: "\"Capacity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_Money",
                table: "Inventories",
                sql: "\"Money\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_UsedCapacity",
                table: "Inventories",
                sql: "\"UsedCapacity\" <= \"Capacity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_UsedWeight",
                table: "Inventories",
                sql: "\"UsedWeight\" <= \"WeightLimit\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_WeightLimit",
                table: "Inventories",
                sql: "\"WeightLimit\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_BidPrice",
                table: "Bids",
                sql: "\"BidPrice\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_RemainingQuantity",
                table: "Bids",
                sql: "\"RemainingQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TEntity_TotalQuantity",
                table: "Bids",
                sql: "\"TotalQuantity\" >= 1");
        }
    }
}
