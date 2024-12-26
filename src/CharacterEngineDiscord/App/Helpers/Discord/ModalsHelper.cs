using CharacterEngine.App.Exceptions;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using Discord;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;


public static class ModalsHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static Modal BuildSakuraAiAuthModal(this ModalBuilder modalBuilder)
    {
        modalBuilder.AddTextInput("Account email", "email", placeholder: "yourmail@bipki.com", required: true, minLength: 2, maxLength: 128);

        return modalBuilder.Build();
    }


    public static Modal BuildCaiAiAuthModal(this ModalBuilder modalBuilder)
    {
        modalBuilder.AddTextInput("Account email", "email", placeholder: "yourmail@bipki.com", required: true, minLength: 2, maxLength: 128);

        return modalBuilder.Build();
    }


    public static Modal BuildOpenRouterAuthModal(this ModalBuilder modalBuilder)
    {
        modalBuilder.AddTextInput("API key", "api-key", required: true, placeholder: "sk-or-v1-0000000000000000000000000000000000000000000000000000000000000069");
        modalBuilder.AddTextInput("Default model", "model", required: false, value: "mistralai/mistral-7b-instruct:free");

        return modalBuilder.Build();
    }


     public static Task CreateSakuraAiIntegrationAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        return InteractionsHelper.SendSakuraAiMailAsync(modal, email);
    }


    public static Task CreateCharacterAiIntegrationAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        return InteractionsHelper.SendCharacterAiMailAsync(modal, email);
    }


    public static async Task CreateOpenRouterIntegrationAsync(SocketModal modal)
    {
        const IntegrationType type = IntegrationType.OpenRouter;

        var apiKey = modal.Data.Components.First(c => c.CustomId == "api-key").Value.Trim('\n', ' ');

        if (!apiKey.StartsWith("sk-or"))
        {
            throw new UserFriendlyException("Wrong API key");
        }

        var model = modal.Data.Components.First(c => c.CustomId == "model").Value.Trim('\n', ' ');

        var newIntegration = new OpenRouterGuildIntegration
        {
            OpenRouterApiKey = apiKey,
            OpenRouterModel = model,
            DiscordGuildId = modal.GuildId!.Value,
            CreatedAt = DateTime.Now
        };

        await using var db = DatabaseHelper.GetDbContext();
        db.OpenRouterIntegrations.Add(newIntegration);
        await db.SaveChangesAsync();

        var embed = new EmbedBuilder().WithTitle($"{type.GetIcon()} {type:G} API key registered")
                                      .WithColor(IntegrationType.CharacterAI.GetColor())
                                      .WithDescription($"From now on, this API key will be used for all {type:G} interactions on this server.\n{type.GetNextStepTail()}");

        await modal.FollowupAsync(embed: embed.Build());
    }


}
