// LobbyManager.Roster.cs — ตารางนักบินในห้องรอสำหรับห้องหลายคน (partial class ของ LobbyManager)
// ห้อง 1 VS 1 ใช้การ์ดใหญ่ 2 ใบแบบเดิม / ห้อง FFA (MaxPlayers > 2) ซ่อนการ์ดใหญ่แล้วแสดงช่องเล็ก 10 ช่อง (5 x 2)
// แต่ละช่อง: ชื่อ ([HOST]/(YOU)), รูปยานพร้อมสี, ชื่อยาน, สถานะ READY — ช่องที่เกินจำนวนคนสูงสุดของห้องจะซ่อน
// Host เตะคนออกได้: แตะช่อง/การ์ดของคนนั้น 2 ครั้งภายใน 3 วินาที (รายชื่อคนโดนเตะเก็บใน Room Property "Kicked" กันเข้ามาใหม่)
// โหมดทีม (เฟส 3): แถวบน BLUE แถวล่าง RED, เข้าทีมที่คนน้อยกว่าอัตโนมัติ, แตะช่องตัวเองเพื่อย้ายทีม
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

public partial class LobbyManager
{
    private sealed class RosterSlot
    {
        public Image panel;
        public TMP_Text name;
        public Image ship;
        public TMP_Text shipName;
        public TMP_Text ready;
    }

    private RectTransform rosterRoot;
    private RosterSlot[] rosterSlots;
    private GameObject pilotCardOne, pilotCardTwo;

    // สร้างตาราง 10 ช่องในพื้นที่เดียวกับการ์ดผู้เล่น 2 ใบ (เรียกจาก BuildLobbyUI)
    private void BuildRoster(RectTransform root)
    {
        var one = root.Find("PilotOne");
        var two = root.Find("PilotTwo");
        pilotCardOne = one != null ? one.gameObject : null;
        pilotCardTwo = two != null ? two.gameObject : null;
        // การ์ดใหญ่ 1 VS 1 ก็แตะเพื่อเตะได้เหมือนกัน
        MakeKickTarget(pilotCardOne, 100);
        MakeKickTarget(pilotCardTwo, 101);
        rosterRoot = UIRect("PilotRoster", root, 0, 108, 1180, 222);
        rosterSlots = new RosterSlot[MatchRules.MaxCombatants];
        for (int i = 0; i < rosterSlots.Length; i++)
        {
            float x = -472 + (i % 5) * 236;
            float y = i < 5 ? 56 : -56;
            var panel = UIPanel("Slot" + i, rosterRoot, x, y, 226, 104, panelColor);
            var slot = new RosterSlot
            {
                panel = panel,
                name = UILabel("Pilot", panel.transform, "", 0, 34, 214, 28, 17, Color.white),
                ship = UIPanel("Ship", panel.transform, -70, -14, 76, 62, Color.clear),
                shipName = UILabel("ShipName", panel.transform, "", 38, 0, 140, 26, 14, accentColor),
                ready = UILabel("Ready", panel.transform, "", 38, -30, 140, 26, 15, accentColor)
            };
            slot.name.richText = false;
            slot.ship.preserveAspect = true;
            MakeKickTarget(panel.gameObject, i);
            rosterSlots[i] = slot;
        }
        rosterRoot.gameObject.SetActive(false);
    }

    // ผู้เล่นที่อยู่ในแต่ละช่องตอนวาดล่าสุด (ใช้ตอนแตะช่อง)
    private readonly Player[] rosterSlotPlayers = new Player[MatchRules.MaxCombatants];
    private float teamRequestUntil;

