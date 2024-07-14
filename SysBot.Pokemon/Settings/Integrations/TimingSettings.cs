using System.ComponentModel;
using static SysBot.Pokemon.RotatingRaidSettingsSV;

namespace SysBot.Pokemon
{
    public class TimingSettings
    {
        private const string OpenGame = nameof(OpenGame);
        private const string CloseGame = nameof(CloseGame);
        private const string RestartGame = nameof(RestartGame);
        private const string Raid = nameof(Raid);
        private const string Misc = nameof(Misc);

        public override string ToString() => "額外時間設定";

        [Category(OpenGame), Description("在標題畫面之後，等待以毫秒為單位的額外時間，以加載遊戲世界。")]
        public int ExtraTimeLoadOverworld { get; set; } = 3000;

        [Category(OpenGame), Description("注入種子和故事進度後，在點擊A之前等待的額外時間（毫秒）。")]
        public int ExtraTimeInjectSeed { get; set; } = 0;

        [Category(CloseGame), Description("按下HOME鍵最小化遊戲後，等待的額外時間（毫秒）。")]
        public int ExtraTimeReturnHome { get; set; }

        [Category(Misc), Description("等待寶可夢傳送門加載的額外時間（毫秒）。")]
        public int ExtraTimeLoadPortal { get; set; } = 1000;

        [Category(Misc), Description("點擊+連接到Y-Comm（SWSH）或按L連接線上（SV）後等待的額外時間（毫秒）。")]
        public int ExtraTimeConnectOnline { get; set; }

        [Category(Misc), Description("連線丟失後嘗試重新連接套接字連接的次數。將其設置為-1以無限次嘗試。")]
        public int ReconnectAttempts { get; set; } = 30;

        [Category(Misc), Description("嘗試重新連接之間等待的額外時間（毫秒）。基礎時間為30秒。")]
        public int ExtraReconnectDelay { get; set; }

        [Category(Misc), Description("在導航Switch選單或輸入連結代碼時，每次按鍵後的等待時間。")]
        public int KeypressTime { get; set; } = 200;

        [Category(RestartGame), Description("與重新啟動遊戲相關的設定。")]
        public RestartGameSettingsCategory RestartGameSettings { get; set; } = new();

        [Category(RestartGame), TypeConverter(typeof(CategoryConverter<RestartGameSettingsCategory>))]
        public class RestartGameSettingsCategory
        {
            public override string ToString() => "重新啟動遊戲設定";

            [Category(OpenGame), Description("啟用此功能以拒絕接收系統更新。")]
            public bool AvoidSystemUpdate { get; set; } = false;

            [Category(OpenGame), Description("啟用此功能以在\"檢查遊戲是否可以遊玩\"彈出窗口時添加延遲。")]
            public bool CheckGameDelay { get; set; } = false;

            [Category(OpenGame), Description("等待\"檢查遊戲是否可以遊玩\"彈出窗口的額外時間（毫秒）。")]
            public int ExtraTimeCheckGame { get; set; } = 200;

            [Category(OpenGame), Description("只有當您的系統上有DLC並且無法使用時，才啟用此功能。")]
            public bool CheckForDLC { get; set; } = false;

            [Category(OpenGame), Description("檢查DLC是否可用的額外等待時間（毫秒）。")]
            public int ExtraTimeCheckDLC { get; set; } = 0;

            [Category(OpenGame), Description("在標題畫面點擊A之前等待的額外時間（毫秒）。")]
            public int ExtraTimeLoadGame { get; set; } = 5000;

            [Category(CloseGame), Description("點擊關閉遊戲後等待的額外時間（毫秒）。")]
            public int ExtraTimeCloseGame { get; set; } = 0;

            [Category(RestartGame), Description("與重新啟動遊戲相關的設定。")]
            public ProfileSelectSettingsCategory ProfileSelectSettings { get; set; } = new();
        }

        [Category(RestartGame), TypeConverter(typeof(CategoryConverter<ProfileSelectSettingsCategory>))]
        public class ProfileSelectSettingsCategory
        {
            public override string ToString() => "個人資料選擇設定";

            [Category(OpenGame), Description("如果在開始遊戲時需要選擇個人資料，請啟用此功能。")]
            public bool ProfileSelectionRequired { get; set; } = true;

            [Category(OpenGame), Description("在開始遊戲時等待個人資料加載的額外時間（毫秒）。")]
            public int ExtraTimeLoadProfiles { get; set; } = 0;
            public int ExtraTimeLoadProfile { get; set; } = 0;
        }
    }
}