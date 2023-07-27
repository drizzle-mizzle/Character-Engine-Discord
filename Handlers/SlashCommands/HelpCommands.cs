using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    [Group("help", "Help commands")]
    public class HelpCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly DiscordSocketClient _client;

        public HelpCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
        }

        [SlashCommand("how-to-use", "All basic info about bot")]
        public async Task BaicsHelp()
        {
            var embed = new EmbedBuilder().WithTitle("Character Engine").WithColor(Color.Gold)
                                          .AddField("How to use", "1. Use one of `/spawn` commands to create a character.\n" +
                                                                  "2. Modify it with one of the `/update-character` commands using a given prefix or webhook ID.\n" +
                                                                  "3. Call character by mentioning his prefix or with reply on one if his messages.\n" +
                                                                  "4. If you want to start the chat with a character from the beginning, use `/reset-character` commands.")
                                          .AddField("API", "By default, bot will use its owner's credentials for accessing all needed servcies like **CharacterAI** or **OpenAI**\n" +
                                                           "To use your own API keys and cAI accounts, change it with `/set-server-[ type ]-token` command.\n" +
                                                           "Each character can use different credentials.")
                                          .AddField("Links", "To get additional info about each command and its subtleties, read:\n" +
                                                             "- [wiki/Commands](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Commands)\n" +
                                                             "- [wiki/Important-Notes-and-Additional-Guides](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Important-Notes-and-Additional-Guides)\n" +
                                                             "Also, it's really recommended to look into `/help messages-format`");
                                          
            await RespondAsync(embed: embed.Build());
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
    }
}
