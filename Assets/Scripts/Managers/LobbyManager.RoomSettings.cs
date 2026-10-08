// LobbyManager.RoomSettings.cs — ปุ่มตั้งค่าห้องในห้องรอ (partial class ของ LobbyManager)
// Host กดวนค่า: จำนวนคน (1 VS 1 / FFA 4-10 คน), จำนวน Kill (3/5/10), เวลา (3/5/10 นาที), อันตรายในแม็พ (เปิด/ปิด), บอทเติมห้อง
// ค่าเก็บใน Room Custom Properties (คีย์อยู่ใน MatchRules.cs) และเพิ่ม MapRevision ให้ทุกคนกด READY ใหม่
// ปิดทั้งระบบได้ด้วย FeatureFlags.RoomSettings = false (ปุ่มจะซ่อน และเกมใช้ 3 Kill / 3 นาที แบบเดิม)
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

// ส่วนปุ่มตั้งค่าห้องของ LobbyManager (partial): สร้างปุ่ม อัปเดตข้อความ และเขียนกติกาลง Room Custom Properties
public partial class LobbyManager
{
    private Button roomKillsButton; // ปุ่มตั้งจำนวน Kill ที่ต้องทำเพื่อชนะ
    private Button roomTimeButton; // ปุ่มตั้งเวลาแมตช์
    private Button roomHazardButton; // ปุ่มเปิด/ปิดอันตรายในแม็พ
    private Button roomBotButton; // ปุ่มตั้งบอทเติมห้อง (OFF / EASY / NORMAL / HARD)
    private Button roomModeButton; // ปุ่มพื้นหลังป้ายโหมด Host กดเพื่อเปลี่ยนจำนวนคน/โหมดทีม
    private TMP_Text roomModeLabel; // ป้ายข้อความโหมดห้อง (เช่น 1 VS 1, TEAM 2v2) วางทับปุ่มโหมด

    // ปุ่มพื้นหลังของป้ายโหมด (ป้ายข้อความวางทับด้านบน) — Host กดเพื่อเปลี่ยนจำนวนคนในห้อง
    private void BuildRoomModeButton(RectTransform root)
    {
        roomModeButton = UIButton("RoomModeButton", root, "", -510, 246, 170, 40, OnRoomModeClicked);
    }

    // วนโหมด: 1 VS 1 -> FFA 4P -> TEAM 2v2 -> FFA 6P -> TEAM 3v3 -> ... -> TEAM 5v5
    // (ข้ามขนาดที่น้อยกว่าจำนวนคนในห้องตอนนี้ / ปิด FeatureFlags.Teams = ไม่มีโหมดทีม)
    private void OnRoomModeClicked()
    {
        if (!FeatureFlags.MultiPlayer || !CanEditRoomSettings()) return;
        var room = PhotonNetwork.CurrentRoom;
        var modes = new System.Collections.Generic.List<(int size, bool teams)>();
        foreach (int size in MatchRules.PlayerOptions)
        {
            modes.Add((size, false));
            if (size >= 4 && FeatureFlags.Teams) modes.Add((size, true));
        }
        int current = modes.IndexOf((room.MaxPlayers, MatchRules.IsTeamRoom(room)));
        for (int step = 1; step <= modes.Count; step++)
        {
            var next = modes[(current + step + modes.Count) % modes.Count];
            if (next.size < room.PlayerCount) continue;
            if (next.size == room.MaxPlayers && next.teams == MatchRules.IsTeamRoom(room)) return;
            room.MaxPlayers = next.size;
            // เพิ่ม MapRevision ให้ทุกคนกด READY ใหม่ (กติกาเปลี่ยน)
            SetRoomRule(MatchRules.TeamsKey, next.teams);
            return;
        }
    }

