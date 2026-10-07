// GameplayManager.Reconnect.cs — กลับเข้าแมตช์เดิมเมื่อเน็ตหลุด (เฟส 9)
// ฝั่งคนที่หลุด: ไม่เด้งกลับล็อบบี้ทันที แต่ค้างอยู่ในฉากต่อสู้ ขึ้นข้อความ "CONNECTION LOST / RECONNECTING..." และลองต่อใหม่ทุก 3 วิ
//   ต่อได้ภายใน 25 วิ = Photon คืนยานเดิมให้ (หรือเกิดใหม่ถ้ายานหาย) เล่นต่อได้เลย คะแนนเดิมยังอยู่ (เก็บใน Player Properties)
//   เกินเวลา = กลับห้องรอแบบเดิม (LobbyManager ใช้ RecoveryRoom)
// ฝั่งคนที่ยังอยู่: คนหลุด (IsInactive) ยังนับว่าอยู่ในแมตช์ 25 วิ ยานของเขาจางลงและไม่โดนดาเมจ (ไม่มีเจ้าของคิดดาเมจ)
//   กลับมาทัน = เล่นต่อ, ไม่ทัน = จัดการแบบเดิม (1v1 ยกเลิกแมตช์ / หลายคน ลบยานแล้วเล่นต่อ)
// ห้องต้องตั้ง PlayerTtl (ห้องของเกมตั้งไว้ 60 วิอยู่แล้ว) ปิด FeatureFlags.BattleReconnect = พฤติกรรมเดิมก่อนเฟส 9
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.SceneManagement;

public partial class GameplayManager
{
    private const float ReconnectGraceSeconds = 25f;

    // ===== ฝั่งเราหลุด =====
    private bool battleReconnecting;
    private float battleReconnectDeadline, nextBattleReconnectTry;
    private float pendingLeftRoomAt = -1f;
    private bool rejoinedNeedsShip;
    private float rejoinShipCheckAt;
    private TMP_Text reconnectLabel;

    private bool BattleReconnectAllowed => FeatureFlags.BattleReconnect && !PhotonNetwork.OfflineMode
        && !intentionalLeave && !resultShown && !returningToRoom && !string.IsNullOrEmpty(battleRoomName);

    // เรียกจาก OnDisconnected: true = จะลองต่อใหม่เอง (ไม่ต้องกลับล็อบบี้)
    private bool TryBeginBattleReconnect(DisconnectCause cause)
    {
        if (!BattleReconnectAllowed) return false;
        switch (cause)
        {
            case DisconnectCause.DisconnectByClientLogic:
            case DisconnectCause.InvalidAuthentication:
            case DisconnectCause.MaxCcuReached:
            case DisconnectCause.InvalidRegion:
            case DisconnectCause.AuthenticationTicketExpired:
            case DisconnectCause.CustomAuthenticationFailed:
            case DisconnectCause.DisconnectByOperationLimit:
                return false;
        }
        if (!battleReconnecting)
        {
            battleReconnecting = true;
            battleReconnectDeadline = Time.unscaledTime + ReconnectGraceSeconds;
            nextBattleReconnectTry = Time.unscaledTime + .5f;
            if (emoteMenu != null) emoteMenu.SetActive(false);
        }
        else nextBattleReconnectTry = Mathf.Min(nextBattleReconnectTry, Time.unscaledTime + 2f);
        ShowReconnectOverlay(true);
        return true;
    }

