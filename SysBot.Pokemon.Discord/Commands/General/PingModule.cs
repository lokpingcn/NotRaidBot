using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class PingModule : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Summary("使機器人做出回應，表明它正在運行。")]
        public async Task PingAsync()
        {
            await ReplyAsync("Pong!").ConfigureAwait(false);
        }

        [Command("speak")]
        [Alias("talk", "say")]
        [Summary("告訴機器人當人們在島上時說話。")]
        [RequireSudo]
        public async Task SpeakAsync([Remainder] string request)
        {
            await ReplyAsync(request).ConfigureAwait(false);
        }
    }
}