    // เรียกจาก UpdateWaitingRoomUI: ห้อง FFA/ทีม = แสดงตาราง, ห้อง 1 VS 1 = แสดงการ์ด 2 ใบแบบเดิม
    // โหมดทีม: แถวบน = ทีม BLUE, แถวล่าง = ทีม RED (ทีมละ ขนาดห้อง/2 ช่อง)
    private void RenderRoster(Player[] players)
    {
        if (rosterRoot == null) return;
        var room = PhotonNetwork.CurrentRoom;
        bool multi = FeatureFlags.MultiPlayer && room != null && room.MaxPlayers > 2;
        rosterRoot.gameObject.SetActive(multi);
        if (pilotCardOne != null) pilotCardOne.SetActive(!multi);
        if (pilotCardTwo != null) pilotCardTwo.SetActive(!multi);
        System.Array.Clear(rosterSlotPlayers, 0, rosterSlotPlayers.Length);
        if (!multi) return;
        bool teams = MatchRules.IsTeamRoom(room) && room.MaxPlayers >= 4;
        int teamSize = room.MaxPlayers / 2;
        if (teams) EnsureLocalTeam(players, teamSize);

        // จัดคนลงช่อง: FFA เรียงตามลำดับ / ทีม: ใส่แถวของทีมตัวเอง (ถ้าเต็มไปช่องว่างแถวอื่น)
        if (!teams)
            for (int i = 0; i < players.Length && i < rosterSlotPlayers.Length; i++) rosterSlotPlayers[i] = players[i];
        else
        {
            var overflow = new List<Player>();
            int[] filled = new int[2];
            foreach (var player in players)
            {
                int team = PlayerTeam(player);
                if (team >= 0 && filled[team] < teamSize) rosterSlotPlayers[team * 5 + filled[team]++] = player;
                else overflow.Add(player);
            }
            foreach (var player in overflow)
                for (int t = 0; t < 2; t++)
                    if (filled[t] < teamSize) { rosterSlotPlayers[t * 5 + filled[t]++] = player; break; }
        }

        for (int i = 0; i < rosterSlots.Length; i++)
        {
            var slot = rosterSlots[i];
            int rowTeam = i < 5 ? 0 : 1;
            bool open = teams ? i % 5 < teamSize : i < room.MaxPlayers;
            slot.panel.gameObject.SetActive(open);
            if (!open) continue;
            slot.panel.color = teams ? Color.Lerp(panelColor, MatchRules.TeamColors[rowTeam], .28f) : panelColor;
            Player player = rosterSlotPlayers[i];
            bool present = player != null;
            int ship = present ? Mathf.Clamp(IntProperty(player.CustomProperties, ShipProperty, 0), 0, ships.Length - 1) : 0;
            slot.name.text = present ? (player.IsMasterClient ? "[HOST] " : "") + LevelTag(player) + player.NickName + (player.IsLocal ? " (YOU)" : "")
                : MatchRules.BotFill(room) ? "BOT WILL JOIN" : teams ? MatchRules.TeamNames[rowTeam] + " SLOT" : "OPEN SLOT";
            slot.name.color = present && player.IsLocal ? new Color(1f, .87f, .4f) : Color.white;
            slot.shipName.text = present ? ships[ship].name : "";
            slot.ship.sprite = present ? BattleLoadoutCatalog.ShipSprite(ship) : null;
            slot.ship.color = present ? ShipPaint.For(player) * BattleLoadoutCatalog.ShipTint(ship) : Color.clear;
            bool ready = IsPlayerReady(player);
            slot.ready.text = !present ? "" : KickArmed(player) ? "TAP AGAIN = KICK" : player.IsInactive ? "RECONNECTING"
                : teams && player.IsLocal && !ready ? "TAP = SWITCH TEAM" : ready ? "READY" : "NOT READY";
            slot.ready.color = present && KickArmed(player) ? new Color(1f, .35f, .35f) : ready ? new Color(0.35f, 1f, 0.7f) : new Color(1f, 0.73f, 0.35f);
        }
    }

    // ทีมที่ผู้เล่นเลือก (-1 = ยังไม่เลือก)
    private static int PlayerTeam(Player player)
        => player != null && player.CustomProperties.TryGetValue(MatchRules.PlayerTeamProperty, out object v) && v is int t && (t == 0 || t == 1) ? t : -1;

    // ยังไม่มีทีม = เข้าทีมที่คนน้อยกว่าอัตโนมัติ
    private void EnsureLocalTeam(Player[] players, int teamSize)
    {
        if (PlayerTeam(PhotonNetwork.LocalPlayer) >= 0 || Time.unscaledTime < teamRequestUntil) return;
        int[] count = new int[2];
        foreach (var player in players) { int t = PlayerTeam(player); if (t >= 0) count[t]++; }
        SetLocalTeam(count[0] <= count[1] ? 0 : 1);
    }

