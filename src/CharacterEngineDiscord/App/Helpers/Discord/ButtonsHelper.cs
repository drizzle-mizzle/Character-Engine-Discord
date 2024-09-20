using Discord;

namespace CharacterEngine.App.Helpers.Discord;


public class ButtonsHelper
{
    private const string _upCustomId = $"sq{InteractionsHelper.SEP}up";
    private const string _downCustomId = $"sq{InteractionsHelper.SEP}down";
    private const string _selectCustomId = $"sq{InteractionsHelper.SEP}select";
    private const string _leftCustomId = $"sq{InteractionsHelper.SEP}left";
    private const string _rightCustomId = $"sq{InteractionsHelper.SEP}right";


    public static MessageComponent BuildSelectButtons(bool withPages)
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
