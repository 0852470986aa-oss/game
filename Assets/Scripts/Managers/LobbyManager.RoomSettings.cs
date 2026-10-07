// LobbyManager.RoomSettings.cs — ปุ่มตั้งค่าห้องในห้องรอ (partial class ของ LobbyManager)
// Host กดวนค่า: จำนวนคน (1 VS 1 / FFA 4-10 คน), จำนวน Kill (3/5/10), เวลา (3/5/10 นาที), อันตรายในแม็พ (เปิด/ปิด), บอทเติมห้อง
// ค่าเก็บใน Room Custom Properties (คีย์อยู่ใน MatchRules.cs) และเพิ่ม MapRevision ให้ทุกคนกด READY ใหม่
// ปิดทั้งระบบได้ด้วย FeatureFlags.RoomSettings = false (ปุ่มจะซ่อน และเกมใช้ 3 Kill / 3 นาที แบบเดิม)
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

public partial class LobbyManager
{
    private Button roomKillsButton;
    private Button roomTimeButton;
    private Button roomHazardButton;
    private Button roomBotButton;
    private Button roomModeButton;
    private TMP_Text roomModeLabel;

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
    }

    // อัปเดตข้อความและสิทธิ์กดของปุ่ม (เรียกทุกครั้งที่ห้องรอรีเฟรช)
    private void RefreshRoomSettingButtons()
    {
        bool visible = FeatureFlags.RoomSettings && PhotonNetwork.InRoom;
        bool canEdit = visible && CanEditRoomSettings();
        var room = PhotonNetwork.CurrentRoom;
        SetRoomSettingButton(roomKillsButton, visible, canEdit, "KILLS " + MatchRules.KillTarget(room));
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

    private void OnRoomKillsClicked()
    {
        if (!CanEditRoomSettings()) return;
        SetRoomRule(MatchRules.KillTargetKey, MatchRules.Next(MatchRules.KillOptions, MatchRules.KillTarget(PhotonNetwork.CurrentRoom)));
    }

    private void OnRoomTimeClicked()
    {
        if (!CanEditRoomSettings()) return;
        SetRoomRule(MatchRules.MatchSecondsKey, MatchRules.Next(MatchRules.TimeOptions, MatchRules.MatchSeconds(PhotonNetwork.CurrentRoom)));
    }

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
