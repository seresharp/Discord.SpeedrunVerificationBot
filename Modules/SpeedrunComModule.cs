using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using VerificationBot.Attributes;
using VerificationBot.SpeedrunCom;
using Qmmands;

namespace VerificationBot.Modules
{
    [Name("SpeedrunCom")]
    [Description("Commands used for retrieving info from speedrun.com")]
    public class SpeedrunComModule : DiscordModuleBase<VCommandContext>
    {
        [Command("setsrrole")]
        [Description("Sets the speedrunner role the bot gives people with verified runs")]
        [RequireCustomPermissions(Permission.Administrator)]
        public async Task SetRoleAsync(ulong roleId)
        {
            IRole role = Context.Guild.Roles.FirstOrDefault(p => p.Key == roleId).Value
                ?? (await Context.Guild.FetchRolesAsync()).FirstOrDefault(r => r.Id == roleId);
            if (role == null)
            {
                await Response("Role not found");
            }

            Context.Bot.Config.GetOrAddGuild(Context.GuildId).SpeedrunnerRole = roleId;
            await Response($"Set role '{role.Name}' as speedrunner role");
        }

        [Command("setsrrolegames")]
        [Description("Sets the required games for granting the speedrunner role")]
        [RequireCustomPermissions(Permission.Administrator)]
        public async Task SetGamesAsync(params string[] games)
        {
            foreach (string gameId in games)
            {
                if (await Game.Find(gameId) == null)
                {
                    await Response($"Could not find game '{gameId}'");
                    return;
                }
            }

            Context.Bot.Config.GetOrAddGuild(Context.GuildId).SpeedrunnerGames.Clear();
            Context.Bot.Config.GetOrAddGuild(Context.GuildId).SpeedrunnerGames.AddRange(games);

            await Response("Speedrunner games set");
        }

        [Command("grantsrrole")]
        [Description("Grants the speedrunner role if a verified run is found")]
        public async Task GrantRoleAsync(string speedruncomName)
        {
            ConfigGuild confGuild = Context.Bot.Config.GetOrAddGuild(Context.GuildId);
            if (confGuild.SpeedrunnerRole == default || confGuild.SpeedrunnerGames.Count == 0)
            {
                await Response("Speedrun system not setup, an admin will have to provide a role id and game list");
                return;
            }

            User user = await User.FindById(speedruncomName);
            if (user == null)
            {
                await Response("User not found");
                return;
            }

            string discord = await user.GetDiscordAsync();
            if (discord?.ToLower() != $"{Context.Author.Name.ToLower()}#{Context.Author.Discriminator}")
            {
                await Response("Please ensure your discord account is linked on your speedrun.com profile");
                return;
            }

            foreach (string gameId in confGuild.SpeedrunnerGames)
            {
                Game game = await Game.Find(gameId);
                if (game == null)
                {
                    return;
                }

                // Only really have to check if any run exists, but not sure how to do this aside from await foreach
                await foreach (Run _ in game.GetRunsAsync(RunStatus.Verified, user.Id))
                {
                    await Context.Author.GrantRoleAsync(confGuild.SpeedrunnerRole);
                    await Response("Role granted");
                    return;
                }
            }
        }
    }
}