    // เรียกทุกเฟรมจาก Update
    private void UpdateBattleReconnect()
    {
        // OnLeftRoom ที่ไม่ได้ตั้งใจ: รอดูว่าเป็นเน็ตหลุด (OnDisconnected จะตามมา) ไม่ใช่ = กลับล็อบบี้ตามเดิม
        if (pendingLeftRoomAt > 0 && !battleReconnecting && Time.unscaledTime >= pendingLeftRoomAt)
        {
            pendingLeftRoomAt = -1f;
            SceneManager.LoadScene("LobbyScene");
            return;
        }
        if (!battleReconnecting) { CheckRejoinedShip(); UpdateAwayPilots(); return; }
        float left = battleReconnectDeadline - Time.unscaledTime;
        if (reconnectLabel != null)
            reconnectLabel.text = "CONNECTION LOST\nRECONNECTING... " + Mathf.Max(0, Mathf.CeilToInt(left));
        if (left <= 0f) { GiveUpBattleReconnect(); return; }
        if (Time.unscaledTime < nextBattleReconnectTry) return;
        nextBattleReconnectTry = Time.unscaledTime + 3f;
        if (Application.internetReachability == NetworkReachability.NotReachable) return;
        var state = PhotonNetwork.NetworkClientState;
        if (state == ClientState.Disconnected || state == ClientState.PeerCreated)
        {
            // วิธีหลัก: ต่อใหม่+กลับห้องเดิมในคำสั่งเดียว / สำรอง: ต่อ Master แล้ว RejoinRoom ใน OnConnectedToMaster
            if (!PhotonNetwork.ReconnectAndRejoin() && !PhotonNetwork.Reconnect()) PhotonNetwork.ConnectUsingSettings();
        }
        else if (state == ClientState.ConnectedToMasterServer || state == ClientState.JoinedLobby)
        {
            PhotonNetwork.RejoinRoom(battleRoomName);
        }
    }

    // หมดเวลารอ: กลับห้องรอแบบเดิม
    private void GiveUpBattleReconnect()
    {
        battleReconnecting = false;
        ShowReconnectOverlay(false);
        StopInterruptedBattle();
        RecoveryRoom = battleRoomName;
        SceneManager.LoadScene("LobbyScene");
    }

    public override void OnConnectedToMaster()
    {
        if (battleReconnecting) PhotonNetwork.RejoinRoom(battleRoomName);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        // ห้องหมดอายุ/ที่ถูกยกเลิกแล้ว = ไม่ต้องรอต่อ
        if (battleReconnecting) battleReconnectDeadline = Mathf.Min(battleReconnectDeadline, Time.unscaledTime + .1f);
    }

    public override void OnJoinedRoom()
    {
        if (!battleReconnecting) return;
        battleReconnecting = false;
        pendingLeftRoomAt = -1f;
        ShowReconnectOverlay(false);
        ShowBanner("RECONNECTED", new Color(.45f, 1f, .6f));
        // ถ้าแมตช์ถูกยกเลิกไปแล้ว Photon จะโหลด LobbyScene ให้เอง (AutomaticallySyncScene)
        rejoinedNeedsShip = true;
        rejoinShipCheckAt = Time.unscaledTime + 3f;
    }

