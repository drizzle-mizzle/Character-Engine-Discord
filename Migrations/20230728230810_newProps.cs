using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class newProps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "LastRequestTokensUsage",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentSwipeIndex",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<ulong>(
                name: "LastCharacterDiscordMsgId",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<string>(
                name: "LastCharacterMsgUuId",
                table: "CharacterWebhooks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastUserMsgUuId",
                table: "CharacterWebhooks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SkipNextBotMessage",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSwipeIndex",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "LastCharacterDiscordMsgId",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "LastCharacterMsgUuId",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "LastUserMsgUuId",
                table: "CharacterWebhooks");

            migrationBuilder.DropColumn(
                name: "SkipNextBotMessage",
                table: "CharacterWebhooks");

            migrationBuilder.AlterColumn<int>(
                name: "LastRequestTokensUsage",
                table: "CharacterWebhooks",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }
    }
}
