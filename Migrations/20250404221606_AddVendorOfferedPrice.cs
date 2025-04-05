using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMarketplaceApi.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorOfferedPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VendorOfferedPrice",
                table: "UserLevels",
                type: "decimal(65,30)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VendorOfferedPrice",
                table: "UserLevels");
        }
    }
}