    // สร้างปุ่ม 3 ปุ่มแถวบนของห้องรอ (เรียกจาก BuildLobbyUI)
    private void BuildRoomSettingButtons(RectTransform root)
    {
        roomKillsButton = UIButton("RoomKills", root, "KILLS 3", -330, 246, 150, 40, OnRoomKillsClicked);
        roomTimeButton = UIButton("RoomTime", root, "TIME 3:00", -175, 246, 150, 40, OnRoomTimeClicked);
        roomHazardButton = UIButton("RoomHazards", root, "HAZARDS ON", -20, 246, 150, 40, OnRoomHazardsClicked);
        // บอทเติมห้อง: ถ้าไม่มีเพื่อนเข้า Host เริ่มเกมกับบอทได้ (OFF → EASY → NORMAL → HARD)
        roomBotButton = UIButton("RoomBots", root, "BOT OFF", 135, 246, 150, 40, OnRoomBotClicked);
        // เงื่อนไขชนะ (FIRST TO X / MOST KILLS / NET SCORE / MOST DAMAGE / BOUNTY HUNT) ดู MatchRules.WinRules.cs
        roomWinRuleButton = UIButton("RoomWinRule", root, "FIRST TO X", 290, 246, 150, 40, OnRoomWinRuleClicked);
    }

    private Button roomWinRuleButton; // ปุ่มเลือกเงื่อนไขชนะของห้อง

    // Host กดปุ่มเงื่อนไขชนะ: วนไปแบบถัดไป แล้วเขียนลงห้อง (ทุกคนต้องกด READY ใหม่เหมือนกติกาอื่น)
    private void OnRoomWinRuleClicked()
    {
        if (!CanEditRoomSettings()) return;
        int next = (MatchRules.WinRule(PhotonNetwork.CurrentRoom) + 1) % MatchRules.WinRuleNames.Length;
        SetRoomRule(MatchRules.WinRuleKey, next);
    }

    // อัปเดตข้อความและสิทธิ์กดของปุ่ม (เรียกทุกครั้งที่ห้องรอรีเฟรช)
    private void RefreshRoomSettingButtons()
    {
        bool visible = FeatureFlags.RoomSettings && PhotonNetwork.InRoom;
        bool canEdit = visible && CanEditRoomSettings();
        var room = PhotonNetwork.CurrentRoom;
        // เงื่อนไขชนะแบบใหม่ไม่ใช้เป้า Kill (เล่นจนหมดเวลา) จึงล็อกปุ่ม KILLS ไว้
        bool usesKillTarget = !MatchRules.UsesRuleScore(room);
        SetRoomSettingButton(roomKillsButton, visible, canEdit && usesKillTarget, usesKillTarget ? "KILLS " + MatchRules.KillTarget(room) : "KILLS  -");
        bool winRuleVisible = visible && FeatureFlags.WinRules && !MatchRules.IsRanked(room) && MatchRules.GameMode(room) == MatchRules.ModeDeathmatch;
        SetRoomSettingButton(roomWinRuleButton, winRuleVisible, canEdit, MatchRules.WinRuleNames[MatchRules.WinRule(room)]);
        var winRuleLabel = roomRulesMenu != null ? roomRulesMenu.Find("RuleLabel8") : null; // หัวข้อ WIN CONDITION ซ่อนตามปุ่ม
        if (winRuleLabel != null) winRuleLabel.gameObject.SetActive(winRuleVisible);
        SetRoomSettingButton(roomTimeButton, visible, canEdit, "TIME " + MatchRules.FormatTime(MatchRules.MatchSeconds(room)));
        SetRoomSettingButton(roomHazardButton, visible, canEdit, MatchRules.Hazards(room) ? "HAZARDS ON" : "HAZARDS OFF");
        // ป้ายโหมด: 1 VS 1 / FFA xP (Host เห็นคำว่า >> ให้รู้ว่ากดได้)
        int maxPlayers = room != null ? room.MaxPlayers : 2;
        if (roomModeLabel != null) roomModeLabel.text = MatchRules.IsRanked(room) ? "RANKED 1V1"
            : MatchRules.ModeName(maxPlayers, MatchRules.IsTeamRoom(room)) + (canEdit && FeatureFlags.MultiPlayer ? "  >>" : "");
        if (roomModeButton != null)
        {
            roomModeButton.gameObject.SetActive(visible && FeatureFlags.MultiPlayer);
            roomModeButton.interactable = canEdit;
        }
        RefreshRoomUpgradesButton();
        RefreshRoomPowerUpsButton();
        RefreshRoomGameModeButton();
        SetRoomSettingButton(roomBotButton, visible && FeatureFlags.Bots, canEdit,
            MatchRules.BotFill(room) ? "BOT " + MatchRules.DifficultyNames[MatchRules.BotDifficulty(room)] : "BOT OFF");
    }

