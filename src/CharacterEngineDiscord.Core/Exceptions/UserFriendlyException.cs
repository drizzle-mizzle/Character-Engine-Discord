namespace CharacterEngineDiscord.Core.Exceptions;

/// <summary>
/// Thrown to signal an expected, user-meaningful business error that should be
/// shown to the originating Discord user verbatim instead of treated as a bug.
/// Routers/handlers that own an interaction context catch this exception, post
/// the message to Discord as an ephemeral followup, and ack the underlying
/// queue message (no requeue, no admin-channel error report).
/// </summary>
public sealed class UserFriendlyException : Exception
{
    public UserFriendlyException(string message) : base(message)
    {
    }

    public UserFriendlyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
