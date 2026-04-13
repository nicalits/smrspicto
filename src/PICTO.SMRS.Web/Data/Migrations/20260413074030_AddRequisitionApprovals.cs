using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PICTO.SMRS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequisitionApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequisitionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestorPosition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestorDivision = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Office = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MrIcsPosition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequisitionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequisitionRecordItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequisitionRecordId = table.Column<int>(type: "int", nullable: false),
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    SerialNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RfNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequisitionRecordItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequisitionRecordItems_RequisitionRecords_RequisitionRecordId",
                        column: x => x.RequisitionRecordId,
                        principalTable: "RequisitionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequisitionRecordItems_RequisitionRecordId",
                table: "RequisitionRecordItems",
                column: "RequisitionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitionRecords_ItemType_Status_Date",
                table: "RequisitionRecords",
                columns: new[] { "ItemType", "Status", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequisitionRecordItems");

            migrationBuilder.DropTable(
                name: "RequisitionRecords");
        }
    }
}
