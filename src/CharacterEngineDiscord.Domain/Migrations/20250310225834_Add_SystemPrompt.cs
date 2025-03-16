using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Add_SystemPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SakuraExampleDialog",
                table: "SakuraAiSpawnedCharacters");

            migrationBuilder.DropColumn(
                name: "SakuraPersona",
                table: "SakuraAiSpawnedCharacters");

            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "OpenRouterIntegrations",
                type: "text",
                maxLength: 2147483647,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "OpenRouterIntegrations");

            migrationBuilder.AddColumn<string>(
                name: "SakuraExampleDialog",
                table: "SakuraAiSpawnedCharacters",
                type: "text",
                maxLength: 2147483647,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SakuraPersona",
                table: "SakuraAiSpawnedCharacters",
                type: "text",
                maxLength: 2147483647,
                nullable: false,
                defaultValue: "");
        }
    }
}
