using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon
{

    public class RotatingRaidSettingsSV : IBotStateSettings
    {
        private const string Hosting = nameof(Hosting);
        private const string Counts = nameof(Counts);
        private const string FeatureToggle = nameof(FeatureToggle);
        
        public override string ToString() => "輪換 Raid 設定（僅限 Sc/Vi）";
        [DisplayName("主動Raid列表")]

        [Category(Hosting), Description("您的主動Raid清單就在這裡。")]
        public List<RotatingRaidParameters> ActiveRaids { get; set; } = new();

        [DisplayName("Raid 設定")]
        public RotatingRaidSettingsCategory RaidSettings { get; set; } = new RotatingRaidSettingsCategory();

        [DisplayName("Discord 嵌入設定")]
        public RotatingRaidPresetFiltersCategory EmbedToggles { get; set; } = new RotatingRaidPresetFiltersCategory();

        [DisplayName("Raid大廳設置")]

        [Category(Hosting), Description("大廳選項"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public LobbyFiltersCategory LobbyOptions { get; set; } = new();

        [DisplayName("禁止攻略列表")]

        [Category(Hosting), Description("這裡的使用者 NID 是被禁止的入侵者。")]
        public RemoteControlAccessList RaiderBanList { get; set; } = new() { AllowIfEmpty = false };

        [DisplayName("隨機設定")]
        public MiscSettingsCategory MiscSettings { get; set; } = new MiscSettingsCategory();

        [Browsable(false)]
        public bool ScreenOff
        {
            get => MiscSettings.ScreenOff;
            set => MiscSettings.ScreenOff = value;
        }

        public class RotatingRaidParameters
        {
            public override string ToString() => $"{Title}";

            [DisplayName("啟用Raid？")]
            public bool ActiveInRotation { get; set; } = true;

            [DisplayName("種")]
            public Species Species { get; set; } = Species.None;

            [DisplayName("強制選擇物種?")]
            public bool ForceSpecificSpecies { get; set; } = false;

            [DisplayName("神奇寶貝表格編號")]
            public int SpeciesForm { get; set; } = 0;

            [DisplayName("是否閃光寶可夢?")]
            public bool IsShiny { get; set; } = true;

            [DisplayName("晶體類型")]
            public TeraCrystalType CrystalType { get; set; } = TeraCrystalType.Base;

            [DisplayName("進行 Raid 編碼嗎？")]
            public bool IsCoded { get; set; } = true;

            [DisplayName("種子")]
            public string Seed { get; set; } = "0";

            [DisplayName("星數")]
            public int DifficultyLevel { get; set; } = 0;

            [DisplayName("遊戲進度")]
            [TypeConverter(typeof(EnumConverter))]
            public GameProgressEnum StoryProgress { get; set; } = GameProgressEnum.Unlocked6Stars;

            [DisplayName("Raid Battler（決戰格式）")]
            public string[] PartyPK { get; set; } = [];

            [DisplayName("動作機器人應該使用")]
            public Action1Type Action1 { get; set; } = Action1Type.GoAllOut;

            [DisplayName("動作延遲（以秒為單位）")]
            public int Action1Delay { get; set; } = 5;

            [DisplayName("組 ID（僅限活動突襲）")]
            public int GroupID { get; set; } = 0;

            [DisplayName("嵌入標題")]
            public string Title { get; set; } = string.Empty;

            [Browsable(false)]
            public bool AddedByRACommand { get; set; } = false;

            [Browsable(false)]
            public bool SpriteAlternateArt { get; set; } = false; // Not enough alt art to even turn on

            [Browsable(false)]
            public string[] Description { get; set; } = [];

            [Browsable(false)]
            public bool RaidUpNext { get; set; } = false;

            [Browsable(false)]
            public string RequestCommand { get; set; } = string.Empty;

            [Browsable(false)]
            public ulong RequestedByUserID { get; set; }

            [Browsable(false)]
            [System.Text.Json.Serialization.JsonIgnore]
            public SocketUser? User { get; set; }

            [Browsable(false)]
            [System.Text.Json.Serialization.JsonIgnore]
            public List<SocketUser> MentionedUsers { get; set; } = [];
        }

        [Category(Hosting), TypeConverter(typeof(CategoryConverter<RotatingRaidSettingsCategory>))]
        public class RotatingRaidSettingsCategory
        {
            private bool _randomRotation = false;
            private bool _mysteryRaids = false;

            public override string ToString() => "Raid 設定";

            [DisplayName("從檔案生成主動 Raids？")]
            [Category(Hosting), Description("啟用後，機器人將嘗試從機器人啟動時的\"raidsv.txt\"檔案自動產生您的攻擊。")]
            public bool GenerateRaidsFromFile { get; set; } = true;

            [DisplayName("退出時將活動 Raids 儲存到文件嗎？")]
            [Category(Hosting), Description("啟用後，機器人將在機器人停止時將您目前的 ActiveRaids 清單儲存到\"savedSeeds.txt\" 檔案中。")]
            public bool SaveSeedsToFile { get; set; } = true;

            [DisplayName("停止前主機的總攻擊次數")]
            [Category(Hosting), Description("輸入機器人自動停止之前要託管的攻擊總數。預設值為 0 以忽略此設定。")]
            public int TotalRaidsToHost { get; set; } = 0;

            [DisplayName("以隨機順序輪換 Raid 列表？"), Category(Hosting), Description("啟用後，機器人將隨機選擇要運行的 Raid，同時保持請求的優先順序。")]
            public bool RandomRotation
            {
                get => _randomRotation;
                set
                {
                    _randomRotation = value;
                    if (value)
                        _mysteryRaids = false;
                }
            }

            [DisplayName("開啟神秘Raid？"), Category(Hosting), Description("當為 true 時，機器人將隨機添加閃亮種子到隊列中。只會運行用戶請求和神秘攻擊。")]
            public bool MysteryRaids
            {
                get => _mysteryRaids;
                set
                {
                    _mysteryRaids = value;
                    if (value)
                        _randomRotation = false;
                }
            }

            [DisplayName("神秘Raid設置")]
            [Category("神秘Raid"), Description("神秘Raid特有的設置")]
            public MysteryRaidsSettings MysteryRaidsSettings { get; set; } = new MysteryRaidsSettings();

            [DisplayName("禁用用戶 Raid 請求？")]
            [Category(Hosting), Description("如果為 true，機器人將不允許用戶要求的攻擊，並通知他們此設定已開啟。")]
            public bool DisableRequests { get; set; } = false;

            [DisplayName("允許私人用戶 Raid 請求嗎？")]
            [Category(Hosting), Description("如果為真，機器人將允許私人攻擊。")]
            public bool PrivateRaidsEnabled { get; set; } = true;

            [DisplayName("限制用戶請求")]
            [Category(Hosting), Description("限制使用者可以發出的請求數量。  設定為 0 以停用。\n命令: $lr <number>")]
            public int LimitRequests { get; set; } = 0;

            [DisplayName("限制請求時間")]
            [Category(Hosting), Description("定義達到 LimitRequests 數量後使用者必須等待請求的時間（以分鐘為單位）。  設定為 0 以停用。\n命令: $lrt <number in minutes>")]
            public int LimitRequestsTime { get; set; } = 0;

            [DisplayName("限制請求用戶錯誤訊息")]
            [Category(Hosting), Description("當用戶達到其請求限制時顯示的自定義訊息。")]
            public string LimitRequestMsg { get; set; } = "如果您想繞過此限制，請[描述如何獲取角色]。";

            [DisplayName("可以繞過限制請求的用戶/角色")]
            [Category(Hosting), Description("具有名稱的用戶和角色ID字典，可以繞過請求限制。\n命令: $alb @角色 或 $alb @用戶")]
            public Dictionary<ulong, string> BypassLimitRequests { get; set; } = new Dictionary<ulong, string>();

            [DisplayName("在大世界中防止戰鬥？")]
            [Category(FeatureToggle), Description("防止攻擊。當為true時，下一次種子注入時會禁用大世界生成的寶可夢。當為false時，下一次種子注入時會啟用大世界生成的寶可夢。")]
            public bool DisableOverworldSpawns { get; set; } = true;

            [DisplayName("在X秒內開始突襲")]
            [Category(Hosting), Description("開始突襲前等待的最小秒數。")]
            public int TimeToWait { get; set; } = 90;

            [DisplayName("保持當天種子？")]
            [Category(Hosting), Description("啟用時，機器人將當天的種子注入到明天的種子。")]
            public bool KeepDaySeed { get; set; } = true;

            [DisplayName("防止日期變更？")]
            [Category(FeatureToggle), Description("啟用時，機器人將時間回退5小時以防止日期變更。確保在啟動機器人時，Switch時間在凌晨12:01到下午7:00之間。")]
            public bool EnableTimeRollBack { get; set; } = true;
        }

        public class MoveTypeEmojiInfo
        {
            [Description("招式類型。")]
            public MoveType MoveType { get; set; }

            [Description("此招式類型的Discord表情字符串。")]
            public string EmojiCode { get; set; }

            public MoveTypeEmojiInfo() { }

            public MoveTypeEmojiInfo(MoveType moveType)
            {
                MoveType = moveType;
            }
            public override string ToString()
            {
                if (string.IsNullOrEmpty(EmojiCode))
                    return MoveType.ToString();

                return $"{EmojiCode}";
            }
        }

        [TypeConverter(typeof(ExpandableObjectConverter))]
        public class EmojiInfo
        {
            [Description("表情的完整字符串。")]
            [DisplayName("表情代碼")]
            public string EmojiString { get; set; } = string.Empty;

            public override string ToString()
            {
                return string.IsNullOrEmpty(EmojiString) ? "未設置" : EmojiString;
            }
        }

        [Category(Hosting), TypeConverter(typeof(CategoryConverter<RotatingRaidPresetFiltersCategory>))]
        public class RotatingRaidPresetFiltersCategory
        {
            public override string ToString() => "嵌入切換";

            [Category(Hosting), Description("在貿易嵌入中將顯示招式類型圖標（僅限Discord）。要求用戶將表情上傳到其服務器。")]
            [DisplayName("使用招式類型表情？")]
            public bool MoveTypeEmojis { get; set; } = true;

            [Category(Hosting), Description("招式類型的自定義表情信息。")]
            [DisplayName("自定義招式類型表情")]
            public List<MoveTypeEmojiInfo> CustomTypeEmojis { get; set; } =
            [
            new(MoveType.Bug),
            new(MoveType.Fire),
            new(MoveType.Flying),
            new(MoveType.Ground),
            new(MoveType.Water),
            new(MoveType.Grass),
            new(MoveType.Ice),
            new(MoveType.Rock),
            new(MoveType.Ghost),
            new(MoveType.Steel),
            new(MoveType.Fighting),
            new(MoveType.Electric),
            new(MoveType.Dragon),
            new(MoveType.Psychic),
            new(MoveType.Dark),
            new(MoveType.Normal),
            new(MoveType.Poison),
            new(MoveType.Fairy),
            ];

            [Category(Hosting), Description("男性性別表情的完整字符串。留空表示不使用。")]
            [DisplayName("男性表情代碼")]
            public EmojiInfo MaleEmoji { get; set; } = new EmojiInfo();

            [Category(Hosting), Description("女性性別表情的完整字符串。留空表示不使用。")]
            [DisplayName("女性表情代碼")]
            public EmojiInfo FemaleEmoji { get; set; } = new EmojiInfo();

            [Category(Hosting), Description("每次發布突襲時，突襲嵌入描述將顯示在嵌入的頂部。")]
            [DisplayName("突襲嵌入描述")]
            public string[] RaidEmbedDescription { get; set; } = Array.Empty<string>();

            [Category(FeatureToggle), Description("選擇在嵌入的作者區域中使用的太晶圖標集。Icon1是自定義圖標，Icon2不是。")]
            [DisplayName("太晶圖標選擇")]
            public TeraIconType SelectedTeraIconType { get; set; } = TeraIconType.Icon1;

            [Category(Hosting), Description("如果為true，機器人將在嵌入中顯示招式。")]
            [DisplayName("在嵌入中包含招式/額外招式？")]
            public bool IncludeMoves { get; set; } = true;

            [Category(Hosting), Description("如果為true，嵌入將顯示當前種子。")]
            [DisplayName("在嵌入中包含當前種子？")]
            public bool IncludeSeed { get; set; } = true;

            [Category(FeatureToggle), Description("啟用時，嵌入將倒數“TimeToWait”中的秒數，直到突襲開始。")]
            [DisplayName("在嵌入中包含倒數計時器？")]
            public bool IncludeCountdown { get; set; } = true;

            [Category(Hosting), Description("如果為true，機器人將在嵌入中顯示類型優勢提示。")]
            [DisplayName("在嵌入中包含類型優勢提示？")]
            public bool IncludeTypeAdvantage { get; set; } = true;

            [Category(Hosting), Description("如果為true，機器人將在嵌入中顯示特殊獎勵。")]
            [DisplayName("在嵌入中包含獎勵？")]
            public bool IncludeRewards { get; set; } = true;

            [Category(Hosting), Description("選擇在嵌入中顯示哪些獎勵。")]
            [DisplayName("顯示的獎勵")]
            public List<string> RewardsToShow { get; set; } = new List<string>
            {
                "Rare Candy",
                "Ability Capsule",
                "Bottle Cap",
                "Ability Patch",
                "Exp. Candy L",
                "Exp. Candy XL",
                "Sweet Herba Mystica",
                "Salty Herba Mystica",
                "Sour Herba Mystica",
                "Bitter Herba Mystica",
                "Spicy Herba Mystica",
                "Pokeball",
                "Shards",
                "Nugget",
                "Tiny Mushroom",
                "Big Mushroom",
                "Pearl",
                "Big Pearl",
                "Stardust",
                "Star Piece",
                "Gold Bottle Cap",
                "PP Up"
            };

            [Category(Hosting), Description("發布請求的突襲嵌入的時間（以秒為單位）。")]
            [DisplayName("發布用戶請求嵌入於...")]
            public int RequestEmbedTime { get; set; } = 30;

            [Category(FeatureToggle), Description("啟用時，機器人將嘗試為突襲嵌入截取屏幕截圖。如果您經常遇到\"大小/參數\"崩潰，請嘗試將此設置為false。")]
            [DisplayName("使用截圖？")]
            public bool TakeScreenshot { get; set; } = true;

            [Category(Hosting), Description("在突襲中捕獲截圖的延遲時間（以毫秒為單位）。\n0 捕獲近距離的突襲精靈。\n3500 只捕獲玩家。\n10000 捕獲玩家和突襲精靈。")]
            [DisplayName("截圖時間（非Gif圖片）")]
            public ScreenshotTimingOptions ScreenshotTiming { get; set; } = ScreenshotTimingOptions._3500;

            [Category(FeatureToggle), Description("啟用時，機器人將拍攝突襲內發生情況的動畫圖像（gif），而不是標準的靜態圖像。")]
            [DisplayName("使用Gif截圖？")]
            public bool AnimatedScreenshot { get; set; } = true;

            [Category(FeatureToggle), Description("捕獲嵌入的幀數。20-30是一個不錯的數字。")]
            [DisplayName("捕獲幀數（僅限Gif）")]
            public int Frames { get; set; } = 30;

            [Category(FeatureToggle), Description("GIF的質量。質量越高，文件大小越大。")]
            [DisplayName("GIF質量")]
            public GifQuality GifQuality { get; set; } = GifQuality.Default;

            [Category(FeatureToggle), Description("啟用時，機器人將在Discord嵌入中隱藏突襲代碼。")]
            public bool HideRaidCode { get; set; } = false;
        }

        [Category("神秘突襲"), TypeConverter(typeof(ExpandableObjectConverter))]
        public class MysteryRaidsSettings
        {
            [TypeConverter(typeof(ExpandableObjectConverter))]
            [DisplayName("3星進度設置")]
            public Unlocked3StarSettings Unlocked3StarSettings { get; set; } = new Unlocked3StarSettings();

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [DisplayName("4星進度設置")]
            public Unlocked4StarSettings Unlocked4StarSettings { get; set; } = new Unlocked4StarSettings();

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [DisplayName("5星進度設置")]
            public Unlocked5StarSettings Unlocked5StarSettings { get; set; } = new Unlocked5StarSettings();

            [TypeConverter(typeof(ExpandableObjectConverter))]
            [DisplayName("6星進度設置")]
            public Unlocked6StarSettings Unlocked6StarSettings { get; set; } = new Unlocked6StarSettings();

            public override string ToString() => "神秘突襲設置";
        }

        public class Unlocked3StarSettings
        {
            [DisplayName("啟用3星進度神秘突襲？")]
            public bool Enabled { get; set; } = true;

            [Category("難度等級"), Description("允許1星突襲在3星解鎖突襲中。")]
            public bool Allow1StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許2星突襲在3星解鎖突襲中。")]
            public bool Allow2StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許3星突襲在3星解鎖突襲中。")]
            public bool Allow3StarRaids { get; set; } = true;

            public override string ToString() => "3星突襲設置";
        }

        public class Unlocked4StarSettings
        {
            [DisplayName("啟用4星進度神秘突襲？")]
            public bool Enabled { get; set; } = true;

            [Category("難度等級"), Description("允許1星突襲在4星解鎖突襲中。")]
            public bool Allow1StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許2星突襲在4星解鎖突襲中。")]
            public bool Allow2StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許3星突襲在4星解鎖突襲中。")]
            public bool Allow3StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許4星突襲在4星解鎖突襲中。")]
            public bool Allow4StarRaids { get; set; } = true;

            public override string ToString() => "4星突襲設置";
        }

        [Category("神秘突襲"), TypeConverter(typeof(ExpandableObjectConverter))]
        public class Unlocked5StarSettings
        {
            [DisplayName("啟用5星進度神秘突襲？")]
            public bool Enabled { get; set; } = true;

            [Category("難度等級"), Description("允許3星突襲在5星解鎖突襲中。")]
            public bool Allow3StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許4星突襲在5星解鎖突襲中。")]
            public bool Allow4StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許5星突襲在5星解鎖突襲中。")]
            public bool Allow5StarRaids { get; set; } = true;

            public override string ToString() => "5星突襲設置";
        }

        [Category("神秘突襲"), TypeConverter(typeof(ExpandableObjectConverter))]
        public class Unlocked6StarSettings
        {
            [DisplayName("啟用6星進度神秘突襲？")]
            public bool Enabled { get; set; } = true;

            [Category("難度等級"), Description("允許3星突襲在6星解鎖突襲中。")]
            public bool Allow3StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許4星突襲在6星解鎖突襲中。")]
            public bool Allow4StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許5星突襲在6星解鎖突襲中。")]
            public bool Allow5StarRaids { get; set; } = true;

            [Category("難度等級"), Description("允許6星突襲在6星解鎖突襲中。")]
            public bool Allow6StarRaids { get; set; } = true;

            public override string ToString() => "6星突襲設置";
        }

        [Category("大廳篩選"), TypeConverter(typeof(CategoryConverter<LobbyFiltersCategory>))]
        public class LobbyFiltersCategory
        {
            public override string ToString() => "大廳篩選";

            [Category("大廳篩選"), Description("OpenLobby - 在多少個空大廳後開啟大廳\nSkipRaid - 在多少次失敗/空大廳後跳過\nContinue - 繼續主持突襲")]
            [DisplayName("大廳方法")]
            public LobbyMethodOptions LobbyMethod { get; set; } = LobbyMethodOptions.SkipRaid;

            [Category("大廳篩選"), Description("在機器人主持一個未編碼的突襲之前，每個參數的空突襲限制。默認值為3個突襲。")]
            [DisplayName("空突襲限制")]
            public int EmptyRaidLimit { get; set; } = 3;

            [Category("大廳篩選"), Description("在機器人跳過到下一個突襲之前，每個參數的空/失敗突襲限制。默認值為3個突襲。")]
            [DisplayName("跳過突襲限制")]
            public int SkipRaidLimit { get; set; } = 3;

            [Category("功能切換"), Description("設置您希望機器人執行的動作。'AFK' 將使機器人進入空閒狀態，而 'MashA' 每3.5秒按下A鍵。")]
            [DisplayName("A鍵動作")]
            public RaidAction Action { get; set; } = RaidAction.MashA;

            [Category("功能切換"), Description("延遲 'MashA' 操作的時間，單位為秒。[默認值為3.5]")]
            [DisplayName("A鍵延遲（秒）")]
            public double MashADelay { get; set; } = 3.5;

            [Category("功能切換"), Description("在突襲大廳解散後，決定不捕捉突襲寶可夢之前的額外等待時間（毫秒）。")]
            [DisplayName("額外解散大廳時間")]
            public int ExtraTimeLobbyDisband { get; set; } = 0;

            [Category("功能切換"), Description("在更換隊伍前的額外等待時間（毫秒）。")]
            [DisplayName("準備突襲隊伍的額外時間")]
            public int ExtraTimePartyPK { get; set; } = 0;
        }

        [Category(Hosting), TypeConverter(typeof(CategoryConverter<MiscSettingsCategory>))]
        public class MiscSettingsCategory
        {
            public override string ToString() => "其他设置";

            [Category(FeatureToggle), Description("在日期/时间设置中设置您的Switch日期/时间格式。如果日期发生变化，日期将自动回退1天。")]
            public DTFormat DateTimeFormat { get; set; } = DTFormat.MMDDYY;

            [Category(Hosting), Description("启用时，机器人将使用超调方法进行日转校正，否则将使用DDOWN点击。")]
            public bool UseOvershoot { get; set; } = false;

            [Category(Hosting), Description("在日转校正期间，按DDOWN键的次数。[默认: 39次点击]")]
            public int DDOWNClicks { get; set; } = 39;

            [Category(Hosting), Description("在日转校正期间访问日期/时间设置的下滚持续时间（以毫秒为单位）。您希望它超调日期/时间设置1次，因为在向下滚动后会点击DUP。[默认: 930毫秒]")]
            public int HoldTimeForRollover { get; set; } = 900;

            [Category(Hosting), Description("启用时，在HOME画面关闭游戏时启动机器人。机器人将仅运行日转校正例程，以便您可以尝试配置准确的时机。")]
            public bool ConfigureRolloverCorrection { get; set; } = false;

            [Category(FeatureToggle), Description("启用时，正常机器人循环操作期间将关闭屏幕以节省电源。")]
            public bool ScreenOff { get; set; }

            private int _completedRaids;

            [Category(Counts), Description("已开始的团战")]
            public int CompletedRaids
            {
                get => _completedRaids;
                set => _completedRaids = value;
            }

            [Category(Counts), Description("启用时，在请求状态检查时将发出计数。")]
            public bool EmitCountsOnStatusCheck { get; set; }

            public int AddCompletedRaids() => Interlocked.Increment(ref _completedRaids);

            public IEnumerable<string> GetNonZeroCounts()
            {
                if (!EmitCountsOnStatusCheck)
                    yield break;
                if (CompletedRaids != 0)
                    yield return $"已开始的突袭： {CompletedRaids}";
            }
        }

        public class CategoryConverter<T> : TypeConverter
        {
            public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

            public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value, Attribute[]? attributes) => TypeDescriptor.GetProperties(typeof(T));

            public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType != typeof(string) && base.CanConvertTo(context, destinationType);
        }
    }
}