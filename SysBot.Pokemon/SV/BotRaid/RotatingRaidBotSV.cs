using Discord;
using Newtonsoft.Json;
using PKHeX.Core;
using RaidCrawler.Core.Structures;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using AnimatedGif;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.RotatingRaidSettingsSV;
using static SysBot.Pokemon.SV.BotRaid.Blocks;

namespace SysBot.Pokemon.SV.BotRaid
{
    public class RotatingRaidBotSV : PokeRoutineExecutor9SV
    {
        private readonly PokeRaidHub<PK9> Hub;
        private readonly RotatingRaidSettingsSV Settings;
        private RemoteControlAccessList RaiderBanList => Settings.RaiderBanList;
        public static Dictionary<string, List<(int GroupID, int Index, string DenIdentifier)>> SpeciesToGroupIDMap = [];


        public RotatingRaidBotSV(PokeBotState cfg, PokeRaidHub<PK9> hub) : base(cfg)
        {
            Hub = hub;
            Settings = hub.Config.RotatingRaidSV;
        }

        public class PlayerInfo
        {
            public string OT { get; set; }
            public int RaidCount { get; set; }
        }

        private int LobbyError;
        private int RaidCount;
        private int WinCount;
        private int LossCount;
        private int SeedIndexToReplace = -1;
        public static GameProgress GameProgress;
        public static bool? currentSpawnsEnabled;
        public int StoryProgress;
        private int EventProgress;
        private int EmptyRaid = 0;
        private int LostRaid = 0;
        private readonly int FieldID = 0;
        private bool firstRun = true;
        public static int RotationCount { get; set; }
        private ulong TodaySeed;
        private ulong OverworldOffset;
        private ulong ConnectedOffset;
        private ulong RaidBlockPointerP;
        private ulong RaidBlockPointerK;
        private ulong RaidBlockPointerB;
        private readonly ulong[] TeraNIDOffsets = new ulong[3];
        private string TeraRaidCode { get; set; } = string.Empty;
        private string BaseDescription = string.Empty;
        private readonly Dictionary<ulong, int> RaidTracker = [];
        private SAV9SV HostSAV = new();
        private static readonly DateTime StartTime = DateTime.Now;
        public static RaidContainer? container;
        public static bool IsKitakami = false;
        public static bool IsBlueberry = false;
        private static DateTime TimeForRollBackCheck = DateTime.Now;
        private string denHexSeed;
        private int seedMismatchCount = 0;
        private readonly bool indicesInitialized = false;
        private static readonly int KitakamiDensCount = 0;
        private static readonly int BlueberryDensCount = 0;
        private readonly int InvalidDeliveryGroupCount = 0;
        private bool shouldRefreshMap = false;

        public override async Task MainLoop(CancellationToken token)
        {
            if (Settings.RaidSettings.GenerateRaidsFromFile)
            {
                GenerateSeedsFromFile();
                Log("完成。");
                Settings.RaidSettings.GenerateRaidsFromFile = false;
            }

            if (Settings.MiscSettings.ConfigureRolloverCorrection)
            {
                await RolloverCorrectionSV(token).ConfigureAwait(false);
                return;
            }

            if (Settings.ActiveRaids.Count < 1)
            {
                Log("ActiveRaids 不能為 0。請設置您主持的突襲的參數。");
                return;
            }

            if (Settings.RaidSettings.TimeToWait is < 0 or > 180)
            {
                Log("等待時間必須介於 0 和 180 秒之間。");
                return;
            }

            try
            {
                Log("正在識別主機主機的訓練師數據。");
                HostSAV = await IdentifyTrainer(token).ConfigureAwait(false);
                await InitializeHardware(Settings, token).ConfigureAwait(false);
                Log("開始主要的 RotatingRaidBot 迴圈。");
                await InnerLoop(token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }
            finally
            {
                SaveSeeds();
            }
            Log($"結束 {nameof(RotatingRaidBotSV)} 迴圈。");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task RebootReset(CancellationToken t)
        {
            await ReOpenGame(new PokeRaidHubConfig(), t).ConfigureAwait(false);
            await HardStop().ConfigureAwait(false);
            await Task.Delay(2_000, t).ConfigureAwait(false);
            if (!t.IsCancellationRequested)
            {
                Log("重新啟動內部迴圈。");
                await InnerLoop(t).ConfigureAwait(false);
            }
        }

        public override Task RefreshMap(CancellationToken t)
        {
            shouldRefreshMap = true;
            return Task.CompletedTask;
        }

        public class PlayerDataStorage
        {
            private readonly string filePath;

            public PlayerDataStorage(string baseDirectory)
            {
                var directoryPath = Path.Combine(baseDirectory, "raidfilessv");
                Directory.CreateDirectory(directoryPath);
                filePath = Path.Combine(directoryPath, "player_data.json");

                if (!File.Exists(filePath))
                    File.WriteAllText(filePath, "{}"); // Create a new JSON file if it does not exist.
            }

            public Dictionary<ulong, PlayerInfo> LoadPlayerData()
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<Dictionary<ulong, PlayerInfo>>(json) ?? [];
            }

            public void SavePlayerData(Dictionary<ulong, PlayerInfo> data)
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
        }

        private void GenerateSeedsFromFile()
        {
            var folder = "raidfilessv";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var prevrotationpath = "raidsv.txt";
            var rotationpath = "raidfilessv\\raidsv.txt";
            if (File.Exists(prevrotationpath))
                File.Move(prevrotationpath, rotationpath);
            if (!File.Exists(rotationpath))
            {
                File.WriteAllText(rotationpath, "000091EC-Kricketune-3-6,0000717F-Seviper-3-6");
                Log("正在創建預設的 raidsv.txt 檔案，因為檔案是空的所以跳過生成。\"");
                return;
            }

            if (!File.Exists(rotationpath))
                Log("raidsv.txt 不存在，跳過參數生成。");

            BaseDescription = string.Empty;
            var prevpath = "bodyparam.txt";
            var filepath = "raidfilessv\\bodyparam.txt";
            if (File.Exists(prevpath))
                File.Move(prevpath, filepath);
            if (File.Exists(filepath))
                BaseDescription = File.ReadAllText(filepath);

            var data = string.Empty;
            var prevpk = "pkparam.txt";
            var pkpath = "raidfilessv\\pkparam.txt";
            if (File.Exists(prevpk))
                File.Move(prevpk, pkpath);
            if (File.Exists(pkpath))
                data = File.ReadAllText(pkpath);

            DirectorySearch(rotationpath, data);
        }

        private void SaveSeeds()
        {
            // Exit the function if saving seeds to file is not enabled
            if (!Settings.RaidSettings.SaveSeedsToFile)
                return;

            // Filter out raids that don't need to be saved
            var raidsToSave = Settings.ActiveRaids.Where(raid => !raid.AddedByRACommand).ToList();

            // Exit the function if there are no raids to save
            if (!raidsToSave.Any())
                return;

            // Define directory and file paths
            var directoryPath = "raidfilessv";
            var fileName = "savedSeeds.txt";
            var savePath = Path.Combine(directoryPath, fileName);

            // Create directory if it doesn't exist
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Initialize StringBuilder to build the save string
            StringBuilder sb = new();

            // Loop through each raid to be saved
            foreach (var raid in raidsToSave)
            {
                // Increment the StoryProgressLevel by 1 before saving
                int storyProgressValue = (int)raid.StoryProgress;

                // Build the string to save, including the incremented StoryProgressLevel
                sb.Append($"{raid.Seed}-{raid.Species}-{raid.DifficultyLevel}-{storyProgressValue}");
            }

            // Remove the trailing comma at the end
            if (sb.Length > 0)
                sb.Length--;

            // Write the built string to the file
            File.WriteAllText(savePath, sb.ToString());
        }

        private void DirectorySearch(string sDir, string data)
        {
            // Clear the active raids before populating it
            Settings.ActiveRaids.Clear();

            // Read the entire content from the file into a string
            string contents = File.ReadAllText(sDir);

            // Split the string based on commas to get each raid entry
            string[] moninfo = contents.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            // Iterate over each raid entry
            for (int i = 0; i < moninfo.Length; i++)
            {
                // Split the entry based on dashes to get individual pieces of information
                var div = moninfo[i].Split(separatorArray, StringSplitOptions.RemoveEmptyEntries);

                // Check if the split result has exactly 4 parts
                if (div.Length != 4)
                {
                    Log($"處理條目錯誤：{moninfo[i]}。預期有4部分，但找到了{div.Length}部分。跳過此條目。");
                    continue; // Skip processing this entry and move to the next one
                }

                // Extracting seed, title, and difficulty level
                var monseed = div[0];
                var montitle = div[1];

                if (!int.TryParse(div[2], out int difficultyLevel))
                {
                    Log($"無法解析條目的難度等級：{moninfo[i]}");
                    continue;
                }

                // Extract and convert the StoryProgressLevel
                if (!int.TryParse(div[3], out int storyProgressLevelFromSeed))
                {
                    Log($"無法解析條目的故事進度等級：{moninfo[i]}");
                    continue;
                }

                int convertedStoryProgressLevel = storyProgressLevelFromSeed - 1; // Converting based on given conditions

                // Determine the TeraCrystalType based on the difficulty level
                TeraCrystalType type = difficultyLevel switch
                {
                    6 => TeraCrystalType.Black,
                    7 => TeraCrystalType.Might,
                    _ => TeraCrystalType.Base,
                };

                // Create a new RotatingRaidParameters object and populate its properties
                RotatingRaidParameters param = new()
                {
                    Seed = monseed,
                    Title = montitle,
                    Species = RaidExtensions<PK9>.EnumParse<Species>(montitle),
                    CrystalType = type,
                    PartyPK = [data],
                    DifficultyLevel = difficultyLevel,
                    StoryProgress = (GameProgressEnum)convertedStoryProgressLevel
                };

                // Add the RotatingRaidParameters object to the ActiveRaids list
                Settings.ActiveRaids.Add(param);

                // Log the raid parameter generation
                Log($"從文本文件生成了 {montitle} 的參數。");
            }
        }

        private async Task InnerLoop(CancellationToken token)
        {
            bool partyReady;
            RotationCount = 0;
            var raidsHosted = 0;

            while (!token.IsCancellationRequested)
            {
                // Initialize offsets at the start of the routine and cache them.
                await InitializeSessionOffsets(token).ConfigureAwait(false);
                if (RaidCount == 0)
                {
                    TodaySeed = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerP, 8, token).ConfigureAwait(false), 0);
                    Log($"今天的 Seed: {TodaySeed:X8}");
                }

                Log($"準備參數中 {Settings.ActiveRaids[RotationCount].Species}");
                await ReadRaids(token).ConfigureAwait(false);

                var currentSeed = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerP, 8, token).ConfigureAwait(false), 0);
                if (TodaySeed != currentSeed || LobbyError >= 2)
                {
                    if (TodaySeed != currentSeed)
                    {
                        Log($"當前的今日種子 {currentSeed:X8} 與起始的今日種子 {TodaySeed:X8} 不符。\n嘗試覆蓋今日種子...");
                        TodaySeed = currentSeed;
                        await OverrideTodaySeed(token).ConfigureAwait(false);
                        Log("今日種子已經被當前的種子覆蓋。");
                    }

                    if (LobbyError >= 2)
                    {
                        string? msg = $"嘗試建立大廳失敗 {LobbyError} 次。\n";
                        Log(msg);
                        await CloseGame(Hub.Config, token).ConfigureAwait(false);
                        await StartGameRaid(Hub.Config, token).ConfigureAwait(false);
                        LobbyError = 0;
                        continue;
                    }
                }

                // Clear NIDs.
                await SwitchConnection.WriteBytesAbsoluteAsync(new byte[32], TeraNIDOffsets[0], token).ConfigureAwait(false);

                // Connect online and enter den.
                int prepareResult = await PrepareForRaid(token).ConfigureAwait(false);
                if (prepareResult == 2)
                {
                    // Seed was injected, restart the loop
                    continue;
                }
                else if (prepareResult == 0)
                {
                    // Preparation failed, reboot the game
                    Log("準備突擊戰失敗，重新啟動遊戲。");
                    await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                    continue;
                }

                // Wait until we're in lobby.
                if (!await GetLobbyReady(false, token).ConfigureAwait(false))
                    continue;

                if (Settings.ActiveRaids[RotationCount].AddedByRACommand)
                {
                    var user = Settings.ActiveRaids[RotationCount].User;
                    var mentionedUsers = Settings.ActiveRaids[RotationCount].MentionedUsers;

                    // Determine if the raid is a "Free For All"
                    bool isFreeForAll = !Settings.ActiveRaids[RotationCount].IsCoded || EmptyRaid >= Settings.LobbyOptions.EmptyRaidLimit;

                    if (!isFreeForAll)
                    {
                        try
                        {
                            // Only get and send the raid code if it's not a "Free For All"
                            var code = await GetRaidCode(token).ConfigureAwait(false);
                            if (user != null)
                            {
                                await user.SendMessageAsync($"您的突襲代碼是 **{code}**").ConfigureAwait(false);
                            }
                            foreach (var mentionedUser in mentionedUsers)
                            {
                                await mentionedUser.SendMessageAsync($"您被 {user?.Username ?? "主持人"} 邀請參加的私人突襲的代碼是 **{code}**").ConfigureAwait(false);
                            }
                        }
                        catch (Discord.Net.HttpException ex)
                        {
                            // Handle exception (e.g., log the error or send a message to a logging channel)
                            Log($"無法發送私訊給使用者或被提及的使用者。可能是因為他們已關閉私訊功能。錯誤訊息: {ex.Message}");
                        }
                    }
                }

