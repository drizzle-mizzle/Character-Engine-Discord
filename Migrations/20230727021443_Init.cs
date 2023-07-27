using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedGuilds",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedGuilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlockedUsers",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Tgt = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Greeting = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", nullable: true),
                    ImageGenEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Interactions = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Stars = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Definition = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildMessagesFormat = table.Column<string>(type: "TEXT", nullable: false),
                    GuildCaiUserToken = table.Column<string>(type: "TEXT", nullable: true),
                    GuildCaiPlusMode = table.Column<bool>(type: "INTEGER", nullable: true),
                    GuildOpenAiApiEndpoint = table.Column<string>(type: "TEXT", nullable: true),
                    GuildOpenAiApiToken = table.Column<string>(type: "TEXT", nullable: true),
                    GuildOpenAiModel = table.Column<string>(type: "TEXT", nullable: true),
                    BtnsRemoveDelay = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RandomReplyChance = table.Column<float>(type: "REAL", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterWebhooks",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WebhookToken = table.Column<string>(type: "TEXT", nullable: false),
                    CallPrefix = table.Column<string>(type: "TEXT", nullable: false),
                    ReferencesEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntegrationType = table.Column<int>(type: "INTEGER", nullable: false),
                    MessagesFormat = table.Column<string>(type: "TEXT", nullable: false),
                    ReplyChance = table.Column<float>(type: "REAL", nullable: false),
                    ReplyDelay = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonalCaiUserAuthToken = table.Column<string>(type: "TEXT", nullable: true),
                    CaiActiveHistoryId = table.Column<string>(type: "TEXT", nullable: true),
                    PersonalOpenAiApiEndpoint = table.Column<string>(type: "TEXT", nullable: true),
                    PersonalOpenAiApiToken = table.Column<string>(type: "TEXT", nullable: true),
                    OpenAiModel = table.Column<string>(type: "TEXT", nullable: true),
                    OpenAiFreqPenalty = table.Column<float>(type: "REAL", nullable: true),
                    OpenAiPresencePenalty = table.Column<float>(type: "REAL", nullable: true),
                    OpenAiTemperature = table.Column<float>(type: "REAL", nullable: true),
                    OpenAiMaxTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    UniversalJailbreakPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LastRequestTokensUsage = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterWebhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterWebhooks_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterWebhooks_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaiHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CharacterWebhookId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaiHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaiHistories_CharacterWebhooks_CharacterWebhookId",
                        column: x => x.CharacterWebhookId,
                        principalTable: "CharacterWebhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HuntedUsers",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Chance = table.Column<float>(type: "REAL", nullable: false),
                    CharacterWebhookId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HuntedUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HuntedUsers_CharacterWebhooks_CharacterWebhookId",
                        column: x => x.CharacterWebhookId,
                        principalTable: "CharacterWebhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenAiHistoryMessages",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterWebhookId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenAiHistoryMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenAiHistoryMessages_CharacterWebhooks_CharacterWebhookId",
                        column: x => x.CharacterWebhookId,
                        principalTable: "CharacterWebhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaiHistories_CharacterWebhookId",
                table: "CaiHistories",
                column: "CharacterWebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildId",
                table: "Channels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterWebhooks_ChannelId",
                table: "CharacterWebhooks",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterWebhooks_CharacterId",
                table: "CharacterWebhooks",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_HuntedUsers_CharacterWebhookId",
                table: "HuntedUsers",
                column: "CharacterWebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenAiHistoryMessages_CharacterWebhookId",
                table: "OpenAiHistoryMessages",
                column: "CharacterWebhookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedGuilds");

            migrationBuilder.DropTable(
                name: "BlockedUsers");

            migrationBuilder.DropTable(
                name: "CaiHistories");

            migrationBuilder.DropTable(
                name: "HuntedUsers");

            migrationBuilder.DropTable(
                name: "OpenAiHistoryMessages");

            migrationBuilder.DropTable(
                name: "CharacterWebhooks");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Guilds");
        }
    }
}
