using System.ComponentModel;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon
{
    public sealed class PokeRaidHubConfig : BaseConfig
    {
        private const string BotRaid = nameof(BotRaid);
        private const string Integration = nameof(Integration);

        [Category(Operation), Description("为较慢的切换添加额外的时间")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public TimingSettings Timings { get; set; } = new();

        [Category(BotRaid), Description("程序正在运行的 Discord 机器人的名称。这将为窗口添加标题以便于识别。需要重新启动程序。")]
        [DisplayName("这个机器人的名字是...")]
        public string BotName { get; set; } = string.Empty;

        [Browsable(false)]
        [Category(Integration), Description("用户主题选项选择。")]
        public string ThemeOption { get; set; } = string.Empty;

        [Category(BotRaid)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public RotatingRaidSettingsSV RotatingRaidSV { get; set; } = new();

        // Integration

        [Category(Integration)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public DiscordSettings Discord { get; set; } = new();
    }
}