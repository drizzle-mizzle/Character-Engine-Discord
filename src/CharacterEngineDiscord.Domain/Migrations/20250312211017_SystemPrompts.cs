using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Domain.Migrations
{
    /// <inheritdoc />
    public partial class SystemPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdoptedCharacterSystemPrompt",
                table: "OpenRouterSpawnedCharacters",
                type: "text",
                maxLength: 2147483647,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "DiscordGuilds",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "DiscordChannels",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdoptedCharacterSystemPrompt",
                table: "OpenRouterSpawnedCharacters");

            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "DiscordGuilds");

            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "DiscordChannels");
        }
    }
}
