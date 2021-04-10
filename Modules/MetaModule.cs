using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using VerificationBot.Attributes;
using Qmmands;

namespace VerificationBot.Modules
{
    [Name("Meta")]
    [Description("Commands that supply information about the bot")]
    [RequireCustomPermissions(Permission.None)]
    public class MetaModule : DiscordModuleBase<VCommandContext>
    {
        [Command("help", "commands")]
        [Description("Lists commands for a given module")]
        public async Task HelpAsync(string moduleName = null)
        {
            moduleName = moduleName?.ToLower() ?? "";
            IReadOnlyList<Module> modules = Context.Bot.Commands.GetAllModules();

            LocalEmbedBuilder embed = new();

            Module module;
            if ((module = modules.FirstOrDefault(m => m.Name.ToLower() == moduleName)) == default)
            {
                embed.Title = $"Please supply a module name from the list below";
                foreach (Module m in modules)
                {
                    embed.AddField(m.Name, m.Description ?? "No description defined");
                }
            }
            else
            {
                embed.Title = $"Commands for module '{module.Name}'";
                foreach (Command c in module.Commands)
                {
                    StringBuilder title = new(c.Name);

                    foreach (Parameter p in c.Parameters)
                    {
                        title.Append(p.IsOptional ? " [" : " <");
                        
                        foreach (char ch in p.Name)
                        {
                            title.Append(char.IsUpper(ch) ? (" " + char.ToLower(ch)) : ch);
                        }

                        title.Append(p.IsOptional ? "]" : ">");
                    }

                    StringBuilder value = new();
                    if (c.Aliases.Count > 1)
                    {
                        value.AppendLine("Aliases: " + string.Join(", ", c.Aliases.Where(alias => alias != c.Name)));
                    }

                    value.Append(c.Description ?? "No description defined");

                    embed.AddField(title.ToString(), value.ToString());
                }
            }

            await Response(embed);
        }

        [Command("uptime")]
        [Description("Responds with the current bot uptime")]
        public async Task PrintUptimeAsync()
        {
            await Response((DateTime.Now - Process.GetCurrentProcess().StartTime).ToString("d'd 'hh'h 'mm'm 'ss's'"));
        }

        [Command("ping")]
        [Description("Responds pong")]
        public async Task PingAsync()
            => await Response("pong");

        [Command("reacttest")]
        public async Task ReactTestAsync()
        {
            Type t = typeof(DiscordClientBase);
            var f = t.GetField(nameof(DiscordClientBase.ReactionAdded), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            Delegate d = f.GetValue(Context.Bot) as Delegate;
            Delegate[] subscribers = d?.GetInvocationList();
            if (subscribers == null || subscribers.Length == 0)
            {
                await Response("No subscribers");
                return;
            }

            await Response(string.Join(", ", subscribers.Select(s => s.Target.GetType().Name + "." + s.Method.Name)));
        }
    }
}
