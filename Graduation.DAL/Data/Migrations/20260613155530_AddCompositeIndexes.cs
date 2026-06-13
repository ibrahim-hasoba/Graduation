using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Graduation.DAL.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_StockQuantity",
                table: "Products",
                columns: new[] { "IsActive", "StockQuantity" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsFeatured_IsActive_StockQuantity",
                table: "Products",
                columns: new[] { "IsFeatured", "IsActive", "StockQuantity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive_StockQuantity",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsFeatured_IsActive_StockQuantity",
                table: "Products");
        }
    }
}
