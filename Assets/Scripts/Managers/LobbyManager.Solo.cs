// LobbyManager.Solo.cs — โหมดเล่นคนเดียวกับบอท และโหมดฝึก/บทสอน (partial class ของ LobbyManager)
// ใช้ Photon Offline Mode: ไม่ต่อเน็ต ไม่นับโควต้าผู้เล่นพร้อมกันของ Photon แต่ใช้โค้ดเกมเดิมทั้งหมด
// ขั้นตอน: กดปุ่ม → ตัดการเชื่อมต่อออนไลน์ → เปิด OfflineMode → สร้างห้อง SOLO (มีบอท 1 ตัว) → โหลดฉากต่อสู้ทันที
// กลับจากแมตช์: LobbyManager.Start ปิด OfflineMode แล้วต่อออนไลน์ใหม่อัตโนมัติ
// ปิดทั้งระบบได้ด้วย FeatureFlags.Bots = false (ปุ่มจะซ่อน)
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public partial class LobbyManager
{
    private Button soloDifficultyButton;
    private Button soloPlayButton;
    private Button trainingButton;
    private Button soloBotsButton;
    private const string SoloModePrefs = "SoloModeIndex";
    // โหมดเล่นคนเดียว: จำนวนบอท / เป็นโหมดทีมไหม / ชื่อบนปุ่ม (1 BOT = 1 VS 1, หลายตัว = FFA, TEAM = เราอยู่ทีม BLUE กับบอท)
    // เฟส 7B: เพิ่มโหมดเกม SURVIVAL / KOTH / STARS / ROYALE / CAMPAIGN (SoloModeGame = MatchRules.Mode*)
    private static readonly int[] SoloModeBots = { 1, 3, 5, 9, 3, 5, 9, 0, 3, 3, 9, 0 };
    private static readonly bool[] SoloModeTeams = { false, false, false, false, true, true, true, false, false, false, false, false };
    private static readonly int[] SoloModeGame = { 0, 0, 0, 0, 0, 0, 0, 3, 1, 2, 4, 5 };
    private static readonly string[] SoloModeLabels = { "1 BOT", "3 BOTS", "5 BOTS", "9 BOTS", "TEAM 2v2", "TEAM 3v3", "TEAM 5v5",
        "SURVIVAL", "HILL 4P", "STARS 4P", "ROYALE 10", "CAMPAIGN" };
    private int SoloMode
    {
        get
        {
            int value = PlayerPrefs.GetInt(SoloModePrefs, 0);
            if (!FeatureFlags.MultiPlayer || value < 0 || value >= SoloModeBots.Length) return 0;
            if (SoloModeTeams[value] && !FeatureFlags.Teams) return 0;
            if (SoloModeGame[value] != 0 && !FeatureFlags.GameModes) return 0;
            return value;
        }
        set { PlayerPrefs.SetInt(SoloModePrefs, value); PlayerPrefs.Save(); }
    }
    private int SoloBots => SoloModeBots[SoloMode];
    private bool pendingSolo;
    private bool pendingTraining;
    private const string SoloDifficultyPrefs = "SoloBotDifficulty";
    // ความยากที่เลือกไว้ (1 ง่าย, 2 กลาง, 3 ยาก) จำไว้ในเครื่อง
    private int SoloDifficulty
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(SoloDifficultyPrefs, 2), 1, 3);
        set { PlayerPrefs.SetInt(SoloDifficultyPrefs, Mathf.Clamp(value, 1, 3)); PlayerPrefs.Save(); }
    }

    // ปุ่มแถวล่างของหน้าหลัก (เรียกจาก BuildHomeScreen)
    private void BuildSoloButtons(RectTransform root)
    {
        soloDifficultyButton = UIButton("SoloDifficulty", root, "NORMAL", 272, -262, 125, 42, OnSoloDifficultyClicked);
        soloPlayButton = UIButton("SoloPlay", root, "VS BOT", 397, -262, 115, 42, () => StartSolo(false));
        trainingButton = UIButton("Training", root, "TRAINING", 520, -262, 125, 42, () => StartSolo(true));
        // จำนวนบอท (แถวล่าง ใต้ปุ่มความยาก) — 1 / 3 / 5 / 9 ตัว
        soloBotsButton = UIButton("SoloBots", root, "1 BOT", 272, -306, 125, 36, OnSoloBotsClicked);
        RefreshSoloButtons();
    }

    private void RefreshSoloButtons()
    {
        bool visible = FeatureFlags.Bots;
        bool canStart = visible && profileLoaded && !pendingSolo && !roomRequestPending && !reconnecting && !PhotonNetwork.InRoom;
        if (soloDifficultyButton != null)
        {
            soloDifficultyButton.gameObject.SetActive(visible);
            soloDifficultyButton.interactable = canStart;
            SetButtonLabel(soloDifficultyButton, MatchRules.DifficultyNames[SoloDifficulty]);
        }
        if (soloPlayButton != null) { soloPlayButton.gameObject.SetActive(visible); soloPlayButton.interactable = canStart; }
        if (trainingButton != null) { trainingButton.gameObject.SetActive(visible); trainingButton.interactable = canStart; }
        if (soloBotsButton != null)
        {
            soloBotsButton.gameObject.SetActive(visible && FeatureFlags.MultiPlayer);
            soloBotsButton.interactable = canStart;
            SetButtonLabel(soloBotsButton, SoloModeGame[SoloMode] == MatchRules.ModeCampaign
                ? "CAMPAIGN " + Campaign.NextStage + "/" + Campaign.Count : SoloModeLabels[SoloMode]);
        }
    }

    private void OnSoloBotsClicked()
    {
        // วนไปโหมดถัดไป (ข้ามโหมดทีมถ้าปิด FeatureFlags.Teams)
        int next = SoloMode;
        for (int step = 0; step < SoloModeBots.Length; step++)
        {
            next = (next + 1) % SoloModeBots.Length;
            if ((!SoloModeTeams[next] || FeatureFlags.Teams) && (SoloModeGame[next] == 0 || FeatureFlags.GameModes)) break;
        }
        SoloMode = next;
        RefreshSoloButtons();
    }

    private void OnSoloDifficultyClicked()
    {
        SoloDifficulty = SoloDifficulty % 3 + 1;
        RefreshSoloButtons();
    }

    // เริ่มเล่นคนเดียว (training = บทสอนกับเป้าซ้อม)
    private void StartSolo(bool training)
    {
        if (!FeatureFlags.Bots || !profileLoaded || pendingSolo || PhotonNetwork.InRoom) return;
        if (!Application.CanStreamedLevelBeLoaded("SampleScene")) { UpdateStatus("Gameplay scene is missing from Build Profiles."); return; }
        pendingSolo = true;
        pendingTraining = training;
        RefreshSoloButtons();
        EnablePlayButtons(false);
        UpdateStatus(training ? "Starting training..." : "Starting solo battle...");
        if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode) PhotonNetwork.Disconnect(); // ต่อใน OnDisconnected
        else EnterOfflineSolo();
    }

    // เรียกจาก OnDisconnected เมื่อกำลังจะเล่นคนเดียว
    private void EnterOfflineSolo()
    {
        if (!PhotonNetwork.OfflineMode) PhotonNetwork.OfflineMode = true; // จะเรียก OnConnectedToMaster ต่อ
        else CreateSoloRoom();
    }

    // เรียกจาก OnConnectedToMaster ในโหมดออฟไลน์: สร้างห้อง SOLO พร้อมกติกาและบอท
    private void CreateSoloRoom()
    {
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [MapProperty] = pendingTraining ? 1 : selectedMapIndex,
            ["MapRevision"] = 0,
            ["Starting"] = false,
            [MatchRules.SoloKey] = true,
            [MatchRules.TutorialKey] = pendingTraining,
            [MatchRules.HumansKey] = 1,
            [MatchRules.BotCountKey] = pendingTraining ? 1 : SoloBots,
            [MatchRules.TeamsKey] = !pendingTraining && SoloModeTeams[SoloMode],
            [MatchRules.BotDifficultyKey] = pendingTraining ? 0 : SoloDifficulty,
            [MatchRules.KillTargetKey] = pendingTraining ? 3 : MatchRules.DefaultKillTarget,
            [MatchRules.MatchSecondsKey] = pendingTraining ? 600 : MatchRules.DefaultMatchSeconds,
            [MatchRules.HazardsKey] = !pendingTraining
        };
        // เฟส 7B: โหมดเกม
        int game = pendingTraining ? 0 : SoloModeGame[SoloMode];
        props[MatchRules.GameModeKey] = game;
        if (game == MatchRules.ModeSurvival) props[MatchRules.MatchSecondsKey] = 900;
        if (game == MatchRules.ModeRoyale) props[MatchRules.MatchSecondsKey] = 300;
        if (game == MatchRules.ModeCampaign)
        {
            int stage = Campaign.NextStage;
            var def = Campaign.Get(stage);
            props[MatchRules.CampaignStageKey] = stage;
            props[MapProperty] = def.map;
            props[MatchRules.BotDifficultyKey] = def.difficulty;
            props[MatchRules.MatchSecondsKey] = 480;
        }
        PhotonNetwork.CreateRoom("SOLO", new RoomOptions { MaxPlayers = 1, CustomRoomProperties = props });
    }

    // เรียกจาก OnJoinedRoom ในโหมดออฟไลน์: ส่ง loadout แล้วโหลดฉากต่อสู้เลย (ไม่ผ่านห้องรอ)
    private void LaunchSoloRoom()
    {
        pendingSolo = false;
        PublishLocalLoadout(true);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        {
            // โหมดทีมคนเดียว: เราอยู่ทีม BLUE (บอทจะถูกแบ่งเติมให้สองทีมเท่ากัน)
            [MatchRules.TeamPrefix + PhotonNetwork.LocalPlayer.ActorNumber] = 0,
            ["BattleToken"] = System.Guid.NewGuid().ToString("N"),
            ["BattleAborted"] = false,
            ["StartTime"] = -1d
        });
        PhotonNetwork.LoadLevel("SampleScene");
    }
}
