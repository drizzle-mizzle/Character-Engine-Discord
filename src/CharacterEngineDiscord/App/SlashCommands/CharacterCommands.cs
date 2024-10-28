using System.ComponentModel;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands;


[DeferAndValidatePermissions]
[ValidateAccessLevel(AccessLevels.Manager)]
[Group("character", "Basic characters commands")]
public class CharacterCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private const string ANY_IDENTIFIER_DESC = "Character call prefix or User ID or Character ID";

    public CharacterCommands(AppDbContext db, DiscordSocketClient discordClient)
    {
        _db = db;
        _discordClient = discordClient;
    }


    [SlashCommand("spawn", "Spawn new character!")]
    public async Task SpawnCharacter(string query, IntegrationType integrationType)
    {
        await FollowupAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var module = integrationType.GetIntegrationModule();
        var characters = await module.SearchAsync(query);

        if (characters.Count == 0)
        {
            await ModifyOriginalResponseAsync(msg => { msg.Embed = $"{integrationType.GetIcon()} No characters were found by query **\"{query}\"**".ToInlineEmbed(Color.Orange, false); });
            return;
        }

        var searchQuery = new SearchQuery(Context.Channel.Id, Context.User.Id, query, characters, integrationType);
        MemoryStorage.SearchQueries.Add(searchQuery);

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = InteractionsHelper.BuildSearchResultList(searchQuery);
            msg.Components = ButtonsHelper.BuildSearchButtons(searchQuery.Pages > 1);
        });
    }


    [SlashCommand("reset", "Start new chat")]
    public async Task ResetCharacter([Description(ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await FollowupAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var cachedCharacter = MemoryStorage.CachedCharacters.Find(anyIdentifier, Context.Channel.Id);

        if (cachedCharacter is null)
        {
            throw new UserFriendlyException("Character not found");
        }

        var spawnedCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
        if (spawnedCharacter is null)
        {
            throw new UserFriendlyException("Character not found");
        }

        spawnedCharacter.ResetWithNextMessage = true;
        await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

        var character = (ICharacter)spawnedCharacter;
        var message = $"{MessagesTemplates.OK_SIGN_DISCORD} Chat with **{character.CharacterName}** reset successfully";
        await ModifyOriginalResponseAsync(msg => { msg.Embed = message.ToInlineEmbed(Color.Green, bold: false); });

        var user = (IGuildUser)Context.User;
        await ((ICharacter)spawnedCharacter).SendGreetingAsync(user.DisplayName);
    }


    [SlashCommand("remove", "Remove character")]
    public async Task RemoveCharacter([Description(ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        var cachedCharacter = MemoryStorage.CachedCharacters.Find(anyIdentifier, Context.Channel.Id);

        if (cachedCharacter is null)
        {
            throw new UserFriendlyException("Character not found");
        }

        var webhookClient = MemoryStorage.CachedWebhookClients.GetById(cachedCharacter.WebhookId)!;
        await webhookClient.DeleteWebhookAsync();

        MemoryStorage.CachedCharacters.Remove(cachedCharacter.Id);
        MemoryStorage.CachedWebhookClients.Remove(cachedCharacter.WebhookId);

        var spawnedCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
        if (spawnedCharacter is null)
        {
            throw new UserFriendlyException("Character not found");
        }

        await DatabaseHelper.DeleteSpawnedCharacterAsync(spawnedCharacter);

        var message = $"{MessagesTemplates.OK_SIGN_DISCORD} Character **{((ICharacter)spawnedCharacter).CharacterName}** removed successfully";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));
    }
}
