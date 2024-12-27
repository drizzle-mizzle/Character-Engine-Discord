namespace CharacterEngineDiscord.Domain.Models;


public record ModalData(Guid Id, ModalActionType ActionType, string Data);


public record ActionSourceDiscordInfo(ulong ChannelId, ulong UserId);
