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
                    table.CheckConstraint("CK_TEntity_UsedCapacity", "\"UsedCapacity\" >= 0");
                    table.CheckConstraint("CK_TEntity_UsedWeight", "\"UsedWeight\" >= 0");
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
                    ExtraBuyoutMoney = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.CheckConstraint("CK_TEntity_BidPrice", "\"BidPrice\" >= 1");
                    table.CheckConstraint("CK_TEntity_ExtraBuyoutMoney", "\"ExtraBuyoutMoney\" >= 0");
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

            migrationBuilder.Sql(@"
                CREATE OR ALTER TRIGGER trg_match_lot_with_bids
                ON Lots
                INSTEAD OF INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
    
                    DECLARE @InsertedRows TABLE (
                        Id UNIQUEIDENTIFIER,
                        OwnerUserId UNIQUEIDENTIFIER,
                        ItemConfigId UNIQUEIDENTIFIER,
                        UnitPrice DECIMAL(18,2),
                        TotalQuantity INT,
                        RemainingQuantity INT,
                        ExtraSoldoutMoney DECIMAL(18,2),
                        CreatedAt DATETIME2
                    );
    
                    INSERT INTO @InsertedRows
                    SELECT * FROM inserted;
    
                    DECLARE @Id UNIQUEIDENTIFIER,
                            @OwnerUserId UNIQUEIDENTIFIER,
                            @ItemConfigId UNIQUEIDENTIFIER,
                            @UnitPrice DECIMAL(18,2),
                            @TotalQuantity INT,
                            @RemainingQuantity INT,
                            @ExtraSoldoutMoney DECIMAL(18,2),
                            @CreatedAt DATETIME2;
    
                    DECLARE lot_cursor CURSOR FOR
                        SELECT Id, OwnerUserId, ItemConfigId, UnitPrice, 
                               TotalQuantity, RemainingQuantity, ExtraSoldoutMoney, CreatedAt
                        FROM @InsertedRows;
    
                    OPEN lot_cursor;
                    FETCH NEXT FROM lot_cursor INTO @Id, @OwnerUserId, @ItemConfigId, @UnitPrice, 
                                                    @TotalQuantity, @RemainingQuantity, @ExtraSoldoutMoney, @CreatedAt;
    
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        DECLARE @TradeQuantity INT;
                        DECLARE @BidId UNIQUEIDENTIFIER;
                        DECLARE @BidPrice DECIMAL(18,2);
                        DECLARE @BidRemainingQuantity INT;
        
                        DECLARE bid_cursor CURSOR FOR
                            SELECT b.Id, b.BidPrice, b.RemainingQuantity
                            FROM Bids b WITH (UPDLOCK, ROWLOCK)
                            WHERE b.ItemConfigId = @ItemConfigId
                              AND b.RemainingQuantity > 0
                              AND b.BidPrice >= @UnitPrice
                            ORDER BY b.BidPrice DESC, b.CreatedAt;
        
                        OPEN bid_cursor;
                        FETCH NEXT FROM bid_cursor INTO @BidId, @BidPrice, @BidRemainingQuantity;
        
                        WHILE @@FETCH_STATUS = 0 AND @RemainingQuantity > 0
                        BEGIN
                            SET @TradeQuantity = CASE 
                                WHEN @RemainingQuantity < @BidRemainingQuantity THEN @RemainingQuantity
                                ELSE @BidRemainingQuantity
                            END;
            
                            UPDATE Bids
                            SET RemainingQuantity = RemainingQuantity - @TradeQuantity,
                                ExtraBuyoutMoney = ExtraBuyoutMoney + 
                                    (@BidPrice - @UnitPrice) * @TradeQuantity
                            WHERE Id = @BidId;
            
                            SET @RemainingQuantity = @RemainingQuantity - @TradeQuantity;
            
                            IF @BidPrice > @UnitPrice
                            BEGIN
                                SET @ExtraSoldoutMoney = @ExtraSoldoutMoney + 
                                    (@BidPrice - @UnitPrice) * @TradeQuantity;
                            END;
            
                            FETCH NEXT FROM bid_cursor INTO @BidId, @BidPrice, @BidRemainingQuantity;
                        END;
        
                        CLOSE bid_cursor;
                        DEALLOCATE bid_cursor;
        
                        INSERT INTO Lots (
                            Id, OwnerUserId, ItemConfigId, UnitPrice, 
                            TotalQuantity, RemainingQuantity, ExtraSoldoutMoney, CreatedAt
                        ) VALUES (
                            @Id, @OwnerUserId, @ItemConfigId, @UnitPrice,
                            @TotalQuantity, @RemainingQuantity, @ExtraSoldoutMoney, @CreatedAt
                        );
        
                        FETCH NEXT FROM lot_cursor INTO @Id, @OwnerUserId, @ItemConfigId, @UnitPrice, 
                                                        @TotalQuantity, @RemainingQuantity, @ExtraSoldoutMoney, @CreatedAt;
                    END;
    
                    CLOSE lot_cursor;
                    DEALLOCATE lot_cursor;
                END;
                GO

                CREATE OR ALTER TRIGGER trg_match_bid_with_lots
                ON Bids
                INSTEAD OF INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    DECLARE @InsertedRows TABLE (
                        Id UNIQUEIDENTIFIER,
                        BidderUserId UNIQUEIDENTIFIER,  -- Изменено с UserId
                        ItemConfigId UNIQUEIDENTIFIER,
                        BidPrice DECIMAL(18,2),
                        TotalQuantity INT,
                        RemainingQuantity INT,
                        ExtraBuyoutMoney DECIMAL(18,2),
                        CreatedAt DATETIME2
                    );

                    INSERT INTO @InsertedRows
                    SELECT Id, BidderUserId, ItemConfigId, BidPrice,
                           TotalQuantity, RemainingQuantity, ExtraBuyoutMoney, CreatedAt
                    FROM inserted;

                    DECLARE @Id UNIQUEIDENTIFIER,
                            @BidderUserId UNIQUEIDENTIFIER,  -- Изменено с UserId
                            @ItemConfigId UNIQUEIDENTIFIER,
                            @BidPrice DECIMAL(18,2),
                            @TotalQuantity INT,
                            @RemainingQuantity INT,
                            @ExtraBuyoutMoney DECIMAL(18,2),
                            @CreatedAt DATETIME2;

                    DECLARE bid_cursor CURSOR FOR
                        SELECT Id, BidderUserId, ItemConfigId, BidPrice,
                               TotalQuantity, RemainingQuantity, ExtraBuyoutMoney, CreatedAt
                        FROM @InsertedRows;

                    OPEN bid_cursor;
                    FETCH NEXT FROM bid_cursor INTO @Id, @BidderUserId, @ItemConfigId, @BidPrice,
                                                    @TotalQuantity, @RemainingQuantity, @ExtraBuyoutMoney, @CreatedAt;

                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        DECLARE @TradeQuantity INT;
                        DECLARE @LotId UNIQUEIDENTIFIER;
                        DECLARE @LotUnitPrice DECIMAL(18,2);
                        DECLARE @LotRemainingQuantity INT;

                        DECLARE lot_cursor CURSOR FOR
                            SELECT l.Id, l.UnitPrice, l.RemainingQuantity
                            FROM Lots l WITH (UPDLOCK, ROWLOCK)
                            WHERE l.ItemConfigId = @ItemConfigId
                              AND l.RemainingQuantity > 0
                              AND l.UnitPrice <= @BidPrice
                            ORDER BY l.UnitPrice, l.CreatedAt;

                        OPEN lot_cursor;
                        FETCH NEXT FROM lot_cursor INTO @LotId, @LotUnitPrice, @LotRemainingQuantity;

                        WHILE @@FETCH_STATUS = 0 AND @RemainingQuantity > 0
                        BEGIN
                            SET @TradeQuantity = CASE
                                WHEN @RemainingQuantity < @LotRemainingQuantity THEN @RemainingQuantity
                                ELSE @LotRemainingQuantity
                            END;

                            UPDATE Lots
                            SET RemainingQuantity = RemainingQuantity - @TradeQuantity,
                                ExtraSoldoutMoney = ExtraSoldoutMoney +
                                    (@BidPrice - @LotUnitPrice) * @TradeQuantity
                            WHERE Id = @LotId;

                            SET @RemainingQuantity = @RemainingQuantity - @TradeQuantity;

                            IF @BidPrice > @LotUnitPrice
                            BEGIN
                                SET @ExtraBuyoutMoney = @ExtraBuyoutMoney +
                                    (@BidPrice - @LotUnitPrice) * @TradeQuantity;
                            END;

                            FETCH NEXT FROM lot_cursor INTO @LotId, @LotUnitPrice, @LotRemainingQuantity;
                        END;

                        CLOSE lot_cursor;
                        DEALLOCATE lot_cursor;

                        INSERT INTO Bids (
                            Id, BidderUserId, ItemConfigId, BidPrice,  -- Изменено с UserId
                            TotalQuantity, RemainingQuantity, ExtraBuyoutMoney, CreatedAt
                        ) VALUES (
                            @Id, @BidderUserId, @ItemConfigId, @BidPrice,
                            @TotalQuantity, @RemainingQuantity, @ExtraBuyoutMoney, @CreatedAt
                        );

                        FETCH NEXT FROM bid_cursor INTO @Id, @BidderUserId, @ItemConfigId, @BidPrice,
                                                        @TotalQuantity, @RemainingQuantity, @ExtraBuyoutMoney, @CreatedAt;
                    END;

                    CLOSE bid_cursor;
                    DEALLOCATE bid_cursor;
                END;
                GO
            ");
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
