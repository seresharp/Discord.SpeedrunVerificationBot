using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Qmmands;
using VerificationBot.Services;

namespace VerificationBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireCustomPermissionsAttribute : GuildOnlyAttribute
    {
        public readonly Permission DefaultPerms;

        public RequireCustomPermissionsAttribute(Permission defaultPerms)
            => DefaultPerms = defaultPerms;

        public override async ValueTask<CheckResult> CheckAsync(CommandContext _)
        {
            // Check base (guild only)
            CheckResult baseResult = await base.CheckAsync(_);
            if (!baseResult.IsSuccessful || _ is not VCommandContext context)
            {
                return baseResult;
            }

            // Check custom perms
            if (PermissionService.UserHasPermission(context))
            {
                return CheckResult.Successful;
            }

            // Check default perms
            Permission userPerms = context.Member.GetPermissionsFor((IGuildChannel)context.Channel).Permissions | context.Member.Permissions;
            return (userPerms & DefaultPerms) != DefaultPerms
                ? CheckResult.Unsuccessful("User is missing required permissions")
                : CheckResult.Successful;
        }
    }
}
