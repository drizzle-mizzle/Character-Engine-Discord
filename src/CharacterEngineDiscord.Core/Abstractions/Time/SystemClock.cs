namespace CharacterEngineDiscord.Core.Abstractions.Time;

/// <summary>
/// Production <see cref="IClock"/> implementation backed by <see cref="DateTime.UtcNow"/>.
/// Registered through an explicit factory in <c>AddCharacterEngineCore</c> because the
/// type is <c>internal</c> and cannot be resolved by direct type-mapping cross-assembly.
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
