using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Graduation.DAL.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymobOrderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymobOrderId",
                table: "Payments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymobOrderId",
                table: "Payments");
        }
    }
}
