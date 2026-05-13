using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PICTO.SMRS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalWorkflowSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionedByUserId",
                table: "RequisitionRecords",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingReason",
                table: "RequisitionRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceivedAt",
                table: "RequisitionRecords",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "RequisitionRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LowStockSince",
                table: "InventoryItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingReason",
                table: "BorrowRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "BorrowRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionedByUserId",
                table: "RequisitionRecords");

            migrationBuilder.DropColumn(
                name: "PendingReason",
                table: "RequisitionRecords");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "RequisitionRecords");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "RequisitionRecords");

            migrationBuilder.DropColumn(
                name: "LowStockSince",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "PendingReason",
                table: "BorrowRecords");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "BorrowRecords");
        }
    }
}
