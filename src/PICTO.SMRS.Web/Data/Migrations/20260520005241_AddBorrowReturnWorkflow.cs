using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PICTO.SMRS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBorrowReturnWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarkedReturnedAt",
                table: "BorrowRecords",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReturnConfirmedAt",
                table: "BorrowRecords",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnConfirmedByUserId",
                table: "BorrowRecords",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkedReturnedAt",
                table: "BorrowRecords");

            migrationBuilder.DropColumn(
                name: "ReturnConfirmedAt",
                table: "BorrowRecords");

            migrationBuilder.DropColumn(
                name: "ReturnConfirmedByUserId",
                table: "BorrowRecords");
        }
    }
}
