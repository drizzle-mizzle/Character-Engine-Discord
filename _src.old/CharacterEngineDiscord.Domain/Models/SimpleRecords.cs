namespace CharacterEngineDiscord.Domain.Models;


public record ModalData(ModalActionType ActionType, string Data);


public record ActionSourceDiscordInfo(ulong ChannelId, ulong UserId);
