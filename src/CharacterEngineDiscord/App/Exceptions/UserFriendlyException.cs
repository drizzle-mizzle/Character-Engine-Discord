namespace CharacterEngine.App.Exceptions;


public class UserFriendlyException : Exception
{
    public bool Bold { get; }


    public UserFriendlyException(string message, bool bold = true) : base(message)
    {
        Bold = bold;
    }
}
