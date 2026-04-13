using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PICTO.SMRS.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequisitionRsNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RsNo",
                table: "RequisitionRecords",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RsNo",
                table: "RequisitionRecords");
        }
    }
}
