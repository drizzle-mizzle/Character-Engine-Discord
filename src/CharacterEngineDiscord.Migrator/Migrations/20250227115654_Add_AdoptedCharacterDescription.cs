using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class Add_AdoptedCharacterDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdoptedCharacterDescription",
                table: "OpenRouterSpawnedCharacters",
                type: "text",
                maxLength: 2147483647,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdoptedCharacterDescription",
                table: "OpenRouterSpawnedCharacters");
        }
    }
}
