using CharacterAi.Client.Exceptions;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Repositories;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Helpers;
using CharacterEngineDiscord.Shared.Models;
using Discord;
using Discord.Webhook;
using Microsoft.EntityFrameworkCore;
using MT = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.Helpers.Masters;


public class IntegrationsMaster
{
    private readonly AppDbContext _db;
    private readonly CacheRepository _cacheRepository;
    private readonly CharactersDbRepository _charactersDbRepository;

    public IntegrationsMaster(AppDbContext db, CacheRepository cacheRepository, CharactersDbRepository charactersDbRepository)
    {
        _db = db;
        _cacheRepository = cacheRepository;
        _charactersDbRepository = charactersDbRepository;
    }


    public async Task<ISpawnedCharacter> SpawnCharacterAsync(ulong channelId, CommonCharacter commonCharacter, IGuildIntegration guildIntegration)
    {
        if (CharacterEngineBot.DiscordClient.GetChannel(channelId) is not ITextChannel channel)
        {
            throw new Exception($"Failed to get channel {channelId}");
        }

        var webhook = await InteractionsHelper.CreateDiscordWebhookAsync(channel, commonCharacter.CharacterName, commonCharacter.CharacterImageLink);
        var webhookClient = new DiscordWebhookClient(webhook.Id, webhook.Token);
        _cacheRepository.CachedWebhookClients.Add(webhook.Id, webhookClient);

        ISpawnedCharacter newSpawnedCharacter;
        try
        {
            newSpawnedCharacter = await _charactersDbRepository.CreateSpawnedCharacterAsync(commonCharacter, webhook, guildIntegration);
        }
        catch
        {
            _cacheRepository.CachedWebhookClients.Remove(webhook.Id);

            try
            {
                await webhookClient.DeleteWebhookAsync();
            }
            catch
            {
                // care not
            }

            throw;
        }

        _cacheRepository.CachedCharacters.Add(newSpawnedCharacter);
        MetricsWriter.Write(MetricType.CharacterSpawned, newSpawnedCharacter.Id, $"{newSpawnedCharacter.GetIntegrationType():G} | {newSpawnedCharacter.CharacterName}");
        return newSpawnedCharacter;
    }


    public async Task SendSakuraAiMailAsync(IDiscordInteraction interaction, string email)
    {
        var sakuraAiModule = IntegrationsHub.SakuraAiModule;
        var attempt = await sakuraAiModule.SendLoginEmailAsync(email);

        // Respond to user
        var msg = $"{IntegrationType.SakuraAI.GetIcon()} **SakuraAI**\n\n" +
                  $"Confirmation mail was sent to **{email}**. Please check your mailbox and follow further instructions.\n\n" +
                  $"- *It's **highly recommended** to open an [Incognito Tab](https://support.google.com/chrome/answer/95464), before you open a link in the mail.*\n" +
                  $"- *It may take up to a minute for the bot to react on successful confirmation.*\n" +
                  $"- *If you're willing to put this account into several integrations on different servers, **DO NOT USE `/integration create` command again**, it may break existing integration; use `/integration copy` command instead.*";

        await interaction.FollowupAsync(embed: msg.ToInlineEmbed(bold: false, color: Color.Green), ephemeral: true);

        // Update db
        var data = StoredActionsHelper.CreateSakuraAiEnsureLoginData(attempt, (ulong)interaction.ChannelId!, interaction.User.Id);
        var newAction = new StoredAction(StoredActionType.SakuraAiEnsureLogin, data, maxAttemtps: 25);

        _db.StoredActions.Add(newAction);
        await _db.SaveChangesAsync();
    }


    public async Task SendCharacterAiMailAsync(IDiscordInteraction interaction, string email)
    {
        try
        {
            await IntegrationsHub.CharacterAiModule.SendLoginEmailAsync(email);
        }
        catch (CharacterAiException e)
        {
            await interaction.FollowupAsync(embed: $"{MT.WARN_SIGN_DISCORD} CharacterAI responded with error:\n```{e.Message}```".ToInlineEmbed(Color.Red), ephemeral: true);

            return;
        }

        var msg = $"{IntegrationType.CharacterAI.GetIcon()} **CharacterAI**\n\n" +
                  $"Sign in mail was sent to **{email}**, please check your mailbox.\nYou should've received a sign in link for CharacterAI in it - **DON'T OPEN IT (!)**, copy it and then paste in `/integration confirm` command.\n" +
                  $"**Example:**\n*`/integration confirm type:CharacterAI data:https://character.ai/login/xxx`*";

        await interaction.FollowupAsync(embed: msg.ToInlineEmbed(bold: false, color: Color.Green), ephemeral: true);
    }


    public async Task EnsureSakuraAiLoginAsync(StoredAction action)
    {
        const IntegrationType type = IntegrationType.SakuraAI;

        var signInAttempt = action.ExtractSakuraAiLoginData();
        var result = await IntegrationsHub.SakuraAiModule.EnsureLoginByEmailAsync(signInAttempt);

        var sourceInfo = action.ExtractDiscordSourceInfo();
        var channel = (ITextChannel)CharacterEngineBot.DiscordClient.GetChannel(sourceInfo.ChannelId);

        var integration = await _db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == channel.GuildId);
        if (integration is not null)
        {
            integration.SakuraEmail = signInAttempt.Email;
            integration.SakuraSessionId = result.SessionId;
            integration.SakuraRefreshToken = result.RefreshToken;
            integration.CreatedAt = DateTime.Now;
        }
        else
        {
            var newSakuraIntegration = new SakuraAiGuildIntegration
            {
                DiscordGuildId = channel.GuildId,
                SakuraEmail = signInAttempt.Email,
                SakuraSessionId = result.SessionId,
                SakuraRefreshToken = result.RefreshToken,
                CreatedAt = DateTime.Now
            };

            MetricsWriter.Write(MetricType.IntegrationCreated, newSakuraIntegration.Id, $"{type:G} | {newSakuraIntegration.SakuraEmail}");
            _db.SakuraAiIntegrations.Add(newSakuraIntegration);
        }
        await _db.SaveChangesAsync();

        var msg = $"Username: **{result.Username}**\n" +
                  "From now on, this account will be used for all SakuraAI interactions on this server.\n" +
                  type.GetNextStepTail();

        var embed = new EmbedBuilder()
                   .WithTitle($"{type.GetIcon()} SakuraAI user authorized")
                   .WithDescription(msg)
                   .WithColor(type.GetColor())
                   .WithThumbnailUrl(result.UserImageUrl);

        var user = await channel.GetUserAsync(sourceInfo.UserId);
        await channel.SendMessageAsync(user.Mention, embed: embed.Build());
    }

}
