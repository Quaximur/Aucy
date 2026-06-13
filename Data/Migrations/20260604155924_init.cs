using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aucy.Data.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Money = table.Column<long>(type: "bigint", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    UsedCapacity = table.Column<int>(type: "int", nullable: false),
                    WeightLimit = table.Column<float>(type: "real", nullable: false),
                    UsedWeight = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                    table.CheckConstraint("CK_TEntity_Capacity", "\"Capacity\" >= 0");
                    table.CheckConstraint("CK_TEntity_Money", "\"Money\" >= 0");
                    table.CheckConstraint("CK_TEntity_UsedCapacity", "\"UsedCapacity\" <= [Capacity]");
                    table.CheckConstraint("CK_TEntity_UsedWeight", "\"UsedWeight\" <= [WeightLimit]");
                    table.CheckConstraint("CK_TEntity_WeightLimit", "\"WeightLimit\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ItemConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsUnary = table.Column<bool>(type: "bit", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Grade = table.Column<int>(type: "int", nullable: true),
                    Rarity = table.Column<int>(type: "int", nullable: true),
                    Weight = table.Column<float>(type: "real", nullable: false),
                    BaseType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Extra = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryPath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidPrice = table.Column<long>(type: "bigint", nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    RemainingQuantity = table.Column<int>(type: "int", nullable: false),
                    IsMatched = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.CheckConstraint("CK_TEntity_BidPrice", "\"BidPrice\" >= 1");
                    table.CheckConstraint("CK_TEntity_RemainingQuantity", "\"RemainingQuantity\" >= 0");
                    table.CheckConstraint("CK_TEntity_TotalQuantity", "\"TotalQuantity\" >= 1");
                    table.ForeignKey(
                        name: "FK_Bids_ItemConfigs_ItemConfigId",
                        column: x => x.ItemConfigId,
                        principalTable: "ItemConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemStacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    InventoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemStacks", x => x.Id);
                    table.CheckConstraint("CK_TEntity_Quantity", "\"Quantity\" >= 1");
                    table.ForeignKey(
                        name: "FK_ItemStacks_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemStacks_ItemConfigs_ConfigId",
                        column: x => x.ConfigId,
                        principalTable: "ItemConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemStacks_ItemConfigs_ItemConfigId",
                        column: x => x.ItemConfigId,
                        principalTable: "ItemConfigs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitPrice = table.Column<long>(type: "bigint", nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    RemainingQuantity = table.Column<int>(type: "int", nullable: false),
                    IsMatched = table.Column<bool>(type: "bit", nullable: false),
                    ExtraSoldoutMoney = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                    table.CheckConstraint("CK_TEntity_ExtraSoldoutMoney", "\"ExtraSoldoutMoney\" >= 0");
                    table.CheckConstraint("CK_TEntity_RemainingQuantity1", "\"RemainingQuantity\" >= 0");
                    table.CheckConstraint("CK_TEntity_TotalQuantity1", "\"TotalQuantity\" >= 1");
                    table.CheckConstraint("CK_TEntity_UnitPrice", "\"UnitPrice\" >= 1");
                    table.ForeignKey(
                        name: "FK_Lots_ItemConfigs_ItemConfigId",
                        column: x => x.ItemConfigId,
                        principalTable: "ItemConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_ItemConfigId_BidderUserId",
                table: "Bids",
                columns: new[] { "ItemConfigId", "BidderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemStacks_ConfigId_InventoryId",
                table: "ItemStacks",
                columns: new[] { "ConfigId", "InventoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemStacks_InventoryId",
                table: "ItemStacks",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemStacks_ItemConfigId",
                table: "ItemStacks",
                column: "ItemConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_ItemConfigId_OwnerUserId",
                table: "Lots",
                columns: new[] { "ItemConfigId", "OwnerUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "ItemStacks");

            migrationBuilder.DropTable(
                name: "Lots");

            migrationBuilder.DropTable(
                name: "Inventories");

            migrationBuilder.DropTable(
                name: "ItemConfigs");
        }
    }
}
