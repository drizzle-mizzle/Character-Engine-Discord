using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class Upd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PersonalCaiUserAuthToken",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "PersonalOpenAiApiToken",
                table: "CharacterWebhooks");

            migrationBuilder.AddColumn<string>(
                name: "GuildHordeApiToken",
                table: "Guilds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuildKoboldAiApiEndpoint",
                table: "Guilds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FromChub",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.RenameTable(
                name: "OpenAiHistoryMessages",
                newName: "StoredHistoryMessages"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuildHordeApiToken",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "GuildKoboldAiApiEndpoint",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "FromChub",
                table: "CharacterWebhooks");

            migrationBuilder.AddColumn<string>(
                name: "PersonalCaiUserAuthToken",
                table: "CharacterWebhooks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalOpenAiApiToken",
                table: "CharacterWebhooks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.RenameTable(
                name: "StoredHistoryMessages",
                newName: "OpenAiHistoryMessages"
                );
        }
    }
}
