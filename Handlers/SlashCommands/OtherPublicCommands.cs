using Discord.Interactions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using Discord;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    public class OtherPublicCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;
        public OtherPublicCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

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

            var channel = await _db.Channels.FindAsync(Context.Channel.Id);
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
                                      characterWebhook.IntegrationType is IntegrationType.OpenAI ? characterWebhook.OpenAiModel :
                                      "empty";

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
