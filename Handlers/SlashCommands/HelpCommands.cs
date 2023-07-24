using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Discord.WebSocket;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.CharacterHub;
using Discord.Webhook;
using CharacterEngineDiscord.Models.Database;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [Group("help", "Help")]
    public class HelpCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;

        public HelpCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("messages-format", "Info about messages format")]
        public async Task MessagesFormatHelp()
        {
            var embed = new EmbedBuilder().WithTitle("Messages format")
                                          .WithColor(Color.Gold)
                                          .AddField("Description", "This setting allows you to change the format of messages that character will get from users.")
                                          .AddField("Commands", "`show-messages-format` - Check the current format of messages\n`update-character messages-format` - Change the format of messages")
                                          .AddField("Placeholders", "You can use these placeholders in your formats to manipulate the data that being inserted in your messages:\n" +
                                                    "**`{{msg}}`** - **required** placeholder that contains the message itself;\n" +
                                                    "**`{{user}}`** - User's Discord name *(server nickname > display name > username)*\n")
                                          .AddField("Example", "Format: *`[System note: User \"{{user}}\" said:]  \"{{msg}}\"`*\n" +
                                                               "Inputs:\n- user with name **`Average AI Enjoyer`**;\n- message with text *`Do you love donuts?`*\n" +
                                                               "Result (what character will see):\n*`[System note: User \"Average AI Enjoyer\" said:]  \"Do you love donuts?\"`*");
            await RespondAsync(embed: embed.Build());
        }


        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        
    }
}
