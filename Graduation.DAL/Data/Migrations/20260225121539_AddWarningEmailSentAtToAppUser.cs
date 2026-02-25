using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Graduation.DAL.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarningEmailSentAtToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WarningEmailSentAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarningEmailSentAt",
                table: "AspNetUsers");
        }
    }
}
