using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using VerificationBot.Attributes;
using VerificationBot.Services;
using Qmmands;

namespace VerificationBot.Modules
{
    [Name("Permissions")]
    [Description("Commands for modifying bot command permissions")]
    [RequireCustomPermissions(Permission.Administrator)]
    public class PermissionsModule : DiscordModuleBase<VCommandContext>
    {
        [Command("addperm")]
        [Description("Adds permissions for a given module/command to a given role/user")]
        public Task AddPermissionAsync(string roleOrUser, ulong id, string nameOfModuleOrCommand)
        => SetPermissionAsync(roleOrUser, id, nameOfModuleOrCommand, true);

        [Command("removeperm", "deleteperm", "remperm", "delperm")]
        [Description("Removes permissions for a given module/command from a given role/user")]
        public Task RemovePermissionAsync(string roleOrUser, ulong id, string nameOfModuleOrCommand)
            => SetPermissionAsync(roleOrUser, id, nameOfModuleOrCommand, false);

        [Command("listperms")]
        [Description("Returns a list of active permission overrides")]
        public async Task ListPermissionsAsync()
        {
            ConfigGuild guild = Context.Bot.Config.GetOrAddGuild(Context.Guild.Id);

            Dictionary<string, List<string>> mentionDict = new();
            foreach ((ulong userId, ConcurrentSet<string> perms) in guild.UserPermOverrides)
            {
                foreach (string perm in perms)
                {
                    if (!mentionDict.TryGetValue(perm, out List<string> mentions))
                    {
                        mentions = new();
                        mentionDict[perm] = mentions;
                    }

                    mentions.Add($"<@{userId}>");
                }
            }

            foreach ((ulong roleId, ConcurrentSet<string> perms) in guild.RolePermOverrides)
            {
                foreach (string perm in perms)
                {
                    if (!mentionDict.TryGetValue(perm, out List<string> mentions))
                    {
                        mentions = new();
                        mentionDict[perm] = mentions;
                    }

                    mentions.Add($"<@&{roleId}>");
                }
            }

            LocalEmbedBuilder embed = new() { Title = "Permission overrides" };
            foreach ((string perm, List<string> mentions) in mentionDict)
            {
                embed.AddField(perm, string.Join(", ", mentions));
            }

            await Response(embed);
        }

        private async Task SetPermissionAsync(string roleOrUser, ulong id, string nameOfModuleOrCommand, bool enable)
        {
            if (roleOrUser == "role")
            {
                (_, string message) = await PermissionService.SetRolePermission(Context, id, nameOfModuleOrCommand, enable);
                await Response(message);
            }
            else if (roleOrUser == "user")
            {
                (_, string message) = await PermissionService.SetUserPermission(Context, id, nameOfModuleOrCommand, enable);
                await Response(message);
            }
            else
            {
                await Response($"Invalid permission type '{roleOrUser}', please specify 'role' or 'user'");
            }
        }
    }
}
