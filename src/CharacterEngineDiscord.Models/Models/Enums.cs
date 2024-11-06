namespace CharacterEngineDiscord.Models;


public enum IntegrationType
{
    SakuraAI = 1,
    CharacterAI = 2,
}


public enum ModalActionType
{
    CreateIntegration = 1
}


public enum ButtonActionType
{
    SearchQuery = 1,
}

public enum MessagesFormatAction { show, update, resetDefault }

public enum MessagesFormatTarget { guild, channel, character }
