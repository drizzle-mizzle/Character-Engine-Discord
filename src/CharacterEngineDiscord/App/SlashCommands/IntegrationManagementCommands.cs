using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db.Discord;
using CharacterEngineDiscord.Models.Db.Integrations;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.SlashCommands;


[ValidateAccessLevel(AccessLevels.Manager)]
[ValidateChannelPermissions]
[Group("integration", "Integrations Management")]
public class IntegrationManagementCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;


    public IntegrationManagementCommands(AppDbContext db, DiscordSocketClient discordClient)
    {
        _db = db;
        _discordClient = discordClient;
    }


    [SlashCommand("create", "Create new integration for this server")]
    public async Task Create(IntegrationType type)
    {
        var customId = InteractionsHelper.NewCustomId(ModalActionType.CreateIntegration, $"{type:D}");
        var modalBuilder = new ModalBuilder().WithTitle($"Create {type:G} integration").WithCustomId(customId);

        var modal = type switch
        {
            IntegrationType.SakuraAI => modalBuilder.BuildSakuraAiAuthModal(),
            IntegrationType.CharacterAI => modalBuilder.BuildCaiAiAuthModal(),
        };

        await RespondWithModalAsync(modal); // next in EnsureSakuraAiLoginAsync()
    }


    [SlashCommand("confirm", "Confirm intergration")]
    public async Task Confirm(IntegrationType type, string data)
    {
        await DeferAsync(ephemeral: true);

        switch (type)
        {
            case IntegrationType.CharacterAI:
            {
                var caiUser = await MemoryStorage.IntegrationModules.CaiModule.LoginByLinkAsync(data);

                var newCaiIntergration = new CaiGuildIntegration
                {
                    CaiAuthToken = caiUser.Token,
                    DiscordGuildId = Context.Guild.Id,
                    CreatedAt = DateTime.Now
                };

                await _db.CaiIntegrations.AddAsync(newCaiIntergration);
                await _db.SaveChangesAsync();

                var msg = $"Username: **{caiUser.Username}**\n" +
                          "From now on, this account will be used for all CharacterAI interactions on this server.\n" +
                          "For the next step, use *`/character spawn`* command to spawn new CharacterAI character in this channel.";

                var embed = new EmbedBuilder()
                           .WithTitle($"{IntegrationType.CharacterAI.GetIcon()} CharacterAI user authorized")
                           .WithDescription(msg)
                           .WithColor(IntegrationType.CharacterAI.GetColor())
                           .WithThumbnailUrl(caiUser.UserImageUrl);

                await FollowupAsync(ephemeral: true, embed: $"{MessagesTemplates.OK_SIGN_DISCORD} OK".ToInlineEmbed(Color.Green));
                await Context.Channel.SendMessageAsync(Context.User.Mention, embed: embed.Build());

                return;
            }
        }
    }


    [SlashCommand("remove", "Remove integration from this server")]
    public async Task Remove(IntegrationType type, bool removeAssociatedCharacters)
    {
        await DeferAsync();

        IGuildIntegration? integration = (type switch
        {
            IntegrationType.SakuraAI => await _db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == Context.Guild.Id),
            IntegrationType.CharacterAI => await _db.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == Context.Guild.Id),
        });

        if (integration is null)
        {
             await FollowupAsync(embed: $"There's no {type:G} integration on this server".ToInlineEmbed(Color.Orange));
             return;
        }

        if (removeAssociatedCharacters)
        {
            var channels = await _db.DiscordChannels.Where(c => c.DiscordGuildId == Context.Guild.Id).ToListAsync();

            var charactersInChannels = channels.Select(TargetedCharacters).SelectMany(character => character);

            foreach (var character in charactersInChannels)
            {
                try
                {
                    var webhookId = ulong.Parse(character.WebhookId);
                    var webhookClient = MemoryStorage.CachedWebhookClients.Find(webhookId);
                    if (webhookClient is not null)
                    {
                        await webhookClient.DeleteWebhookAsync();
                        MemoryStorage.CachedWebhookClients.Remove(webhookId);
                    }

                    MemoryStorage.CachedCharacters.Remove(character.Id);
                }
                catch (Exception e) // TODO: handle
                {
                    //
                }
            }

            IEnumerable<CachedCharacterInfo> TargetedCharacters(DiscordChannel channel)
                => MemoryStorage.CachedCharacters.ToList(channel.Id).Where(c => c.IntegrationType == type);
        }

        await DatabaseHelper.DeleteGuildIntegrationAsync(integration, removeAssociatedCharacters);


        await FollowupAsync(embed: $"{type:G} integration {(removeAssociatedCharacters ? "and all associated characters were" : "was")} successfully removed".ToInlineEmbed(Color.Green));
    }
}
