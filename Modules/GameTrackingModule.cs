using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Qmmands;
using VerificationBot.Attributes;
using VerificationBot.SpeedrunCom;

namespace VerificationBot.Modules
{
    [Name("GameTracking")]
    [Description("Commands for tracking of run verification on speedrun.com")]
    [RequireCustomPermissions(Permission.ManageChannels)]
    public class GameTrackingModule : DiscordModuleBase<VCommandContext>
    {
        [Command("addgame")]
        [Description("Enables tracking of a game's speedrun.com verification queue in the current channel")]
        public async Task AddGameAsync(string gameName)
        {
            Game game = await Game.Find(gameName);
            if (game == null)
            {
                await Response("Couldn't locate game, please supply a valid speedrun.com url or game id");
                return;
            }

            ConcurrentSet<string> trackedGames = Context.Bot.Config
                .GetOrAddGuild(Context.Guild.Id)
                .GetOrAddGameList(Context.Channel.Id);

            if (trackedGames.Contains(game.Id))
            {
                await Response($"Game '{game.Name}' is already being tracked in this channel");
                return;
            }

            trackedGames.Add(game.Id);
            await Response($"Now tracking game '{game.Name}'");
        }

        [Command("removegame", "deletegame", "remgame", "delgame")]
        [Description("Disables tracking of a game's speedrun.com verification queue in the current channel")]
        public async Task RemoveGameAsync(string gameName)
        {
            Game game = await Game.Find(gameName);
            if (game == null)
            {
                await Response("Couldn't locate game, please supply a valid speedrun.com url or game id");
                return;
            }

            ConfigGuild confGuild = Context.Bot.Config.GetOrAddGuild(Context.Guild.Id);

            ConcurrentSet<string> trackedGames = confGuild.GetOrAddGameList(Context.Channel.Id);

            if (!trackedGames.Contains(game.Id))
            {
                await Response($"Game '{game.Name}' is not being tracked in this channel");
                return;
            }

            ConcurrentDictionary<string, ConfigRun> runMessages = confGuild.GetOrAddChannel(Context.ChannelId).RunMessages;
            foreach ((string runId, ConfigRun run) in runMessages)
            {
                IMessage msg = await Context.Bot.GetMessageAsync(Context.ChannelId, run.MsgId);
                if (msg?.Author?.Id != Context.Bot.CurrentUser.Id)
                {
                    continue;
                }

                await msg.DeleteAsync();
                runMessages.Remove(runId, out _);
            }

            trackedGames.Remove(game.Id);
            await Response($"No longer tracking game '{game.Name}' in this channel");
        }
    }
}
