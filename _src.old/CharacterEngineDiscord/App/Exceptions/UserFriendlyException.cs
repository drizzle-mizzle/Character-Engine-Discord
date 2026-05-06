using Discord;

namespace CharacterEngine.App.Exceptions;


public class UserFriendlyException : Exception
{
    public bool Bold { get; }
    public Color Color { get; }


    public UserFriendlyException(string message, bool bold = true, Color? color = null) : base(message)
    {
        Bold = bold;
        Color = color ?? Color.LightOrange;
    }
}