                // Read trainers until someone joins.
                (partyReady, _) = await ReadTrainers(token).ConfigureAwait(false);
                if (!partyReady)
                {
                    if (LostRaid >= Settings.LobbyOptions.SkipRaidLimit && Settings.LobbyOptions.LobbyMethod == LobbyMethodOptions.SkipRaid)
                    {
                        await SkipRaidOnLosses(token).ConfigureAwait(false);
                        EmptyRaid = 0;
                        continue;
                    }

                    // Should add overworld recovery with a game restart fallback.
                    await RegroupFromBannedUser(token).ConfigureAwait(false);

                    if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    {
                        Log("出現問題，嘗試恢復中。");
                        await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                        continue;
                    }

                    // Clear trainer OTs.
                    Log("清除存儲的原始訓練家數據");
                    for (int i = 0; i < 3; i++)
                    {
                        List<long> ptr = new(Offsets.Trader2MyStatusPointer);
                        ptr[2] += i * 0x30;
                        await SwitchConnection.PointerPoke(new byte[16], ptr, token).ConfigureAwait(false);
                    }
                    continue;
                }
                await CompleteRaid(token).ConfigureAwait(false);
                raidsHosted++;
                if (raidsHosted == Settings.RaidSettings.TotalRaidsToHost && Settings.RaidSettings.TotalRaidsToHost > 0)
                    break;
            }
            if (Settings.RaidSettings.TotalRaidsToHost > 0 && raidsHosted != 0)
                Log("已達到要主持的總團數。");
        }

        public override async Task HardStop()
        {
            try
            {
                Directory.Delete("缓存", true);
            }
            catch (Exception)
            { }
            Settings.ActiveRaids.RemoveAll(p => p.AddedByRACommand);
            Settings.ActiveRaids.RemoveAll(p => p.Title == "神秘閃光團戰");
            await CleanExit(CancellationToken.None).ConfigureAwait(false);
        }

        private async Task LocateSeedIndex(CancellationToken token)
        {
            int upperBound = KitakamiDensCount == 25 ? 94 : 95;
            int startIndex = KitakamiDensCount == 25 ? 94 : 95;

            var data = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerP, 2304, token).ConfigureAwait(false);
            for (int i = 0; i < 69; i++)  // Paldea Raids
            {
                var seed = BitConverter.ToUInt32(data.AsSpan(0x20 + i * 0x20, 4));
                if (seed == 0)
                {
                    SeedIndexToReplace = i;
                    Log($"位於帕底亞地區的 {i} 號團戰洞穴。");
                    return;
                }
            }

            data = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerK + 0x10, 0xC80, token).ConfigureAwait(false);
            for (int i = 69; i < upperBound; i++)  // Kitakami Raids
            {
                var seed = BitConverter.ToUInt32(data.AsSpan((i - 69) * 0x20, 4));
                if (seed == 0)
                {
                    SeedIndexToReplace = i;
                    Log($"位於北上鄉的 {i} 號團戰洞穴。");
                    IsKitakami = true;
                    return;
                }
            }

            // Adding support for Blueberry Raids
            data = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerB + 0x10, 0xA00, token).ConfigureAwait(false);
            for (int i = startIndex; i < 118; i++)  // Blueberry Raids
            {
                var seed = BitConverter.ToUInt32(data.AsSpan((i - startIndex) * 0x20, 4));
                if (seed == 0)
                {
                    SeedIndexToReplace = i - 1;  // Adjusting the index by subtracting one
                    Log($"位於藍莓學院的 {i} 號團戰洞穴。");
                    IsBlueberry = true;
                    return;
                }
            }
            Log($"索引未找到。");
        }

        private async Task CompleteRaid(CancellationToken token)
        {
            try
            {
                var trainers = new List<(ulong, RaidMyStatus)>();

                if (!await CheckIfConnectedToLobbyAndLog(token))
                {
                    throw new Exception("未連接到大廳");
                }

                if (!await EnsureInRaid(token))
                {
                    throw new Exception("未在團體戰中");
                }

                if (!Settings.EmbedToggles.AnimatedScreenshot)
                {
                    var screenshotDelay = (int)Settings.EmbedToggles.ScreenshotTiming;
                    await Task.Delay(screenshotDelay, token).ConfigureAwait(false);
                }

                var lobbyTrainersFinal = new List<(ulong, RaidMyStatus)>();
                if (!await UpdateLobbyTrainersFinal(lobbyTrainersFinal, trainers, token))
                {
                    throw new Exception("無法更新大廳訓練師");
                }

                if (!await HandleDuplicatesAndEmbeds(lobbyTrainersFinal, token))
                {
                    throw new Exception("無法處理重複和嵌入");
                }

                await Task.Delay(10_000, token).ConfigureAwait(false);

                if (!await ProcessBattleActions(token))
                {
                    throw new Exception("無法處理戰鬥行動");
                }

                bool isRaidCompleted = await HandleEndOfRaidActions(token);
                if (!isRaidCompleted)
                {
                    throw new Exception("團體戰未完成");
                }

                await FinalizeRaidCompletion(trainers, isRaidCompleted, token);
            }
            catch (Exception ex)
            {
                Log($"在團體戰期間發生錯誤: {ex.Message}");
                await PerformRebootAndReset(token);
            }
        }

        private async Task PerformRebootAndReset(CancellationToken t)
        {
            EmbedBuilder embed = new()
            {
                Title = "機器人重置",
                Description = "機器人遇到問題，目前正在重置中。請稍候。",
                Color = Discord.Color.Red,
                ThumbnailUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/x.png"
            };
            EchoUtil.RaidEmbed(null, "", embed);

            await ReOpenGame(new PokeRaidHubConfig(), t).ConfigureAwait(false);
            await HardStop().ConfigureAwait(false);
            await Task.Delay(2_000, t).ConfigureAwait(false);

            if (!t.IsCancellationRequested)
            {
                Log("重新啟動內部迴圈。");
                await InnerLoop(t).ConfigureAwait(false);
            }
        }

        private async Task<bool> CheckIfConnectedToLobbyAndLog(CancellationToken token)
        {
            try
            {
                if (await IsConnectedToLobby(token).ConfigureAwait(false))
                {
                    Log("準備戰鬥！");
                    return true;
                }
                else
                {
                    Log("未連接到大廳，重新開啟遊戲。");
                    await ReOpenGame(Hub.Config, token);
                    return false;
                }
            }
            catch (Exception ex) // 捕獲適當的異常
            {
                Log($"檢查大廳連接時發生錯誤：{ex.Message}，重新開啟遊戲。");
                await ReOpenGame(Hub.Config, token);
                return false;
            }
        }

        private async Task<bool> EnsureInRaid(CancellationToken linkedToken)
        {
            var startTime = DateTime.Now;

            while (!await IsInRaid(linkedToken).ConfigureAwait(false))
            {
                if (linkedToken.IsCancellationRequested || (DateTime.Now - startTime).TotalMinutes > 5)
                {
                    Log("已達到超時或取消請求，重新開啟遊戲。");
                    await ReOpenGame(Hub.Config, linkedToken);
                    return false;
                }

                if (!await IsConnectedToLobby(linkedToken).ConfigureAwait(false))
                {
                    Log("與大廳的連接中斷，重新開啟遊戲。");
                    await ReOpenGame(Hub.Config, linkedToken);
                    return false;
                }

                await Click(A, 1_000, linkedToken).ConfigureAwait(false);
            }
            return true;
        }

        public async Task<bool> UpdateLobbyTrainersFinal(List<(ulong, RaidMyStatus)> lobbyTrainersFinal, List<(ulong, RaidMyStatus)> trainers, CancellationToken token)
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var storage = new PlayerDataStorage(baseDirectory);
            var playerData = storage.LoadPlayerData();

            // Clear NIDs to refresh player check.
            await SwitchConnection.WriteBytesAbsoluteAsync(new byte[32], TeraNIDOffsets[0], token).ConfigureAwait(false);
            await Task.Delay(5_000, token).ConfigureAwait(false);

            // Loop through trainers again in case someone disconnected.
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var nidOfs = TeraNIDOffsets[i];
                    var data = await SwitchConnection.ReadBytesAbsoluteAsync(nidOfs, 8, token).ConfigureAwait(false);
                    var nid = BitConverter.ToUInt64(data, 0);

                    if (nid == 0)
                        continue;

                    List<long> ptr = new(Offsets.Trader2MyStatusPointer);
                    ptr[2] += i * 0x30;
                    var trainer = await GetTradePartnerMyStatus(ptr, token).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(trainer.OT) || HostSAV.OT == trainer.OT)
                        continue;

                    lobbyTrainersFinal.Add((nid, trainer));

                    if (!playerData.TryGetValue(nid, out var info))
                    {
                        // 新玩家
                        playerData[nid] = new PlayerInfo { OT = trainer.OT, RaidCount = 1 };
                        Log($"新玩家：{trainer.OT} | TID：{trainer.DisplayTID} | NID：{nid}。");
                    }
                    else
                    {
                        // 回歸玩家
                        info.RaidCount++;
                        playerData[nid] = info; // 更新資訊至字典中
                        Log($"回歸玩家：{trainer.OT} | TID：{trainer.DisplayTID} | NID：{nid} | 參加的團戰數量：{info.RaidCount}");
                    }
                }
                catch (IndexOutOfRangeException ex)
                {
                    Log($"捕捉到索引超出範圍的例外情況：{ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    Log($"發生未知錯誤：{ex.Message}");
                    return false;
                }
            }

            // Save player data after processing all players.
            storage.SavePlayerData(playerData);
            return true;
        }

        private async Task<bool> HandleDuplicatesAndEmbeds(List<(ulong, RaidMyStatus)> lobbyTrainersFinal, CancellationToken token)
        {
            var nidDupe = lobbyTrainersFinal.Select(x => x.Item1).ToList();
            var dupe = lobbyTrainersFinal.Count > 1 && nidDupe.Distinct().Count() == 1;
            if (dupe)
            {
                // 我們讀取到了錯誤的資料，重新啟動遊戲以提前結束並進行恢復。
                var msg = "糟糕！出了些問題，正在重置以進行恢復。";
                bool success = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        await Task.Delay(5_000, token);
                        await EnqueueEmbed(null, "", false, false, false, false, token).ConfigureAwait(false);
                        success = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log($"第 {attempt} 次嘗試失敗，錯誤訊息: {ex.Message}");
                        if (attempt == 3)
                        {
                            Log("所有嘗試均失敗。將繼續進行而不發送嵌入訊息。");
                        }
                    }
                }

                if (!success)
                {
                    await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                    return false;
                }
            }

            var names = lobbyTrainersFinal.Select(x => x.Item2.OT).ToList();
            bool hatTrick = lobbyTrainersFinal.Count == 3 && names.Distinct().Count() == 1;

            bool embedSuccess = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await EnqueueEmbed(names, "", hatTrick, false, false, true, token).ConfigureAwait(false);
                    embedSuccess = true;
                    break;
                }
                catch (Exception ex)
                {
                    Log($"第 {attempt} 次嘗試失敗，錯誤訊息: {ex.Message}");
                    if (attempt == 3)
                    {
                        Log("所有嘗試均失敗。將繼續進行而不發送嵌入訊息。");
                    }
                }
            }

            return embedSuccess;
        }

        private async Task<bool> ProcessBattleActions(CancellationToken token)
        {
            int nextUpdateMinute = 2;
            DateTime battleStartTime = DateTime.Now;
            bool hasPerformedAction1 = false;
            bool timedOut = false;
            bool hasPressedHome = false;

            while (await IsConnectedToLobby(token).ConfigureAwait(false))
            {
                // 新檢查: 我們仍然在戰鬥中嗎？
                if (!await IsInRaid(token).ConfigureAwait(false))
                {
                    Log("不再處於戰鬥中，停止戰鬥動作。");
                    return false;
                }

                TimeSpan timeInBattle = DateTime.Now - battleStartTime;

                // 檢查是否超時
                if (timeInBattle.TotalMinutes >= 15)
                {
                    Log("戰鬥在15分鐘後超時。就連 Netflix 都會問我是否還在觀看...");
                    timedOut = true;
                    break;
                }

                // 使用延遲處理第一個動作
                if (!hasPerformedAction1)
                {
                    int action1DelayInSeconds = Settings.ActiveRaids[RotationCount].Action1Delay;
                    var action1Name = Settings.ActiveRaids[RotationCount].Action1;
                    int action1DelayInMilliseconds = action1DelayInSeconds * 1000;
                    Log($"等待 {action1DelayInSeconds} 秒。別著急，我們正在放鬆。");
                    await Task.Delay(action1DelayInMilliseconds, token).ConfigureAwait(false);
                    await MyActionMethod(token).ConfigureAwait(false);
                    Log($"{action1Name} 完成。這不是很有趣嗎？");
                    hasPerformedAction1 = true;
                }
                else
                {
                    // 根據設定執行團體戰行動
                    switch (Settings.LobbyOptions.Action)
                    {
                        case RaidAction.AFK:
                            await Task.Delay(3_000, token).ConfigureAwait(false);
                            break;

                        case RaidAction.MashA:
                            if (await IsConnectedToLobby(token).ConfigureAwait(false))
                            {
                                int mashADelayInMilliseconds = (int)(Settings.LobbyOptions.MashADelay * 1000);
                                await Click(A, mashADelayInMilliseconds, token).ConfigureAwait(false);
                            }
                            break;
                    }
                }

                // 每隔2分鐘記錄一次戰鬥狀態
                if (timeInBattle.TotalMinutes >= nextUpdateMinute)
                {
                    Log($"{nextUpdateMinute} 分鐘已過。我們仍在戰鬥中...");
                    nextUpdateMinute += 2; // 更新下一次狀態更新的時間
                }

                // 檢查是否戰鬥已進行了6分鐘且未按過主畫面按鈕
                if (timeInBattle.TotalMinutes >= 6 && !hasPressedHome)
                {
                    // 按兩次主畫面按鈕，以防卡住
                    await Click(HOME, 0_500, token).ConfigureAwait(false);
                    await Click(HOME, 0_500, token).ConfigureAwait(false);
                    hasPressedHome = true;
                }

                // 確保在下一次迴圈前等待一段時間，避免過於密集的迴圈
                await Task.Delay(1000, token); // 等待一秒後再次檢查
            }

            return !timedOut;
        }

        private async Task<bool> HandleEndOfRaidActions(CancellationToken token)
        {
            LobbyFiltersCategory settings = new();

            Log("團隊大廳已解散！");
            await Task.Delay(1_500 + settings.ExtraTimeLobbyDisband, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(DDOWN, 0_500, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
            bool ready = true;

            if (Settings.LobbyOptions.LobbyMethod == LobbyMethodOptions.SkipRaid)
            {
                Log($"失敗/空團隊數量: {LostRaid}/{Settings.LobbyOptions.SkipRaidLimit}");

                if (LostRaid >= Settings.LobbyOptions.SkipRaidLimit)
                {
                    Log($"已達到 {Settings.LobbyOptions.SkipRaidLimit} 次失敗/空團隊數量。繼續下一步！");
                    await SanitizeRotationCount(token).ConfigureAwait(false);
                    await EnqueueEmbed(null, "", false, false, true, false, token).ConfigureAwait(false);
                    ready = true;
                }
            }

            return ready;
        }

        private async Task FinalizeRaidCompletion(List<(ulong, RaidMyStatus)> trainers, bool ready, CancellationToken token)
        {
            Log("返回到主世界...");
            await Task.Delay(2_500, token).ConfigureAwait(false);
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(A, 1_000, token).ConfigureAwait(false);

            await LocateSeedIndex(token).ConfigureAwait(false);
            await CountRaids(trainers, token).ConfigureAwait(false);
            // Update RotationCount after locating seed index
            if (Settings.ActiveRaids.Count > 1)
            {
                await SanitizeRotationCount(token).ConfigureAwait(false);
            }
            await EnqueueEmbed(null, "", false, false, true, false, token).ConfigureAwait(false);
            await Task.Delay(0_500, token).ConfigureAwait(false);
            await CloseGame(Hub.Config, token).ConfigureAwait(false);

            if (ready)
                await StartGameRaid(Hub.Config, token).ConfigureAwait(false);
            else
            {
                if (Settings.ActiveRaids.Count > 1)
                {
                    RotationCount = (RotationCount + 1) % Settings.ActiveRaids.Count;
                    if (RotationCount == 0)
                    {
                        Log($"重置旋轉計數為 {RotationCount}");
                    }

                    Log($"開始進行下一輪 {Settings.ActiveRaids[RotationCount].Species} 的操作。");
                    await StartGameRaid(Hub.Config, token).ConfigureAwait(false);
                }
                else
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
            }

            if (Settings.RaidSettings.KeepDaySeed)
                await OverrideTodaySeed(token).ConfigureAwait(false);
        }

        public async Task MyActionMethod(CancellationToken token)
        {
            // Let's rock 'n roll with these moves!
            switch (Settings.ActiveRaids[RotationCount].Action1)
            {
                case Action1Type.GoAllOut:
                    await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                    await Click(A, 0_500, token).ConfigureAwait(false);
                    for (int i = 0; i < 3; i++)
                    {
                        await Click(A, 0_500, token).ConfigureAwait(false);
                    }
                    break;

                case Action1Type.HangTough:
                case Action1Type.HealUp:
                    await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                    await Click(A, 0_500, token).ConfigureAwait(false);
                    int ddownTimes = Settings.ActiveRaids[RotationCount].Action1 == Action1Type.HangTough ? 1 : 2;
                    for (int i = 0; i < ddownTimes; i++)
                    {
                        await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        await Click(A, 0_500, token).ConfigureAwait(false);
                    }
                    break;

                case Action1Type.Move1:
                    await Click(A, 0_500, token).ConfigureAwait(false);
                    for (int i = 0; i < 3; i++)
                    {
                        await Click(A, 0_500, token).ConfigureAwait(false);
                    }
                    break;

                case Action1Type.Move2:
                case Action1Type.Move3:
                case Action1Type.Move4:
                    await Click(A, 0_500, token).ConfigureAwait(false);
                    int moveDdownTimes = Settings.ActiveRaids[RotationCount].Action1 == Action1Type.Move2 ? 1 : Settings.ActiveRaids[RotationCount].Action1 == Action1Type.Move3 ? 2 : 3;
                    for (int i = 0; i < moveDdownTimes; i++)
                    {
                        await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        await Click(A, 0_500, token).ConfigureAwait(false);
                    }
                    break;

                default:
                    Console.WriteLine("未知的動作類型，請問您的意圖是什麼？");
                    throw new InvalidOperationException("未知的動作類型！");
            }
        }

        private async Task<uint> ReadAreaId(int raidIndex, CancellationToken token)
        {
            List<long> pointer = CalculateDirectPointer(raidIndex);
            int areaIdOffset = 20;

            return await ReadValue("區域識別碼", 4, AdjustPointer(pointer, areaIdOffset), token);
        }

        private async Task CountRaids(List<(ulong, RaidMyStatus)>? trainers, CancellationToken token)
        {
            if (trainers is not null)
            {
                Log("回到了遊戲世界，檢查是否勝利或失敗。");

                int currentRaidIndex = SeedIndexToReplace;
                uint areaId = await ReadAreaId(currentRaidIndex, token);

                if (areaId == 0)
                {
                    Log("耶！我們打敗了這次的團戰！");
                    WinCount++;
                }
                else
                {
                    Log("哎呀，我們輸了這場團戰。");
                    LossCount++;
                }
            }
            else
            {
                Log("沒有可用的訓練師來檢查勝負狀態。");
            }
        }

        private async Task OverrideTodaySeed(CancellationToken token)
        {
            Log("嘗試覆蓋今日種子...");

            var todayoverride = BitConverter.GetBytes(TodaySeed);
            List<long> ptr = new(Offsets.RaidBlockPointerP);
            ptr[3] += 0x8;
            await SwitchConnection.PointerPoke(todayoverride, ptr, token).ConfigureAwait(false);

            Log("今日種子覆蓋完成。");
        }

        private async Task OverrideSeedIndex(int index, CancellationToken token)
        {
            if (index == -1)
            {
                Log("索引為 -1，跳過種子覆蓋。");
                return;
            }

            var crystalType = Settings.ActiveRaids[RotationCount].CrystalType;
            var seed = uint.Parse(Settings.ActiveRaids[RotationCount].Seed, NumberStyles.AllowHexSpecifier);
            var speciesName = Settings.ActiveRaids[RotationCount].Species.ToString();
            var groupID = Settings.ActiveRaids[RotationCount].GroupID;
            var denLocations = LoadDenLocations("SysBot.Pokemon.SV.BotRaid.DenLocations.den_locations_base.json");
            string? denIdentifier = null;

            // Check if the user is not in Paldea and adjust the crystal type accordingly
            if ((IsKitakami || IsBlueberry) && (crystalType == TeraCrystalType.Might || crystalType == TeraCrystalType.Distribution))
            {
                crystalType = TeraCrystalType.Black;
                Log("用戶不在帕底亞。將水晶類型設置為黑色。");
            }

            if (crystalType == TeraCrystalType.Might || crystalType == TeraCrystalType.Distribution)
            {
                uint defaultSeed = uint.Parse("000118C8", NumberStyles.AllowHexSpecifier);
                if (index != -1)
                {
                    List<long> prevPtr = DeterminePointer(index);
                    byte[] defaultSeedBytes = BitConverter.GetBytes(defaultSeed);
                    await SwitchConnection.PointerPoke(defaultSeedBytes, prevPtr, token).ConfigureAwait(false);
                    Log($"在上一個索引 {index} 設置默認種子 {defaultSeed:X8}。");
                    await Task.Delay(1_500, token).ConfigureAwait(false);
                }
                if (SpeciesToGroupIDMap.TryGetValue(speciesName, out var groupIDAndIndices))
                {
                    var specificIndexInfo = groupIDAndIndices.FirstOrDefault(x => x.GroupID == groupID);
                    if (specificIndexInfo != default)
                    {
                        index = specificIndexInfo.Index; // 根據 GroupID 和物種調整的索引
                        denIdentifier = specificIndexInfo.DenIdentifier; // 捕獲巢穴標識以進行傳送
                        Log($"使用特定索引 {index} 進行 GroupID：{groupID}，物種：{speciesName}，和 Den 識別符：{denIdentifier}。");
                    }
                }
                List<long> ptr = DeterminePointer(index);
                byte[] seedBytes = BitConverter.GetBytes(seed);
                await SwitchConnection.PointerPoke(seedBytes, ptr, token).ConfigureAwait(false);
                Log($"已注入種子 {seed:X8} 到索引 {index}。");

                var crystalPtr = new List<long>(ptr);
                crystalPtr[3] += 0x08;
                byte[] crystalBytes = BitConverter.GetBytes((int)crystalType);
                await SwitchConnection.PointerPoke(crystalBytes, crystalPtr, token).ConfigureAwait(false);
                await Task.Delay(1_000, token).ConfigureAwait(false);

                // Teleportation logic
                if (denIdentifier != null && denLocations.TryGetValue(denIdentifier, out var coordinates))
                    if (coordinates != null && coordinates.Length == 3)
                    {
                        await TeleportToDen(coordinates[0], coordinates[1], coordinates[2], token);
                        Log($"成功傳送到巢穴：{denIdentifier}，座標為 {String.Join(", ", coordinates)}。");
                    }
                    else
                    {
                        Log($"未找到巢穴位置：{denIdentifier}。");
                    }
            }
            else
            {
                List<long> ptr = DeterminePointer(index);
                // 覆蓋種子
                byte[] inj = BitConverter.GetBytes(seed);
                var currseed = await SwitchConnection.PointerPeek(4, ptr, token).ConfigureAwait(false);

                // 對於日誌目的，如果需要，反轉當前種子的字节数組
                byte[] currSeedForLogging = (byte[])currseed.Clone();
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(currSeedForLogging);
                }

                // 對於日誌目的，如果需要，反轉新種子的字节数組
                byte[] injForLogging = (byte[])inj.Clone();
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(injForLogging);
                }

                // 將字節數組轉換為十六進制字符串以進行日誌記錄
                string currSeedHex = BitConverter.ToString(currSeedForLogging).Replace("-", "");
                string newSeedHex = BitConverter.ToString(injForLogging).Replace("-", "");

                Log($"將 {currSeedHex} 替換為 {newSeedHex}。");
                await SwitchConnection.PointerPoke(inj, ptr, token).ConfigureAwait(false);

                // 覆蓋水晶類型
                var ptr2 = new List<long>(ptr);
                ptr2[3] += 0x08;
                var crystal = BitConverter.GetBytes((int)crystalType);
                var currcrystal = await SwitchConnection.PointerPeek(1, ptr2, token).ConfigureAwait(false);
                if (currcrystal != crystal)
                    await SwitchConnection.PointerPoke(crystal, ptr2, token).ConfigureAwait(false);
            }
        }

        private void CreateAndAddRandomShinyRaidAsRequested()
        {
            // 生成隨機的閃光寶可夢種子
            uint randomSeed = GenerateRandomShinySeed(); // 生成隨機的閃光寶可夢種子
            Random random = new Random(); // 初始化隨機數生成器

            // 獲取設定對象
            var mysteryRaidsSettings = Settings.RaidSettings.MysteryRaidsSettings; // 獲取神秘突襲設置

            // 檢查是否啟用任何神秘突襲設置
            if (!(mysteryRaidsSettings.Unlocked3StarSettings.Enabled || mysteryRaidsSettings.Unlocked4StarSettings.Enabled ||
                  mysteryRaidsSettings.Unlocked5StarSettings.Enabled || mysteryRaidsSettings.Unlocked6StarSettings.Enabled))
            {
                Log("所有神秘突襲選項都已禁用。將關閉神秘突襲功能。");
                Settings.RaidSettings.MysteryRaids = false; // 禁用神秘突襲
                return; // 退出方法
            }

            // 建立已啟用的故事進度級別列表
            var enabledLevels = new List<GameProgress>();
            if (mysteryRaidsSettings.Unlocked3StarSettings.Enabled) enabledLevels.Add(GameProgress.Unlocked3Stars);
            if (mysteryRaidsSettings.Unlocked4StarSettings.Enabled) enabledLevels.Add(GameProgress.Unlocked4Stars);
            if (mysteryRaidsSettings.Unlocked5StarSettings.Enabled) enabledLevels.Add(GameProgress.Unlocked5Stars);
            if (mysteryRaidsSettings.Unlocked6StarSettings.Enabled) enabledLevels.Add(GameProgress.Unlocked6Stars);

            // 從已啟用的級別中隨機選擇一個故事進度級別
            GameProgress gameProgress = enabledLevels[random.Next(enabledLevels.Count)];

            // 初始化存儲可能難度的列表
            List<int> possibleDifficulties = new List<int>();
            // 根據選定的故事進度級別確定可能的難度
            // (這部分程式碼應該是接下來實現的)

            // 在此省略了可能的難度的處理邏輯，需要根據具體需求進行實現

            // 下面可以添加實現具體邏輯的程式碼
            switch (gameProgress)
            {
                case GameProgress.Unlocked3Stars:
                    if (mysteryRaidsSettings.Unlocked3StarSettings.Allow1StarRaids) possibleDifficulties.Add(1);
                    if (mysteryRaidsSettings.Unlocked3StarSettings.Allow2StarRaids) possibleDifficulties.Add(2);
                    if (mysteryRaidsSettings.Unlocked3StarSettings.Allow3StarRaids) possibleDifficulties.Add(3);
                    break;

                case GameProgress.Unlocked4Stars:
                    if (mysteryRaidsSettings.Unlocked4StarSettings.Allow1StarRaids) possibleDifficulties.Add(1);
                    if (mysteryRaidsSettings.Unlocked4StarSettings.Allow2StarRaids) possibleDifficulties.Add(2);
                    if (mysteryRaidsSettings.Unlocked4StarSettings.Allow3StarRaids) possibleDifficulties.Add(3);
                    if (mysteryRaidsSettings.Unlocked4StarSettings.Allow4StarRaids) possibleDifficulties.Add(4);
                    break;

                case GameProgress.Unlocked5Stars:
                    if (mysteryRaidsSettings.Unlocked5StarSettings.Allow3StarRaids) possibleDifficulties.Add(3);
                    if (mysteryRaidsSettings.Unlocked5StarSettings.Allow4StarRaids) possibleDifficulties.Add(4);
                    if (mysteryRaidsSettings.Unlocked5StarSettings.Allow5StarRaids) possibleDifficulties.Add(5);
                    break;

                case GameProgress.Unlocked6Stars:
                    if (mysteryRaidsSettings.Unlocked6StarSettings.Allow3StarRaids) possibleDifficulties.Add(3);
                    if (mysteryRaidsSettings.Unlocked6StarSettings.Allow4StarRaids) possibleDifficulties.Add(4);
                    if (mysteryRaidsSettings.Unlocked6StarSettings.Allow5StarRaids) possibleDifficulties.Add(5);
                    if (mysteryRaidsSettings.Unlocked6StarSettings.Allow6StarRaids) possibleDifficulties.Add(6);
                    break;
            }

            // 檢查是否有啟用的難度級別
            if (possibleDifficulties.Count == 0)
            {
                Log("所選的故事進度沒有啟用的難度級別。將關閉神秘突襲功能。");
                Settings.RaidSettings.MysteryRaids = false; // 禁用神秘突襲
                return; // 退出方法
            }

            // 從可能的難度中隨機選擇一個難度級別
            int randomDifficultyLevel = possibleDifficulties[random.Next(possibleDifficulties.Count)];

            // 根據難度級別決定水晶類型
            var crystalType = randomDifficultyLevel switch
            {
                >= 1 and <= 5 => TeraCrystalType.Base,
                6 => TeraCrystalType.Black,
                _ => throw new ArgumentException("無效的難度級別。")
            };

            RotatingRaidParameters newRandomShinyRaid = new()
            {
                Seed = randomSeed.ToString("X8"),
                Species = Species.None,
                Title = "神秘閃光Raid",
                AddedByRACommand = true,
                DifficultyLevel = randomDifficultyLevel,
                StoryProgress = (GameProgressEnum)gameProgress,
                CrystalType = crystalType,
                IsShiny = true
            };

            // 尋找由 RA 命令添加的最後一個突襲的位置
            int lastRaCommandRaidIndex = Settings.ActiveRaids.FindLastIndex(raid => raid.AddedByRACommand);
            int insertPosition = lastRaCommandRaidIndex != -1 ? lastRaCommandRaidIndex + 1 : RotationCount + 1;

            // 在確定的位置插入新的神秘閃光突襲
            Settings.ActiveRaids.Insert(insertPosition, newRandomShinyRaid);

            // 為了調試目的記錄添加操作
            Log($"已添加神秘閃光突襲，種子為：{randomSeed:X}，位置為 {insertPosition}");
        }

        private static uint GenerateRandomShinySeed()
        {
            Random random = new();
            uint seed;

            do
            {
                // Generate a random uint
                byte[] buffer = new byte[4];
                random.NextBytes(buffer);
                seed = BitConverter.ToUInt32(buffer, 0);
            }
            while (Raidshiny(seed) == 0);

            return seed;
        }

        private static int Raidshiny(uint Seed)
        {
            Xoroshiro128Plus xoroshiro128Plus = new(Seed);
            _ = (uint)xoroshiro128Plus.NextInt(4294967295uL);
            uint num2 = (uint)xoroshiro128Plus.NextInt(4294967295uL);
            uint num3 = (uint)xoroshiro128Plus.NextInt(4294967295uL);
            return (((num3 >> 16) ^ (num3 & 0xFFFF)) >> 4 == ((num2 >> 16) ^ (num2 & 0xFFFF)) >> 4) ? 1 : 0;
        }

        private async Task<uint> ReadValue(string fieldName, int size, List<long> pointer, CancellationToken token)
        {
            // 從指定的指標位置讀取指定大小的位元組陣列
            byte[] valueBytes = await SwitchConnection.PointerPeek(size, pointer, token).ConfigureAwait(false);
            //  Log($"{fieldName} - Read Value: {BitConverter.ToString(valueBytes)}");

            // 根據欄位名稱確定位元組順序
            bool isBigEndian = fieldName.Equals("Den ID");

            if (isBigEndian)
            {
                // 如果數值是大端格式，反轉位元組陣列
                Array.Reverse(valueBytes);
            }

            // 將位元組陣列轉換為 uint（現在是小端格式）
            return BitConverter.ToUInt32(valueBytes, 0);
        }

        private async Task LogAndUpdateValue(string fieldName, uint value, int size, List<long> pointer, CancellationToken token)
        {
            _ = await SwitchConnection.PointerPeek(size, pointer, token).ConfigureAwait(false);
            // 記錄目前的值（如果需要）
            // Log($"{fieldName} - Current Value: {BitConverter.ToString(currentValue)}");

            // 根據欄位名稱確定位元組順序
            bool isBigEndian = fieldName.Equals("Den ID");

            // 創建新的位元組陣列來存放新值
            byte[] newValue = new byte[4]; // 假設 uint 是 4 個位元組
            if (isBigEndian)
            {
                newValue[0] = (byte)(value >> 24); // 最高有效位元組
                newValue[1] = (byte)(value >> 16);
                newValue[2] = (byte)(value >> 8);
                newValue[3] = (byte)(value);       // 最低有效位元組
            }
            else
            {
                newValue[0] = (byte)(value);       // 最低有效位元組
                newValue[1] = (byte)(value >> 8);
                newValue[2] = (byte)(value >> 16);
                newValue[3] = (byte)(value >> 24); // 最高有效位元組
            }

            await SwitchConnection.PointerPoke(newValue, pointer, token).ConfigureAwait(false);
            _ = await SwitchConnection.PointerPeek(size, pointer, token).ConfigureAwait(false);
            //  Log($"{fieldName} - Updated Value: {BitConverter.ToString(updatedValue)}");
        }

        private static List<long> AdjustPointer(List<long> basePointer, int offset)
        {
            var adjustedPointer = new List<long>(basePointer);
            adjustedPointer[3] += offset; // Adjusting the offset at the 4th index
            return adjustedPointer;
        }

        private List<long> CalculateDirectPointer(int index)
        {
            int blueberrySubtractValue = KitakamiDensCount == 25 ? 94 : 95;

            if (IsKitakami)
            {
                return new List<long>(Offsets.RaidBlockPointerK)
                {
                    [3] = 0xCE8 + ((index - 70) * 0x20)
                };
            }
            else if (IsBlueberry)
            {
                return new List<long>(Offsets.RaidBlockPointerB)
                {
                    [3] = 0x1968 + ((index - blueberrySubtractValue) * 0x20)
                };
            }
            else
            {
                return new List<long>(Offsets.RaidBlockPointerP)
                {
                    [3] = 0x40 + index * 0x20
                };
            }
        }

        private List<long> DeterminePointer(int index)
        {
            int blueberrySubtractValue = KitakamiDensCount == 25 ? 93 : 94;

            if (index < 69)
            {
                return new List<long>(Offsets.RaidBlockPointerP)
                {
                    [3] = 0x60 + index * 0x20
                };
            }
            else if (index < 94)
            {
                return new List<long>(Offsets.RaidBlockPointerK)
                {
                    [3] = 0xCE8 + ((index - 69) * 0x20)
                };
            }
            else
            {
                return new List<long>(Offsets.RaidBlockPointerB)
                {
                    [3] = 0x1968 + ((index - blueberrySubtractValue) * 0x20)
                };
            }
        }

        private async Task SanitizeRotationCount(CancellationToken token)
        {
            try
            {
                await Task.Delay(50, token).ConfigureAwait(false);

                if (Settings.ActiveRaids.Count == 0)
                {
                    Log("ActiveRaids is empty. Exiting SanitizeRotationCount.");
                    RotationCount = 0;
                    return;
                }

                // Normalize RotationCount to be within the range of ActiveRaids
                RotationCount = Math.Max(0, Math.Min(RotationCount, Settings.ActiveRaids.Count - 1));

                // Update RaidUpNext for the next raid
                int nextRaidIndex = FindNextPriorityRaidIndex(RotationCount, Settings.ActiveRaids);
                for (int i = 0; i < Settings.ActiveRaids.Count; i++)
                {
                    Settings.ActiveRaids[i].RaidUpNext = i == nextRaidIndex;
                }

                // Process RA command raids
                if (Settings.ActiveRaids[RotationCount].AddedByRACommand)
                {
                    bool isMysteryRaid = Settings.ActiveRaids[RotationCount].Title.Contains("Mystery Shiny Raid");
                    bool isUserRequestedRaid = !isMysteryRaid && Settings.ActiveRaids[RotationCount].Title.Contains("'s Requested Raid");

                    if (isUserRequestedRaid || isMysteryRaid)
                    {
                        Log($"Raid for {Settings.ActiveRaids[RotationCount].Species} was added via RA command and will be removed from the rotation list.");
                        Settings.ActiveRaids.RemoveAt(RotationCount);
                        // Adjust RotationCount after removal
                        if (RotationCount >= Settings.ActiveRaids.Count)
                        {
                            RotationCount = 0;
                        }

                        // After a raid is removed, find the new next priority raid and update RaidUpNext
                        nextRaidIndex = FindNextPriorityRaidIndex(RotationCount, Settings.ActiveRaids);
                        for (int i = 0; i < Settings.ActiveRaids.Count; i++)
                        {
                            Settings.ActiveRaids[i].RaidUpNext = i == nextRaidIndex;
                        }
                    }
                    else if (!firstRun)
                    {
                        RotationCount = (RotationCount + 1) % Settings.ActiveRaids.Count;
                    }
                }
                else if (!firstRun)
                {
                    RotationCount = (RotationCount + 1) % Settings.ActiveRaids.Count;
                }

                if (firstRun)
                {
                    firstRun = false;
                }

                if (Settings.RaidSettings.RandomRotation)
                {
                    ProcessRandomRotation();
                    return;
                }

                // Find next priority raid
                int nextPriorityIndex = FindNextPriorityRaidIndex(RotationCount, Settings.ActiveRaids);
                if (nextPriorityIndex != -1)
                {
                    RotationCount = nextPriorityIndex;
                }
                Log($"Next raid in the list: {Settings.ActiveRaids[RotationCount].Species}.");
            }
            catch (Exception ex)
            {
                Log($"Index was out of range. Resetting RotationCount to 0. {ex.Message}");
                RotationCount = 0;
            }
        }

        private int FindNextPriorityRaidIndex(int currentRotationCount, List<RotatingRaidParameters> raids)
        {
            if (raids == null || raids.Count == 0)
            {
                // Handle edge case where raids list is empty or null
                return currentRotationCount;
            }

            int count = raids.Count;

            // First, check for user-requested RA command raids
            for (int i = 0; i < count; i++)
            {
                int index = (currentRotationCount + i) % count;
                RotatingRaidParameters raid = raids[index];

                if (raid.AddedByRACommand && !raid.Title.Contains("Mystery Shiny Raid"))
                {
                    return index; // Prioritize user-requested raids
                }
            }

            // Next, check for Mystery Shiny Raids if enabled
            if (Settings.RaidSettings.MysteryRaids)
            {
                for (int i = 0; i < count; i++)
                {
                    int index = (currentRotationCount + i) % count;
                    RotatingRaidParameters raid = raids[index];

                    if (raid.Title.Contains("Mystery Shiny Raid"))
                    {
                        return index; // Only consider Mystery Shiny Raids after user-requested raids
                    }
                }
            }

            // Return current rotation count if no priority raids are found
            return -1;
        }

        private void ProcessRandomRotation()
        {
            // Turn off RandomRotation if both RandomRotation and MysteryRaid are true
            if (Settings.RaidSettings.RandomRotation && Settings.RaidSettings.MysteryRaids)
            {
                Settings.RaidSettings.RandomRotation = false;
                Log("RandomRotation turned off due to MysteryRaids being active.");
                return;  // Exit the method as RandomRotation is now turned off
            }

            // Check the remaining raids for any added by the RA command
            for (var i = RotationCount; i < Settings.ActiveRaids.Count; i++)
            {
                if (Settings.ActiveRaids[i].AddedByRACommand)
                {
                    RotationCount = i;
                    Log($"Setting Rotation Count to {RotationCount}");
                    return;  // Exit method as a raid added by RA command was found
                }
            }

            // If no raid added by RA command was found, select a random raid
            var random = new Random();
            RotationCount = random.Next(Settings.ActiveRaids.Count);
            Log($"Setting Rotation Count to {RotationCount}");
        }

        private async Task InjectPartyPk(string battlepk, CancellationToken token)
        {
            var set = new ShowdownSet(battlepk);
            var template = AutoLegalityWrapper.GetTemplate(set);
            PK9 pk = (PK9)HostSAV.GetLegal(template, out _);
            pk.ResetPartyStats();
            var offset = await SwitchConnection.PointerAll(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(pk.EncryptedBoxData, offset, token).ConfigureAwait(false);
        }

        private async Task<int> PrepareForRaid(CancellationToken token)
        {
            if (shouldRefreshMap)
            {
                Log("Starting Refresh map process...");
                await HardStop().ConfigureAwait(false);
                await Task.Delay(2_000, token).ConfigureAwait(false);
                await Click(B, 3_000, token).ConfigureAwait(false);
                await Click(B, 3_000, token).ConfigureAwait(false);
                await GoHome(Hub.Config, token).ConfigureAwait(false);
                await AdvanceDaySV(token).ConfigureAwait(false);
                await SaveGame(Hub.Config, token).ConfigureAwait(false);
                shouldRefreshMap = false;
                if (!token.IsCancellationRequested)
                {
                    Log("Map Refresh Completed. Restarting the main loop...");
                    await MainLoop(token).ConfigureAwait(false);
                }
            }

            _ = Settings.ActiveRaids[RotationCount];
            var currentSeed = Settings.ActiveRaids[RotationCount].Seed.ToUpper();

            if (!denHexSeed.Equals(currentSeed, StringComparison.CurrentCultureIgnoreCase))
            {
                seedMismatchCount++;
                Log($"Raid Den and Current Seed do not match. Mismatch count: {seedMismatchCount}");

                if (seedMismatchCount >= 2)
                {
                    Log("Seeds have mismatched 2 times in a row. Refreshing the map.");
                    shouldRefreshMap = true;
                    seedMismatchCount = 0;
                    return 2;
                }

                Log("Injecting correct seed.");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGameRaid(Hub.Config, token).ConfigureAwait(false);
                Log("Seed injected Successfully!");
                return 2;
            }
            else
            {
                seedMismatchCount = 0;
            }

            if (Settings.ActiveRaids[RotationCount].AddedByRACommand)
            {
                var user = Settings.ActiveRaids[RotationCount].User;
                var mentionedUsers = Settings.ActiveRaids[RotationCount].MentionedUsers;

                // Determine if the raid is a "Free For All"
                bool isFreeForAll = !Settings.ActiveRaids[RotationCount].IsCoded || EmptyRaid >= Settings.LobbyOptions.EmptyRaidLimit;

                if (!isFreeForAll)
                {
                    try
                    {
                        // Only send the message if it's not a "Free For All"
                        if (user != null)
                        {
                            await user.SendMessageAsync("Get Ready! Your raid is being prepared now!").ConfigureAwait(false);
                        }

                        foreach (var mentionedUser in mentionedUsers)
                        {
                            await mentionedUser.SendMessageAsync($"Get Ready! The raid you were invited to by {user?.Username ?? "the host"} is about to start!").ConfigureAwait(false);
                        }
                    }
                    catch (Discord.Net.HttpException ex)
                    {
                        // Handle exception (e.g., log the error or send a message to a logging channel)
                        Log($"Failed to send DM to the user or mentioned users. They might have DMs turned off. Exception: {ex.Message}");
                    }
                }
            }

            Log("Preparing lobby...");
            LobbyFiltersCategory settings = new();

            if (!await ConnectToOnline(Hub.Config, token))
            {
                return 0;
            }

            await Task.Delay(0_500, token).ConfigureAwait(false);
            var len = string.Empty;
            foreach (var l in Settings.ActiveRaids[RotationCount].PartyPK)
                len += l;
            if (len.Length > 1 && EmptyRaid == 0)
            {
                Log("Preparing PartyPK. Sit tight.");
                await Task.Delay(2_500 + settings.ExtraTimeLobbyDisband, token).ConfigureAwait(false);
                await SetCurrentBox(0, token).ConfigureAwait(false);
                var res = string.Join("\n", Settings.ActiveRaids[RotationCount].PartyPK);
                if (res.Length > 4096)
                    res = res[..4096];
                await InjectPartyPk(res, token).ConfigureAwait(false);

                await Click(X, 2_000, token).ConfigureAwait(false);
                await Click(DRIGHT, 0_500, token).ConfigureAwait(false);
                await SetStick(SwitchStick.LEFT, 0, -32000, 1_000, token).ConfigureAwait(false);
                await SetStick(SwitchStick.LEFT, 0, 0, 0, token).ConfigureAwait(false);
                for (int i = 0; i < 2; i++)
                    await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                await Click(A, 3_500, token).ConfigureAwait(false);
                await Click(Y, 0_500, token).ConfigureAwait(false);
                await Click(DLEFT, 0_800, token).ConfigureAwait(false);
                await Click(Y, 0_500, token).ConfigureAwait(false);
                for (int i = 0; i < 2; i++)
                    await Click(B, 1_500, token).ConfigureAwait(false);
                Log("PartyPK switch successful.");
            }

            for (int i = 0; i < 4; i++)
                await Click(B, 1_000, token).ConfigureAwait(false);

            await Task.Delay(1_500, token).ConfigureAwait(false);

            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return 0;

            await Click(A, 3_000, token).ConfigureAwait(false);
            await Click(A, 3_000, token).ConfigureAwait(false);

            if (!Settings.ActiveRaids[RotationCount].IsCoded || (Settings.ActiveRaids[RotationCount].IsCoded && EmptyRaid == Settings.LobbyOptions.EmptyRaidLimit && Settings.LobbyOptions.LobbyMethod == LobbyMethodOptions.OpenLobby))
            {
                if (Settings.ActiveRaids[RotationCount].IsCoded && EmptyRaid == Settings.LobbyOptions.EmptyRaidLimit && Settings.LobbyOptions.LobbyMethod == LobbyMethodOptions.OpenLobby)
                    Log($"We had {Settings.LobbyOptions.EmptyRaidLimit} empty raids.. Opening this raid to all!");
                await Click(DDOWN, 1_000, token).ConfigureAwait(false);
            }
            else
            {
                await Click(A, 3_000, token).ConfigureAwait(false);
            }

            await Click(A, 8_000, token).ConfigureAwait(false);
            return 1;
        }

        private async Task RollBackHour(CancellationToken token)
        {
            for (int i = 0; i < 2; i++)
                await Click(B, 0_150, token).ConfigureAwait(false);
            Log("Navigating to time settings.");
            for (int i = 0; i < 2; i++)
                await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(A, 1_250, token).ConfigureAwait(false); // Enter settings

            await PressAndHold(DDOWN, 2_000, 0_250, token).ConfigureAwait(false); // Scroll to system settings
            await Click(A, 1_250, token).ConfigureAwait(false);

            if (Settings.MiscSettings.UseOvershoot)
            {
                await PressAndHold(DDOWN, Settings.MiscSettings.HoldTimeForRollover, 1_000, token).ConfigureAwait(false);
                await Click(DUP, 0_500, token).ConfigureAwait(false);
            }
            else if (!Settings.MiscSettings.UseOvershoot)
            {
                for (int i = 0; i < 39; i++)
                    await Click(DDOWN, 0_100, token).ConfigureAwait(false);
            }

            await Click(A, 1_250, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);

            for (int i = 0; i < 3; i++) // Navigate to the hour setting
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);
            Log("Rolling Time Back 1 Hour.");
            for (int i = 0; i < 1; i++) // Roll back the hour by 1
                await Click(DDOWN, 0_200, token).ConfigureAwait(false);

            for (int i = 0; i < 8; i++) // Mash DRIGHT to confirm
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);

            await Click(A, 0_200, token).ConfigureAwait(false); // Confirm date/time change
            await Click(HOME, 1_000, token).ConfigureAwait(false); // Back to title screen
        }

        private async Task RollBackTime(CancellationToken token)
        {
            for (int i = 0; i < 2; i++)
                await Click(B, 0_150, token).ConfigureAwait(false);

            for (int i = 0; i < 2; i++)
                await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(A, 1_250, token).ConfigureAwait(false); // Enter settings

            await PressAndHold(DDOWN, 2_000, 0_250, token).ConfigureAwait(false); // Scroll to system settings
            await Click(A, 1_250, token).ConfigureAwait(false);

            if (Settings.MiscSettings.UseOvershoot)
            {
                await PressAndHold(DDOWN, Settings.MiscSettings.HoldTimeForRollover, 1_000, token).ConfigureAwait(false);
                await Click(DUP, 0_500, token).ConfigureAwait(false);
            }
            else if (!Settings.MiscSettings.UseOvershoot)
            {
                for (int i = 0; i < 39; i++)
                    await Click(DDOWN, 0_100, token).ConfigureAwait(false);
            }

            await Click(A, 1_250, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);

            for (int i = 0; i < 3; i++) // Navigate to the hour setting
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);

            for (int i = 0; i < 5; i++) // Roll back the hour by 5
                await Click(DDOWN, 0_200, token).ConfigureAwait(false);

            for (int i = 0; i < 8; i++) // Mash DRIGHT to confirm
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);

            await Click(A, 0_200, token).ConfigureAwait(false); // Confirm date/time change
            await Click(HOME, 1_000, token).ConfigureAwait(false); // Back to title screen
        }

        private async Task<bool> GetLobbyReady(bool recovery, CancellationToken token)
        {
            var x = 0;
            Log("Connecting to lobby...");
            while (!await IsConnectedToLobby(token).ConfigureAwait(false))
            {
                await Click(A, 1_000, token).ConfigureAwait(false);
                x++;
                if (x == 15 && recovery)
                {
                    Log("No den here! Rolling again.");
                    return false;
                }
                if (x == 45)
                {
                    Log("Failed to connect to lobby, restarting game incase we were in battle/bad connection.");
                    LobbyError++;
                    await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                    Log("Attempting to restart routine!");
                    return false;
                }
            }
            return true;
        }

        private async Task<string> GetRaidCode(CancellationToken token)
        {
            var data = await SwitchConnection.PointerPeek(6, Offsets.TeraRaidCodePointer, token).ConfigureAwait(false);
            TeraRaidCode = Encoding.ASCII.GetString(data).ToLower(); // Convert to lowercase for easier reading
            return $"{TeraRaidCode}";
        }

        private async Task<bool> CheckIfTrainerBanned(RaidMyStatus trainer, ulong nid, int player, CancellationToken token)
        {
            RaidTracker.TryAdd(nid, 0);
            var msg = string.Empty;
            var banResultCFW = RaiderBanList.List.FirstOrDefault(x => x.ID == nid);
            bool isBanned = banResultCFW != default;

            if (isBanned)
            {
                msg = $"{banResultCFW!.Name} was found in the host's ban list.\n{banResultCFW.Comment}";
                Log(msg);
                await EnqueueEmbed(null, msg, false, true, false, false, token).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        private async Task<(bool, List<(ulong, RaidMyStatus)>)> ReadTrainers(CancellationToken token)
        {
            if (!await IsConnectedToLobby(token))
                return (false, new List<(ulong, RaidMyStatus)>());

            await EnqueueEmbed(null, "", false, false, false, false, token).ConfigureAwait(false);

            List<(ulong, RaidMyStatus)> lobbyTrainers = [];
            var wait = TimeSpan.FromSeconds(Settings.RaidSettings.TimeToWait);
            var endTime = DateTime.Now + wait;
            bool full = false;

            while (!full && DateTime.Now < endTime)
            {
                if (!await IsConnectedToLobby(token))
                    return (false, lobbyTrainers);

                for (int i = 0; i < 3; i++)
                {
                    var player = i + 2;
                    Log($"Waiting for Player {player} to load...");

                    // Check connection to lobby here
                    if (!await IsConnectedToLobby(token))
                        return (false, lobbyTrainers);

                    var nidOfs = TeraNIDOffsets[i];
                    var data = await SwitchConnection.ReadBytesAbsoluteAsync(nidOfs, 8, token).ConfigureAwait(false);
                    var nid = BitConverter.ToUInt64(data, 0);
                    while (nid == 0 && DateTime.Now < endTime)
                    {
                        await Task.Delay(0_500, token).ConfigureAwait(false);

                        // Check connection to lobby again here after the delay
                        if (!await IsConnectedToLobby(token))
                            return (false, lobbyTrainers);

                        data = await SwitchConnection.ReadBytesAbsoluteAsync(nidOfs, 8, token).ConfigureAwait(false);
                        nid = BitConverter.ToUInt64(data, 0);
                    }

                    List<long> ptr = new(Offsets.Trader2MyStatusPointer);
                    ptr[2] += i * 0x30;
                    var trainer = await GetTradePartnerMyStatus(ptr, token).ConfigureAwait(false);

                    while (trainer.OT.Length == 0 && DateTime.Now < endTime)
                    {
                        await Task.Delay(0_500, token).ConfigureAwait(false);

                        // Check connection to lobby again here after the delay
                        if (!await IsConnectedToLobby(token))
                            return (false, lobbyTrainers);

                        trainer = await GetTradePartnerMyStatus(ptr, token).ConfigureAwait(false);
                    }

                    if (nid != 0 && !string.IsNullOrWhiteSpace(trainer.OT))
                    {
                        if (await CheckIfTrainerBanned(trainer, nid, player, token).ConfigureAwait(false))
                            return (false, lobbyTrainers);
                    }

                    // Check if the NID is already in the list to prevent duplicates
                    if (lobbyTrainers.Any(x => x.Item1 == nid))
                    {
                        Log($"Duplicate NID detected: {nid}. Skipping...");
                        continue; // Skip adding this NID if it's a duplicate
                    }

                    // If NID is not a duplicate and has a valid trainer OT, add to the list
                    if (nid > 0 && trainer.OT.Length > 0)
                        lobbyTrainers.Add((nid, trainer));

                    full = lobbyTrainers.Count == 3;
                    if (full || DateTime.Now >= endTime)
                        break;
                }
            }

            await Task.Delay(5_000, token).ConfigureAwait(false);

            if (lobbyTrainers.Count == 0)
            {
                EmptyRaid++;
                LostRaid++;
                Log($"Nobody joined the raid, recovering...");
                if (Settings.LobbyOptions.LobbyMethod == LobbyMethodOptions.OpenLobby)
                    Log($"Empty Raid Count #{EmptyRaid}");
                if (Settings.LobbyOptions.LobbyMethod == LobbyMethodOptions.SkipRaid)
                    Log($"Lost/Empty Lobbies: {LostRaid}/{Settings.LobbyOptions.SkipRaidLimit}");

                return (false, lobbyTrainers);
            }

            RaidCount++; // Increment RaidCount only when a raid is actually starting.
            Log($"Raid #{RaidCount} is starting!");
            if (EmptyRaid != 0)
                EmptyRaid = 0;
            return (true, lobbyTrainers);
        }

        private async Task<bool> IsConnectedToLobby(CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesMainAsync(Offsets.TeraLobbyIsConnected, 1, token).ConfigureAwait(false);
            return data[0] != 0x00; // 0 when in lobby but not connected
        }

        private async Task<bool> IsInRaid(CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesMainAsync(Offsets.LoadedIntoDesiredState, 1, token).ConfigureAwait(false);
            return data[0] == 0x02; // 2 when in raid, 1 when not
        }

        private async Task AdvanceDaySV(CancellationToken token)
        {
            var scrollroll = Settings.MiscSettings.DateTimeFormat switch
            {
                DTFormat.DDMMYY => 0,
                DTFormat.YYMMDD => 2,
                _ => 1,
            };

            for (int i = 0; i < 2; i++)
                await Click(B, 0_150, token).ConfigureAwait(false);

            for (int i = 0; i < 2; i++)
                await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(A, 1_250, token).ConfigureAwait(false); // Enter settings

            await PressAndHold(DDOWN, 2_000, 0_250, token).ConfigureAwait(false); // Scroll to system settings
            await Click(A, 1_250, token).ConfigureAwait(false);

            if (Settings.MiscSettings.UseOvershoot)
            {
                await PressAndHold(DDOWN, Settings.MiscSettings.HoldTimeForRollover, 1_000, token).ConfigureAwait(false);
                await Click(DUP, 0_500, token).ConfigureAwait(false);
            }
            else if (!Settings.MiscSettings.UseOvershoot)
            {
                for (int i = 0; i < 39; i++)
                    await Click(DDOWN, 0_100, token).ConfigureAwait(false);
            }

            await Click(A, 1_250, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
            for (int i = 0; i < scrollroll; i++) // 0 to roll day for DDMMYY, 1 to roll day for MMDDYY, 3 to roll hour
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);

            await Click(DUP, 0_200, token).ConfigureAwait(false); // Advance a day

            for (int i = 0; i < 8; i++) // Mash DRIGHT to confirm
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);

            await Click(A, 0_200, token).ConfigureAwait(false); // Confirm date/time change
            await Click(HOME, 1_000, token).ConfigureAwait(false); // Back to title screen

            await Click(A, 0_200, token).ConfigureAwait(false); // Back in Game
        }

        private async Task RolloverCorrectionSV(CancellationToken token)
        {
            var scrollroll = Settings.MiscSettings.DateTimeFormat switch
            {
                DTFormat.DDMMYY => 0,
                DTFormat.YYMMDD => 2,
                _ => 1,
            };

            for (int i = 0; i < 2; i++)
                await Click(B, 0_150, token).ConfigureAwait(false);

            for (int i = 0; i < 2; i++)
                await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(A, 1_250, token).ConfigureAwait(false); // Enter settings

            await PressAndHold(DDOWN, 2_000, 0_250, token).ConfigureAwait(false); // Scroll to system settings
            await Click(A, 1_250, token).ConfigureAwait(false);

            if (Settings.MiscSettings.UseOvershoot)
            {
                await PressAndHold(DDOWN, Settings.MiscSettings.HoldTimeForRollover, 1_000, token).ConfigureAwait(false);
                await Click(DUP, 0_500, token).ConfigureAwait(false);
            }
            else if (!Settings.MiscSettings.UseOvershoot)
            {
                for (int i = 0; i < 39; i++)
                    await Click(DDOWN, 0_100, token).ConfigureAwait(false);
            }

            await Click(A, 1_250, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
            for (int i = 0; i < scrollroll; i++) // 0 to roll day for DDMMYY, 1 to roll day for MMDDYY, 3 to roll hour
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);

            await Click(DDOWN, 0_200, token).ConfigureAwait(false);

            for (int i = 0; i < 8; i++) // Mash DRIGHT to confirm
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);

            await Click(A, 0_200, token).ConfigureAwait(false); // Confirm date/time change
            await Click(HOME, 1_000, token).ConfigureAwait(false); // Back to title screen
        }

        private async Task RegroupFromBannedUser(CancellationToken token)
        {
            Log("Attempting to remake lobby..");
            await Click(B, 2_000, token).ConfigureAwait(false);
            await Click(A, 3_000, token).ConfigureAwait(false);
            await Click(A, 3_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
        }

        private async Task InitializeSessionOffsets(CancellationToken token)
        {
            Log("Caching session offsets...");
            OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            ConnectedOffset = await SwitchConnection.PointerAll(Offsets.IsConnectedPointer, token).ConfigureAwait(false);
            RaidBlockPointerP = await SwitchConnection.PointerAll(Offsets.RaidBlockPointerP, token).ConfigureAwait(false);
            RaidBlockPointerK = await SwitchConnection.PointerAll(Offsets.RaidBlockPointerK, token).ConfigureAwait(false);
            RaidBlockPointerB = await SwitchConnection.PointerAll(Offsets.RaidBlockPointerB, token).ConfigureAwait(false);
            sbyte FieldID = await ReadEncryptedBlockByte(RaidDataBlocks.KPlayerCurrentFieldID, token).ConfigureAwait(false);
            string regionName = FieldID switch
            {
                0 => "Paldea",
                1 => "Kitakami",
                2 => "Blueberry",
                _ => "Unknown"
            };
            Log($"Player in Region: {regionName}");
            if (regionName == "Kitakami")
            {
                IsKitakami = true;
            }
            else if (regionName == "Blueberry")
            {
                IsBlueberry = true;
            }
            if (firstRun)
            {
                GameProgress = await ReadGameProgress(token).ConfigureAwait(false);
                Log($"Current Game Progress identified as {GameProgress}.");
                currentSpawnsEnabled = (bool?)await ReadBlock(RaidDataBlocks.KWildSpawnsEnabled, CancellationToken.None);
            }

            var nidPointer = new long[] { Offsets.LinkTradePartnerNIDPointer[0], Offsets.LinkTradePartnerNIDPointer[1], Offsets.LinkTradePartnerNIDPointer[2] };
            for (int p = 0; p < TeraNIDOffsets.Length; p++)
            {
                nidPointer[2] = Offsets.LinkTradePartnerNIDPointer[2] + p * 0x8;
                TeraNIDOffsets[p] = await SwitchConnection.PointerAll(nidPointer, token).ConfigureAwait(false);
            }
            Log("Caching offsets complete!");
        }

        private static async Task<bool> IsValidImageUrlAsync(string url)
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex) when (ex.InnerException is WebException webEx && webEx.Status == WebExceptionStatus.TrustFailure)
            {
            }
            catch (Exception)
            {
            }
            return false;
        }

        private readonly Dictionary<string, string> TypeAdvantages = new()
        {
            { "normal", "Fighting" },
            { "fire", "Water, Ground, Rock" },
            { "water", "Electric, Grass" },
            { "grass", "Flying, Poison, Bug, Fire, Ice" },
            { "electric", "Ground" },
            { "ice", "Fighting, Rock, Steel, Fire" },
            { "fighting", "Flying, Psychic, Fairy" },
            { "poison", "Ground, Psychic" },
            { "ground", "Water, Ice, Grass" },
            { "flying", "Rock, Electric, Ice" },
            { "psychic", "Bug, Ghost, Dark" },
            { "bug", "Flying, Rock, Fire" },
            { "rock", "Fighting, Ground, Steel, Water, Grass" },
            { "ghost", "Ghost, Dark" },
            { "dragon", "Ice, Dragon, Fairy" },
            { "dark", "Fighting, Bug, Fairy" },
            { "steel", "Fighting, Ground, Fire" },
            { "fairy", "Poison, Steel" }
        };
        private static readonly char[] separator = [','];
        private static readonly char[] separatorArray = ['-'];

        private string GetTypeAdvantage(string teraType)
        {
            // Check if the type exists in the dictionary and return the corresponding advantage
            if (TypeAdvantages.TryGetValue(teraType.ToLower(), out string advantage))
            {
                return advantage;
            }
            return "Unknown Type";  // Return "Unknown Type" if the type doesn't exist in our dictionary
        }

        private async Task<byte[]?> CaptureGifScreenshotsAsync(CancellationToken token)
        {
            var frameCount = Settings.EmbedToggles.Frames;
            var gifFrames = new List<System.Drawing.Image>();
            var gifWidth = 450;
            var gifHeight = 270;
            var gifQuality = (AnimatedGif.GifQuality)Settings.EmbedToggles.GifQuality;
            var frameDelay = 180;

            for (int i = 0; i < frameCount; i++)
            {
                byte[] bytes;
                try
                {
                    bytes = await SwitchConnection.PixelPeek(token).ConfigureAwait(false) ?? Array.Empty<byte>();
                }
                catch (Exception ex)
                {
                    Log($"Error while fetching pixels: {ex.Message}");
                    return null;
                }

                if (bytes.Length == 0)
                {
                    Log("No frame data received.");
                    return null;
                }

                using var ms = new MemoryStream(bytes);
                using var bitmap = new Bitmap(ms);
                var resizedFrame = bitmap.GetThumbnailImage(gifWidth, gifHeight, null, IntPtr.Zero);
                var frame = ((Bitmap)resizedFrame).Clone(new Rectangle(0, 0, resizedFrame.Width, resizedFrame.Height), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                gifFrames.Add(frame);
                resizedFrame.Dispose();

                await Task.Delay(50, token);
            }

            using var outputMs = new MemoryStream();
            using (var gif = new AnimatedGifCreator(outputMs, frameDelay))
            {
                foreach (var frame in gifFrames)
                {
                    gif.AddFrame(frame, quality: (AnimatedGif.GifQuality)(int)gifQuality);
                    frame.Dispose();
                }
            }

            return outputMs.ToArray();
        }

        private async Task EnqueueEmbed(List<string>? names, string message, bool hatTrick, bool disband, bool upnext, bool raidstart, CancellationToken token)
        {
            string code = string.Empty;

            // Determine if the raid is a "Free For All" based on the settings and conditions
            if (Settings.ActiveRaids[RotationCount].IsCoded && EmptyRaid < Settings.LobbyOptions.EmptyRaidLimit)
            {
                // If it's not a "Free For All", retrieve the raid code
                code = await GetRaidCode(token).ConfigureAwait(false);
            }
            else
            {
                // If it's a "Free For All", set the code as such
                code = "Free For All";
            }

            // Apply delay only if the raid was added by RA command, not a Mystery Shiny Raid, and has a code
            if (Settings.ActiveRaids[RotationCount].AddedByRACommand &&
                Settings.ActiveRaids[RotationCount].Title != "Mystery Shiny Raid" &&
                code != "Free For All")
            {
                await Task.Delay(Settings.EmbedToggles.RequestEmbedTime * 1000, token).ConfigureAwait(false);
            }

            // Description can only be up to 4096 characters.
            //var description = Settings.ActiveRaids[RotationCount].Description.Length > 0 ? string.Join("\n", Settings.ActiveRaids[RotationCount].Description) : "";
            var description = Settings.EmbedToggles.RaidEmbedDescription.Length > 0 ? string.Join("\n", Settings.EmbedToggles.RaidEmbedDescription) : "";
            if (description.Length > 4096) description = description[..4096];

            if (EmptyRaid == Settings.LobbyOptions.EmptyRaidLimit && Settings.LobbyOptions.LobbyMethod == LobbyMethodOptions.OpenLobby)
                EmptyRaid = 0;

            if (disband) // Wait for trainer to load before disband
                await Task.Delay(5_000, token).ConfigureAwait(false);

            byte[]? imageBytes = null;
            string fileName = string.Empty;

            if (!disband && names is not null && !upnext && Settings.EmbedToggles.TakeScreenshot)
            {
                try
                {
                    if (Settings.EmbedToggles.AnimatedScreenshot)
                    {
                        try
                        {
                            imageBytes = await Task.Run(() => CaptureGifScreenshotsAsync(token)).ConfigureAwait(false);
                            fileName = $"raidecho{RotationCount}.gif";
                        }
                        catch (Exception ex)
                        {
                            Log($"Error while capturing GIF screenshots: {ex.Message}");
                            Log("Falling back to standard JPG screenshot.");

                            imageBytes = await SwitchConnection.PixelPeek(token).ConfigureAwait(false) ?? Array.Empty<byte>();
                            fileName = $"raidecho{RotationCount}.jpg";
                        }
                    }
                    else
                    {
                        imageBytes = await SwitchConnection.PixelPeek(token).ConfigureAwait(false) ?? Array.Empty<byte>();
                        fileName = $"raidecho{RotationCount}.jpg";
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error while capturing screenshots: {ex.Message}");
                }
            }
            else if (Settings.EmbedToggles.TakeScreenshot && !upnext)
            {
                try
                {
                    imageBytes = await SwitchConnection.PixelPeek(token).ConfigureAwait(false) ?? Array.Empty<byte>();
                    fileName = $"raidecho{RotationCount}.jpg";
                }
                catch (Exception ex)
                {
                    Log($"Error while fetching pixels: {ex.Message}");
                }
            }

            string disclaimer = Settings.ActiveRaids.Count > 1
                                ? $"Pokemon S/V Raid Bot"
                                : "";

            var turl = string.Empty;
            var form = string.Empty;

            Log($"Rotation Count: {RotationCount} | Species is {Settings.ActiveRaids[RotationCount].Species}");
            if (!disband && !upnext && !raidstart)
                Log($"Raid Code is: {code}");
            PK9 pk = new()
            {
                Species = (ushort)Settings.ActiveRaids[RotationCount].Species,
                Form = (byte)Settings.ActiveRaids[RotationCount].SpeciesForm
            };
            if (pk.Form != 0)
                form = $"-{pk.Form}";
            if (Settings.ActiveRaids[RotationCount].IsShiny == true)
                pk.SetIsShiny(true);
            else
                pk.SetIsShiny(false);

            if (Settings.ActiveRaids[RotationCount].SpriteAlternateArt && Settings.ActiveRaids[RotationCount].IsShiny)
            {
                var altUrl = AltPokeImg(pk);

                try
                {
                    // Check if AltPokeImg URL is valid
                    if (await IsValidImageUrlAsync(altUrl))
                    {
                        turl = altUrl;
                    }
                    else
                    {
                        Settings.ActiveRaids[RotationCount].SpriteAlternateArt = false;  // Set SpriteAlternateArt to false if no img found
                        turl = RaidExtensions<PK9>.PokeImg(pk, false, false);
                        Log($"AltPokeImg URL was not valid. Setting SpriteAlternateArt to false.");
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception and use the default sprite
                    Log($"Error while validating alternate image URL: {ex.Message}");
                    Settings.ActiveRaids[RotationCount].SpriteAlternateArt = false;  // Set SpriteAlternateArt to false due to error
                    turl = RaidExtensions<PK9>.PokeImg(pk, false, false);
                }
            }
            else
            {
                turl = RaidExtensions<PK9>.PokeImg(pk, false, false);
            }

            if (Settings.ActiveRaids[RotationCount].Species is 0)
                turl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/combat.png";

            // Fetch the dominant color from the image only AFTER turl is assigned
            (int R, int G, int B) dominantColor = RaidExtensions<PK9>.GetDominantColor(turl);

            // Use the dominant color, unless it's a disband or hatTrick situation
            var embedColor = disband ? Discord.Color.Red : hatTrick ? Discord.Color.Purple : new Discord.Color(dominantColor.R, dominantColor.G, dominantColor.B);

            TimeSpan duration = new(0, 2, 31);

            // Calculate the future time by adding the duration to the current time
            DateTimeOffset futureTime = DateTimeOffset.Now.Add(duration);

            // Convert the future time to Unix timestamp
            long futureUnixTime = futureTime.ToUnixTimeSeconds();

            // Create the future time message using Discord's timestamp formatting
            string futureTimeMessage = $"**Raid Posting: <t:{futureUnixTime}:R>**";

            // 初始化 EmbedBuilder 對象
            var embed = new EmbedBuilder()
            {
                Title = disband ? $"**Raid 取消: [{TeraRaidCode}]**" : upnext && Settings.RaidSettings.TotalRaidsToHost != 0 ? $"Raid結束 - 準備下一次Raid！" : upnext && Settings.RaidSettings.TotalRaidsToHost == 0 ? $"Raid結束 - 準備下一次Raid！" : "",
                Color = embedColor,
                Description = disband ? message : upnext ? Settings.RaidSettings.TotalRaidsToHost == 0 ? $"# {Settings.ActiveRaids[RotationCount].Title}\n\n{futureTimeMessage}" : $"# {Settings.ActiveRaids[RotationCount].Title}\n\n{futureTimeMessage}" : raidstart ? "" : description,
                ThumbnailUrl = upnext ? turl : (imageBytes == null ? turl : null), // Set ThumbnailUrl based on upnext and imageBytes
                ImageUrl = imageBytes != null ? $"attachment://{fileName}" : null, // Set ImageUrl based on imageBytes
            };

            // Only include footer if not posting 'upnext' embed with the 'Preparing Raid' title
            if (!(upnext && Settings.RaidSettings.TotalRaidsToHost == 0))
            {
                string programIconUrl = $"https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/icon4.png";
                int raidsInRotationCount = Hub.Config.RotatingRaidSV.ActiveRaids.Count(r => !r.AddedByRACommand);
                // Calculate uptime
                TimeSpan uptime = DateTime.Now - StartTime;

                // Check for singular or plural days/hours
                string dayLabel = uptime.Days == 1 ? "day" : "days";
                string hourLabel = uptime.Hours == 1 ? "hour" : "hours";
                string minuteLabel = uptime.Minutes == 1 ? "minute" : "minutes";

                // Format the uptime string, omitting the part if the value is 0
                string uptimeFormatted = "";
                if (uptime.Days > 0)
                {
                    uptimeFormatted += $"{uptime.Days} {dayLabel} ";
                }
                if (uptime.Hours > 0 || uptime.Days > 0) // Show hours if there are any hours, or if there are days even if hours are 0
                {
                    uptimeFormatted += $"{uptime.Hours} {hourLabel} ";
                }
                if (uptime.Minutes > 0 || uptime.Hours > 0 || uptime.Days > 0) // Show minutes if there are any minutes, or if there are hours/days even if minutes are 0
                {
                    uptimeFormatted += $"{uptime.Minutes} {minuteLabel}";
                }

                // Trim any excess whitespace from the string
                uptimeFormatted = uptimeFormatted.Trim();
                embed.WithFooter(new EmbedFooterBuilder()
                {
                    Text = $"完成 Raids: {RaidCount} (W: {WinCount} | L: {LossCount})\nActiveRaids: {raidsInRotationCount} | Uptime: {uptimeFormatted}\n" + disclaimer,
                    IconUrl = programIconUrl
                });
            }

            // Prepare the tera icon URL
            string teraType = RaidEmbedInfoHelpers.RaidSpeciesTeraType.ToLower();
            string folderName = Settings.EmbedToggles.SelectedTeraIconType == TeraIconType.Icon1 ? "icon1" : "icon2"; // Add more conditions for more icon types
            string teraIconUrl = $"https://raw.githubusercontent.com/bdawg1989/sprites/main/teraicons/{folderName}/{teraType}.png";

            // Only include author (header) if not posting 'upnext' embed with the 'Preparing Raid' title
            if (!(upnext && Settings.RaidSettings.TotalRaidsToHost == 0))
            {
                // Set the author (header) of the embed with the tera icon
                embed.WithAuthor(new EmbedAuthorBuilder()
                {
                    Name = RaidEmbedInfoHelpers.RaidEmbedTitle,
                    IconUrl = teraIconUrl
                });
            }
            if (!disband && !upnext && !raidstart)
            {
                StringBuilder statsField = new();
                statsField.AppendLine($"**等級**: {RaidEmbedInfoHelpers.RaidLevel}");
                statsField.AppendLine($"**性別**: {RaidEmbedInfoHelpers.RaidSpeciesGender}");
                statsField.AppendLine($"**性格**: {RaidEmbedInfoHelpers.RaidSpeciesNature}");
                statsField.AppendLine($"**能力**: {RaidEmbedInfoHelpers.RaidSpeciesAbility}");
                statsField.AppendLine($"**个体**: {RaidEmbedInfoHelpers.RaidSpeciesIVs}");
                statsField.AppendLine($"**尺寸**: {RaidEmbedInfoHelpers.ScaleText}({RaidEmbedInfoHelpers.ScaleNumber})");

                if (Settings.EmbedToggles.IncludeSeed)
                {
                    var storyProgressValue = Settings.ActiveRaids[RotationCount].StoryProgress switch
                    {
                        GameProgressEnum.Unlocked6Stars => 6,
                        GameProgressEnum.Unlocked5Stars => 5,
                        GameProgressEnum.Unlocked4Stars => 4,
                        GameProgressEnum.Unlocked3Stars => 3,
                        _ => 6,
                    };
                    statsField.AppendLine($"**Seed种子**: `{Settings.ActiveRaids[RotationCount].Seed} {Settings.ActiveRaids[RotationCount].DifficultyLevel} {storyProgressValue}`");
                }

                embed.AddField("**__數據__**", statsField.ToString(), true);
                embed.AddField("\u200b", "\u200b", true);
            }

            if (!disband && !upnext && !raidstart && Settings.EmbedToggles.IncludeMoves)
            {
                embed.AddField("**__招式__**", string.IsNullOrEmpty($"{RaidEmbedInfoHelpers.ExtraMoves}") ? string.IsNullOrEmpty($"{RaidEmbedInfoHelpers.Moves}") ? "沒有可顯示的招式" : $"{RaidEmbedInfoHelpers.Moves}" : $"{RaidEmbedInfoHelpers.Moves}\n**額外招式:**\n{RaidEmbedInfoHelpers.ExtraMoves}", true);
                RaidEmbedInfoHelpers.ExtraMoves = string.Empty;
            }

            if (!disband && !upnext && !raidstart && !Settings.EmbedToggles.IncludeMoves)
            {
                embed.AddField(" **__特別獎勵__**", string.IsNullOrEmpty($"{RaidEmbedInfoHelpers.SpecialRewards}") ? "沒有獎勵可顯示" : $"{RaidEmbedInfoHelpers.SpecialRewards}", true);
                RaidEmbedInfoHelpers.SpecialRewards = string.Empty;
            }
            // Fetch the type advantage using the static RaidSpeciesTeraType from RaidEmbedInfo
            string typeAdvantage = GetTypeAdvantage(RaidEmbedInfoHelpers.RaidSpeciesTeraType);

            // Only include the Type Advantage if not posting 'upnext' embed with the 'Preparing Raid' title and if the raid isn't starting or disbanding
            if (!disband && !upnext && !raidstart && Settings.EmbedToggles.IncludeTypeAdvantage)
            {
                embed.AddField(" **__類型優勢__**", typeAdvantage, true);
                embed.AddField("\u200b", "\u200b", true);
            }

            if (!disband && !upnext && !raidstart && Settings.EmbedToggles.IncludeMoves)
            {
                embed.AddField(" **__特別獎勵__**", string.IsNullOrEmpty($"{RaidEmbedInfoHelpers.SpecialRewards}") ? "沒有獎勵可顯示" : $"{RaidEmbedInfoHelpers.SpecialRewards}", true);
                RaidEmbedInfoHelpers.SpecialRewards = string.Empty;
            }
            if (!disband && names is null && !upnext)
            {
                embed.AddField(Settings.EmbedToggles.IncludeCountdown ? $"**__Raid開始__**:\n**<t:{DateTimeOffset.Now.ToUnixTimeSeconds() + Settings.RaidSettings.TimeToWait}:R>**" : $"**大廳等候!**", $"Raid密语: **{code}**", true);
            }
            if (!disband && names is not null && !upnext)
            {
                var players = string.Empty;
                if (names.Count == 0)
                    players = "我們的派對向我們傾斜 :/";
                else
                {
                    int i = 2;
                    names.ForEach(x =>
                    {
                        players += $"玩家 {i} - **{x}**\n";
                        i++;
                    });
                }

                embed.AddField($"**Raid #{RaidCount} 正在开始!**", players);
            }
            if (imageBytes != null)
            {
                embed.ThumbnailUrl = turl;
                embed.WithImageUrl($"attachment://{fileName}");
            }

            EchoUtil.RaidEmbed(imageBytes, fileName, embed);
        }

        private async Task<bool> ConnectToOnline(PokeRaidHubConfig config, CancellationToken token)
        {
            int attemptCount = 0;
            const int maxAttempt = 5;
            const int waitTime = 10; // time in minutes to wait after max attempts

            while (true) // Loop until a successful connection is made or the task is canceled
            {
                if (token.IsCancellationRequested)
                {
                    Log("連線嘗試已取消。");
                    break;
                }
                try
                {
                    if (await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
                    {
                        Log("Connection established successfully.");
                        break; // Exit the loop if connected successfully
                    }

                    if (attemptCount >= maxAttempt)
                    {
                        Log($"Failed to connect after {maxAttempt} attempts. Assuming a softban. Initiating wait for {waitTime} minutes before retrying.");
                        // Log details about sending an embed message
                        Log("Sending an embed message to notify about technical difficulties.");
                        EmbedBuilder embed = new()
                        {
                            Title = "遇到技術困難",
                            Description = "機器人在線上連線時遇到問題。請耐心等待，我們會盡力解決問題。",
                            Color = Discord.Color.Red,
                            ThumbnailUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/x.png"
                        };
                        EchoUtil.RaidEmbed(null, "", embed);
                        // Waiting process
                        await Click(B, 0_500, token).ConfigureAwait(false);
                        await Click(B, 0_500, token).ConfigureAwait(false);
                        Log($"Waiting for {waitTime} minutes before attempting to reconnect.");
                        await Task.Delay(TimeSpan.FromMinutes(waitTime), token).ConfigureAwait(false);
                        Log("Attempting to reopen the game.");
                        await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                        attemptCount = 0; // Reset attempt count
                    }

                    attemptCount++;
                    Log($"Attempt {attemptCount} of {maxAttempt}: Trying to connect online...");

                    // Connection attempt logic
                    await Click(X, 3_000, token).ConfigureAwait(false);
                    await Click(L, 5_000 + config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);

                    // Wait a bit before rechecking the connection status
                    await Task.Delay(5000, token).ConfigureAwait(false); // Wait 5 seconds before rechecking

                    if (attemptCount < maxAttempt)
                    {
                        Log("Rechecking the online connection status...");
                        // Wait and recheck logic
                        await Click(B, 0_500, token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Exception occurred during connection attempt: {ex.Message}");
                    // Handle exceptions, like connectivity issues here

                    if (attemptCount >= maxAttempt)
                    {
                        Log($"Failed to connect after {maxAttempt} attempts due to exception. Waiting for {waitTime} minutes before retrying.");
                        await Task.Delay(TimeSpan.FromMinutes(waitTime), token).ConfigureAwait(false);
                        Log("Attempting to reopen the game.");
                        await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                        attemptCount = 0;
                    }
                }
            }

            // Final steps after connection is established
            await Task.Delay(3_000 + config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Task.Delay(3_000, token).ConfigureAwait(false);

            return true;
        }

        public async Task StartGameRaid(PokeRaidHubConfig config, CancellationToken token)
        {
            // First, check if the time rollback feature is enabled
            if (Settings.RaidSettings.EnableTimeRollBack && DateTime.Now - TimeForRollBackCheck >= TimeSpan.FromHours(5))
            {
                Log("Rolling Time back 5 hours.");
                // Call the RollBackTime function
                await RollBackTime(token).ConfigureAwait(false);
                await Click(A, 1_500, token).ConfigureAwait(false);
                // Reset TimeForRollBackCheck
                TimeForRollBackCheck = DateTime.Now;
            }

            var timing = config.Timings;
            var loadPro = timing.RestartGameSettings.ProfileSelectSettings.ProfileSelectionRequired ? timing.RestartGameSettings.ProfileSelectSettings.ExtraTimeLoadProfile : 0;

            await Click(A, 1_000 + loadPro, token).ConfigureAwait(false); // Initial "A" Press to start the Game + a delay if needed for profiles to load

            // Really Shouldn't keep this but we will for now
            if (timing.RestartGameSettings.AvoidSystemUpdate)
            {
                await Task.Delay(0_500, token).ConfigureAwait(false); // Delay bc why not
                await Click(DUP, 0_600, token).ConfigureAwait(false); // Highlight "Start Software"
                await Click(A, 1_000 + loadPro, token).ConfigureAwait(false); // Select "Sttart Software" + delay if Profile selection is needed
            }

            // Only send extra Presses if we need to
            if (timing.RestartGameSettings.ProfileSelectSettings.ProfileSelectionRequired)
            {
                await Click(A, 1_000, token).ConfigureAwait(false); // Now we are on the Profile Screen
                await Click(A, 1_000, token).ConfigureAwait(false); // Select the profile
            }

            // Digital game copies take longer to load
            if (timing.RestartGameSettings.CheckGameDelay)
            {
                await Task.Delay(2_000 + timing.RestartGameSettings.ExtraTimeCheckGame, token).ConfigureAwait(false);
            }

            // If they have DLC on the system and can't use it, requires an UP + A to start the game.
            if (timing.RestartGameSettings.CheckForDLC)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 0_600, token).ConfigureAwait(false);
            }

            Log("Restarting the game!");

            await Task.Delay(19_000 + timing.RestartGameSettings.ExtraTimeLoadGame, token).ConfigureAwait(false); // Wait for the game to load before writing to memory
            await InitializeRaidBlockPointers(token);

            if (Settings.ActiveRaids.Count > 1)
            {
                Log($"Rotation for {Settings.ActiveRaids[RotationCount].Species} has been found.");
                Log($"Checking Current Game Progress Level.");

                var desiredProgress = Settings.ActiveRaids[RotationCount].StoryProgress;
                if (GameProgress != (GameProgress)desiredProgress)
                {
                    Log($"Updating game progress level to: {desiredProgress}");
                    await WriteProgressLive((GameProgress)desiredProgress).ConfigureAwait(false);
                    GameProgress = (GameProgress)desiredProgress;
                    Log($"Done.");
                }
                else
                {
                    Log($"Game progress level is already {GameProgress}. No update needed.");
                }

                RaidDataBlocks.AdjustKWildSpawnsEnabledType(Settings.RaidSettings.DisableOverworldSpawns);

                if (Settings.RaidSettings.DisableOverworldSpawns)
                {
                    Log("Checking current state of Overworld Spawns.");
                    if (currentSpawnsEnabled.HasValue)
                    {
                        Log($"Current Overworld Spawns state: {currentSpawnsEnabled.Value}");

                        if (currentSpawnsEnabled.Value)
                        {
                            Log("Overworld Spawns are enabled, attempting to disable.");
                            await WriteBlock(false, RaidDataBlocks.KWildSpawnsEnabled, token, currentSpawnsEnabled);
                            currentSpawnsEnabled = false;
                            Log("Overworld Spawns successfully disabled.");
                        }
                        else
                        {
                            Log("Overworld Spawns are already disabled, no action taken.");
                        }
                    }
                }
                else // When Settings.DisableOverworldSpawns is false, ensure Overworld spawns are enabled
                {
                    Log("Settings indicate Overworld Spawns should be enabled. Checking current state.");
                    Log($"Current Overworld Spawns state: {currentSpawnsEnabled.Value}");

                    if (!currentSpawnsEnabled.Value)
                    {
                        Log("Overworld Spawns are disabled, attempting to enable.");
                        await WriteBlock(true, RaidDataBlocks.KWildSpawnsEnabled, token, currentSpawnsEnabled);
                        currentSpawnsEnabled = true;
                        Log("Overworld Spawns successfully enabled.");
                    }
                    else
                    {
                        Log("Overworld Spawns are already enabled, no action needed.");
                    }
                }
                Log($"Attempting to override seed for {Settings.ActiveRaids[RotationCount].Species}.");
                await OverrideSeedIndex(SeedIndexToReplace, token).ConfigureAwait(false);
                Log("Seed override completed.");

                await Task.Delay(2_000, token).ConfigureAwait(false);
                await LogPlayerLocation(token); // Teleports user to closest Active Den
                await Task.Delay(2_000, token).ConfigureAwait(false);
            }

            for (int i = 0; i < 8; i++)
                await Click(A, 1_000, token).ConfigureAwait(false);

            var timeout = TimeSpan.FromMinutes(1);
            var delayTask = Task.Delay(timeout, token);

            while (true)
            {
                var isOnOverworldTitleTask = IsOnOverworldTitle(token);

                // Wait for either the delay task or the isOnOverworldTitle task to complete
                var completedTask = await Task.WhenAny(isOnOverworldTitleTask, delayTask).ConfigureAwait(false);

                if (completedTask == isOnOverworldTitleTask)
                {
                    // If the task that completed is the isOnOverworldTitleTask, check its result
                    if (await isOnOverworldTitleTask.ConfigureAwait(false))
                    {
                        // If we are on the overworld title, exit the loop
                        break;
                    }
                }
                else
                {
                    // If the delayTask completed first, initiate the reboot protocol
                    Log("Still not in the game, initiating reboot protocol!");
                    await PerformRebootAndReset(token);
                    return;
                }

                // Add a small delay before the next check to avoid tight looping
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }

            await Task.Delay(5_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
            Log("Back in the overworld!");

            LostRaid = 0;

            if (Settings.RaidSettings.MysteryRaids)
            {
                // Count the number of existing Mystery Shiny Raids
                int mysteryRaidCount = Settings.ActiveRaids.Count(raid => raid.Title.Contains("Mystery Shiny Raid"));

                // Only create and add a new Mystery Shiny Raid if there are two or fewer in the list
                if (mysteryRaidCount <= 1)
                {
                    CreateAndAddRandomShinyRaidAsRequested();
                }
            }
        }

        private static Dictionary<string, float[]> LoadDenLocations(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<Dictionary<string, float[]>>(json);
        }

        private static string FindNearestLocation((float, float, float) playerLocation, Dictionary<string, float[]> denLocations)
        {
            string? nearestDen = null;
            float minDistance = float.MaxValue;

            foreach (var den in denLocations)
            {
                var denLocation = den.Value;
                float distance = CalculateDistance(playerLocation, (denLocation[0], denLocation[1], denLocation[2]));

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestDen = den.Key;
                }
            }

            return nearestDen;
        }

        private static float CalculateDistance((float, float, float) loc1, (float, float, float) loc2)
        {
            return (float)Math.Sqrt(
                Math.Pow(loc1.Item1 - loc2.Item1, 2) +
                Math.Pow(loc1.Item2 - loc2.Item2, 2) +
                Math.Pow(loc1.Item3 - loc2.Item3, 2));
        }

        private async Task<(float, float, float)> GetPlayersLocation(CancellationToken token)
        {
            // Read the data block (automatically handles encryption)
            var data = await ReadBlock(RaidDataBlocks.KCoordinates, token) as byte[];

            // Extract coordinates
            float x = BitConverter.ToSingle(data, 0);
            float y = BitConverter.ToSingle(data, 4);
            float z = BitConverter.ToSingle(data, 8);

            return (x, y, z);
        }

        public async Task TeleportToDen(float x, float y, float z, CancellationToken token)
        {
            const float offset = 1.8f;
            x += offset;

            // Convert coordinates to byte array
            byte[] xBytes = BitConverter.GetBytes(x);
            byte[] yBytes = BitConverter.GetBytes(y);
            byte[] zBytes = BitConverter.GetBytes(z);
            byte[] coordinatesData = new byte[xBytes.Length + yBytes.Length + zBytes.Length];
            Array.Copy(xBytes, 0, coordinatesData, 0, xBytes.Length);
            Array.Copy(yBytes, 0, coordinatesData, xBytes.Length, yBytes.Length);
            Array.Copy(zBytes, 0, coordinatesData, xBytes.Length + yBytes.Length, zBytes.Length);

            // Write the coordinates
            var teleportBlock = RaidDataBlocks.KCoordinates;
            teleportBlock.Size = coordinatesData.Length;
            var currentCoordinateData = await ReadBlock(teleportBlock, token) as byte[];
            _ = await WriteEncryptedBlockSafe(teleportBlock, currentCoordinateData, coordinatesData, token);

            // Set rotation to face North
            float northRX = 0.0f;
            float northRY = -0.63828725f;
            float northRZ = 0.0f;
            float northRW = 0.7697983f;

            // Convert rotation to byte array
            byte[] rotationData = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(northRX), 0, rotationData, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(northRY), 0, rotationData, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(northRZ), 0, rotationData, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(northRW), 0, rotationData, 12, 4);

            // Write the rotation
            var rotationBlock = RaidDataBlocks.KPlayerRotation;
            rotationBlock.Size = rotationData.Length;
            var currentRotationData = await ReadBlock(rotationBlock, token) as byte[];
            _ = await WriteEncryptedBlockSafe(rotationBlock, currentRotationData, rotationData, token);
        }

        private async Task<List<(uint Area, uint LotteryGroup, uint Den, uint Seed, uint Flags, bool IsEvent)>> ExtractRaidInfo(TeraRaidMapParent mapType, CancellationToken token)
        {
            byte[] raidData = mapType switch
            {
                TeraRaidMapParent.Paldea => await ReadPaldeaRaids(token),
                TeraRaidMapParent.Kitakami => await ReadKitakamiRaids(token),
                TeraRaidMapParent.Blueberry => await ReadBlueberryRaids(token),
                _ => throw new InvalidOperationException("Invalid region"),
            };
            var raids = new List<(uint Area, uint LotteryGroup, uint Den, uint Seed, uint Flags, bool IsEvent)>();
            for (int i = 0; i < raidData.Length; i += Raid.SIZE)
            {
                var raid = new Raid(raidData.AsSpan()[i..(i + Raid.SIZE)]);
                if (raid.IsValid)
                {
                    raids.Add((raid.Area, raid.LotteryGroup, raid.Den, raid.Seed, raid.Flags, raid.IsEvent));
                }
            }

            return raids;
        }

        private async Task LogPlayerLocation(CancellationToken token)
        {
            var playerLocation = await GetPlayersLocation(token);

            // Load den locations for all regions
            var blueberryLocations = LoadDenLocations("SysBot.Pokemon.SV.BotRaid.DenLocations.den_locations_blueberry.json");
            var kitakamiLocations = LoadDenLocations("SysBot.Pokemon.SV.BotRaid.DenLocations.den_locations_kitakami.json");
            var baseLocations = LoadDenLocations("SysBot.Pokemon.SV.BotRaid.DenLocations.den_locations_base.json");

            // Find the nearest location for each set and keep track of the overall nearest
            var nearestDen = new Dictionary<string, string>
    {
        { "Blueberry", FindNearestLocation(playerLocation, blueberryLocations) },
        { "Kitakami", FindNearestLocation(playerLocation, kitakamiLocations) },
        { "Paldea", FindNearestLocation(playerLocation, baseLocations) }
    };

            var overallNearest = nearestDen.Select(kv =>
            {
                var denLocationArray = kv.Key switch
                {
                    "Blueberry" => blueberryLocations[kv.Value],
                    "Kitakami" => kitakamiLocations[kv.Value],
                    "Paldea" => baseLocations[kv.Value],
                    _ => throw new InvalidOperationException("Invalid region")
                };

                var denLocationTuple = (denLocationArray[0], denLocationArray[1], denLocationArray[2]);
                return new { Region = kv.Key, DenIdentifier = kv.Value, Distance = CalculateDistance(playerLocation, denLocationTuple) };
            })
            .OrderBy(d => d.Distance)
            .First();

            TeraRaidMapParent mapType = overallNearest.Region switch
            {
                "Blueberry" => TeraRaidMapParent.Blueberry,
                "Kitakami" => TeraRaidMapParent.Kitakami,
                "Paldea" => TeraRaidMapParent.Paldea,
                _ => throw new InvalidOperationException("Invalid region")
            };

            var activeRaids = await GetActiveRaidLocations(mapType, token);

            // Find the nearest active raid, if any
            var nearestActiveRaid = activeRaids
                .Select(raid => new { Raid = raid, Distance = CalculateDistance(playerLocation, (raid.Coordinates[0], raid.Coordinates[1], raid.Coordinates[2])) })
                .OrderBy(raid => raid.Distance)
                .FirstOrDefault();

            if (nearestActiveRaid != null)
            {
                // Check if the player is already at the nearest active den
                float distanceToNearestActiveDen = CalculateDistance(playerLocation, (nearestActiveRaid.Raid.Coordinates[0], nearestActiveRaid.Raid.Coordinates[1], nearestActiveRaid.Raid.Coordinates[2]));

                // Define a threshold for how close the player needs to be to be considered "at" the den
                const float threshold = 2.0f;

                uint denSeed = nearestActiveRaid.Raid.Seed;
                string hexDenSeed = denSeed.ToString("X8");
                denHexSeed = hexDenSeed;
                Log($"Seed: {hexDenSeed} Nearest active den: {nearestActiveRaid.Raid.DenIdentifier}");

                bool onOverworld = await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false);
                if (!onOverworld)
                {
                    if (distanceToNearestActiveDen > threshold)
                    {
                        uint seedOfNearestDen = nearestActiveRaid.Raid.Seed;

                        // Player is not at the den, so teleport
                        await TeleportToDen(nearestActiveRaid.Raid.Coordinates[0], nearestActiveRaid.Raid.Coordinates[1], nearestActiveRaid.Raid.Coordinates[2], token);
                        Log($"Teleported to nearest active den: {nearestActiveRaid.Raid.DenIdentifier} Seed: {nearestActiveRaid.Raid.Seed:X8} in {overallNearest.Region}.");
                    }
                }
                else
                {
                    // Player is already at the den
                    //  Log($"Already at the nearest active den: {nearestActiveRaid.Raid.DenIdentifier}");
                }
            }
            else
            {
                Log($"No active dens found in {overallNearest.Region}");
            }
            bool IsKitakami = overallNearest.Region == "Kitakami";
            bool IsBlueberry = overallNearest.Region == "Blueberry";
        }

        private static bool IsRaidActive((uint Area, uint LotteryGroup, uint Den) raid)
        {
            return true;
        }

        private async Task<List<(string DenIdentifier, float[] Coordinates, int Index, uint Seed, uint Flags, bool IsEvent)>> GetActiveRaidLocations(TeraRaidMapParent mapType, CancellationToken token)
        {
            var raidInfo = await ExtractRaidInfo(mapType, token);
            Dictionary<string, float[]> denLocations = mapType switch
            {
                TeraRaidMapParent.Paldea => LoadDenLocations("SysBot.Pokemon.SV.BotRaid.DenLocations.den_locations_base.json"),
                TeraRaidMapParent.Kitakami => LoadDenLocations("SysBot.Pokemon.SV.BotRaid.DenLocations.den_locations_kitakami.json"),
                TeraRaidMapParent.Blueberry => LoadDenLocations("SysBot.Pokemon.SV.BotRaid.DenLocations.den_locations_blueberry.json"),
                _ => throw new InvalidOperationException("Invalid region")
            };

            var activeRaids = new List<(string DenIdentifier, float[] Coordinates, int Index, uint Seed, uint Flags, bool IsEvent)>();
            int index = 0;
            foreach (var (Area, LotteryGroup, Den, Seed, Flags, IsEvent) in raidInfo)
            {
                string raidIdentifier = $"{Area}-{LotteryGroup}-{Den}";
                if (denLocations.TryGetValue(raidIdentifier, out var coordinates) && IsRaidActive((Area, LotteryGroup, Den)))
                {
                    activeRaids.Add((raidIdentifier, coordinates, index, Seed, Flags, IsEvent));
                }
                index++;
            }

            return activeRaids;
        }

        private async Task WriteProgressLive(GameProgress progress)
        {
            if (Connection is null)
                return;

            if (progress >= GameProgress.Unlocked3Stars)
            {
                var toexpect = (bool?)await ReadBlock(RaidDataBlocks.KUnlockedRaidDifficulty3, CancellationToken.None);
                await WriteBlock(true, RaidDataBlocks.KUnlockedRaidDifficulty3, CancellationToken.None, toexpect);
            }
            else
            {
                var toexpect = (bool?)await ReadBlock(RaidDataBlocks.KUnlockedRaidDifficulty3, CancellationToken.None);
                await WriteBlock(false, RaidDataBlocks.KUnlockedRaidDifficulty3, CancellationToken.None, toexpect);
            }

            if (progress >= GameProgress.Unlocked4Stars)
            {
                var toexpect = (bool?)await ReadBlock(RaidDataBlocks.KUnlockedRaidDifficulty4, CancellationToken.None);
                await WriteBlock(true, RaidDataBlocks.KUnlockedRaidDifficulty4, CancellationToken.None, toexpect);
            }
            else
            {
                var toexpect = (bool?)await ReadBlock(RaidDataBlocks.KUnlockedRaidDifficulty4, CancellationToken.None);
                await WriteBlock(false, RaidDataBlocks.KUnlockedRaidDifficulty4, CancellationToken.None, toexpect);
            }

            if (progress >= GameProgress.Unlocked5Stars)
            {
                var toexpect = (bool?)await ReadBlock(RaidDataBlocks.KUnlockedRaidDifficulty5, CancellationToken.None);
                await WriteBlock(true, RaidDataBlocks.KUnlockedRaidDifficulty5, CancellationToken.None, toexpect);
            }
            else
            {
                var toexpect = (bool?)await ReadBlock(RaidDataBlocks.KUnlockedRaidDifficulty5, CancellationToken.None);
                await WriteBlock(false, RaidDataBlocks.KUnlockedRaidDifficulty5, CancellationToken.None, toexpect);
            }

            if (progress >= GameProgress.Unlocked6Stars)
            {
                var toexpect = (bool?)await ReadBlock(RaidDataBlocks.KUnlockedRaidDifficulty6, CancellationToken.None);
                await WriteBlock(true, RaidDataBlocks.KUnlockedRaidDifficulty6, CancellationToken.None, toexpect);
            }
            else
            {
                var toexpect = (bool?)await ReadBlock(RaidDataBlocks.KUnlockedRaidDifficulty6, CancellationToken.None);
                await WriteBlock(false, RaidDataBlocks.KUnlockedRaidDifficulty6, CancellationToken.None, toexpect);
            }
        }

        private async Task SkipRaidOnLosses(CancellationToken token)
        {
            Log($"We had {Settings.LobbyOptions.SkipRaidLimit} lost/empty raids.. Moving on!");

            await SanitizeRotationCount(token).ConfigureAwait(false);
            // Prepare and send an embed to inform users
            await EnqueueEmbed(null, "", false, false, true, false, token).ConfigureAwait(false);
            await CloseGame(Hub.Config, token).ConfigureAwait(false);
            await StartGameRaid(Hub.Config, token).ConfigureAwait(false);
        }

        private static string AltPokeImg(PKM pkm)
        {
            string pkmform = string.Empty;
            if (pkm.Form != 0)
                pkmform = $"-{pkm.Form}";

            return _ = $"https://raw.githubusercontent.com/zyro670/PokeTextures/main/Placeholder_Sprites/scaled_up_sprites/Shiny/AlternateArt/" + $"{pkm.Species}{pkmform}" + ".png";
        }

        private async Task ReadRaids(CancellationToken token)
        {
            Log("Getting Raid data...");
            await InitializeRaidBlockPointers(token);
            if (firstRun)
            {
                await LogPlayerLocation(token); // Get seed from current den for processing
            }
            string game = await DetermineGame(token);
            container = new(game);
            container.SetGame(game);

            await SetStoryAndEventProgress(token);

            var allRaids = new List<Raid>();
            var allEncounters = new List<ITeraRaid>();
            var allRewards = new List<List<(int, int, int)>>();

            if (IsBlueberry)
            {
                // Process only Blueberry raids
                var dataB = await ReadBlueberryRaids(token);
                Log("Reading Blueberry Raids...");
                var (blueberryRaids, blueberryEncounters, blueberryRewards) = await ProcessRaids(dataB, TeraRaidMapParent.Blueberry, token);
                allRaids.AddRange(blueberryRaids);
                allEncounters.AddRange(blueberryEncounters);
                allRewards.AddRange(blueberryRewards);
            }
            else if (IsKitakami)
            {
                // Process only Kitakami raids
                var dataK = await ReadKitakamiRaids(token);
                Log("Reading Kitakami Raids...");
                var (kitakamiRaids, kitakamiEncounters, kitakamiRewards) = await ProcessRaids(dataK, TeraRaidMapParent.Kitakami, token);
                allRaids.AddRange(kitakamiRaids);
                allEncounters.AddRange(kitakamiEncounters);
                allRewards.AddRange(kitakamiRewards);
            }
            else
            {
                // Default to processing Paldea raids
                var dataP = await ReadPaldeaRaids(token);
                Log("Reading Paldea Raids...");
                var (paldeaRaids, paldeaEncounters, paldeaRewards) = await ProcessRaids(dataP, TeraRaidMapParent.Paldea, token);
                allRaids.AddRange(paldeaRaids);
                allEncounters.AddRange(paldeaEncounters);
                allRewards.AddRange(paldeaRewards);
            }

            // Set combined data to container and process all raids
            container.SetRaids(allRaids);
            container.SetEncounters(allEncounters);
            container.SetRewards(allRewards);
            await ProcessAllRaids(token);
        }

        private async Task<(List<Raid>, List<ITeraRaid>, List<List<(int, int, int)>>)> ProcessRaids(byte[] data, TeraRaidMapParent mapType, CancellationToken token)
        {
            int delivery, enc;
            var tempContainer = new RaidContainer(container.Game);
            tempContainer.SetGame(container.Game);

            Log("Reading event raid status...");
            // Read event raids into tempContainer
            var BaseBlockKeyPointer = await SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
            await ReadEventRaids(BaseBlockKeyPointer, tempContainer, token).ConfigureAwait(false);
            await ReadEventRaids(BaseBlockKeyPointer, container, token).ConfigureAwait(false);

            (delivery, enc) = tempContainer.ReadAllRaids(data, StoryProgress, EventProgress, 0, mapType);

            var raidsList = tempContainer.Raids.ToList();
            var encountersList = tempContainer.Encounters.ToList();
            var rewardsList = tempContainer.Rewards.Select(r => r.ToList()).ToList();

            return (raidsList, encountersList, rewardsList);
        }

        private async Task InitializeRaidBlockPointers(CancellationToken token)
        {
            RaidBlockPointerP = await SwitchConnection.PointerAll(Offsets.RaidBlockPointerP, token).ConfigureAwait(false);
            RaidBlockPointerK = await SwitchConnection.PointerAll(Offsets.RaidBlockPointerK, token).ConfigureAwait(false);
            RaidBlockPointerB = await SwitchConnection.PointerAll(Offsets.RaidBlockPointerB, token).ConfigureAwait(false);
        }

        private async Task<string> DetermineGame(CancellationToken token)
        {
            string id = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
            return id switch
            {
                RaidCrawler.Core.Structures.Offsets.ScarletID => "Scarlet",
                RaidCrawler.Core.Structures.Offsets.VioletID => "Violet",
                _ => "",
            };
        }

        private async Task SetStoryAndEventProgress(CancellationToken token)
        {
            var BaseBlockKeyPointer = await SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
            StoryProgress = await GetStoryProgress(BaseBlockKeyPointer, token).ConfigureAwait(false);
            EventProgress = Math.Min(StoryProgress, 3);
        }

        private async Task<byte[]> ReadPaldeaRaids(CancellationToken token)
        {
            var dataP = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerP + RaidBlock.HEADER_SIZE, (int)RaidBlock.SIZE_BASE, token).ConfigureAwait(false);
            return dataP;
        }

        private async Task<byte[]> ReadKitakamiRaids(CancellationToken token)
        {
            var dataK = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerK, (int)RaidBlock.SIZE_KITAKAMI, token).ConfigureAwait(false);
            return dataK;
        }

        private async Task<byte[]> ReadBlueberryRaids(CancellationToken token)
        {
            var dataB = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerB, (int)RaidBlock.SIZE_BLUEBERRY, token).ConfigureAwait(false);
            return dataB;
        }

        private static (List<int> distGroupIDs, List<int> mightGroupIDs) GetPossibleGroups(RaidContainer container)
        {
            List<int> distGroupIDs = [];
            List<int> mightGroupIDs = [];

            if (container.DistTeraRaids != null)
            {
                foreach (TeraDistribution e in container.DistTeraRaids)
                {
                    if (TeraDistribution.AvailableInGame(e.Entity, container.Game) && !distGroupIDs.Contains(e.DeliveryGroupID))
                        distGroupIDs.Add(e.DeliveryGroupID);
                }
            }

            if (container.MightTeraRaids != null)
            {
                foreach (TeraMight e in container.MightTeraRaids)
                {
                    if (TeraMight.AvailableInGame(e.Entity, container.Game) && !mightGroupIDs.Contains(e.DeliveryGroupID))
                        mightGroupIDs.Add(e.DeliveryGroupID);
                }
            }

            return (distGroupIDs, mightGroupIDs);
        }

        private async Task<List<(uint Area, uint LotteryGroup, uint Den, uint Seed, uint Flags, bool IsEvent)>> ExtractPaldeaRaidInfo(CancellationToken token)
        {
            byte[] raidData = await ReadPaldeaRaids(token);
            var activeRaids = new List<(uint Area, uint LotteryGroup, uint Den, uint Seed, uint Flags, bool IsEvent)>();

            for (int i = 0; i < raidData.Length; i += Raid.SIZE)
            {
                var raid = new Raid(raidData.AsSpan()[i..(i + Raid.SIZE)]);
                if (raid.IsValid && IsRaidActive((raid.Area, raid.LotteryGroup, raid.Den)))
                {
                    activeRaids.Add((raid.Area, raid.LotteryGroup, raid.Den, raid.Seed, raid.Flags, raid.IsEvent));
                }
            }

            return activeRaids;
        }

        private async Task ProcessAllRaids(CancellationToken token)
        {
            var allRaids = container.Raids;
            var allEncounters = container.Encounters;
            var allRewards = container.Rewards;
            uint denHexSeedUInt;
            denHexSeedUInt = uint.Parse(denHexSeed, NumberStyles.AllowHexSpecifier);
            await FindSeedIndexInRaids(denHexSeedUInt, token);
            var raidInfoList = await ExtractPaldeaRaidInfo(token);
            bool newEventSpeciesFound = false;
            var (distGroupIDs, mightGroupIDs) = GetPossibleGroups(container);

            int raidsToCheck = Math.Min(5, allRaids.Count);

            if (!IsKitakami || !IsBlueberry)
            {
                // check if new event species is found
                for (int i = 0; i < raidsToCheck; i++)
                {
                    var raid = allRaids[i];
                    var encounter = allEncounters[i];
                    bool isEventRaid = raid.Flags == 2 || raid.Flags == 3;

                    if (isEventRaid)
                    {
                        string speciesName = SpeciesName.GetSpeciesName(encounter.Species, 2);
                        if (!SpeciesToGroupIDMap.ContainsKey(speciesName))
                        {
                            newEventSpeciesFound = true;
                            SpeciesToGroupIDMap.Clear(); // Clear the map as we've found a new event species
                            break; // No need to check further
                        }
                    }
                }
            }

            for (int i = 0; i < allRaids.Count; i++)
            {
                if (newEventSpeciesFound)
                {
                    // stuff for paldea events
                    var raid = allRaids[i];
                    var encounter1 = allEncounters[i];
                    bool isDistributionRaid = raid.Flags == 2;
                    bool isMightRaid = raid.Flags == 3;
                    var (Area, LotteryGroup, Den, Seed, Flags, IsEvent) = raidInfoList.FirstOrDefault(r =>
                    r.Seed == raid.Seed &&
                    r.Flags == raid.Flags &&
                    r.Area == raid.Area &&
                    r.LotteryGroup == raid.LotteryGroup &&
                    r.Den == raid.Den);

                    string denIdentifier = $"{Area}-{LotteryGroup}-{Den}";

                    if (isDistributionRaid || isMightRaid)
                    {
                        string speciesName = SpeciesName.GetSpeciesName(encounter1.Species, 2);
                        string speciesKey = string.Join("", speciesName.Split(' '));
                        int groupID = -1;

                        if (isDistributionRaid)
                        {
                            var distRaid = container.DistTeraRaids.FirstOrDefault(d => d.Species == encounter1.Species && d.Form == encounter1.Form);
                            if (distRaid != null)
                            {
                                groupID = distRaid.DeliveryGroupID;
                            }
                        }
                        else if (isMightRaid)
                        {
                            var mightRaid = container.MightTeraRaids.FirstOrDefault(m => m.Species == encounter1.Species && m.Form == encounter1.Form);
                            if (mightRaid != null)
                            {
                                groupID = mightRaid.DeliveryGroupID;
                            }
                        }

                        if (groupID != -1)
                        {
                            if (!SpeciesToGroupIDMap.ContainsKey(speciesKey))
                            {
                                SpeciesToGroupIDMap[speciesKey] = [(groupID, i, denIdentifier)];
                            }
                            else
                            {
                                SpeciesToGroupIDMap[speciesKey].Add((groupID, i, denIdentifier));
                            }
                        }
                    }
                }

                var (pk, seed) = IsSeedReturned(allEncounters[i], allRaids[i]);

                for (int a = 0; a < Settings.ActiveRaids.Count; a++)
                {
                    uint set;
                    try
                    {
                        set = uint.Parse(Settings.ActiveRaids[a].Seed, NumberStyles.AllowHexSpecifier);
                    }
                    catch (FormatException)
                    {
                        Log($"Invalid seed format detected. Removing {Settings.ActiveRaids[a].Seed} from list.");
                        Settings.ActiveRaids.RemoveAt(a);
                        a--;  // Decrement the index so that it does not skip the next element.
                        continue;  // Skip to the next iteration.
                    }
                    if (seed == set)
                    {
                        // Species and Form
                        RaidEmbedInfoHelpers.RaidSpecies = (Species)allEncounters[i].Species;
                        RaidEmbedInfoHelpers.RaidSpeciesForm = allEncounters[i].Form;

                        // Update Species and SpeciesForm in ActiveRaids
                        if (!Settings.ActiveRaids[a].ForceSpecificSpecies)
                        {
                            Settings.ActiveRaids[a].Species = (Species)allEncounters[i].Species;
                            Settings.ActiveRaids[a].SpeciesForm = allEncounters[i].Form;
                        }

                        // Encounter Info
                        int raid_delivery_group_id = Settings.ActiveRaids[a].GroupID;
                        var encounter = allRaids[i].GetTeraEncounter(container, allRaids[i].IsEvent ? 3 : StoryProgress, raid_delivery_group_id);
                        if (encounter != null)
                        {
                            RaidEmbedInfoHelpers.RaidLevel = encounter.Level;
                        }
                        else
                        {
                            RaidEmbedInfoHelpers.RaidLevel = 75;
                        }

                        // Star Rating
                        var stars = allRaids[i].IsEvent ? allEncounters[i].Stars : allRaids[i].GetStarCount(allRaids[i].Difficulty, StoryProgress, allRaids[i].IsBlack);

                        // Raid Title
                        var pkinfo = RaidExtensions<PK9>.GetRaidPrintName(pk);
                        var titlePrefix = allRaids[i].IsShiny ? "Shiny" : "";
                        RaidEmbedInfoHelpers.RaidEmbedTitle = $"{stars} ★ {titlePrefix} {(Species)allEncounters[i].Species}{pkinfo}";

                        // Gender
                        var maleEmoji = Settings.EmbedToggles.MaleEmoji.EmojiString;
                        var femaleEmoji = Settings.EmbedToggles.FemaleEmoji.EmojiString;
                        RaidEmbedInfoHelpers.RaidSpeciesGender = pk.Gender switch
                        {
                            0 when !string.IsNullOrEmpty(maleEmoji) => $"{maleEmoji} Male",
                            1 when !string.IsNullOrEmpty(femaleEmoji) => $"{femaleEmoji} Female",
                            _ => pk.Gender == 0 ? "Male" : pk.Gender == 1 ? "Female" : "Genderless"
                        };

                        // Nature
                        RaidEmbedInfoHelpers.RaidSpeciesNature = GameInfo.Strings.Natures[(int)pk.Nature];

                        // Ability
                        RaidEmbedInfoHelpers.RaidSpeciesAbility = $"{(Ability)pk.Ability}";

                        // IVs
                        RaidEmbedInfoHelpers.RaidSpeciesIVs = $"{pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}";

                        // Tera Type
                        RaidEmbedInfoHelpers.RaidSpeciesTeraType = $"{(MoveType)allRaids[i].GetTeraType(encounter)}";

                        // Moves
                        var strings = GameInfo.GetStrings(1);
                        var moves = new ushort[4] { allEncounters[i].Move1, allEncounters[i].Move2, allEncounters[i].Move3, allEncounters[i].Move4 };
                        var moveNames = new List<string>();
                        var useTypeEmojis = Settings.EmbedToggles.MoveTypeEmojis;
                        var typeEmojis = Settings.EmbedToggles.CustomTypeEmojis
                           .Where(e => !string.IsNullOrEmpty(e.EmojiCode))
                           .ToDictionary(
                               e => e.MoveType,
                               e => $"{e.EmojiCode}"
                           );

                        for (int j = 0; j < moves.Length; j++)
                        {
                            if (moves[j] != 0)
                            {
                                string moveName = strings.Move[moves[j]];
                                byte moveTypeId = MoveInfo.GetType(moves[j], pk.Context);
                                MoveType moveType = (MoveType)moveTypeId;

                                if (useTypeEmojis && typeEmojis.TryGetValue(moveType, out var moveEmoji))
                                {
                                    moveNames.Add($"{moveEmoji} {moveName}");
                                }
                                else
                                {
                                    moveNames.Add($"\\- {moveName}");
                                }
                            }
                        }
                        RaidEmbedInfoHelpers.Moves = string.Join("\n", moveNames);

                        // Extra Moves
                        var extraMoveNames = new List<string>();
                        if (allEncounters[i].ExtraMoves.Length != 0)
                        {
                            for (int j = 0; j < allEncounters[i].ExtraMoves.Length; j++)
                            {
                                if (allEncounters[i].ExtraMoves[j] != 0)
                                {
                                    string moveName = strings.Move[allEncounters[i].ExtraMoves[j]];
                                    byte moveTypeId = MoveInfo.GetType(allEncounters[i].ExtraMoves[j], pk.Context);
                                    MoveType moveType = (MoveType)moveTypeId;

                                    if (useTypeEmojis && typeEmojis.TryGetValue(moveType, out var moveEmoji))
                                    {
                                        extraMoveNames.Add($"{moveEmoji} {moveName}");
                                    }
                                    else
                                    {
                                        extraMoveNames.Add($"\\- {moveName}");
                                    }
                                }
                            }
                            RaidEmbedInfoHelpers.ExtraMoves = string.Join("\n", extraMoveNames);
                        }

                        // Scale Text and Number
                        RaidEmbedInfoHelpers.ScaleText = $"{PokeSizeDetailedUtil.GetSizeRating(pk.Scale)}";
                        RaidEmbedInfoHelpers.ScaleNumber = pk.Scale;

                        // Special Rewards
                        var res = GetSpecialRewards(allRewards[i], Settings.EmbedToggles.RewardsToShow);
                        RaidEmbedInfoHelpers.SpecialRewards = res;
                        if (string.IsNullOrEmpty(res))
                            res = string.Empty;
                        else
                            res = "**Special Rewards:**\n" + res;

                        // Area Text
                        var areaText = $"{Areas.GetArea((int)(allRaids[i].Area - 1), allRaids[i].MapParent)} - Den {allRaids[i].Den}";
                        Log($"Seed {seed:X8} found for {(Species)allEncounters[i].Species} in {areaText}");
                    }
                }
            }
        }

        private async Task FindSeedIndexInRaids(uint denHexSeedUInt, CancellationToken token)
        {
            var upperBound = KitakamiDensCount == 25 ? 94 : 95;
            var startIndex = KitakamiDensCount == 25 ? 94 : 95;

            // Search in Paldea region
            var dataP = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerP, 2304, token).ConfigureAwait(false);
            for (int i = 0; i < 69; i++)
            {
                var seed = BitConverter.ToUInt32(dataP.AsSpan(0x20 + i * 0x20, 4));
                if (seed == denHexSeedUInt)
                {
                    SeedIndexToReplace = i;
                    return;
                }
            }

            // Search in Kitakami region
            var dataK = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerK + 0x10, 0xC80, token).ConfigureAwait(false);
            for (int i = 0; i < upperBound; i++)
            {
                var seed = BitConverter.ToUInt32(dataK.AsSpan(i * 0x20, 4));
                if (seed == denHexSeedUInt)
                {
                    SeedIndexToReplace = i + 69;
                    return;
                }
            }

            // Search in Blueberry region
            var dataB = await SwitchConnection.ReadBytesAbsoluteAsync(RaidBlockPointerB + 0x10, 0xA00, token).ConfigureAwait(false);
            for (int i = startIndex; i < 118; i++)
            {
                var seed = BitConverter.ToUInt32(dataB.AsSpan((i - startIndex) * 0x20, 4));
                if (seed == denHexSeedUInt)
                {
                    SeedIndexToReplace = i - 1;
                    return;
                }
            }

            Log($"Seed {denHexSeedUInt:X8} not found in any region.");
        }

        public static (PK9, Embed) RaidInfoCommand(string seedValue, int contentType, TeraRaidMapParent map, int storyProgressLevel, int raidDeliveryGroupID, List<string> rewardsToShow, bool moveTypeEmojis, List<MoveTypeEmojiInfo> customTypeEmojis, int queuePosition = 0, bool isEvent = false)
        {
            byte[] enabled = StringToByteArray("00000001");
            byte[] area = StringToByteArray("00000001");
            byte[] displaytype = StringToByteArray("00000001");
            byte[] spawnpoint = StringToByteArray("00000001");
            byte[] thisseed = StringToByteArray(seedValue);
            byte[] unused = StringToByteArray("00000000");
            byte[] content = StringToByteArray($"0000000{contentType}"); // change this to 1 for 6-Star, 2 for 1-6 Star Events, 3 for Mighty 7-Star Raids
            byte[] leaguepoints = StringToByteArray("00000000");
            byte[] raidbyte = enabled.Concat(area).ToArray().Concat(displaytype).ToArray().Concat(spawnpoint).ToArray().Concat(thisseed).ToArray().Concat(unused).ToArray().Concat(content).ToArray().Concat(leaguepoints).ToArray();

            storyProgressLevel = storyProgressLevel switch
            {
                3 => 1,
                4 => 2,
                5 => 3,
                6 => 4,
                0 => 0,
                _ => 4 // default 6Unlocked
            };

            var raid = new Raid(raidbyte, map);
            var progress = storyProgressLevel;
            var raid_delivery_group_id = raidDeliveryGroupID;
            var encounter = raid.GetTeraEncounter(container, raid.IsEvent ? 3 : progress, contentType == 3 ? 1 : raid_delivery_group_id);
            var reward = encounter.GetRewards(container, raid, 0);
            var stars = raid.IsEvent ? encounter.Stars : raid.GetStarCount(raid.Difficulty, storyProgressLevel, raid.IsBlack);
            var teraType = raid.GetTeraType(encounter);
            var form = encounter.Form;
            var level = encounter.Level;

            var param = encounter.GetParam();
            var pk = new PK9
            {
                Species = encounter.Species,
                Form = encounter.Form,
                Move1 = encounter.Move1,
                Move2 = encounter.Move2,
                Move3 = encounter.Move3,
                Move4 = encounter.Move4,
            };
            if (raid.IsShiny) pk.SetIsShiny(true);
            Encounter9RNG.GenerateData(pk, param, EncounterCriteria.Unrestricted, raid.Seed);
            var strings = GameInfo.GetStrings(1);
            var useTypeEmojis = moveTypeEmojis;
            var typeEmojis = customTypeEmojis
                .Where(e => !string.IsNullOrEmpty(e.EmojiCode))
                .ToDictionary(
                    e => e.MoveType,
                    e => $"{e.EmojiCode}"
                );

            var movesList = "";
            bool hasMoves = false;
            for (int i = 0; i < pk.Moves.Length; i++)
            {
                if (pk.Moves[i] != 0)
                {
                    string moveName = strings.Move[pk.Moves[i]];
                    byte moveTypeId = MoveInfo.GetType(pk.Moves[i], pk.Context);
                    MoveType moveType = (MoveType)moveTypeId;

                    if (useTypeEmojis && typeEmojis.TryGetValue(moveType, out var moveEmoji))
                    {
                        movesList += $"{moveEmoji} {moveName}\n";
                    }
                    else
                    {
                        movesList += $"\\- {moveName}\n";
                    }
                    hasMoves = true;
                }
            }

            var extraMoves = "";
            for (int i = 0; i < encounter.ExtraMoves.Length; i++)
            {
                if (encounter.ExtraMoves[i] != 0)
                {
                    string moveName = strings.Move[encounter.ExtraMoves[i]];
                    byte moveTypeId = MoveInfo.GetType(encounter.ExtraMoves[i], pk.Context);
                    MoveType moveType = (MoveType)moveTypeId;

                    if (useTypeEmojis && typeEmojis.TryGetValue(moveType, out var moveEmoji))
                    {
                        extraMoves += $"{moveEmoji} {moveName}\n";
                    }
                    else
                    {
                        extraMoves += $"\\- {moveName}\n";
                    }
                    hasMoves = true;
                }
            }
            if (!string.IsNullOrEmpty(extraMoves))
            {
                movesList += $"*額外招式:**\n{extraMoves}";
            }
            var specialRewards = string.Empty;

            try
            {
                specialRewards = GetSpecialRewards(reward, rewardsToShow);
            }
            catch
            {
                specialRewards = "沒有可顯示的有效獎勵";
            }
            var teraTypeLower = strings.Types[teraType].ToLower();
            var teraIconUrl = $"https://raw.githubusercontent.com/bdawg1989/sprites/main/teraicons/icon1/{teraTypeLower}.png";
            var disclaimer = $"目前位置: {queuePosition}";
            var titlePrefix = raid.IsShiny ? "閃光 " : "";
            var formName = ShowdownParsing.GetStringFromForm(pk.Form, strings, pk.Species, pk.Context);
            var authorName = $"{stars} ★ {titlePrefix}{(Species)encounter.Species}{(pk.Form != 0 ? $"-{formName}" : "")}{(isEvent ? " (事件 Raid)" : "")}";

            (int R, int G, int B) = RaidExtensions<PK9>.GetDominantColor(RaidExtensions<PK9>.PokeImg(pk, false, false));
            var embedColor = new Discord.Color(R, G, B);

            var embed = new EmbedBuilder
            {
                Color = embedColor,
                ThumbnailUrl = RaidExtensions<PK9>.PokeImg(pk, false, false),
            };
            embed.AddField(x =>
            {
                x.Name = "**__數據__**";
                x.Value = $"{Format.Bold($"TeraType:")} {strings.Types[teraType]} \n" +
                          $"{Format.Bold($"Level:")} {level}\n" +
                          $"{Format.Bold($"Ability:")} {strings.Ability[pk.Ability]}\n" +
                          $"{Format.Bold("Nature:")} {(Nature)pk.Nature}\n" +
                          $"{Format.Bold("IVs:")} {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}\n" +
                          $"{Format.Bold($"Scale:")} {PokeSizeDetailedUtil.GetSizeRating(pk.Scale)}";
                x.IsInline = true;
            });

            if (hasMoves)
            {
                embed.AddField("**__招式__**", movesList, true);
            }
            else
            {
                embed.AddField("**__招式__**", "沒有可用的動作", true);  // Default message
            }

            if (!string.IsNullOrEmpty(specialRewards))
            {
                embed.AddField("**__特別獎勵__**", specialRewards, true);
            }
            else
            {
                embed.AddField("**__特別獎勵__**", "沒有特別獎勵", true);
            }

            var programIconUrl = "https://raw.githubusercontent.com/bdawg1989/sprites/main/imgs/icon4.png";
            embed.WithFooter(new EmbedFooterBuilder()
            {
                Text = $"" + disclaimer,
                IconUrl = programIconUrl
            });

            embed.WithAuthor(auth =>
            {
                auth.Name = authorName;
                auth.IconUrl = teraIconUrl;
            });

            return (pk, embed.Build());
        }

        public static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            Array.Reverse(bytes);
            return bytes;
        }

        private async Task<bool> SaveGame(PokeRaidHubConfig config, CancellationToken token)
        {
            Log("保存遊戲。");
            await Click(B, 3_000, token).ConfigureAwait(false);
            await Click(B, 3_000, token).ConfigureAwait(false);
            await Click(X, 3_000, token).ConfigureAwait(false);
            await Click(L, 5_000 + Hub.Config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);
            return true;
        }
    }
}