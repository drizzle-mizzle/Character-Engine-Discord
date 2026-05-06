using Discord.Interactions;

namespace CharacterEngineDiscord.Domain.Models;


public enum ModalActionType
{
    CreateIntegration = 1,
    OpenRouterSettings = 2,
}


public enum SettingTarget
{
    Guild = 1,
    Channel = 2,
    Integration = 4,
    Character = 3,
}


public enum ButtonActionType
{
    SearchQuery = 1,
}


public enum SinglePropertyAction
{
    show, update,

    [ChoiceDisplay("reset-default")]
    resetDefault
}


public enum UserAction
{
    show, add, remove,

    [ChoiceDisplay("clear-all")]
    clearAll
}


public enum MetricUserSource
{
    SlashCommand, Button, CharacterCall
}
