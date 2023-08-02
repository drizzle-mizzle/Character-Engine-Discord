using CharacterEngineDiscord.Models.Database;
using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Discord.Webhook;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Threading.Channels;
using System.ComponentModel;

namespace CharacterEngineDiscord.Handlers
{
    internal class ModalsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;

        public ModalsHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationsService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            _client.ModalSubmitted += (modal) =>
            {
                Task.Run(async () => {
                    try { await HandleModalAsync(modal); }
                    catch (Exception e)
                    {
                        LogException(new[] { e });
                        var channel = modal.Channel as SocketGuildChannel;
                        var guild = channel?.Guild;
                        await TryToReportInLogsChannel(_client, title: "Exception",
                                                                desc: $"In Guild `{guild?.Name} ({guild?.Id})`, Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                                      $"User: {modal.User?.Username}\n" +
                                                                      $"Modal ID: {modal.Data.CustomId}\n" +
                                                                      $"```cs\n" +
                                                                      $"{e}\n" +
                                                                      $"```",
                                                                color: Color.Red);
                    }
                });
                return Task.CompletedTask;
            };
        }

        internal async Task HandleModalAsync(SocketModal modal)
        {
            if (await UserIsBannedCheckOnly(modal.User)) return;

            await modal.DeferAsync();
            var modalId = modal.Data.CustomId;
            
            // Update call prefix command
            if (modalId.StartsWith("upd"))
            {
                await UpdateCharacterAsync(modal);
            } // Spawn custom character command
            else if (modalId == "spawn")
            {
                await SpawnCustomCharacterAsync(modal);
            }
        }

        private static async Task UpdateCharacterAsync(SocketModal modal)
        {
            var db = new StorageContext();

            ulong webhookId = ulong.Parse(modal.Data.CustomId.Split('~').Last());
            var characterWebhook = await db.CharacterWebhooks.FindAsync(webhookId);

            if (characterWebhook is null)
            {
                await modal.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Orange));
                return;
            }

            string? newJailbreakPrompt = modal.Data.Components.FirstOrDefault(c => c.CustomId == "new-prompt")?.Value;
            if (string.IsNullOrWhiteSpace(newJailbreakPrompt)) return;

            characterWebhook.UniversalJailbreakPrompt = newJailbreakPrompt;
            await db.SaveChangesAsync();

            await modal.FollowupAsync(embed: SuccessEmbed());
        }

        private async Task SpawnCustomCharacterAsync(SocketModal modal)
        {
            var channel = await FindOrStartTrackingChannelAsync(modal.Channel.Id, (ulong)modal.GuildId!);
            if (channel is null)
            {
                await modal.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Channel not found".ToInlineEmbed(Color.Orange));
                return;
            }

            string definition = modal.Data.Components.FirstOrDefault(c => c.CustomId == "definition-1")?.Value! +
                               (modal.Data.Components.FirstOrDefault(c => c.CustomId == "definition-2")?.Value ?? "");

            var unsavedCharacter = new Character()
            {
                Id = $"custom-{channel.Id}-{modal.Id}-{modal.User.Id}",
                AuthorName = modal.User.GlobalName,
                Name = modal.Data.Components.FirstOrDefault(c => c.CustomId == "name")?.Value ?? "Unknown",
                Greeting = modal.Data.Components.FirstOrDefault(c => c.CustomId == "first-message")?.Value ?? "",
                Definition = definition,
                AvatarUrl = modal.Data.Components.FirstOrDefault(c => c.CustomId == "avatar-url")?.Value,
                Title = "Custom character",
                Description = null,
                ImageGenEnabled = false,
                Interactions = null,
                Stars = null,
                Tgt = null
            };

            var context = new InteractionContext(_client, modal, modal.Channel);
            var characterWebhook = await CreateCharacterWebhookAsync(IntegrationType.Empty, context, unsavedCharacter, _integration);
            if (characterWebhook is null) return;
            
            var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);

            await modal.FollowupAsync(embed: SpawnCharacterEmbed(characterWebhook));
            await webhookClient.SendMessageAsync($"{modal.User.Mention} {characterWebhook.Character.Greeting}");
        }
    }
}
