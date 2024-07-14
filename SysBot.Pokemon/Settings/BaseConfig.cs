using System.ComponentModel;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Console agnostic settings
    /// </summary>
    public abstract class BaseConfig
    {
        protected const string FeatureToggle = nameof(FeatureToggle);
        protected const string Operation = nameof(Operation);
        private const string Debug = nameof(Debug);

        [Category(FeatureToggle), Description("啟用後，機器人會在不處理任何內容時偶爾按下 B 按鈕（以避免休眠）。")]
        public bool AntiIdle { get; set; }

        [Category(FeatureToggle), Description("啟用文字日誌。重新啟動以套用變更。")]
        public bool LoggingEnabled { get; set; } = true;

        [Category(FeatureToggle), Description("要保留的舊文字日誌檔案的最大數量。將其設為 <= 0 以停用日誌清理。重新啟動以套用變更。")]
        public int MaxArchiveFiles { get; set; } = 14;

        [Category(Debug), Description("程式啟動時跳過創建機器人；有助於測試整合。")]
        public bool SkipConsoleBotCreation { get; set; }
    }
}