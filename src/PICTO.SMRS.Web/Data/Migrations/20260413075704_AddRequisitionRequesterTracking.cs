using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PICTO.SMRS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequisitionRequesterTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestorUserId",
                table: "RequisitionRecords",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitionRecords_RequestorUserId",
                table: "RequisitionRecords",
                column: "RequestorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequisitionRecords_RequestorUserId",
                table: "RequisitionRecords");

            migrationBuilder.DropColumn(
                name: "RequestorUserId",
                table: "RequisitionRecords");
        }
    }
}
