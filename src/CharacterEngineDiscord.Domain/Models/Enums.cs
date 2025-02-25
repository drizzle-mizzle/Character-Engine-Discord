using Discord.Interactions;

namespace CharacterEngineDiscord.Domain.Models;


public enum IntegrationType
{
    SakuraAI = 1,
    CharacterAI = 2,
    OpenRouter = 3,
}


public enum CharacterSourceType
{
    SakuraAI = 1,
    // ChubAI = 3,
    // CharacterTavern = 4
}


public enum ModalActionType
{
    CreateIntegration = 1
}


public enum ButtonActionType
{
    SearchQuery = 1,
}


public enum MessagesFormatAction
{
    show, update,

    [ChoiceDisplay("reset-default")]
    resetDefault
}

public enum MessagesFormatTarget { guild, channel, character }

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
