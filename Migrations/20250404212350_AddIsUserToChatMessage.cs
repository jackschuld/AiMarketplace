using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMarketplaceApi.Migrations
{
    /// <inheritdoc />
    public partial class AddIsUserToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsFromUser",
                table: "ChatMessages",
                newName: "IsUser");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsUser",
                table: "ChatMessages",
                newName: "IsFromUser");
        }
    }
}
