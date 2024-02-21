using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAisekai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuildAisekaiAuthToken",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "GuildAisekaiRefreshToken",
                table: "Guilds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuildAisekaiAuthToken",
                table: "Guilds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuildAisekaiRefreshToken",
                table: "Guilds",
                type: "TEXT",
                nullable: true);
        }
    }
}
