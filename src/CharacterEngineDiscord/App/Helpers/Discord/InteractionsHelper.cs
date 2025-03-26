using System.Text.RegularExpressions;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Infrastructure;
using CharacterEngineDiscord.Domain.Models;
using Discord;
using Discord.WebSocket;
using PhotoSauce.MagicScaler;
using static CharacterEngine.App.Helpers.CommonHelper;
using MT = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.Helpers.Discord;


public static class InteractionsHelper
{
    private static readonly Regex DISCORD_REGEX = new("discord", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);


    #region CustomId


    public static string NewCustomId(ModalActionType action, string data)
        => $"{action:D}{COMMAND_SEPARATOR}{data}";


    public static ModalData ParseCustomId(string customId)
    {
        var parts = customId.Split(COMMAND_SEPARATOR);
        return new ModalData(Enum.Parse<ModalActionType>(parts[0]), parts[1]);
    }

    #endregion





    public static async Task<IWebhook> CreateDiscordWebhookAsync(IIntegrationChannel channel, string name, string? imageUrl)
    {
        var characterName = name.Trim();
        var match = DISCORD_REGEX.Match(characterName);
        if (match.Success)
        {
            var discordCensored = match.Value.Replace('o', 'о').Replace('O', 'О');
            characterName = characterName.Replace(match.Value, discordCensored);
        }

        var avatar = await DownloadFileAsync(imageUrl);

        using var avatarInput = new MemoryStream();
        using var avatarOutput = new MemoryStream();

        if (avatar is null)
        {
            var defaultAvatar = await File.ReadAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "img", BotConfig.DEFAULT_AVATAR_FILE));
            await avatarOutput.WriteAsync(defaultAvatar);
        }
        else
        {
            await avatar.CopyToAsync(avatarInput);
            avatarInput.Seek(0, SeekOrigin.Begin);

            if (avatarInput.Length < 10240000)
            {
                await avatarInput.CopyToAsync(avatarOutput);
            }
            else
            {
                var settings = new ProcessImageSettings
                {
                    Interpolation = InterpolationSettings.Cubic,
                    ResizeMode = CropScaleMode.Crop,
                    Anchor = CropAnchor.Top,
                    HybridMode = HybridScaleMode.Turbo,
                    OrientationMode = OrientationMode.Normalize,
                    ColorProfileMode = ColorProfileMode.ConvertToSrgb,
                    EncoderOptions = new JpegEncoderOptions(0, ChromaSubsampleMode.Default),
                    Width = 600,
                };

                MagicImageProcessor.ProcessImage(avatarInput, avatarOutput, settings);
            }
        }

        avatarOutput.Seek(0, SeekOrigin.Begin);

        return channel is SocketThreadChannel { ParentChannel: ITextChannel parentChannel }
                ? await parentChannel.CreateWebhookAsync(characterName, avatarOutput)
                : await channel.CreateWebhookAsync(characterName, avatarOutput);
    }
    public static async Task RespondWithErrorAsync(IDiscordInteraction interaction, Exception e, string traceId)
    {
        var userFriendlyExceptionCheck = e.ValidateUserFriendlyException();

        Embed embed;

        if (userFriendlyExceptionCheck.Pass)
        {
            var ufEx = e as UserFriendlyException ?? e.InnerException as UserFriendlyException;

            var message = (ufEx?.Bold ?? true) ? $"**{userFriendlyExceptionCheck.Message}**" : userFriendlyExceptionCheck.Message!;
            if (!(message.StartsWith(MT.X_SIGN_DISCORD) || message.StartsWith(MT.WARN_SIGN_DISCORD)))
            {
                message = $"{MT.X_SIGN_DISCORD} {message}";
            }

            embed = new EmbedBuilder().WithColor(ufEx?.Color ?? Color.Red).WithDescription(message).Build();
        }
        else
        {
            embed = new EmbedBuilder().WithColor(Color.Red)
                                      .WithDescription($"{MT.X_SIGN_DISCORD} **Something went wrong!**")
                                      .WithFooter($"ERROR TRACE ID: {traceId}")
                                      .Build();
        }

        try
        {
            await interaction.RespondAsync(embed: embed);
        }
        catch
        {
            try
            {
                await interaction.FollowupAsync(embed: embed);
            }
            catch
            {
                try
                {
                    await interaction.ModifyOriginalResponseAsync(msg => { msg.Embed = embed; });
                }
                catch
                {
                    try
                    {
                        var channel = (ITextChannel)CharacterEngineBot.DiscordClient.GetChannel((ulong)interaction.ChannelId!);
                        await channel.SendMessageAsync(embed: embed);
                    }
                    catch
                    {
                        // ...but in the end, it doesn't even matter
                    }
                }
            }
        }
    }


}
