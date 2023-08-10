using Discord;
using Discord.Interactions;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    public class HelpCommands : InteractionModuleBase<InteractionContext>
    {
        //private readonly IntegrationsService _integration;
        //private readonly DiscordSocketClient _client;

        public HelpCommands()//IServiceProvider services)
        {
            //_integration = services.GetRequiredService<IntegrationsService>();
            //_client = services.GetRequiredService<DiscordSocketClient>();
        }

        [SlashCommand("help-how-to-use", "All basic info about bot")]
        public async Task BaicsHelp()
        {
            var embed = new EmbedBuilder().WithTitle("Character Engine").WithColor(Color.Gold)
                                          .AddField("How to use", "1. Use one of the `/spawn` commands to create a character.\n" +
                                                                  "2. Modify it with one of the `/update` commands using a given prefix or webhook ID.\n" +
                                                                  "3. Call character by mentioning his prefix or with reply on one of his messages.\n" +
                                                                  "4. If you want to start the chat with a character from the beginning, use `/reset-character` commands.")
                                          .AddField("API", "By default, bot will use its owner's credentials for accessing all needed servcies like **CharacterAI** or **OpenAI**\n" +
                                                           "To use your own API keys and cAI accounts, change it with `/set-server-[ type ]-token` command.\n" +
                                                           "Each character can use different credentials.")
                                          .AddField("Links", "To get additional info about each command and its subtleties, read:\n" +
                                                             "- [wiki/Commands](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Commands)\n" +
                                                             "- [wiki/Important-Notes-and-Additional-Guides](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Important-Notes-and-Additional-Guides)\n" +
                                                             "Also, it's really recommended to look into `/help-messages-format`");
                                          
            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("help-messages-format", "Info about messages format")]
        public async Task MessagesFormatHelp()
        {
            var embed = new EmbedBuilder().WithTitle("Messages format")
                                          .WithColor(Color.Gold)
                                          .AddField("Description", "This setting allows you to change the format of messages that character will get from users.")
                                          .AddField("Commands", "`/show messages-format` - Check the current format of messages for this server or certain character\n" +
                                                                "`/update messages-format` - Change the format of messages for certain character\n" +
                                                                "`/set-default-messages-format` - Change the format of messages for all **new** characters on this server")
                                          .AddField("Placeholders", "You can use these placeholders in your formats to manipulate the data that being inserted in your messages:\n" +
                                                                    "**`{{msg}}`** - **Required** placeholder that contains the message itself.\n" +
                                                                    "**`{{user}}`** - Placeholder that contains the user's Discord name *(server nickname > display name > username)*.\n" +
                                                                    "**`{{ref_msg_begin}}`**, **`{{ref_msg_user}}`**, **`{{ref_msg_text}}`**, **`{{ref_msg_end}}`** - Combined placeholder that contains the referenced message (one that user was replying to). *Begin* and *end* parts are needed because user message can have no referenced message, and then placeholder will be removed.\n")
                                          .AddField("Example", "Format:\n*`{{ref_msg_begin}}((In response to '{{ref_msg_text}}' from '{{ref_msg_user}}')){{ref_msg_end}}\\n{{user}} says:\\n{{msg}}`*\n" +
                                                               "Inputs:\n- referenced message with text *`Hello`* from user *`Dude`*;\n- user with name *`Average AI Enjoyer`*;\n- message with text *`Do you love donuts?`*\n" +
                                                               "Result (what character will see):\n*`((In response to 'Hello' from 'Dude'))\nAverage AI Enjoyer says:\nDo you love donuts?`*\n" +
                                                               "Example above is used by default, but you are free to play with it the way you want, or you can simply disable it by setting the default message format with `{{msg}}`.");
            await RespondAsync(embed: embed.Build());
        }
    }
}
