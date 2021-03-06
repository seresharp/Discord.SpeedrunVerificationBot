﻿using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;

namespace VerificationBot.Services
{
    public static class ChangelogService
    {
        public static async Task HandleMessageEditedAsync(VerificationBot bot, MessageUpdatedEventArgs e)
        {
            // Sometimes fires for messages that haven't been edited
            // Unsure if this is a bug, or some other form of message update
            if (e.GuildId == null || e.OldMessage == null
                || e?.OldMessage?.Content == e?.NewMessage?.Content)
            {
                return;
            }

            LocalEmbedBuilder embed = null;
            foreach (ConfigChannel channel in bot.Config
                .GetOrAddGuild(e.GuildId.Value).Channels.Values
                .Where(c => c.ChangelogChannels.Contains(e.ChannelId)))
            {
                if (embed == null)
                {
                    embed = new LocalEmbedBuilder()
                        .WithTitle("Message edited")
                        .AddField("Channel", $"<#{e.OldMessage.ChannelId}>")
                        .AddField("Author", e.OldMessage.Author.Mention)
                        .AddField("Old Content", e.OldMessage.Content.Length > 0 ? e.OldMessage.Content : "N/A")
                        .AddField("Link", $"https://discord.com/channels/{e.GuildId.Value.RawValue}/{e.ChannelId.RawValue}/{e.MessageId.RawValue}");
                }

                if (await bot.GetChannelAsync(channel.GuildId, channel.Id) is ITextChannel textChannel)
                {
                    await textChannel.SendMessageAsync
                    (
                        new LocalMessageBuilder()
                        .WithEmbed(embed)
                        .Build()
                    );
                }
            }
        }

        public static async Task HandleMessageDeletedAsync(VerificationBot bot, MessageDeletedEventArgs e)
        {
            if (e.GuildId == null || e.Message == null)
            {
                return;
            }

            LocalEmbedBuilder embed = null;
            foreach (ConfigChannel channel in bot.Config
                .GetOrAddGuild(e.GuildId.Value).Channels.Values
                .Where(c => c.ChangelogChannels.Contains(e.ChannelId)))
            {
                if (embed == null)
                {
                    embed = new LocalEmbedBuilder()
                        .WithTitle("Message deleted")
                        .AddField("Channel", $"<#{e.Message.ChannelId}>")
                        .AddField("Author", e.Message.Author.Mention);

                    if (e.Message.Content.Length > 0)
                    {
                        embed.AddField("Content", e.Message.Content);
                    }

                    if (e.Message.Attachments.Count > 0)
                    {
                        embed.AddField("Attachments", string.Join('\n', e.Message.Attachments.Select(a => a.Url)));
                    }
                }

                if (await bot.GetChannelAsync(channel.GuildId, channel.Id) is ITextChannel textChannel)
                {
                    await textChannel.SendMessageAsync
                    (
                        new LocalMessageBuilder()
                        .WithEmbed(embed)
                        .Build()
                    );
                }
            }
        }
    }
}