    private void SetLocalTeam(int team)
    {
        teamRequestUntil = Time.unscaledTime + 1f;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [MatchRules.PlayerTeamProperty] = team });
    }

    // แตะช่องของตัวเองในโหมดทีม = ย้ายไปอีกทีม (ถ้าอีกทีมยังมีที่ว่าง และยังไม่กด READY)
    private void TrySwitchTeam()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null || !MatchRules.IsTeamRoom(room) || room.MaxPlayers < 4 || RoomStarting || isStartingGame) return;
        if (IsPlayerReady(PhotonNetwork.LocalPlayer)) { UpdateStatus("Cancel READY before switching team."); return; }
        int mine = Mathf.Max(0, PlayerTeam(PhotonNetwork.LocalPlayer));
        int other = 1 - mine, inOther = 0;
        foreach (var player in PhotonNetwork.PlayerList) if (!player.IsLocal && PlayerTeam(player) == other) inOther++;
        if (inOther >= room.MaxPlayers / 2) { UpdateStatus(MatchRules.TeamNames[other] + " team is full."); return; }
        SetLocalTeam(other);
        UpdateStatus("Switched to " + MatchRules.TeamNames[other] + " team.");
    }

    // ===== Host เตะผู้เล่น =====
    private const string KickedProperty = "Kicked";
    private int kickArmedActor = -1;
    private float kickArmedUntil;
    private bool wasKicked;

    private bool KickArmed(Player player)
        => player != null && player.ActorNumber == kickArmedActor && Time.unscaledTime < kickArmedUntil;

    // ใส่ปุ่มให้การ์ด/ช่อง (index = ลำดับผู้เล่นเรียงตาม ActorNumber แบบเดียวกับที่แสดง)
    private void MakeKickTarget(GameObject target, int index)
    {
        if (target == null) return;
        var image = target.GetComponent<Image>();
        if (image == null) return;
        image.raycastTarget = true;
        var button = target.GetComponent<Button>();
        if (button == null) button = target.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnPilotSlotClicked(index));
    }

    // index 0-9 = ช่องในตาราง, 100/101 = การ์ดใหญ่ใบซ้าย/ขวาของห้อง 1 VS 1
    private void OnPilotSlotClicked(int index)
    {
        if (!PhotonNetwork.InRoom) return;
        Player target;
        if (index >= 100)
        {
            Player[] players = PhotonNetwork.PlayerList;
            System.Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
            target = index - 100 < players.Length ? players[index - 100] : null;
        }
        else target = index >= 0 && index < rosterSlotPlayers.Length ? rosterSlotPlayers[index] : null;
        if (target == null) return;
        // แตะช่องตัวเอง = ย้ายทีม (โหมดทีม)
        if (target.IsLocal) { TrySwitchTeam(); UpdateWaitingRoomUI(); return; }
        if (!PhotonNetwork.IsMasterClient || !CanEditRoomSettings()) return;
        // แตะครั้งแรก = เตรียมเตะ (กันกดพลาด), แตะซ้ำภายใน 3 วินาที = เตะจริง
        if (!KickArmed(target))
        {
            kickArmedActor = target.ActorNumber;
            kickArmedUntil = Time.unscaledTime + 3f;
            UpdateStatus("Tap " + target.NickName + " again to remove them from the room.");
            UpdateWaitingRoomUI();
            return;
        }
        kickArmedActor = -1;
        var list = new List<string>(KickedList());
        list.Add("#" + target.ActorNumber);
        if (target.CustomProperties.TryGetValue(FirebaseUidProperty, out object uid) && uid is string text && !string.IsNullOrWhiteSpace(text)) list.Add(text);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [KickedProperty] = list.ToArray() });
        UpdateStatus(target.NickName + " was removed from the room.");
    }

    private static string[] KickedList()
    {
        var room = PhotonNetwork.CurrentRoom;
        return room != null && room.CustomProperties.TryGetValue(KickedProperty, out object value) && value is string[] list ? list : new string[0];
    }

    // เรียกตอนเข้าห้อง และเมื่อรายชื่อคนโดนเตะเปลี่ยน: ถ้าเราอยู่ในรายชื่อ ให้ออกจากห้องทันที
    private void CheckKicked()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode || isLeavingRoom) return;
        string me = "#" + PhotonNetwork.LocalPlayer.ActorNumber;
        string uid = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetUserId() : "";
        foreach (string entry in KickedList())
        {
            if (entry != me && (string.IsNullOrWhiteSpace(uid) || entry != uid)) continue;
            wasKicked = true;
            isLeavingRoom = PhotonNetwork.LeaveRoom(false);
            return;
        }
    }
}
