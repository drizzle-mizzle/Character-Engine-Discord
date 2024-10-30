using Discord;

namespace CharacterEngine.App.Helpers.Discord;


public class ButtonsHelper
{
    private const string _upCustomId = $"sq{InteractionsHelper.COMMAND_SEPARATOR}up";
    private const string _downCustomId = $"sq{InteractionsHelper.COMMAND_SEPARATOR}down";
    private const string _selectCustomId = $"sq{InteractionsHelper.COMMAND_SEPARATOR}select";
    private const string _leftCustomId = $"sq{InteractionsHelper.COMMAND_SEPARATOR}left";
    private const string _rightCustomId = $"sq{InteractionsHelper.COMMAND_SEPARATOR}right";


    public static MessageComponent BuildSearchButtons(bool withPages)
    {
        // List navigation buttons
        var buttons = new ComponentBuilder().WithButton(emote: new Emoji("\u2B06"), customId: _upCustomId, style: ButtonStyle.Secondary)
                                            .WithButton(emote: new Emoji("\u2B07"), customId: _downCustomId, style: ButtonStyle.Secondary)
                                            .WithButton(emote: new Emoji("\u2705"), customId: _selectCustomId, style: ButtonStyle.Success);
        // Pages navigation buttons
        if (withPages)
        {
            buttons.WithButton(emote: new Emoji("\u2B05"), customId: _leftCustomId, row: 1)
                   .WithButton(emote: new Emoji("\u27A1"), customId: _rightCustomId, row: 1);
        }

        return buttons.Build();
    }
}
