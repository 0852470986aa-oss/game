// GameplayManager.cs — ไฟล์หลักของ partial class GameplayManager อยู่ใน Scene SampleScene (ฉากต่อสู้)
// หน้าที่: เริ่มแมตช์พร้อมกันทุกเครื่อง (1v1 หรือหลายคน FFA) นับเวลา นับ Kill (ถึงเป้าก่อนชนะ / หมดเวลาคะแนนเท่ากัน = เสมอ)
// จัดการคนหลุด/ออกห้อง (ยกเลิกแมตช์กลับห้องรอ) เกิดยานของเรา และอัปเดตเลือด/HUD ทุกเฟรม
// ไฟล์ partial อื่นของคลาสนี้: .HUD.cs (สร้าง/ผูก UI), .Results.cs (หน้าผล+Firebase), .StatusUI.cs (สถานะ/สกิล), .Maps.cs (แม็พ)
// .Bots.cs (บอท/บทสอน), .Multi.cs (ห้องหลายคน: จุดเกิด ตารางคะแนน Kill Feed หน้าผลจัดอันดับ)
// ทำงานคู่กับ PlayerController (ยานแต่ละลำ) และ LobbyManager (ห้องรอใน LobbyScene)
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

// ตัวประสานงานฉากต่อสู้และสถานะห้อง Photon; ส่วน HUD/แม็พ/ผลแข่งแยกไว้ในไฟล์ partial ข้างเคียง
// สืบทอด MonoBehaviourPunCallbacks เพื่อรับ Callback ของ Photon เช่น OnPlayerLeftRoom / OnDisconnected / OnLeftRoom
public partial class GameplayManager : MonoBehaviourPunCallbacks
{
    // ชื่อห้องที่ควรกลับเข้าใหม่หลังหลุดโดยไม่ตั้งใจ (null = ไม่ต้องกู้) เป็น static เพื่อให้ LobbyManager อ่านได้หลังเปลี่ยนฉาก
    public static string RecoveryRoom;
    // ชื่อห้อง Photon ของแมตช์นี้ (จำไว้ใช้ตอนหลุดการเชื่อมต่อ)
    private string battleRoomName;
    // returningToRoom = กำลังพากลับห้องรอแล้ว (กันทำซ้ำ), intentionalLeave = ผู้เล่นกดออกเอง (ไม่นับว่าหลุด)
    // requestedRematch = กดปุ่มกลับห้องเดิมในหน้าผลแล้ว, rematchLabel = ข้อความบนปุ่มนั้น
    private bool returningToRoom;
    private bool intentionalLeave;
    private bool requestedRematch;
    private TMP_Text rematchLabel;


    // หยุดการต่อสู้ทันที: ตั้งว่าแมตช์กำลังจบ แล้วสั่งยานทุกลำในเครื่องนี้ให้หยุด (เรียกเมธอดตรงในเครื่อง ไม่ได้ส่ง RPC)
    // ใช้ตอนแมตช์ถูกยกเลิก เช่น อีกฝ่ายหลุด/ออก เราหลุดการเชื่อมต่อ หรือเรากดออกเอง
    private void StopInterruptedBattle()
    {
        isMatchEnding = true;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            ship.SetMatchEndedRPC();
    }

