using Discord;
using Discord.Commands;
using SysBot.Pokemon.SV.BotRaid.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    // src: https://github.com/foxbot/patek/blob/master/src/Patek/Modules/InfoModule.cs
    // ISC License (ISC)
    // Copyright 2017, Christopher F. <foxbot@protonmail.com>
    public class InfoModule : ModuleBase<SocketCommandContext>
    {
        private const string detail = "我是一个由 PKHeX.Core 和其他开源软件提供支持的开源 RaidBot。";
        public const string version = NotRaidBot.Version;
        private const string support = NotRaidBot.Repo;
        private const ulong DisallowedUserId = 195756980873199618;

        [Command("info")]
        [Alias("about", "whoami", "owner")]
        public async Task InfoAsync()
        {
            if (Context.User.Id == DisallowedUserId)
            {
                await ReplyAsync("我们不会让可疑的人使用这个命令。").ConfigureAwait(false);
                return;
            }
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            var programIconUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/icon4.png";
            var builder = new EmbedBuilder
            {
                Color = new Color(114, 137, 218),
                Description = detail,
                ImageUrl = programIconUrl
            };

            builder.AddField("# __Bot 信息__",
                $"- **版本**: {version}\n" +
                $"- {Format.Bold("所有者")}: {app.Owner} ({app.Owner.Id})\n" +
                $"- {Format.Bold("运行时间")}: {GetUptime()}\n" +
                $"- {Format.Bold("内核版本")}: {GetVersionInfo("PKHeX.Core")}\n" +
                $"- {Format.Bold("自动合法性版本")}: {GetVersionInfo("PKHeX.Core.AutoMod")}\n"
                );

            builder.AddField("频道數據",
                $"- {Format.Bold("行會")}: {Context.Client.Guilds.Count}\n" +
                $"- {Format.Bold("頻道")}: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
                $"- {Format.Bold("用户")}: {Context.Client.Guilds.Sum(g => g.MemberCount)}\n"
                );

            await ReplyAsync("这里是关于我的信息", embed: builder.Build()).ConfigureAwait(false);
        }

        private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");

        private static string GetVersionInfo(string assemblyName)
        {
            const string _default = "未知";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = assemblies.FirstOrDefault(x => x.GetName().Name == assemblyName);
            if (assembly is null)
                return _default;

            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute is null)
                return _default;

            var info = attribute.InformationalVersion;
            var split = info.Split('+');
            if (split.Length >= 2)
            {
                var versionParts = split[0].Split('.');
                if (versionParts.Length == 3)
                {
                    var major = versionParts[0].PadLeft(2, '0');
                    var minor = versionParts[1].PadLeft(2, '0');
                    var patch = versionParts[2].PadLeft(2, '0');
                    return $"{major}.{minor}.{patch}";
                }
            }
            return _default;
        }
    }
}