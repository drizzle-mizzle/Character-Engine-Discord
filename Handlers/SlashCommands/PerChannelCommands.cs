using Discord;
using Discord.Webhook;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.CharacterHub;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    public class PerChannelCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;

        public PerChannelCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        //[SlashCommand("spawn-custom-tavern-character", "Add a new character to this channel with full customization")]
        //public async Task SpawnCustomTavernCharacter()
        //{
        //    try { }
        //    catch (Exception e) { LogException(new[] { e }); }
        //}

        [SlashCommand("show-characters", "Show all characters in this channel")]
        public async Task ShowCharacters()
        {
            try { await ShowCharactersAsync(); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        private async Task ShowCharactersAsync()
        {
            await DeferAsync();

            var channel = await _db.Channels.FindAsync(Context.Interaction.ChannelId);
            if (channel is null || channel.CharacterWebhooks.Count == 0)
            {
                await FollowupAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} No characters were found in this channel", Color.Orange));
                return;
            }

            int count = 0;
            string result = "";
            foreach (var characterWebhook in channel.CharacterWebhooks)
            {
                var integrationType = characterWebhook.IntegrationType is IntegrationType.CharacterAI ? "c.ai" :
                                      characterWebhook.IntegrationType is IntegrationType.OpenAI ? characterWebhook.OpenAiModel : "";
                result += $"{count++}. **{characterWebhook.Character.Name}** | *`{characterWebhook.CallPrefix}`* | `{characterWebhook.Id}` | `{integrationType}` \n";
            }
            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} {count} character(s) in this channel:")
                                          .WithDescription(result)
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed);
        }

    }
}