    // ตั้งค่าปุ่มตั้งค่าห้อง 1 ปุ่ม: แสดง/ซ่อน, กดได้เฉพาะคนที่แก้ได้ (Host) และเปลี่ยนข้อความบนปุ่ม
    private static void SetRoomSettingButton(Button button, bool visible, bool canEdit, string label)
    {
        if (button == null) return;
        button.gameObject.SetActive(visible);
        button.interactable = canEdit;
        SetButtonLabel(button, label);
    }

    // แก้ได้เฉพาะ Host ตอนห้องยังไม่เริ่มเกม
    private bool CanEditRoomSettings()
        => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && !isStartingGame && !isLeavingRoom && !RoomStarting && !reconnecting
            && !MatchRules.IsRanked(PhotonNetwork.CurrentRoom); // ห้องแรงค์: กติกาตายตัว

    // Host กดปุ่ม KILLS: วนจำนวน Kill เป้าหมายไปค่าถัดไปใน KillOptions แล้วเขียนลงห้อง
    private void OnRoomKillsClicked()
    {
        if (!CanEditRoomSettings()) return;
        SetRoomRule(MatchRules.KillTargetKey, MatchRules.Next(MatchRules.KillOptions, MatchRules.KillTarget(PhotonNetwork.CurrentRoom)));
    }

    // Host กดปุ่ม TIME: วนเวลาแมตช์ไปค่าถัดไปใน TimeOptions แล้วเขียนลงห้อง
    private void OnRoomTimeClicked()
    {
        if (!CanEditRoomSettings()) return;
        SetRoomRule(MatchRules.MatchSecondsKey, MatchRules.Next(MatchRules.TimeOptions, MatchRules.MatchSeconds(PhotonNetwork.CurrentRoom)));
    }

    // Host กดปุ่ม HAZARDS: สลับเปิด/ปิดอันตรายในแม็พ แล้วเขียนลงห้อง
    private void OnRoomHazardsClicked()
    {
        if (!CanEditRoomSettings()) return;
        SetRoomRule(MatchRules.HazardsKey, !MatchRules.Hazards(PhotonNetwork.CurrentRoom));
    }

    // วนค่า: ปิด → ง่าย → กลาง → ยาก → ปิด
    private void OnRoomBotClicked()
    {
        if (!CanEditRoomSettings()) return;
        var room = PhotonNetwork.CurrentRoom;
        bool fill = MatchRules.BotFill(room);
        int level = MatchRules.BotDifficulty(room);
        if (!fill) { fill = true; level = 1; }
        else if (level < 3) level++;
        else fill = false;
        var expected = new ExitGames.Client.Photon.Hashtable { ["MapRevision"] = MapRevision, ["Starting"] = false };
        room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            { [MatchRules.BotFillKey] = fill, [MatchRules.BotDifficultyKey] = level, ["MapRevision"] = MapRevision + 1 }, expected);
    }

    // เขียนค่าลงห้องแบบมีเงื่อนไข (เหมือนการเปลี่ยนแม็พ) แล้วเพิ่ม MapRevision ให้ Ready เดิมหมดอายุ
    private void SetRoomRule(string key, object value)
    {
        var expected = new ExitGames.Client.Photon.Hashtable { ["MapRevision"] = MapRevision, ["Starting"] = false };
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { [key] = value, ["MapRevision"] = MapRevision + 1 }, expected);
    }
}
