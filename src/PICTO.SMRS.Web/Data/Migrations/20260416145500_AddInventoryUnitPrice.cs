using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PICTO.SMRS.Web.Data;

#nullable disable

namespace PICTO.SMRS.Web.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260416145500_AddInventoryUnitPrice")]
    public partial class AddInventoryUnitPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('InventoryItems', 'UnitPrice') IS NULL
                BEGIN
                    ALTER TABLE [InventoryItems]
                    ADD [UnitPrice] decimal(18,2) NOT NULL CONSTRAINT [DF_InventoryItems_UnitPrice] DEFAULT (0);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('InventoryItems', 'UnitPrice') IS NOT NULL
                BEGIN
                    DECLARE @dfName sysname;
                    SELECT @dfName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    INNER JOIN sys.tables t ON t.object_id = c.object_id
                    WHERE t.name = 'InventoryItems' AND c.name = 'UnitPrice';

                    IF @dfName IS NOT NULL
                        EXEC('ALTER TABLE [InventoryItems] DROP CONSTRAINT [' + @dfName + ']');

                    ALTER TABLE [InventoryItems] DROP COLUMN [UnitPrice];
                END
                """);
        }
    }
}
