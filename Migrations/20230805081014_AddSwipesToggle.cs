using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class AddSwipesToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SwipesEnabled",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SwipesEnabled",
                table: "CharacterWebhooks");
        }
    }
}
