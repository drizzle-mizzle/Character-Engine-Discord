namespace CharacterEngineDiscord.Core.Abstractions.Time;

/// <summary>
/// Wall-clock abstraction for testability. Production implementation returns
/// <see cref="DateTime.UtcNow"/>; tests substitute a controllable clock.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