    // Callback ของ Photon (รันบนเครื่องที่ยังอยู่ในห้อง) เมื่ออีกฝ่ายออกจากห้อง
    // ถ้ายังไม่ถึงหน้าผลและเราไม่ได้กดออกเอง = ยกเลิกแมตช์ แล้วพากลับห้องรอ (กติกา: หลุด = ยกเลิกแมตช์)
    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        if (intentionalLeave || resultShown) return;
        // เฟส 9: หลุดชั่วคราว = รอให้กลับมาก่อน 25 วิ (GameplayManager.Reconnect.cs)
        if (IsAwayInGrace(otherPlayer)) return;
        awayUntil.Remove(otherPlayer.ActorNumber);
        // ห้องหลายคนที่เริ่มแล้ว: คนที่เหลือเล่นต่อได้ (ดู GameplayManager.Multi.cs)
        if (HandlePilotLeftFreeForAll(otherPlayer)) return;
        StopInterruptedBattle();
        ReturnToWaitingRoom();
    }

    // Callback ของ Photon เมื่อ Master Client เปลี่ยนคน (เช่น Master เดิมหลุด)
    // นับผู้เล่นที่ยังออนไลน์ ถ้าเหลือไม่ถึง 2 คน = ยกเลิกแมตช์แล้วกลับห้องรอ
    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (intentionalLeave || resultShown || !PhotonNetwork.InRoom) return;
        // Master ใหม่รับช่วงควบคุมยานบอทต่อ (บอทเป็นวัตถุของห้อง)
        TakeOverBots();
        int active = 0;
        foreach (var pilot in PhotonNetwork.PlayerList) if (PilotCounts(pilot)) active++;
        if (active < MatchRules.ExpectedHumans(PhotonNetwork.CurrentRoom) && !ContinueWithoutMissingPilots()) { StopInterruptedBattle(); ReturnToWaitingRoom(); }
    }

    // พาห้องกลับ LobbyScene (ห้องรอ) เมื่อแมตช์ถูกยกเลิก — ทำเฉพาะบน Master Client และทำครั้งเดียว
    // รีเซ็ต Room Properties (Starting/StartTime/BattleToken และตั้ง BattleAborted = true) เปิดห้องให้เข้า/มองเห็นได้อีกครั้ง
    // ลบวัตถุเครือข่ายของแมตช์ทั้งหมด แล้วโหลด LobbyScene ด้วย PhotonNetwork.LoadLevel
    private void ReturnToWaitingRoom()
    {
        if (returningToRoom || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        returningToRoom = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        { ["Starting"] = false, ["StartTime"] = -1d, ["BattleToken"] = "", ["BattleAborted"] = true });
        PhotonNetwork.CurrentRoom.IsOpen = true;
        PhotonNetwork.CurrentRoom.IsVisible = true;
        // Remove cached battle objects so a reconnect cannot revive the cancelled match.
        PhotonNetwork.DestroyAll();
        PhotonNetwork.LoadLevel("LobbyScene");
    }

    // Callback ของ Photon เมื่อเครื่องนี้หลุดจากเซิร์ฟเวอร์: หยุดการต่อสู้ แล้วกลับ LobbyScene
    // ถ้าหลุดเองโดยไม่ได้ตั้งใจ (ไม่ได้กดออก และไม่ได้สั่งตัดการเชื่อมต่อจากโค้ด) จะจำชื่อห้องไว้ใน RecoveryRoom
    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        // เฟส 9: ค้างในฉากต่อสู้แล้วลองต่อใหม่ (GameplayManager.Reconnect.cs)
        if (TryBeginBattleReconnect(cause)) return;
        StopInterruptedBattle();
        RecoveryRoom = !intentionalLeave && cause != Photon.Realtime.DisconnectCause.DisconnectByClientLogic
            ? battleRoomName : null;
        SceneManager.LoadScene("LobbyScene");
    }

    // Singleton: สคริปต์อื่น (เช่น PlayerController) เรียกผ่าน GameplayManager.Instance ได้ (ตั้งค่าใน Awake)
    public static GameplayManager Instance;


    // ปุ่มควบคุมบนจอมือถือ (ลากใส่ใน Inspector): จอยสติ๊กเคลื่อนที่ ปุ่มยิง ปุ่มสกิล รูปคูลดาวน์และไอคอนสกิล
    [Header("UI Controls")]
    public UIJoystick joystick;
    public UIButton fireButton;
    public UIButton skillButton;
    public Image skillCooldownImage;
    public Image skillIconImage;

    // ข้อความ Ping และข้อความชื่อผู้เล่น (playerInfoText ถูกใช้เป็นตัวอ้างอิง Canvas ของมินิแม็พด้วย)
    [Header("UI Text")]
    public TMP_Text pingText;
    public TMP_Text playerInfoText;

    // ยานของเรา (ตั้งผ่าน SetLocalPlayer) และยานศัตรู (ค้นหาเองใน Update)
    [Header("Players")]
    public PlayerController localPlayer;
    public PlayerController remotePlayer;

    // ความยาวแมตช์ หน่วยวินาที (180 = 3 นาที) หมดเวลาแล้วตัดสินจาก Kill ถ้าเท่ากัน = เสมอ
    [Header("Match Settings")]
    public float matchDuration = 180f; // 3 minutes
    // เวลาที่เหลือ (วินาที) คำนวณจาก StartTime ของห้อง
    private float matchTimer;
    // true เมื่อนับถอยหลังเริ่มเกมครบแล้ว (ก่อนหน้านั้นบังคับยานไม่ได้)
    private bool matchStarted = false;
    // ข้อความนับถอยหลัง 3-2-1 / GO! กลางจอ
    private TMP_Text battleCountdownText;
    // ลูกศรบอกทิศที่ถูกยิง: กรอบที่หมุนรอบยานเรา, แถบสีแดง, เวลาที่จะซ่อน (unscaledTime) และตำแหน่งคนยิง
    private RectTransform damageDirectionRoot;
    private Image damageDirectionMarker;
    private float damageDirectionUntil;
    private Vector3 damageSourcePosition;

    // แสดงลูกศรชี้ไปทางคนที่ยิงเรา ค้างไว้ 1.1 วินาที — เรียกจาก RPC TakeDamage บนเครื่องเจ้าของยาน (เรา) ตอนโดนดาเมจ
    // shooterId = ActorNumber ของคนยิง ถ้าหายานคนยิงไม่เจอ (เช่น ดาเมจจากสิ่งแวดล้อม) จะไม่แสดง
    public void ShowIncomingDamage(int shooterId)
    {
        if (localPlayer == null || battleHud == null || isMatchEnding || shooterId <= 0) return;
        PlayerController source = null;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (ship.CombatantId == shooterId && ship != localPlayer) { source = ship; break; }
        if (source == null) return; // Do not invent a direction for environmental damage.
        damageSourcePosition = source.transform.position;
        damageDirectionUntil = Time.unscaledTime + 1.1f;
        if (damageDirectionRoot == null) CreateIncomingDamageMarker();
    }

    // สร้างลูกศรบอกทิศดาเมจด้วยโค้ด (สร้างครั้งแรกตอนต้องใช้ หรือสร้างลง Scene จากเมนู Editable)
    private void CreateIncomingDamageMarker()
    {
        damageDirectionRoot = BattleRect("IncomingDamage", battleHud, 0, 0, 1, 1);
        damageDirectionMarker = BattlePanel("Direction", damageDirectionRoot, 0, 118, 34, 7, new Color(1, .2f, .1f));
        BattlePanel("Tip", damageDirectionRoot, 0, 127, 10, 10, new Color(1, .45f, .15f));
    }

    // เรียกทุกเฟรมจาก Update: วางลูกศรไว้ที่ตำแหน่งยานเราบนจอ แล้วหมุนให้ชี้ไปทางตำแหน่งคนยิง
    // ค่อยๆ จางหายช่วงท้าย และซ่อนเมื่อหมดเวลา / ยานเราตาย / แมตช์จบ
    private void UpdateDamageDirection()
    {
        if (damageDirectionRoot == null) return;
        bool visible = localPlayer != null && !localPlayer.isDead && !isMatchEnding
            && Time.unscaledTime < damageDirectionUntil;
        damageDirectionRoot.gameObject.SetActive(visible);
        if (!visible) return;
        var camera = Camera.main;
        if (camera == null) { damageDirectionRoot.gameObject.SetActive(false); return; }
        Vector3 shipScreen = camera.WorldToScreenPoint(localPlayer.transform.position);
        Vector3 sourceScreen = camera.WorldToScreenPoint(damageSourcePosition);
        var canvas = battleHud.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(battleHud, shipScreen, uiCamera, out Vector2 shipUI);
        damageDirectionRoot.anchoredPosition = shipUI;
        Vector2 direction = sourceScreen - shipScreen;
        damageDirectionRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        Color color = damageDirectionMarker.color;
        color.a = Mathf.Clamp01((damageDirectionUntil - Time.unscaledTime) * 2f);
        damageDirectionMarker.color = color;
    }
    // ข้อความ "HIT" / "SHIELD HIT" ยืนยันว่ายิงโดน และเวลาที่จะซ่อน
    private TMP_Text hitConfirmationText;
    private float hitConfirmationUntil;

    // แสดงข้อความยืนยันว่าเรายิงโดน 0.3 วินาที พร้อมเสียง (shield = true แสดง SHIELD HIT สีฟ้า)
    // รันบนเครื่องคนยิง เมื่อได้ RPC ConfirmProjectileHitRPC จากเจ้าของยานที่โดน
    public void ShowConfirmedHit(bool shield)
    {
        if (battleHud == null || isMatchEnding) return;
        if (hitConfirmationText == null) CreateHitConfirmation();
        hitConfirmationText.text = shield ? "SHIELD HIT" : "HIT";
        hitConfirmationText.color = shield ? new Color(.3f, .85f, 1f) : new Color(1f, .8f, .3f);
        hitConfirmationUntil = Time.unscaledTime + .3f;
        hitConfirmationText.gameObject.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(shield ? "SFX_ShieldHit" : "SFX_Hit");
    }
    // สร้างข้อความยืนยันการยิงโดน (ใต้กึ่งกลางจอเล็กน้อย)
    private void CreateHitConfirmation()
    {
        hitConfirmationText = BattleLabel("HitConfirmation", battleHud, "", 0, -100, 260, 38, 22);
    }

    // หน้าต่าง "SHIP DESTROYED" ตอนยานเราตาย: แสดงสาเหตุการตาย และนับถอยหลังเกิดใหม่
    private Image respawnPanel;
    private TMP_Text respawnReasonText;
    private TMP_Text respawnTimerText;

    // เรียกทุกเฟรม: แสดงหน้าต่างตายเมื่อยานเราตายและแมตช์ยังไม่จบ
    // นับถอยหลังจาก RespawnReadyAt ของ PlayerController ถ้าถึงเวลาแล้วจะแสดงว่ากำลังหาจุดเกิดที่ปลอดภัย
    private void UpdateRespawnHUD()
    {
        bool visible = localPlayer != null && localPlayer.isDead && !isMatchEnding && !localPlayer.HasMatchEnded;
        if (visible && respawnPanel == null && battleHud != null) CreateRespawnPanel();
        if (respawnPanel == null) return;
        respawnPanel.gameObject.SetActive(visible);
        if (!visible) return;
        respawnReasonText.text = localPlayer.DeathReason;
        int seconds = Mathf.CeilToInt(localPlayer.RespawnReadyAt - Time.unscaledTime);
        respawnTimerText.text = !CanRespawn(localPlayer) ? "OUT OF LIVES  -  SPECTATING"
            : seconds > 0 ? "RESPAWNING IN " + seconds : "FINDING A SAFE SPAWN...";
    }
    // สร้างหน้าต่างตาย (หัวข้อ / สาเหตุ / นับถอยหลัง) ด้วยโค้ด
    private void CreateRespawnPanel()
    {
        respawnPanel = BattlePanel("RespawnPanel", battleHud, 0, 5, 650, 160, new Color(.035f, .055f, .12f, .94f));
        BattleLabel("Title", respawnPanel.transform, "SHIP DESTROYED", 0, 48, 610, 38, 30);
        respawnReasonText = BattleLabel("Reason", respawnPanel.transform, "", 0, 6, 610, 32, 20);
        respawnReasonText.richText = false;
        respawnTimerText = BattleLabel("Countdown", respawnPanel.transform, "", 0, -43, 610, 36, 25);
        respawnTimerText.color = new Color(.25f, .95f, 1f);
    }

    // เวลาถัดไปที่จะเช็ค/ส่งสถานะพร้อม (ทำทุก 1 วินาที ไม่ใช่ทุกเฟรม)
    private float nextReadyUpdate;
    // อนุญาตให้บังคับยานไหม: แมตช์ยังไม่จบ และเริ่มแล้ว (ถ้าไม่ได้อยู่ในห้อง เช่น ทดสอบออฟไลน์ ให้เล่นได้เลย)
    public bool MatchInputAllowed => !isMatchEnding && (!PhotonNetwork.InRoom || matchStarted);

    // ระบบเริ่มแมตช์พร้อมกัน (เรียกทุกเฟรมจาก Update บนทุกเครื่อง)
    // ใช้ BattleToken (รหัสของรอบแข่ง) + LoadedBattleToken ของผู้เล่นแต่ละคน เพื่อรู้ว่าทั้งคู่โหลดฉากเสร็จแล้ว
    // Master Client ตั้ง StartTime = PhotonNetwork.Time + 3 วินาที แล้วทุกเครื่องนับถอยหลังด้วยเวลาเซิร์ฟเวอร์เดียวกัน
    private void UpdateBattleStart()
    {
        if (!PhotonNetwork.InRoom || isMatchEnding) return;
        // 1) อ่าน BattleToken และ StartTime (-1 = ยังไม่ได้ตั้ง) จาก Room Properties
        var room = PhotonNetwork.CurrentRoom;
        room.CustomProperties.TryGetValue("BattleToken", out object tokenValue);
        string token = tokenValue as string;
        double start = room.CustomProperties.TryGetValue("StartTime", out object value) && value is double timestamp
            ? timestamp : -1d;
        // 2) พร้อมทั้งคู่ = มี token, มีผู้เล่น 2 คน, ทุกคนยังออนไลน์และรายงาน LoadedBattleToken ตรงกับ token
        // จำนวนคนจริงที่ต้องมี = 2 - จำนวนบอท (เล่นกับบอทใช้คนเดียวได้) และบอทต้องเกิดครบก่อน
        // ห้องหลายคน: ต้องครบตามจำนวนที่ Host เริ่มไว้เหมือนกัน (Humans) แต่ถ้านับถอยหลังแล้วมีคนหลุด จะไม่ยกเลิก
        bool bothReady = !string.IsNullOrEmpty(token) && PhotonNetwork.PlayerList.Length == MatchRules.ExpectedHumans(room)
            && (MatchRules.BotCount(room) == 0 || botsPresent);
        foreach (var pilot in PhotonNetwork.PlayerList)
            bothReady &= !pilot.IsInactive && pilot.CustomProperties.TryGetValue("LoadedBattleToken", out object loaded)
                && Equals(loaded, token);

        // 3) ทุก 1 วินาที (ก่อนเริ่ม): ถ้ายานเราเกิดแล้ว ให้บอกห้องว่าเครื่องเราโหลดเสร็จ (ตั้ง LoadedBattleToken)
        if (!matchStarted && Time.unscaledTime >= nextReadyUpdate)
        {
            nextReadyUpdate = Time.unscaledTime + 1f;
            if (localPlayer != null && !string.IsNullOrEmpty(token))
            {
                if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("LoadedBattleToken", out object loaded)
                    || !Equals(loaded, token))
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["LoadedBattleToken"] = token });
            }
            // 4) เฉพาะ Master: ยังไม่มี token -> สร้างใหม่, พร้อมทั้งคู่ -> ตั้งเวลาเริ่มอีก 3 วิ, มีคนไม่พร้อมก่อนถึงเวลา -> ยกเลิกเวลาเริ่ม
            // อาร์กิวเมนต์ตัวที่ 2 ของ SetCustomProperties คือค่าที่คาดว่าห้องมีอยู่ (ตั้งค่าเฉพาะเมื่อค่าเดิมตรง กันเขียนทับกัน)
            // บอท: Master สร้างยานบอทเมื่อรอบแข่งพร้อม (ดู GameplayManager.Bots.cs) และเช็คว่าบอทเกิดครบหรือยัง
            if (!string.IsNullOrEmpty(token) && localPlayer != null) SpawnBotsIfNeeded();
            botsPresent = CountBots() >= MatchRules.BotCount(room);
            if (PhotonNetwork.IsMasterClient)
            {
                if (string.IsNullOrEmpty(token))
                    room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
                    { ["BattleToken"] = System.Guid.NewGuid().ToString("N"), ["StartTime"] = -1d });
                else if (bothReady && start < 0)
                    room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["StartTime"] = PhotonNetwork.Time + 3d },
                        new ExitGames.Client.Photon.Hashtable { ["StartTime"] = -1d });
                else if (!bothReady && start > PhotonNetwork.Time)
                    room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["StartTime"] = -1d },
                        new ExitGames.Client.Photon.Hashtable { ["StartTime"] = start });
            }
        }
        // 5) ถึงเวลาเริ่มแล้ว -> matchStarted = true (ปลดล็อกการบังคับยาน)
        if ((bothReady || isFreeForAll) && start >= 0 && PhotonNetwork.Time >= start) matchStarted = true;
        // 6) แสดงข้อความ: "WAITING FOR PILOTS" / ตัวเลขนับถอยหลัง / "GO!" (ค้าง 1 วินาทีหลังเริ่ม)
        if (battleCountdownText == null && battleHud != null) CreateBattleCountdown();
        if (battleCountdownText != null)
        {
            battleCountdownText.gameObject.SetActive(!matchStarted || PhotonNetwork.Time - start < 1d);
            battleCountdownText.text = matchStarted ? "GO!" : !bothReady || start < 0
                ? "WAITING FOR PILOTS" : Mathf.Max(1, Mathf.CeilToInt((float)(start - PhotonNetwork.Time))).ToString();
        }
    }
    // สร้างข้อความนับถอยหลังตัวใหญ่กลางจอ
    private void CreateBattleCountdown()
    {
        battleCountdownText = BattleLabel("BattleCountdown", battleHud, "", 0, 40, 800, 100, 48);
    }

    // true เมื่อแมตช์จบหรือถูกยกเลิก ใช้หยุดการอัปเดตเวลา คะแนน และ HUD ต่างๆ
    private bool isMatchEnding = false;

    // ข้อความเวลาที่เหลือ (MM:SS) และคะแนน Kill (สร้างใน CreateMatchUI)
    private TMP_Text matchTimerText;
    private TMP_Text scoreText;

    // อ้างอิง UI หน้าผล: กรอบสี ชื่อ รูปยาน สถานะ เหรียญที่ได้ ของเราและคู่แข่ง + ปุ่มกลับ Lobby (สร้าง/ผูกใน BuildResultUI)
    [Header("Result UI")]
    public GameObject resultPanel;
    public TMP_Text resultRoomNumber;
    public UnityEngine.UI.Outline localResultOutline;
    public TMP_Text localResultName;
    public Image localResultShip;
    public TMP_Text localResultStatus;
    public TMP_Text localResultCoins;
    public UnityEngine.UI.Outline remoteResultOutline;
    public TMP_Text remoteResultName;
    public Image remoteResultShip;
    public TMP_Text remoteResultStatus;
    public TMP_Text remoteResultCoins;
    public Button btnReturnToMenu;
    
    // ภาพพื้นหลังแม็พ, ขอบจอแดงเตือนเลือดต่ำ, สวิตช์สร้างสิ่งกีดขวางอัตโนมัติตอนเริ่ม
    [Header("Map UI")]
    public SpriteRenderer backgroundSprite;
    public GameObject lowHPWarning; // ขอบจอแดงเมื่อเลือดต่ำ
    public bool autoGenerateMap = true; // เปิด/ปิด การเสกอุกกาบาตอัตโนมัติ

    // ข้อความและแถบเลือดของผู้เล่น 1 (เรา) และผู้เล่น 2 (ศัตรู)
    private TMP_Text p1HpText, p2HpText;
    private RectTransform p1HpFill, p2HpFill;

    // Awake: ตั้ง Singleton ให้ Instance ชี้มาที่ตัวนี้ (ทำงานก่อน Start ของทุกสคริปต์)
    void Awake()
    {
        Instance = this;
    }

    // --- PREFAB CACHE ---
    // แคช Prefab ที่โหลดจาก Resources (key = path) จะได้ไม่ต้องโหลดซ้ำ
    private static System.Collections.Generic.Dictionary<string, GameObject> prefabCache = new System.Collections.Generic.Dictionary<string, GameObject>();

    // คืน Prefab จาก Resources ตามชื่อ ถ้ายังไม่เคยโหลดจะโหลดแล้วเก็บแคชไว้
    public static GameObject GetPrefab(string name)
    {
        if (!prefabCache.ContainsKey(name))
        {
            prefabCache[name] = Resources.Load<GameObject>(name);
        }
        return prefabCache[name];
    }
    // --------------------

    // Start: เรียกครั้งเดียวเมื่อเข้า SampleScene (ทุกเครื่องรันของตัวเอง)
    // เตรียมกล้อง/มินิแม็พ สร้าง HUD และหน้าผล จัดแม็พตาม MapIndex รีเซ็ต Kill แล้วสร้างยานของเราด้วย PhotonNetwork.Instantiate
    void Start()
    {
        // 1) จำชื่อห้อง และเปิดเพลงต่อสู้
        if (PhotonNetwork.InRoom) battleRoomName = PhotonNetwork.CurrentRoom.Name;
        // กติกาที่ Host ตั้งไว้ในห้องรอ (ไม่มีค่า = 3 Kill / 3 นาที แบบเดิม) ดู MatchRules.cs
        if (PhotonNetwork.InRoom)
        {
            targetKills = MatchRules.KillTarget(PhotonNetwork.CurrentRoom);
            matchDuration = MatchRules.MatchSeconds(PhotonNetwork.CurrentRoom);
        }
        // ห้องหลายคน: ตั้งความถี่ส่งข้อมูล / ระยะเกิดใหม่ (ดู GameplayManager.Multi.cs)
        ConfigureMultiplayerMatch();
        // เฟส 4: รีเซ็ตตัวนับของแมตช์นี้ (GameplayManager.Progress.cs)
        ResetMatchProgress();
        // เฟส 7B: Master ล้างคะแนนโหมด/ระลอก/บอทของแมตช์ก่อน (ห้องเดิมเล่นซ้ำ)
        ClearModeLeftovers();
        // เฟส 7: ไอเท็มเกิดในแม็พ (PowerUpManager.cs)
        if (GetComponent<PowerUpManager>() == null) gameObject.AddComponent<PowerUpManager>();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BGM_Battle");
        }

        // (CreateMatchUI ถูกเรียกด้านล่างแล้ว ไม่ต้องเรียกซ้ำ)

        // 2) ติดมินิแม็พ และกล้องติดตามยาน
        // Attach Minimap
        gameObject.AddComponent<RadarMinimap>();

        // Attach CameraFollow explicitly by searching for Camera
        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = FindObjectOfType<Camera>();
        
        if (mainCam != null && mainCam.GetComponent<CameraFollow>() == null)
        {
            mainCam.gameObject.AddComponent<CameraFollow>();
        }

        // 3) หา HUD เลือดแบบเก่าใน Scene (ถ้ามี)
        // ค้นหา UI อัตโนมัติจากชื่อ
        var p1txt = GameObject.Find("Player1HUD/HP_BG/HP_Text");
        var p1fill = GameObject.Find("Player1HUD/HP_BG/HP_Fill");
        if(p1txt) p1HpText = p1txt.GetComponent<TMP_Text>();
        if(p1fill) p1HpFill = p1fill.GetComponent<RectTransform>();

        var p2txt = GameObject.Find("Player2HUD/HP_BG/HP_Text");
        var p2fill = GameObject.Find("Player2HUD/HP_BG/HP_Fill");
        if(p2txt) p2HpText = p2txt.GetComponent<TMP_Text>();
        if(p2fill) p2HpFill = p2fill.GetComponent<RectTransform>();

        // 4) สร้าง/ผูก HUD และหน้าผลการแข่ง (ดู GameplayManager.HUD.cs)
        CreateMatchUI();
        StyleBattleHUD();
        BuildResultUI();

        // PHASE 5: เล่นเพลงตอนสู้
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BGM_Battle");
        }

        // 5) จัดเลย์เอาต์แม็พตามแม็พที่เลือก (ดู GameplayManager.Maps.cs)
        // Scene setup must also run in offline previews, before spawning any player.
        ApplySelectedMapLayout(GetCurrentMapIndex());

        // 6) ส่วนที่ต้องเชื่อมต่อ Photon: สร้างสิ่งกีดขวาง เลือกยาน ตั้งพื้นหลัง รีเซ็ต Kill และเกิดยาน
        if (PhotonNetwork.IsConnected && PhotonNetwork.LocalPlayer != null)
        {
            // สร้างสิ่งกีดขวาง (ทำแค่ครั้งเดียวตอนเริ่มเกม)
            if (autoGenerateMap)
            {
                GenerateMapObstacles();
            }
            
            // อ่านค่าเรือที่เลือก (key ต้องตรงกับ LobbyManager ที่ตั้ง "ShipType")
            int shipIndex = 0;
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("ShipType", out object shipProp))
            {
                shipIndex = (int)shipProp;
            }
            // ShipType -> Prefab ใน Resources ชื่อ ShipPrefabs/Ship(ShipType+1)
            // เฟส 7: ยานใหม่ใช้ Prefab ของยานเดิม (BattleLoadoutCatalog.PrefabName)
            string prefabName = BattleLoadoutCatalog.PrefabName(shipIndex);

            // 6.1) ตั้งรูปพื้นหลัง และเปิดเฉพาะ Map{index}_Layout ของแม็พที่เลือก (0 แมงกะพรุน, 1 ปริซึม, 2 หุ่นยนต์)
            // Load Background based on MapIndex
            if (backgroundSprite == null)
            {
                GameObject bgObj = GameObject.Find("BackgroundMap");
                if (bgObj != null) backgroundSprite = bgObj.GetComponent<SpriteRenderer>();
            }
            
            if (backgroundSprite != null && PhotonNetwork.CurrentRoom != null)
            {
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("MapIndex", out object mapProp))
                {
                    int mapIdx = (int)mapProp;
                    string[] mapImages = MapBackgroundImages; // รวมแม็พใหม่ 3, 4
                    if (mapIdx >= 0 && mapIdx < mapImages.Length)
                    {
                        Sprite bg = Resources.Load<Sprite>(mapImages[mapIdx]);
                        if (bg != null) backgroundSprite.sprite = bg;
                    }

                    // --- Toggle map layouts (supports inactive GameObjects) ---
                    GameObject[] rootObjs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    for (int i = 0; i <= 2; i++)
                    {
                        string targetName = "Map" + i + "_Layout";
                        foreach (GameObject rootObj in rootObjs)
                        {
                            if (rootObj.name != targetName) continue;

                            Transform layoutBackground = rootObj.transform.Find("Background");
                            if (layoutBackground != null)
                                FitBackgroundToArena(layoutBackground.GetComponent<SpriteRenderer>());

                            rootObj.SetActive(i == mapIdx);
                            break;
                        }
                    }
                }
                // Scale Background ให้ใหญ่พอครอบคลุมแม็พ แต่ไม่ใหญ่เกินจนกิน GPU มือถือ
                FitBackgroundToArena(backgroundSprite);
            }

            // 6.2) รีเซ็ต Kills ของเราเป็น 0 ใน Player Properties (ซิงก์ให้ทุกเครื่องเห็น)
            // Initialize Kills
            // (ถ้ากลับเข้าแมตช์เดิมหลังหลุด = LoadedBattleToken ตรงกับรอบนี้ จะไม่ล้างคะแนน)
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("BattleToken", out object roundToken);
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("LoadedBattleToken", out object loadedToken);
            if (roundToken == null || !Equals(roundToken, loadedToken))
            {
                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                props.Add("Kills", 0);
                props.Add("Deaths", 0);
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            }


            // 6.3) เกิดยาน: Master อยู่ล่าง (y = -16) หันขึ้น, อีกคนอยู่บน (y = 16) หมุน 180 องศา
            // แม็พหุ่นยนต์ (index 2) เกิดผ่าน Coroutine SpawnMechPlayerWhenClear ใน GameplayManager.Maps.cs
            // Spawn Player (Vertical Layout - ห่างกันมากขึ้นเพื่อไม่ให้โดนยิงทันที)
            Vector3 spawnPos = PhotonNetwork.IsMasterClient ? new Vector3(0f, -16f, 0f) : new Vector3(0f, 16f, 0f);
            Quaternion spawnRot = PhotonNetwork.IsMasterClient ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);
            // ห้องหลายคน: เกิดเป็นวงรอบสนาม แต่ละคนได้ช่องของตัวเอง หันหน้าเข้ากลาง
            if (isFreeForAll) StartCoroutine(SpawnSlotWhenClear(prefabName, LocalSpawnSlot(), MatchRules.TotalCombatants(PhotonNetwork.CurrentRoom)));
            else if (GetCurrentMapIndex() == 2) StartCoroutine(SpawnMechPlayerWhenClear(prefabName, spawnRot));
            else PhotonNetwork.Instantiate(prefabName, spawnPos, spawnRot);

            if (playerInfoText != null)
                playerInfoText.text = "Player: " + PhotonNetwork.NickName;
        }
    }

    // ตัวจับเวลาค้นหายานศัตรู (ค้นทุก 1 วินาทีจนกว่าจะเจอ)
    private float remoteFindTimer = 0f;

    // Update: ทุกเฟรมบนทุกเครื่อง — เช็คคนหลุด อัปเดต Ping หายานศัตรู แล้วอัปเดตการเริ่มแมตช์ เวลา คะแนน เลือด และ HUD
    void Update()
    {
        // 1) หน้าผล: เช็คว่าทุกคนกดกลับห้องเดิมครบหรือยัง (GameplayManager.Results.cs)
        CheckSameRoomReturn();
        // เฟส 9: ต่อใหม่เมื่อหลุด / รอคนที่หลุด
        UpdateBattleReconnect();
        HandleBattleBackKey(); // ปุ่มย้อนกลับมือถือ / Esc
        // 2) ผู้เล่นออนไลน์เหลือไม่ถึง 2 คนระหว่างแข่ง = ยกเลิกแมตช์กลับห้องรอ
        if (PhotonNetwork.InRoom)
        {
            battleRoomName = PhotonNetwork.CurrentRoom.Name;
            int activePilots = 0;
            foreach (var pilot in PhotonNetwork.PlayerList) if (PilotCounts(pilot)) activePilots++;
            if (activePilots < MatchRules.ExpectedHumans(PhotonNetwork.CurrentRoom) && !intentionalLeave && !resultShown
                && !ContinueWithoutMissingPilots())
            {
                StopInterruptedBattle();
                ReturnToWaitingRoom();
                return;
            }
        }
        // 3) ปรับขนาด HUD ให้พอดีจอ และแสดง Ping (ms)
        FitBattleHUD();
        if (pingText != null && PhotonNetwork.IsConnected)
        {
            pingText.text = "Ping: " + PhotonNetwork.GetPing() + " ms";
        }
        // เฟส 9: ระหว่างต่อใหม่ (ไม่อยู่ในห้อง) หยุดอัปเดตระบบแมตช์ไว้ก่อน
        if (battleReconnecting || pendingLeftRoomAt > 0) return;

        // 4) หา remotePlayer = ยานที่ไม่ใช่ของเรา
        // ค้นหาศัตรูถ้ายังไม่เจอ (เช็คทุก 1 วินาที แทน ทุกเฟรม เพื่อประหยัดเปอร์ฟอร์แมนซ์)
        if (remotePlayer == null && PhotonNetwork.CurrentRoom != null)
        {
            remoteFindTimer -= Time.deltaTime;
            if (remoteFindTimer <= 0f)
            {
                remoteFindTimer = 1f;
                PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
                foreach(var p in allPlayers)
                {
                    // คู่แข่ง = ยานที่ไม่ใช่ของเรา (รวมยานบอทที่ Master ควบคุม)
                    if (p == localPlayer || (p.photonView.IsMine && !p.IsBot)) continue;
                    remotePlayer = p; break;
                }
            }
        }

        // 5) อัปเดตระบบของแมตช์และ HUD
        UpdateBattleStart();
        UpdateMultiplayer();
        UpdateGameMode();
        UpdateTutorial();
        UpdateRespawnHUD();
        UpdateDamageDirection();
        if (hitConfirmationText != null && Time.unscaledTime >= hitConfirmationUntil)
            hitConfirmationText.gameObject.SetActive(false);
        UpdateMatchTimerAndScore();
        UpdateHealthBars();
        UpdateSkillUI();
    }



    // จำนวน Kill ที่ต้องได้เพื่อชนะ (ใครถึงก่อนชนะทันที)
    public int targetKills = 3; // PHASE 5: ใครถึง 3 Kills ก่อน ชนะ

    // เรียกทุกเฟรม (ทุกเครื่องคำนวณเอง): อัปเดตเวลาที่เหลือและคะแนน แล้วเช็คเงื่อนไขจบเกม
    // จบเมื่อมีคนได้ Kill ครบ targetKills หรือหมดเวลา (เวลาคิดจาก PhotonNetwork.Time - StartTime จึงตรงกันทุกเครื่อง)
    private void UpdateMatchTimerAndScore()
    {
        if (isMatchEnding || PhotonNetwork.CurrentRoom == null) return;
        // 1) ยังไม่เริ่ม: แสดงเวลาเต็ม (เช่น 03:00)
        if (!matchStarted)
        {
            if (matchTimerText != null) matchTimerText.text = string.Format("{0:00}:{1:00}",
                Mathf.FloorToInt(matchDuration / 60f), Mathf.FloorToInt(matchDuration % 60f));
            return;
        }

        // 2) อ่าน Kill ของเราและศัตรูจาก Player Properties แล้วแสดงคะแนน
        // Update Score Text first so we have the latest kills
        // MatchRules: ของเรา vs คู่แข่งที่ได้มากที่สุด (ใช้ได้ทั้ง 1v1 และหลายคน)
        int myKills = MatchRules.LocalKills();
        int enemyKills = MatchRules.BestRivalKills();
        // โหมดทีม: เทียบ Kill รวมของทีมเรากับทีมตรงข้าม (เป้าหมายเป็นของทีม)
        if (isTeamMode) { myKills = MatchRules.TeamKills(LocalTeam); enemyKills = MatchRules.TeamKills(1 - LocalTeam); }

        // เฟส 7B: โหมดเกมพิเศษ ใช้คะแนน/เงื่อนไขจบของโหมดแทนการนับ Kill (GameplayManager.Modes.cs)
        if (gameMode != MatchRules.ModeDeathmatch)
        {
            if (scoreText != null) scoreText.text = ModeScoreLine();
            if (ModeShouldEnd()) { isMatchEnding = true; EndMatch(); return; }
        }
        else
        {
            if (scoreText != null)
            {
                scoreText.text = isTeamMode ? TeamScoreLine() : isFreeForAll ? FreeForAllScoreLine(myKills) : $"YOU  {myKills}  :  {enemyKills}  RIVAL";
            }

            // 3) มีคนได้ Kill ครบ -> จบแมตช์
            // เช็คเงื่อนไขจบเกม: First to 3 Kills
            if (myKills >= targetKills || enemyKills >= targetKills)
            {
                isMatchEnding = true;
                EndMatch();
                return;
            }
        }

        // 4) คำนวณเวลาที่เหลือจาก StartTime ของห้อง แสดงเป็น MM:SS ถ้าหมดเวลา -> จบแมตช์
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("StartTime", out object startTimeObj))
        {
            double startTime = (double)startTimeObj;
            float timePassed = (float)(PhotonNetwork.Time - startTime);
            matchTimer = Mathf.Max(0, matchDuration - timePassed);

            if (matchTimerText != null)
            {
                int minutes = Mathf.FloorToInt(matchTimer / 60F);
                int seconds = Mathf.FloorToInt(matchTimer - minutes * 60);
                matchTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }

            if (matchTimer <= 0)
            {
                isMatchEnding = true;
                EndMatch();
            }
        }
    }

    // จบแมตช์บนเครื่องนี้ (เรียกจาก UpdateMatchTimerAndScore เมื่อเงื่อนไขจบเป็นจริง)
    // ส่ง RPC SetMatchEndedRPC ของยานเราไปทุกเครื่อง สรุปผลจาก Kill แล้วเปิดหน้าผลเสมอ หรือ ชนะ/แพ้ (GameplayManager.Results.cs)
    private void EndMatch()
    {
        if (localPlayer != null)
        {
            localPlayer.photonView.RPC("SetMatchEndedRPC", RpcTarget.All);
        }

        // ห้องหลายคน: หน้าผลแบบจัดอันดับ (GameplayManager.Multi.cs)
        if (isFreeForAll) { ShowFreeForAllResult(); return; }

        // 1) อ่าน Kill ล่าสุดของเราและศัตรู
        int myKills = MatchRules.LocalKills();
        int enemyKills = MatchRules.BestRivalKills();

        // 2) ตัดสินผล: เท่ากัน = เสมอ, เรามากกว่า = ชนะ
        bool isDraw = myKills == enemyKills;
        bool isWinner = myKills > enemyKills;

        // 3) เตรียมชื่อยาน (ตัด "(Clone)" ออกจากชื่อ GameObject) และชื่อศัตรูสำหรับหน้าผล
        string myShip = localPlayer != null ? localPlayer.gameObject.name.Replace("(Clone)", "") : "MyShip";
        string enemyName = "Enemy";
        string enemyShip = "Unknown";
        if (remotePlayer != null)
        {
            enemyName = remotePlayer.PilotName;
            enemyShip = remotePlayer.gameObject.name.Replace("(Clone)", "");
        }
        else if (PhotonNetwork.PlayerListOthers.Length > 0)
        {
            enemyName = PhotonNetwork.PlayerListOthers[0].NickName;
        }

        // 4) เปิดหน้าผล
        if (isDraw)
            ShowDrawResultScreen(enemyName);
        else
            ShowResultScreen(isWinner, myShip, enemyShip, enemyName, myKills);
    }



    // เรียกจาก PlayerController (บนเครื่องเจ้าของยาน) เมื่อยานของเราเกิดแล้ว เพื่อบอกว่า localPlayer คือยานไหน
    // และโหลดไอคอนสกิลตาม skillType (0 stun, 1 shield, 2 nova, 3 seeker)
    public void SetLocalPlayer(PlayerController player)
    {
        localPlayer = player;
        if (playerInfoText != null)
            playerInfoText.text = "YOU / " + PhotonNetwork.NickName;

        // โหลดรูปไอคอนสกิล
        if (skillIconImage != null)
        {
            // ไอคอนจาก Catalog (รองรับสกิลใหม่เฟส 7: Images/icon_blink / icon_heal / icon_cloak)
            string iconName = BattleLoadoutCatalog.Skills[BattleLoadoutCatalog.ValidSkill(player.skillType)].iconPath.Replace("Images/", "");

            Sprite sp = Resources.Load<Sprite>("Images/" + iconName);
            skillIconImage.sprite = sp;
            skillIconImage.enabled = sp != null;
        }
    }

    // เรียกทุกเฟรม: อัปเดตตัวเลขและความยาวแถบเลือดของเราและศัตรู สีไล่ เขียว -> เหลือง -> แดง ตามสัดส่วน HP
    // และเปิดขอบจอแดงกระพริบเมื่อเลือดเราเหลือน้อยกว่า 35% (hpRatio < 0.35)
    private void UpdateHealthBars()
    {
        // 1) ยานของเรา
        if (localPlayer != null)
        {
            float hpRatio = Mathf.Clamp01(localPlayer.currentHp / Mathf.Max(1f, localPlayer.maxHp));
            if (p1HpText) p1HpText.text = $"{Mathf.Ceil(localPlayer.currentHp)} / {localPlayer.maxHp}";
            if (p1HpFill)
            {
                p1HpFill.localScale = new Vector3(hpRatio, 1, 1);
                // เปลี่ยนสีแถบเลือดตาม HP: เขียว → เหลือง → แดง
                Image fillImg = p1HpFill.GetComponent<Image>();
                if (fillImg != null)
                {
                    if (hpRatio > 0.5f) fillImg.color = Color.Lerp(Color.yellow, Color.green, (hpRatio - 0.5f) * 2f);
                    else fillImg.color = Color.Lerp(Color.red, Color.yellow, hpRatio * 2f);
                }
            }

            // Low HP Warning (ขอบจอแดงกระพริบเมื่อเลือดต่ำกว่า 30%)
            if (lowHPWarning != null)
            {
                if (hpRatio < 0.35f && hpRatio > 0f)
                {
                    lowHPWarning.SetActive(true);
                    CanvasGroup cg = lowHPWarning.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = Mathf.PingPong(Time.time * 3f, 0.4f) + 0.2f; // กระพริบเร็วขึ้น สว่างขึ้นนิดหน่อย
                        cg.blocksRaycasts = false; // กันเหนียว ไม่ให้บังปุ่มกด
                    }
                    Image img = lowHPWarning.GetComponent<Image>();
                    if (img != null) img.raycastTarget = false;
                }
                else
                {
                    lowHPWarning.SetActive(false);
                }
            }
        }

        // 2) ยานศัตรู
        if (remotePlayer != null)
        {
            float hpRatio2 = Mathf.Clamp01(remotePlayer.currentHp / Mathf.Max(1f, remotePlayer.maxHp));
            if (p2HpText) p2HpText.text = $"{Mathf.Ceil(remotePlayer.currentHp)} / {remotePlayer.maxHp}";
            if (p2HpFill)
            {
                p2HpFill.localScale = new Vector3(hpRatio2, 1, 1);
                Image fillImg2 = p2HpFill.GetComponent<Image>();
                if (fillImg2 != null)
                {
                    if (hpRatio2 > 0.5f) fillImg2.color = Color.Lerp(Color.yellow, Color.green, (hpRatio2 - 0.5f) * 2f);
                    else fillImg2.color = Color.Lerp(Color.red, Color.yellow, hpRatio2 * 2f);
                }
            }
        }
    }

    // ผู้เล่นกดออกเอง (เช่น ปุ่ม BACK TO LOBBY ในหน้าผล): ตั้ง intentionalLeave กันถูกนับว่าหลุด หยุดการต่อสู้ แล้วออกจากห้อง
    // ถ้าสั่ง LeaveRoom ไม่สำเร็จจะคืนค่าธงเพื่อให้กดใหม่ได้ เมื่อออกสำเร็จ Photon จะเรียก OnLeftRoom
    public void LeaveRoom()
    {
        if (intentionalLeave) return;
        intentionalLeave = true;
        StopInterruptedBattle();
        if (!PhotonNetwork.InRoom) SceneManager.LoadScene("LobbyScene");
        else if (!PhotonNetwork.LeaveRoom(false)) intentionalLeave = false;
    }

    // Callback ของ Photon เมื่อเครื่องเราออกจากห้องสำเร็จ: กลับไป LobbyScene
    public override void OnLeftRoom()
    {
        // เฟส 9: ออกจากห้องโดยไม่ได้กดออกเอง = อาจเป็นเน็ตหลุด รอ OnDisconnected ก่อน 1 วิ
        if (battleReconnecting) return;
        if (!intentionalLeave && FeatureFlags.BattleReconnect && !PhotonNetwork.OfflineMode && !resultShown)
        {
            pendingLeftRoomAt = Time.unscaledTime + 1f;
            return;
        }
        SceneManager.LoadScene("LobbyScene");
    }


    // Coroutine เอฟเฟกต์เด้งขยาย (scale 0 -> 1 ใน 0.4 วินาที แบบ Ease Out Back) ใช้ตอนเปิดหน้าผล
    private System.Collections.IEnumerator ScaleTweenRoutine(Transform targetTransform)
    {
        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one;

        targetTransform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease out back formula for bouncy effect
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float ease = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            targetTransform.localScale = Vector3.LerpUnclamped(startScale, endScale, ease);
            yield return null;
        }

        targetTransform.localScale = endScale;
    }
}

