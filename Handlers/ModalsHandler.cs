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

namespace CharacterEngineDiscord.Handlers
{
    internal class ModalsHandler
    {
        private readonly IDiscordClient _client;
        private readonly IntegrationsService _integrations;

        public ModalsHandler(IServiceProvider services, IDiscordClient client)
        {
            _client = client;
            _integrations = services.GetRequiredService<IntegrationsService>();
        }

        public Task HandleModal(SocketModal modal)
        {
            Task.Run(async () => {
                try { await HandleModalAsync(modal); }
                catch (Exception e)
                {
                    LogException(new[] { e });
                    var channel = modal.Channel as SocketGuildChannel;
                    var guild = channel?.Guild;
                    TryToReportInLogsChannel(_client, title: "Modal Exception",
                                                      desc: $"Guild: `{guild?.Name} ({guild?.Id})`\n" +
                                                            $"Owner: `{guild?.Owner.GetBestName()} ({guild?.Owner.Username})`\n" +
                                                            $"Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                            $"User: `{modal.User.Username}`\n" +
                                                            $"Modal ID: `{modal.Data.CustomId}`",
                                                      content: e.ToString(),
                                                      color: Color.Red,
                                                      error: true);
                }
            });

            return Task.CompletedTask;
        }

        private async Task HandleModalAsync(SocketModal modal)
        {
            await modal.DeferAsync();
            if (await UserIsBannedCheckOnly(modal.User.Id)) return;

            var modalId = modal.Data.CustomId;
            
            // Update call prefix command
            if (modalId.StartsWith("upd")) {
                await UpdateCharacterAsync(modal);
            } // New jailbreak prompt
            else if (modalId.StartsWith("guild")) {
                await UpdateGuildAsync(modal);
            } // Spawn custom character command
            else if (modalId == "spawn") {
                await SpawnCustomCharacterAsync(modal);
            }
        }

        private async Task UpdateGuildAsync(SocketModal modal)
        {
            using var db = new StorageContext();
            string guildId = modal.Data.CustomId.Split('~').Last();

            var context = new InteractionContext(_client, modal, modal.Channel);
            var guild = await FindOrStartTrackingGuildAsync(ulong.Parse(guildId), db);

            string? newJailbreakPrompt = modal.Data.Components.FirstOrDefault(c => c.CustomId == "new-prompt")?.Value;
            if (string.IsNullOrWhiteSpace(newJailbreakPrompt)) return;

            guild.GuildJailbreakPrompt = newJailbreakPrompt;
            await TryToSaveDbChangesAsync(db);

            await modal.FollowupAsync(embed: SuccessEmbed());
        }

        private async Task UpdateCharacterAsync(SocketModal modal)
        {
            using var db = new StorageContext();
            string webhookIdOrPrefix = modal.Data.CustomId.Split('~').Last();

            var context = new InteractionContext(_client, modal, modal.Channel);
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, context, db);

            if (characterWebhook is null)
            {
                await modal.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Character with the given call prefix or webhook ID was not found in the current channel".ToInlineEmbed(Color.Orange));
                return;
            }

            var type = characterWebhook.IntegrationType;
            if (type is not IntegrationType.OpenAI && type is not IntegrationType.KoboldAI && type is not IntegrationType.HordeKoboldAI)
            {
                await modal.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Not available for {type} integrations".ToInlineEmbed(Color.Orange));
                return;
            }

            string? newJailbreakPrompt = modal.Data.Components.FirstOrDefault(c => c.CustomId == "new-prompt")?.Value;
            if (string.IsNullOrWhiteSpace(newJailbreakPrompt)) return;

            characterWebhook.PersonalJailbreakPrompt = newJailbreakPrompt;
            await TryToSaveDbChangesAsync(db);

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
                ImageGenEnabled = false,
                Interactions = null,
                Stars = null
            };

            if (unsavedCharacter.Name.Length < 2)
            {
                await modal.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Character name is too short".ToInlineEmbed(Color.Orange));
                return;
            }

            var context = new InteractionContext(_client, modal, modal.Channel);
            var characterWebhook = await _integrations.CreateCharacterWebhookAsync(IntegrationType.Empty, context, unsavedCharacter, _integrations, false);
            if (characterWebhook is null) return;
            
            var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            _integrations.WebhookClients.TryAdd(characterWebhook.Id, webhookClient);

            await modal.FollowupAsync(embed: SpawnCharacterEmbed(characterWebhook));
            await webhookClient.SendMessageAsync($"{modal.User.Mention} {characterWebhook.Character.Greeting}");
        }
    }
}
