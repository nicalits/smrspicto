using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PICTO.SMRS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBorrowWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BorrowRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RfNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BorrowerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BorrowerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BorrowerDivision = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Office = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SlipDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SlipTime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TelNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActionedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BorrowRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BorrowRecordItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BorrowRecordId = table.Column<int>(type: "int", nullable: false),
                    InventoryItemId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    LocationVenue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BorrowDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BorrowTime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReturnTime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BorrowRecordItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BorrowRecordItems_BorrowRecords_BorrowRecordId",
                        column: x => x.BorrowRecordId,
                        principalTable: "BorrowRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRecordItems_BorrowRecordId",
                table: "BorrowRecordItems",
                column: "BorrowRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRecords_BorrowerUserId",
                table: "BorrowRecords",
                column: "BorrowerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowRecords_Status_CreatedAt",
                table: "BorrowRecords",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BorrowRecordItems");

            migrationBuilder.DropTable(
                name: "BorrowRecords");
        }
    }
}