    // หลังกลับเข้าห้อง 3 วิ ยังไม่มียานของเรา (ยานถูกลบไปแล้ว) = เกิดใหม่
    private void CheckRejoinedShip()
    {
        if (!rejoinedNeedsShip || Time.unscaledTime < rejoinShipCheckAt) return;
        rejoinedNeedsShip = false;
        if (!PhotonNetwork.InRoom || isMatchEnding || resultShown || localPlayer != null) return;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (!ship.IsBot && ship.photonView.IsMine) return;
        int lives = MatchRules.Lives(PhotonNetwork.CurrentRoom);
        if (lives > 0 && MatchRules.Deaths(PhotonNetwork.LocalPlayer) >= lives) return; // ชีวิตหมดแล้ว = ชมต่อ
        int shipIndex = PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("ShipType", out object value) && value is int index ? index : 0;
        string prefabName = BattleLoadoutCatalog.PrefabName(shipIndex);
        if (isFreeForAll) StartCoroutine(SpawnSlotWhenClear(prefabName, LocalSpawnSlot(), MatchRules.TotalCombatants(PhotonNetwork.CurrentRoom)));
        else
        {
            bool bottom = PhotonNetwork.IsMasterClient;
            PhotonNetwork.Instantiate(prefabName, new Vector3(0f, bottom ? -16f : 16f, 0f), bottom ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f));
        }
    }

    private void ShowReconnectOverlay(bool show)
    {
        if (battleHud == null) return;
        if (reconnectLabel == null)
        {
            if (!show) return;
            reconnectLabel = BattleLabel("ReconnectOverlay", battleHud, "", 0, 40, 900, 120, 34);
            reconnectLabel.color = new Color(1f, .8f, .35f);
            reconnectLabel.outlineWidth = .25f;
            reconnectLabel.outlineColor = Color.black;
            // ปุ่มยอมแพ้การต่อใหม่ กลับล็อบบี้เลย
            var leave = BattlePanel("ReconnectLeave", reconnectLabel.transform, 0, -105, 260, 54, new Color(.35f, .1f, .1f, .95f));
            leave.raycastTarget = true;
            BattleLabel("Label", leave.transform, "BACK TO LOBBY", 0, 0, 250, 50, 20);
            leave.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                battleReconnecting = false;
                ShowReconnectOverlay(false);
                LeaveRoom();
            });
        }
        reconnectLabel.gameObject.SetActive(show);
        if (show) reconnectLabel.text = "CONNECTION LOST\nRECONNECTING...";
    }

    // ===== ฝั่งคนอื่นหลุด =====
    // ActorNumber -> เวลาที่หมดสิทธิ์รอ
    private readonly Dictionary<int, float> awayUntil = new Dictionary<int, float>();
    private readonly HashSet<int> awayExpired = new HashSet<int>();
    private float nextAwayGhostRefresh;

    private bool ReconnectGraceActive => FeatureFlags.BattleReconnect && !PhotonNetwork.OfflineMode
        && !resultShown && !isMatchEnding && !intentionalLeave && !returningToRoom;

    // คนนี้หลุดชั่วคราวและยังอยู่ในเวลารอหรือไม่ (ครั้งแรกที่เห็นจะเริ่มนับเวลา)
    private bool IsAwayInGrace(Player pilot)
    {
        if (pilot == null || !pilot.IsInactive || !ReconnectGraceActive) return false;
        if (awayExpired.Contains(pilot.ActorNumber)) return false;
        if (!awayUntil.ContainsKey(pilot.ActorNumber))
        {
            awayUntil[pilot.ActorNumber] = Time.unscaledTime + ReconnectGraceSeconds;
            AddFeedLine(CleanName(pilot.NickName) + "  lost connection - waiting " + Mathf.RoundToInt(ReconnectGraceSeconds) + "s", new Color(1f, .8f, .35f));
        }
        return true;
    }

    // นับเป็นผู้เล่นที่ยังอยู่ในแมตช์ (ออนไลน์ หรือหลุดแต่ยังรอได้)
    private bool PilotCounts(Player pilot) => pilot != null && (!pilot.IsInactive || IsAwayInGrace(pilot));

    // ตรวจคนที่หลุดทุกเฟรม: กลับมาแล้ว / หมดเวลา
    private void UpdateAwayPilots()
    {
        if (awayUntil.Count == 0 || !PhotonNetwork.InRoom) return;
        List<int> done = null;
        foreach (var pair in awayUntil)
        {
            var pilot = PhotonNetwork.CurrentRoom.GetPlayer(pair.Key);
            bool back = pilot != null && !pilot.IsInactive;
            bool expired = pilot == null || Time.unscaledTime >= pair.Value || !ReconnectGraceActive;
            if (!back && !expired) continue;
            if (done == null) done = new List<int>();
            done.Add(pair.Key);
        }
        if (done != null)
        {
            foreach (int actor in done)
            {
                awayUntil.Remove(actor);
                var pilot = PhotonNetwork.CurrentRoom.GetPlayer(actor);
                if (pilot != null && !pilot.IsInactive)
                {
                    AddFeedLine(CleanName(pilot.NickName) + "  reconnected", new Color(.45f, 1f, .6f));
                    SetGhost(actor, false);
                    continue;
                }
                awayExpired.Add(actor);
                if (intentionalLeave || resultShown) continue;
                // หมดเวลา: ทำแบบเดิม (หลายคน = ลบยานเล่นต่อ, 1v1 = ยกเลิกแมตช์)
                if (pilot != null && HandlePilotLeftFreeForAll(pilot)) continue;
                if (pilot == null && ContinueWithoutMissingPilots()) continue;
                StopInterruptedBattle();
                ReturnToWaitingRoom();
            }
        }
        // ยานของคนที่หลุดจางลง (ทุก 0.5 วิ)
        if (Time.unscaledTime >= nextAwayGhostRefresh)
        {
            nextAwayGhostRefresh = Time.unscaledTime + .5f;
            foreach (int actor in awayUntil.Keys) SetGhost(actor, true);
        }
    }

    private void SetGhost(int actor, bool ghost)
    {
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (ship.IsBot || ship.photonView.OwnerActorNr != actor) continue;
            var sprite = ship.GetComponent<SpriteRenderer>();
            if (sprite == null) continue;
            var color = sprite.color;
            color.a = ghost ? .35f : 1f;
            sprite.color = color;
        }
    }
}
