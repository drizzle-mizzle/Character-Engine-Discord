using System.Text;

namespace CharacterEngineDiscord.Modules.Helpers;


public static class Templates
{
    public static string BuildCharacterDescription(string characterName, string? characterTagline, string characterDescription, string? characterScenario)
    {
        var sb = new StringBuilder($"**{characterName}**\n");


        var tagline = characterTagline?.Trim(' ', '\n');
        if (!string.IsNullOrEmpty(tagline))
        {
            sb.AppendLine(tagline);
            sb.AppendLine();
        }

        var descSplit = characterDescription.Split('\n', StringSplitOptions.TrimEntries);
        var linesCount = Math.Min(descSplit.Length, 16);
        var description = string.Join("\n", descSplit[..linesCount]);

        sb.AppendLine(description.Length < 2 ? "*No description*" : description);

        var scenario = characterScenario?.Trim(' ', '\n');
        if (!string.IsNullOrEmpty(scenario))
        {
            sb.AppendLine();
            sb.AppendLine("**Scenario**");
            sb.AppendLine(scenario);
        }

        sb.AppendLine();

        return sb.ToString();
    }

    public static string BuildCharacterDefinition(string characterName, string characterPersonality, string? characterScenario, (string role, string content)[] exampleDialog)
    {
        string? exampleDialogString = null;

        var messages = exampleDialog.Where(msg => !string.IsNullOrWhiteSpace(msg.content)).ToArray();
        if (messages.Length != 0)
        {
            var dialogLines = messages.Select(message => $"{(message.role.StartsWith('a') ? characterName : "User")}: {message.content}");
            exampleDialogString = string.Join('\n', dialogLines).Trim('\n', ' ');
        }

        return BuildCharacterDefinition(characterName, characterPersonality, characterScenario, exampleDialogString);
    }

    public static string BuildCharacterDefinition(string characterName, string characterPersonality, string? characterScenario, string? exampleDialog)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"About {characterName}:");

        var personality = characterPersonality.Trim(' ', '\n');
        if (!string.IsNullOrWhiteSpace(personality))
        {
            sb.AppendLine("[PERSONALITY]");
            sb.AppendLine(personality);
            sb.AppendLine("[PERSONALITY_END]\n");
        }

        if (!string.IsNullOrWhiteSpace(exampleDialog))
        {
            sb.AppendLine("[EXAMPLE_DIALOG]");
            sb.AppendLine(exampleDialog);
            sb.AppendLine("[EXAMPLE_DIALOG_END]\n");
        }

        var scenario = characterScenario?.Trim(' ', '\n');
        if (!string.IsNullOrWhiteSpace(scenario))
        {
            sb.AppendLine("[SCENARIO]");
            sb.AppendLine(scenario);
            sb.AppendLine("[SCENARIO_END]");
        }

        return sb.ToString().FillCharacterPlaceholders(characterName);

    }


    public static string FillCharacterPlaceholders(this string source, string characterName)
        => source.Replace("{{CHAR}}", characterName, StringComparison.InvariantCultureIgnoreCase)
                 .Replace("{{BOT}}", characterName, StringComparison.InvariantCultureIgnoreCase)
                 .Replace("<CHAR>", characterName, StringComparison.InvariantCultureIgnoreCase)
                 .Replace("<BOT>", characterName, StringComparison.InvariantCultureIgnoreCase);


    public static string FillUserPlaceholders(this string source, string userMention)
        => source.Replace("{{user}}", userMention, StringComparison.InvariantCultureIgnoreCase)
                 .Replace("<user>", userMention, StringComparison.InvariantCultureIgnoreCase);
}
