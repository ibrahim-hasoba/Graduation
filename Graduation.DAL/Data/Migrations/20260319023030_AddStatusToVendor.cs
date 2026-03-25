using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Graduation.DAL.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusToVendor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vendors_IsApproved",
                table: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_Vendors_IsApproved_IsActive",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Vendors");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Vendors",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Vendors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_ApprovalStatus",
                table: "Vendors",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_ApprovalStatus_IsActive",
                table: "Vendors",
                columns: new[] { "ApprovalStatus", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vendors_ApprovalStatus",
                table: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_Vendors_ApprovalStatus_IsActive",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Vendors");

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Vendors",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_IsApproved",
                table: "Vendors",
                column: "IsApproved");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_IsApproved_IsActive",
                table: "Vendors",
                columns: new[] { "IsApproved", "IsActive" });
        }
    }
}
