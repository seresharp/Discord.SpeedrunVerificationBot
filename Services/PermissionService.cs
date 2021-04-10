using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Rest;

namespace VerificationBot.Services
{
    public static class PermissionService
    {
        public static bool UserHasPermission(VCommandContext context)
        {
            ConfigGuild guild = context.Bot.Config.GetOrAddGuild(context.Guild.Id);

            // Check user perms
            ConcurrentSet<string> perms = guild.GetOrAddUserPerms(context.CurrentMember.Id);
            if (perms.Contains(context.Command.Module.Name) || perms.Contains(context.Command.Name))
            {
                return true;
            }

            // Check role perms
            foreach (ulong roleId in context.CurrentMember.RoleIds)
            {
                perms = guild.GetOrAddRolePerms(roleId);
                if (perms.Contains(context.Command.Module.Name) || perms.Contains(context.Command.Name))
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task<(bool success, string message)> SetUserPermission(VCommandContext context, ulong userId, string permInput, bool givePerm)
        {
            if (!MatchModuleOrCommand(context.Bot, permInput, out string perm))
            {
                return (false, $"Permission '{permInput}' is not valid");
            }

            if (await context.Guild.FetchMemberAsync(userId) is not IMember user)
            {
                return (false, "User not found");
            }

            ConcurrentSet<string> userPerms = context.Bot.Config
                .GetOrAddGuild(context.Guild.Id)
                .GetOrAddUserPerms(userId);

            // Give permission
            if (givePerm)
            {
                if (userPerms.Contains(perm))
                {
                    return (false, $"User {user.Name}#{user.Discriminator} already has permission '{perm}'");
                }

                userPerms.Add(perm);
                await context.Bot.Config.Save(Program.CONFIG_FILE);
                return (true, $"Given permission '{perm}' to user {user.Name}#{user.Discriminator}");
            }

            // Revoke permission
            if (!userPerms.Contains(perm))
            {
                return (false, $"User {user.Name}#{user.Discriminator} does not have permission '{perm}'");
            }

            userPerms.Remove(perm);
            await context.Bot.Config.Save(Program.CONFIG_FILE);
            return (true, $"Revoked permission '{perm}' from user {user.Name}#{user.Discriminator}");
        }

        public static async Task<(bool success, string message)> SetRolePermission(VCommandContext context, ulong roleId, string permInput, bool givePerm)
        {
            if (!MatchModuleOrCommand(context.Bot, permInput, out string perm))
            {
                return (false, $"Permission '{permInput}' is not valid");
            }

            if ((await context.Guild.FetchRolesAsync())
                .FirstOrDefault(role => role.Id == roleId)
                is not IRole role)
            {
                return (false, "Role not found");
            }

            ConcurrentSet<string> rolePerms = context.Bot.Config
                .GetOrAddGuild(context.Guild.Id)
                .GetOrAddRolePerms(roleId);

            // Give permission
            if (givePerm)
            {
                if (rolePerms.Contains(perm))
                {
                    return (false, $"Role '{role.Name}' already has permission '{perm}'");
                }

                rolePerms.Add(perm);
                await context.Bot.Config.Save(Program.CONFIG_FILE);
                return (true, $"Given permission '{perm}' to role '{role.Name}'");
            }

            // Revoke permission
            if (!rolePerms.Contains(perm))
            {
                return (false, $"Role '{role.Name}' does not have permission '{perm}'");
            }

            rolePerms.Remove(perm);
            await context.Bot.Config.Save(Program.CONFIG_FILE);
            return (true, $"Revoked permission '{perm}' from role '{role.Name}'");
        }

        public static bool MatchModuleOrCommand(VerificationBot bot, string match, out string perm)
        {
            foreach ((string moduleName, IReadOnlyList<string> commandNames) in bot.AllCommandNames)
            {
                if (string.Equals(match, moduleName, StringComparison.InvariantCultureIgnoreCase))
                {
                    perm = moduleName;
                    return true;
                }

                foreach (string commandName in commandNames)
                {
                    if (string.Equals(match, commandName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        perm = commandName;
                        return true;
                    }
                }
            }

            perm = null;
            return false;
        }
    }
}
