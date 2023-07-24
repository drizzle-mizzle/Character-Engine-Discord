using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [Group("update-character", "Change character settings")]
    public class UpdateCharacterCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;

        public UpdateCharacterCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }


        [SlashCommand("call-prefix", "Change character call prefix")]
        public async Task CallPrefix(string webhookId, string newCallPrefix, [Summary(description: "Add a following spacebar for the prefix, e.g. `..prefix `")] bool addFollowingSpacebar)
        {
            var user = Context.User as SocketGuildUser;
            if (user.IsCharManager() || user.IsServerOwner() || user.IsHoster())
            {
                try { await UpdatePrefixAsync(webhookId, newCallPrefix, addFollowingSpacebar); }
                catch (Exception e) { LogException(new[] { e }); }
            }
            else
                await Context.SendNoPowerFileAsync();
        }

        [SlashCommand("jailbreak-prompt", "Change character jailbreak prompt")]
        public async Task JailbreakPrompt(string webhookId)
        {
            var user = Context.User as SocketGuildUser;
            if (user.IsCharManager() || user.IsServerOwner() || user.IsHoster())
            {
                try { await UpdateJailbreakPromptAsync(webhookId); }
                catch (Exception e) { LogException(new[] { e }); }
            }
            else
                await Context.SendNoPowerFileAsync();
        }

        [SlashCommand("messages-format", "Change character messages format")]
        public async Task MessagesFormat(string webhookId, string newFormat)
        {
            var user = Context.User as SocketGuildUser;
            if (user.IsCharManager() || user.IsServerOwner() || user.IsHoster())
            {
                try { await UpdateMessagesFormatAsync(webhookId, newFormat); }
                catch (Exception e) { LogException(new[] { e }); }
            }
            else
                await Context.SendNoPowerFileAsync();
        }


        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        private async Task UpdatePrefixAsync(string webhookId, string newCallPrefix, [Summary(description: "Add a following spacebar for the prefix, e.g. `..prefix `")] bool addFollowingSpacebar)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix + (addFollowingSpacebar ? " " : "");
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        private async Task UpdateMessagesFormatAsync(string webhookId, string newFormat)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Can't set format without **`{{{{msg}}}}`** placeholder!", Color.Red));
                return;
            }

            characterWebhook.MessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **{characterWebhook.Character.Name}**")
                                          .AddField("New format:", $"`{characterWebhook.MessagesFormat}`")
                                          .AddField("[Example]", $"User message: *`Hello!`*\nResult (what character will see): *`{characterWebhook.MessagesFormat.Replace("{{msg}}", "Hello!")}`*")
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed);
        }

        private async Task UpdateJailbreakPromptAsync(string webhookId)
        {
            var modal = new ModalBuilder().WithTitle($"Update jailbreak prompt for a character")
                                          .WithCustomId($"{webhookId}")
                                          .AddTextInput("New jailbreak prompt", "new-prompt", TextInputStyle.Paragraph)
                                          .Build();
            await RespondWithModalAsync(modal);
        }
    }
}
