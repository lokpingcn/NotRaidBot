using System.ComponentModel;
using static SysBot.Pokemon.RotatingRaidSettingsSV;

namespace SysBot.Pokemon
{
    public class DiscordSettings
    {
        private const string Startup = nameof(Startup);
        private const string Operation = nameof(Operation);
        private const string Channels = nameof(Channels);
        private const string Roles = nameof(Roles);
        private const string Users = nameof(Users);

        public override string ToString() => "Discord 整合設定";

        // Startup

        [Category(Startup), Description("機器人登入令牌。")]
        public string Token { get; set; } = string.Empty;

        [Category(Startup), Description("機器人命令前綴。")]
        public string CommandPrefix { get; set; } = "%";

        [Category(Startup), Description("切換以非同步或同步處理命令。")]
        public bool AsyncCommands { get; set; }

        [Category(Startup), Description("玩遊戲的自訂狀態。")]
        public string BotGameStatus { get; set; } = "主持 S/V Raids";

        [Category(Operation), Description("當用戶向機器人打招呼時，機器人將回覆自訂訊息。使用字串格式在回覆中提及使用者。")]
        public string HelloResponse { get; set; } = "你好，{0}！  我在線！";

        // Whitelists
        [Category(Roles), Description("具有此角色的使用者可以進入Raid隊列。")]
        public RemoteControlAccessList RoleRaidRequest { get; set; } = new() { AllowIfEmpty = false };

        [Browsable(false)]
        [Category(Roles), Description("具有此角色的使用者可以遠端控制控制台（如果作為遠端控制機器人運行）。")]
        public RemoteControlAccessList RoleRemoteControl { get; set; } = new() { AllowIfEmpty = false };

        [Category(Roles), Description("具有此角色的使用者可以繞過命令限制。")]
        public RemoteControlAccessList RoleSudo { get; set; } = new() { AllowIfEmpty = false };

        // Operation
        [Category(Users), Description("具有這些使用者 ID 的使用者無法使用該機器人。")]
        public RemoteControlAccessList UserBlacklist { get; set; } = new();

        [Category(Channels), Description("具有這些 ID 的通道是機器人確認命令的唯一通道。")]
        public RemoteControlAccessList ChannelWhitelist { get; set; } = new();

        [Category(Users), Description("以逗號分隔的 Discord 使用者 ID，將對 Bot Hub 具有 sudo 存取權。")]
        public RemoteControlAccessList GlobalSudoList { get; set; } = new();

        [Category(Users), Description("停用此功能將刪除全域 sudo 支援。")]
        public bool AllowGlobalSudo { get; set; } = true;

        [Category(Channels), Description("將回顯日誌機器人資料的通道 ID。")]
        public RemoteControlAccessList LoggingChannels { get; set; } = new();

        [Category(Channels), Description("Raid 嵌入頻道。")]
        public RemoteControlAccessList EchoChannels { get; set; } = new();

        public AnnouncementSettingsCategory AnnouncementSettings { get; set; } = new();

        [Category(Operation), TypeConverter(typeof(CategoryConverter<AnnouncementSettingsCategory>))]
        public class AnnouncementSettingsCategory
        {
            public override string ToString() => "公告設定";

            [Category("嵌入設置"), Description("公告的縮圖選項。")]
            public ThumbnailOption AnnouncementThumbnailOption { get; set; } = ThumbnailOption.Gengar;

            [Category("嵌入設置"), Description("公告的自訂縮圖 URL。")]
            public string CustomAnnouncementThumbnailUrl { get; set; } = string.Empty;

            public EmbedColorOption AnnouncementEmbedColor { get; set; } = EmbedColorOption.Blue;

            [Category("嵌入設置"), Description("啟用公告的隨機縮圖選擇。")]
            public bool RandomAnnouncementThumbnail { get; set; } = false;

            [Category("嵌入設置"), Description("啟用公告的隨機顏色選擇。")]
            public bool RandomAnnouncementColor { get; set; } = false;
        }
    }
}