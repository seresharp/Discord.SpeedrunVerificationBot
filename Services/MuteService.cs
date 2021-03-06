﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;

namespace VerificationBot.Services
{
    public static class MuteService
    {
        public static async Task<(bool success, string failureReason)> MuteUserAsync(CachedGuild guild, IMember user, string time, Config config)
        {
            IRole mutedRole = await GetMutedRoleAsync(guild);
            if (mutedRole == null)
            {
                return (false, "Failed getting/creating muted role");
            }

            if (user.RoleIds.Contains(mutedRole.Id))
            {
                return (false, "User is already muted");
            }

            TimeSpan muteTime = ParseTime(time);
            if (muteTime == TimeSpan.Zero)
            {
                return (false, "Failed parsing mute time, please give in the format '1d6h30m'");
            }

            List<ulong> revokedRoles = new();
            foreach (ulong role in user.RoleIds)
            {
                try
                {
                    await user.RevokeRoleAsync(role);
                    revokedRoles.Add(role);
                }
                catch
                {
                    // Do nothing, ignoring roles that the bot has no perms to remove
                }
            }

            await user.GrantRoleAsync(mutedRole.Id);
            config.GetOrAddGuild(guild.Id).CurrentMutes[user.Id] = new Mute
            {
                GuildId = guild.Id,
                UserId = user.Id,
                RoleIds = revokedRoles.ToArray(),
                UnmuteTime = DateTime.Now + muteTime
            };

            return (true, string.Empty);
        }

        public static async Task<(bool success, string failureReason)> UnmuteUserAsync(CachedGuild guild, IMember user, Config config)
        {
            IRole mutedRole = await GetMutedRoleAsync(guild);
            if (mutedRole == null)
            {
                return (false, "Failed getting/creating muted role");
            }

            await user.RevokeRoleAsync(mutedRole.Id);

            config.GetOrAddGuild(guild.Id).CurrentMutes.Remove(user.Id, out Mute muteInfo);

            foreach (ulong roleId in muteInfo?.RoleIds ?? new ulong[0])
            {
                try
                {
                    await user.GrantRoleAsync(roleId);
                }
                catch
                {
                    // Do nothing, ignoring roles that stopped existing since the mute
                }
            }

            return (true, string.Empty);
        }

        public static async Task<IRole> GetMutedRoleAsync(CachedGuild guild)
        {
            const string MUTED_NAME = "Muted";

            // Get or create role
            IRole muted = (await guild.FetchRolesAsync()).FirstOrDefault(r => r.Name == MUTED_NAME);
            if (muted == null)
            {
                muted = await guild.CreateRoleAsync(prop =>
                {
                    prop.Color = Color.Gray;
                    prop.IsHoisted = false;
                    prop.IsMentionable = false;
                    prop.Name = MUTED_NAME;
                    prop.Permissions = new GuildPermissions(Permission.ViewChannel);
                });
            }

            if (muted == null)
            {
                return null;
            }

            // Ensure no channels allow this role to speak
            LocalOverwrite denyPerms = new
            (
                muted,
                new OverwritePermissions
                (
                    ChannelPermissions.None,
                    new ChannelPermissions(Permission.Speak | Permission.SendMessages | Permission.AddReactions)
                )
            );

            try
            {
                await Task.WhenAll((await guild.FetchChannelsAsync()).Select(channel => channel.SetOverwriteAsync(denyPerms)));
            }
            catch (RestApiException)
            {
                // Do nothing, ignoring channels the bot lacks permissions for
            }

            return muted;
        }

        public static async Task CheckUnmutes(Config config, IReadOnlyDictionary<Snowflake, CachedGuild> guilds)
        {
            foreach ((ulong guildId, ConfigGuild confGuild) in config.Guilds)
            {
                foreach ((ulong userId, Mute mute) in confGuild.CurrentMutes)
                {
                    if (!guilds.TryGetValue(mute.GuildId, out CachedGuild guild))
                    {
                        confGuild.CurrentMutes.Remove(userId, out _);
                        continue;
                    }

                    if (DateTime.Now < mute.UnmuteTime)
                    {
                        continue;
                    }

                    IMember user = await guild.FetchMemberAsync(mute.UserId);
                    if (user != null)
                    {
                        await UnmuteUserAsync(guild, user, config);
                    }

                    confGuild.CurrentMutes.Remove(userId, out _);
                }
            }
        }

        public static TimeSpan ParseTime(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr))
            {
                return TimeSpan.Zero;
            }

            TimeSpan timeTotal = TimeSpan.Zero;
            int timeCurrent = 0;
            foreach (char c in timeStr)
            {
                if (c >= '0' && c <= '9')
                {
                    timeCurrent = timeCurrent * 10 + (c - '0');
                }
                else
                {
                    switch (char.ToLower(c))
                    {
                        case 'd':
                            timeTotal += TimeSpan.FromDays(timeCurrent);
                            break;
                        case 'h':
                            timeTotal += TimeSpan.FromHours(timeCurrent);
                            break;
                        case 'm':
                            timeTotal += TimeSpan.FromMinutes(timeCurrent);
                            break;
                        default:
                            return TimeSpan.Zero;
                    }

                    timeCurrent = 0;
                }
            }

            return timeTotal;
        }

        public static string GetTimeString(TimeSpan time)
        {
            StringBuilder timeStr = new();

            if (time.Days > 0) timeStr.Append($"{time.Days}d");
            if (time.Hours > 0) timeStr.Append($"{time.Hours}h");
            if (time.Minutes > 0) timeStr.Append($"{time.Minutes}m");
            if (time.Seconds > 0) timeStr.Append($"{time.Seconds}s");

            return timeStr.ToString();
        }
    }
}
