using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Rest;
using Qmmands;
using VerificationBot.Services;

namespace VerificationBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireCustomPermissionsAttribute : DiscordGuildCheckAttribute
    {
        public readonly Permission DefaultPerms;

        public RequireCustomPermissionsAttribute(Permission defaultPerms)
            => DefaultPerms = defaultPerms;

        public override async ValueTask<CheckResult> CheckAsync(DiscordGuildCommandContext _)
        {
            if (_ is not VCommandContext context)
            {
                return CheckResult.Failed("Could not check permissions");
            }

            // Check custom perms
            if (PermissionService.UserHasPermission(context))
            {
                return CheckResult.Successful;
            }

            // Check default perms
            List<IRole> roles = await FetchUserRolesAsync(context.Guild, context.Author);
            Permission userPerms = Discord.Permissions.CalculatePermissions(context.Guild, context.Channel, context.Author, roles)
                | (Permission)Discord.Permissions.CalculatePermissions(context.Guild, context.Author, roles);

            return (userPerms & DefaultPerms) != DefaultPerms
                ? CheckResult.Failed("User is missing required permissions")
                : CheckResult.Successful;
        }

        private async Task<List<IRole>> FetchUserRolesAsync(CachedGuild guild, IMember member)
        {
            List<IRole> roles = new();
            foreach (IRole role in await guild.FetchRolesAsync())
            {
                if (member.RoleIds.Contains(role.Id))
                {
                    roles.Add(role);
                }
            }

            return roles;
        }
    }
}
