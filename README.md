# 非RaidBot

## 您好，歡迎來到我的RaidBot項目。如需支持，請訪問我們的Discord社區 [https://notpaldea.net](https://notpaldea.net)

![image](https://github.com/bdawg1989/NotRaidBot/assets/80122551/258bd7ab-0982-4641-9a3d-41bd192828dc)

![image](https://github.com/bdawg1989/NotPaldeaNET/assets/80122551/cc9e2d9e-eb54-4717-a896-b7afa163e1c3)

![image](https://github.com/bdawg1989/NotRaidBot/assets/80122551/a9aeb158-b6b6-415e-aa76-0e692d1283ce)

# 設置程序的視頻指南。
[YouTube](https://youtu.be/Zgbs6bW7Fag)

# __功能__
## 自動傳送
- 當團隊巢穴丟失時，自動將用戶傳送到最近的團隊巢穴。

## 團隊巢穴請求
__新增請求__
- 用戶可以使用命令 `ra <seed> <difficulty> <storyprogress>` 請求他們自己的團隊巢穴。
- 我們提供了一個線上種子查找器給您的用戶使用：[https://genpkm.com/seeds.html](https://genpkm.com/seeds.html)。

__移除請求__
- 用戶可以通過簡單輸入 `rqc` 命令來移除他們的團隊巢穴請求，從而將其從隊列中移除。

__團隊巢穴查看__
- 用戶可以查看包含所有團隊巢穴詳細信息的嵌入式消息，包括統計數據、獎勵等等！使用命令 `rv <seed> <difficulty> <storyprogress>`。

__禁用請求__
- 機器人擁有者可以點擊一個按鈕來啟用或禁用請求團隊巢穴的功能。

__限制請求__
- 機器人擁有者可以決定用戶在特定時間內可以提交多少請求。

## 完整事件支持
- 用戶可以請求他們想要的活動事件。機器人將自動將您的角色傳送到最近的活動巢穴並進行種子覆寫。
- 自動檢測可用的強者和分配組ID。

## 神秘團隊巢穴
- 沒有時間來添加自己的團隊巢穴到列表中嗎？打開神秘團隊巢穴功能，每次都能獲得一個隨機的閃光團隊巢穴！

## 禁用野外出現
- 厭倦了角色與野外寶可夢的隨機戰鬥？打開這個功能，不再有野外寶可夢的出現！

## 嵌入設置
- 提供豐富的嵌入設置供機器人擁有者自由選擇，量身定制您的機器人！

## 自動故事進度
- 每次團隊巢穴都會自動修改遊戲標誌來更改故事進度！再也不需要兩個機器人分別處理“新手”和“成年”團隊巢穴了！

# __Not RaidBot 指南__

- **ActiveRaids**
  - 更改戰鬥寶可夢 - 在集合編輯器中，您可以找到所有已請求和自動輪轉的寶可夢。通過編輯 `PartyPK` 設置並打開編輯器，您可以更改機器人的寶可夢。在這裡，您可以設置您希望機器人在團隊巢穴中使用的精靈格式。機器人將僅在該團隊巢穴中使用該寶可夢。一旦團隊巢穴完成，下一個團隊巢穴將使用您隊伍中原始的第一個寶可夢，除非下一個團隊巢穴也有填寫了 PartyPK。

- **RaidSettings**
 - GenerateRaidsFromFile - 設置為 `True`，並將您希望輪轉的種子添加到此文件中。當您首次啟動程序時，它將為您創建一個名為 `raidfilessv` 的新文件夾，並將 `raidsv.txt` 文件添加到其中。您將打開此文件並像這樣添加種子：`<seed>-<speciesname>-<stars/difficulty>-<storyprogresslevel>`。
   - 例如：如果我正在查看 Raidcalc，您的設置是 Story Progress: 4* Unlocked 和 Stars: 3，您將其作為 `3739A70B-Goomy-3-4` 添加到種子中。
   - 自 2023 年 10 月 25 日起，我在文件夾中為您提供了兩個模板：paldeaseeds.txt 和 kitakamiseeds.txt - 您可以將這些種子複製並添加到 raidsv.txt 文件中作為起點。
  - 保存 `raidsv.txt` 文件以保存您的新更改。
  - 開始 NotRaidBot，列表中的設置 `ActiveRaids` 將開始填充為 raidsv.txt 中的列表。

 - SaveSeedsToFile - 設置為 true，以便機器人保存當前 ActiveRaids 的備份，如果需要，您可以將它們粘貼回 raidsv.txt。

 - RandomRotation - 如果您希望機器人在 ActiveRaids 列表中進行隨機團隊巢穴，同時保持優先處理請求的團隊巢穴，請將其設置為 true。

 - MysteryRaids - 設置為 true，以便機器人隨機注入一個閃光團隊巢穴。不能與 RandomRotation 同時使用。

 - DisableRequests - 禁止用戶請求團隊巢穴。

 - DisableOverworldSpawns - 如果設置為 true，則停止野外寶可夢在野外的出現。將其設置回 false 可使寶可夢重新出現。

 - KeepDaySeed - 將其設置為 True，以便機器人在日期翻轉到第二天時注入正確的今天種子。

 - EnableTimeRollback - 將其設置為 true，以便機器人將時間回退 5 小時，以防止日期更改。

- **Embed Toggles**
 - RaidEmbedDescription - 添加您希望顯示在所有嵌入中頂部的任何文本。
 - SelectedTeraIconType - 更改嵌入中使用的圖標。Icon1 是定製的特拉圖標，看起來非常棒。
 - IncludeMoves - 如果您希望在嵌入中顯示團隊寶可夢的招式，則設置為 true。
 - IncludeRewards - 如果您希望在嵌入中顯示團隊寶可夢的獎勵，則設置為 true。
 - IncludeSeed - 設置為 true 以在嵌入中顯示當前團隊巢穴的種子。
 - IncludeCountdown - 設置為 true 以在嵌入中顯示距離團隊巢穴開始的時間。
 - IncludeTypeAdvantage - 設置為 true 以在嵌入中顯示超有效類型。
 - RewardsToShow - 您希望在嵌入中顯示的獎勵列表。
 - RequestEmbedTime - 等待將用戶請求的團隊巢穴發布到公共頻道的時間。
 - TakeScreenShot - 設置為 true 以在嵌入中顯示遊戲的截圖。
 - ScreenshotTiming - 設置為 1500ms 或 22000ms，以在團隊巢穴中拍攝不同的截圖。
 - HideRaidCode - 從嵌入中隱藏團隊巢穴代碼。

- **EventSettings**
 - EventActive - 如果事件活動（Might 或 Distribution）正在進行中，則設置為 true。如果檢測到事件，機器人將自動設置此選項。
 - RaidDeliveryGroupID - 當前事件的索引在此處。如果檢測到事件，機器人將自動設置此選項。

- **LobbyOptions**
 - LobbyMethod
  - SkipRaid - 如果在 `SkipRaidLimit` 中定義的指定次數後團隊巢穴為空，將跳過團隊巢穴。
  - Continue - 將持續發布相同的團隊巢穴，直到有人加入。
  - OpenLobby - 在 X 個空白大廳後，將大廳開放為自由模式。
 - Action - 將其設置為 `MashA`，以便機器人在戰鬥中每 3.5 秒按下 "A"。`AFK` 表示機器人將不做任何操作。
 - ExtraTimeLobbyDisband - 一旦大廳解散，如果需要返回到野外，則添加額外的時間。
 - ExtraTimePartyPK - 等待切換您的領先團隊寶可夢進行戰鬥的額外時間。僅適用於慢切換。
- **RaiderBanList**
  - List - 這裡是所有被封禁用戶的NID清單。使用 `ban <玩家名稱或NID>` 命令來封禁或手動添加他們到這裡。
  - AllowIfEmpty - 保持為 false。

- **MiscSettings**
  - DateTimeFormat - 設置與你的Switch上顯示的時間格式相同。
  - UseOvershoot - 如果為 true，機器人將使用過度擊鍵方法而不是按下 DDown 來進入日期/時間設置。如果設置為 true，請確保配置RolloverCorrection。
  - DDownClicks - 機器人需要按下 DDown 進入日期/時間設置的次數。
  - ScreenOff - 在遊玩過程中關閉屏幕以保護LED/電源。或使用命令 `screenOff` 或 `screenOn`。

- **DiscordSettings**
  - Token - 添加你從 [Discord 開發者平台](https://discord.com/developers/applications/) 獲得的 Discord 機器人令牌。
  - CommandPrefix - 機器人將使用的命令前綴。常見的是 $
  - RoleSudo - 告訴機器人誰是它的管理者。在機器人有權讀取的伺服器頻道中，輸入 `$sudo @YOURUSERNAME`。機器人現在受你指揮。
  - ChannelWhitelist - 這些是你希望機器人監聽命令的頻道。使用 `$addchannel` 命令來自動添加頻道到機器人中。
  - LoggingChannels - 如果你希望將機器人在程式日誌標籤中輸出的所有信息記錄到頻道中，使用 `$loghere` 命令。
  - EchoChannels - 這些是你希望機器人將其戰鬥嵌入發布到的頻道。使用命令 `$aec` 將頻道添加到此列表中。

## __Announcement Settings__

如果你的機器人在多個伺服器中運行，並且你需要通知所有正在使用它的人機器人的狀態（在線、離線、休眠等），而不必自己發送大量消息，那麼這些設置將非常有用。只需使用 `$announce TEXT HERE` 命令，以漂亮的嵌入式公告發送你想要的消息，並選擇你喜歡的縮略圖和顏色。

- AnnouncementThumbnailOption - 設置為你喜歡的預設寶可夢圖片。
- CustomAnnouncementThumbnailURL - 如果你不喜歡預設的圖片，可以放入你自己的縮略圖圖片URL。
- AnnouncementEmbedColor - 自解釋。
- RandomAnnouncementThumbnail - 如果你想從我的自定義縮略圖中隨機使用圖片，設置為 true。如果你有自定義圖片，則無效。
- RandomAnnouncementColor - 讓機器人從列表中選擇這次嵌入式的顏色。

# __In-Game Set Up__
- 站在你的雷達水晶前
- 在遊戲選項中，確保以下設置：
  - `Give Nicknames` 為 Off
  - `SendToBoxes` 為 `Automatic`（以防機器人捕獲到的寶可夢自動發送到箱子）
  - Auto Save 為 Off
  - Text Speed 為 Fast
- 開始運行機器人

## 程式設置
- 隨您喜歡填寫或留空雷達描述。
- 若要在特定頻道發布雷達嵌入，使用 `aec` 命令。
- 在 Seed 參數中粘貼您的雷達種子。
- **編碼雷達**
  - 如果希望設置為編碼雷達，設置為 true。
- **等待時間**
  - 進入雷達前的總等待時間。
- **日期/時間格式**
  - 設置正確的日期/時間格式，以便在應用日更校正時使用。
- **滾動至日更設置的時間**
  - 用於超過日期/時間設置：
    - Lites 約為 800
    - OLEDs 約為 950
    - V1s 約為 920
    - 時間因人而異。
- **配置日更校正**
  - 如果需要進行日更校正，設置為 true。
  - 在遊戲關閉時運行此功能。

# 我的所有項目

## Showdown 替代網站
- [genpkm.com](https://genpkm.com) - 提供替代 Showdown 的在線服務，具有合法性檢查和批量交換代碼功能，使生成寶可夢變得更輕鬆。

## Scarlet/Violet RaidBot

- [NotRaidBot](https://github.com/bdawg1989/NotPaldeaNET) - 絕對是 Scarlet/Violet 最先進的 RaidBot。

## PKHeX - AIO（一體化）
- [PKHeX-AIO](https://github.com/bdawg1989/PKHeX-ALL-IN-ONE) - 包含 ALM、TeraFinder 和 PokeNamer 插件的單一 .exe 檔案。無需額外的文件夾和 plugin.dll。

## MergeBot - 終極交易機器人
- [源代碼](https://github.com/bdawg1989/MergeBot)

## Grand Oak - SysBot 助手
- 一個 Discord 機器人，幫助解決提交錯誤 Showdown 格式的合法性問題。[加入我的 Discord 了解更多](https://discord.gg/GtUu9BmCzy)
  
![image](https://github.com/bdawg1989/MergeBot/assets/80122551/0842b48e-1b4d-4621-b321-89f478db508b)
