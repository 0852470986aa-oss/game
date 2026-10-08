// LobbyManager.cs — ไฟล์หลักของ LobbyManager (อยู่ใน LobbyScene) เป็น partial class แยก 4 ไฟล์:
// LobbyManager.cs (field, Start/Update, สลับหน้า, ห้องรอ Ready/Start), .Views.cs (สร้าง UI/โหลดโปรไฟล์),
// .Inventory.cs (คลังยาน/สกิล), .Rooms.cs (Photon callback และสร้าง/เข้าห้อง)
// ดึงข้อมูลผู้เล่นจาก FirebaseManager แล้วใช้ Photon PUN 2 จับคู่ 1v1 เมื่อพร้อมจะโหลด SampleScene (GameplayManager)
// มีปุ่มออกจากระบบ (ผ่านหน้าตั้งค่า) กลับไป LoginScene
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

// ตัวประสานงานล็อบบี้: เก็บสถานะผู้เล่น/ห้อง และใช้เมธอดร่วมกับส่วน Views, Inventory และ Rooms
public partial class LobbyManager : MonoBehaviourPunCallbacks
{
    // ชื่อ key ของ Custom Properties ใน Photon (ส่งให้ทุกเครื่องในห้องเห็น)
    // ReadyProperty/ShipProperty/SkillProperty/FirebaseUidProperty เป็นของผู้เล่น, MapProperty เป็นของห้อง
    // FirebaseUid ใช้ให้ GameplayManager.Results รู้ UID ของคู่แข่งเพื่อบันทึกประวัติแมตช์
    private const string ReadyProperty = "IsReady";
    private const string ShipProperty = "ShipType";
    private const string SkillProperty = "SkillType";
    private const string MapProperty = "MapIndex";
    private const string FirebaseUidProperty = "FirebaseUid";

    // แคชรายการห้องจาก OnRoomListUpdate (Photon ส่งมาเฉพาะห้องที่เปลี่ยน จึงต้องเก็บรวมเอง) key = ชื่อห้อง
    private readonly Dictionary<string, RoomInfo> cachedRooms = new Dictionary<string, RoomInfo>();
    // ธงสถานะกันการกดซ้ำ/สั่งซ้อน: กำลังออกจากห้อง, กำลังเริ่มเกม, กำลังออกจากระบบ, กำลังรอผลสร้าง/เข้าห้อง
    private bool isLeavingRoom;
    private bool isStartingGame;
    private bool loggingOut;
    private bool roomRequestPending;

    // ช่อง UI ด้านล่างหลายช่องถูกสร้าง/ผูกใหม่ตอนรันใน BuildLobbyUI (LobbyManager.Views.cs)
    [Header("=== Main Lobby UI ===")]
    public TMP_Text statusText;
    public TMP_Text playerNameText;
    public TMP_Text coinText;
    public TMP_Text winsText;
    public TMP_Text playersOnlineText;
    public TMP_Text shipNameText;
    public TMP_Text shipHPText;
    public TMP_Text shipATKText;
    public TMP_Text shipSPDText;
    public TMP_Text shipSkillText;
    public Image shipImage;
    public Button playButton;
    public Button createRoomButton;
    public Button inventoryButton;
    public Button settingsButton;
    public Button logoutButton;

    [Header("=== Panels ===")]
    public GameObject mainPanel;
    public GameObject inventoryPanel;
    public GameObject roomPanel;
    public GameObject settingsPanel;
    public GameObject waitingRoomPanel;
    public GameObject tutorialPanel;

    [Header("=== Inventory UI ===")]
    public Button[] shipButtons;
    public TMP_Text inventoryShipName;
    public TMP_Text inventoryShipHP;
    public TMP_Text inventoryShipATK;
    public TMP_Text inventoryShipSPD;
    public TMP_Text inventoryShipSkill;
    public Image inventoryShipImage;
    public Button inventoryActionButton;
    public TMP_Text inventoryActionText;
    public Button backFromInventoryButton;

    [Header("=== Room UI ===")]
    public TMP_Text roomNumberText;
    public TMP_Text roomModeText;
    public UnityEngine.UI.InputField roomSearchInput;
    public Button searchRoomButton;
    public Button createRoomConfirmButton;
    public Button backFromRoomButton;
    public Transform roomListContent;
    public GameObject roomItemPrefab;

    [Header("=== Settings UI ===")]
    public Button closeSettingsButton;
    public UnityEngine.UI.Slider volumeSlider;
    public UnityEngine.UI.Slider musicSlider;
    public UnityEngine.UI.Slider sfxSlider;

    [Header("=== Waiting Room UI ===")]
    public TMP_Text waitRoomNumberText;
    public TMP_Text waitP1NameText;
    public TMP_Text waitP1ShipNameText;
    public TMP_Text waitP1StatsText;
    public TMP_Text waitP1SkillText;
    public Image waitP1ShipImage;
    public TMP_Text waitP1ReadyText;
    public TMP_Text waitP2NameText;
    public TMP_Text waitP2ShipNameText;
    public TMP_Text waitP2StatsText;
    public TMP_Text waitP2SkillText;
    public Image waitP2ShipImage;
    public TMP_Text waitP2ReadyText;
    public Button waitReadyButton;
    public Button waitCancelButton;
    public Button waitStartButton;
    public TMP_Text waitMapNameText;
    public Image waitMapImage;

    // ข้อมูลแผนที่ (Map)
    public TMP_Text createRoomMapNameText;
    // ให้ด่านซากปรักหักพังหุ่นเหล็กเป็นด่านเริ่มต้นตามลำดับการพัฒนาเกมเพลย์
    private int selectedMapIndex = 2;
    // ชื่อแม็พเรียงตาม index: 0 = แมงกะพรุน, 1 = ปริซึม, 2 = หุ่นยนต์
    private string[] mapNames = { "Electric Jellyfish Core", "Obelisk Plains of Prism", "Abandoned Mech Warzone", "Asteroid Station", "Molten Nebula" };
    // path รูปตัวอย่างแม็พในโฟลเดอร์ Resources เรียง index เดียวกับ mapNames
    private string[] mapImages = { "Images/Map_ThunderJellyfish", "Images/Map_ObeliskPlains", "Images/Map_AncientMech", "Images/Map_AsteroidStation_Preview", "Images/Map_MoltenNebula_Preview" };
    // รูปการ์ดแม็พในล็อบบี้: แม็พปริซึมรุ่น 3 (FeatureFlags.PrismMapV3) ใช้รูปตัวอย่างมุมบนแบบใหม่
    private string MapImage(int map) => map == 1 && FeatureFlags.PrismMapV3 ? "Images/Map_PrismNebula_Preview" : mapImages[map];
    // จำนวนแม็พที่เลือกได้ (ปิด FeatureFlags.NewMaps = 3 แม็พเดิม)
    private static int MapChoices => GameplayManager.MapCount;

    // ข้อมูลยาน
    // selectedShipIndex = ยานที่กำลังดูในคลัง, equippedShipIndex = ยานที่ใส่ไปรบจริง
    private int selectedShipIndex = 0;
    private ShipData[] ships = BattleLoadoutCatalog.Ships;
    // ข้อมูลผู้เล่นจากฐานข้อมูล
    private List<int> unlockedShips = new List<int> { 0 };
    private int equippedShipIndex = 0;
    
    // ข้อมูลสกิล
    // selectedSkillIndex = สกิลที่กำลังดู, equippedSkillIndex = สกิลที่ติดตั้งไปรบจริง
    private int selectedSkillIndex = 0;
    private int equippedSkillIndex = 0;
    private SkillData[] skills = BattleLoadoutCatalog.Skills;

    [Header("=== Skill UI ===")]
    public TMP_Text skillDescText;
    public Button installSkillButton;
    public TMP_Text installSkillText;

    // Unity เรียกก่อน Start: เปิด AutomaticallySyncScene ให้เมื่อ Master สั่ง LoadLevel ทุกเครื่องในห้องโหลด Scene ตาม
    // แล้วสร้าง UI ล็อบบี้ทั้งหมดด้วยโค้ด
    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        BuildLobbyUI();
    }

    // Unity เรียกตอนเข้า LobbyScene ขั้นตอน:
    // 1) รีเซ็ตเวลาเกม ปิดปุ่มเล่นไว้ก่อน แสดงหน้าหลัก เปิดเพลง และเริ่มโหลดโปรไฟล์จาก Firebase
    // 2) ตั้งค่าตัวเลื่อนเสียง  3) ตั้งชื่อ Photon จากชื่อผู้เล่น
    // 4) ถ้าเพิ่งหลุดจากแมตช์ (GameplayManager.RecoveryRoom) พยายามกลับเข้าห้องรอเดิมภายใน 50 วินาที
    // 5) ไม่เช่นนั้นเชื่อมต่อ Photon ใหม่ / กลับห้องรอถ้ายังอยู่ในห้อง / หรือเข้า Lobby
    void Start()
    {
        Time.timeScale = 1f;
        // กลับมาจากโหมดเล่นคนเดียว (ออฟไลน์): ปิดโหมดออฟไลน์แล้วต่อออนไลน์ใหม่ตามปกติ
        if (PhotonNetwork.OfflineMode)
        {
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            PhotonNetwork.OfflineMode = false;
        }
        EnablePlayButtons(false);
        ShowMainPanel();

        // PHASE 5: เล่นเพลงหน้าเมนู
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BGM_Lobby");
        }
        
        LoadLobbyProfile();

        // Initialize Audio Settings
        // ตั้งค่าตัวเลื่อนเสียงจากค่าที่บันทึกใน PlayerPrefs และผูกให้เปลี่ยนเสียงใน AudioManager ทันทีเมื่อเลื่อน
        if (AudioManager.Instance != null)
        {
            if (volumeSlider != null) 
            {
                volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
                volumeSlider.onValueChanged.AddListener(vol => AudioManager.Instance.SetMasterVolume(vol));
            }
            if (musicSlider != null) 
            {
                musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", .65f);
                musicSlider.onValueChanged.AddListener(vol => AudioManager.Instance.SetMusicVolume(vol));
            }
            if (sfxSlider != null) 
            {
                sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", .8f);
                sfxSlider.onValueChanged.AddListener(vol => AudioManager.Instance.SetSFXVolume(vol));
            }
        }

        // ตั้งชื่อ Photon
        PhotonNetwork.NickName = FirebaseManager.Instance != null
            ? FirebaseManager.Instance.GetUsername()
            : "Player_" + Random.Range(1000, 9999);

        // เชื่อมต่อ Photon
        // กรณีกลับมาจากแมตช์ที่เน็ตหลุด: ใช้ ReconnectAndRejoin/Reconnect เพื่อกลับห้องเดิม (ห้องจองที่ไว้ 60 วินาทีตาม PlayerTtl)
        if (!string.IsNullOrEmpty(GameplayManager.RecoveryRoom))
        {
            previousRoom = GameplayManager.RecoveryRoom;
            GameplayManager.RecoveryRoom = null;
            reconnecting = true;
            reconnectDeadline = Time.unscaledTime + 50f;
            UpdateStatus("Battle interrupted. Reconnecting to your waiting room...");
            if (PhotonNetwork.ReconnectAndRejoin() || PhotonNetwork.Reconnect()) return;
            if (!PhotonNetwork.IsConnected && PhotonNetwork.ConnectUsingSettings()) return;
            RecoveryFailed("Room recovery unavailable. Retrying the lobby connection automatically...");
            return;
        }
        if (!PhotonNetwork.IsConnected)
        {
            // ใช้ Firebase UID เป็น UserId ของ Photon (ไม่มีก็สุ่ม GUID) เพื่อให้ Rejoin ห้องเดิมได้ด้วยตัวตนเดิม
            if (PhotonNetwork.AuthValues == null || string.IsNullOrEmpty(PhotonNetwork.AuthValues.UserId))
            {
                string userId = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetUserId() : "";
                if (string.IsNullOrEmpty(userId)) userId = System.Guid.NewGuid().ToString("N");
                PhotonNetwork.AuthValues = new AuthenticationValues(userId);
            }
            UpdateStatus("Connecting to server...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else if (PhotonNetwork.InRoom)
        {
            OnJoinedRoom();
        }
        else if (PhotonNetwork.InLobby)
        {
            UpdateStatus("Ready! Press button to enter room");
            EnablePlayButtons(true);
        }
        else
        {
            UpdateStatus("Entering Lobby...");
            if (PhotonNetwork.IsConnectedAndReady) PhotonNetwork.JoinLobby();
        }

    }

    // Unity เรียกทุกเฟรม: ปรับขนาด UI ตาม Safe Area, จัดการเชื่อมต่อใหม่อัตโนมัติ
    // ทุก 1 วินาทีอัปเดตข้อความ ONLINE/ping และหน้าห้องรอ; ถ้าเลยเวลากู้ห้อง (reconnectDeadline) ให้เลิกกู้และกลับหน้าหลัก
    void Update()
    {
        HandleBackKey(); // ปุ่มย้อนกลับมือถือ / Esc (LobbyManager.Back.cs)
        FitLobbyUI();
        UpdateAutomaticRecovery();
        CheckSoloTimeout(); // กดเล่นกับบอทแล้วค้าง (LobbyManager.Solo.cs)
        if (Time.unscaledTime >= nextStatusRefresh)
        {
            nextStatusRefresh = Time.unscaledTime + 1f;
            string connection = PhotonNetwork.IsConnectedAndReady
                ? "ONLINE  |  " + PhotonNetwork.GetPing() + " ms"
                : reconnecting ? "RECONNECTING..." : "OFFLINE";
            if (playersOnlineText != null) playersOnlineText.text = connection;
            if (connectionText != null) connectionText.text = connection;
            if (PhotonNetwork.InRoom) UpdateWaitingRoomUI();
            if (!profileLoaded && Time.unscaledTime > profileDeadline)
                UpdateStatus("Loadout delayed. Retrying automatically...");
        }
        if (reconnecting && Time.unscaledTime > reconnectDeadline)
        {
            reconnecting = false;
            previousRoom = null;
            PhotonNetwork.Disconnect();
            ShowMainPanel();
            UpdateStatus("Room recovery expired. Reconnecting to the lobby automatically...");
        }
    }

    // เวลาที่จะลองเชื่อมต่อ/โหลดโปรไฟล์ครั้งถัดไป (หน่วยเป็นวินาทีของ Time.unscaledTime) และจำนวนครั้งที่ลองต่อเนื่อง
    // automaticRecoveryBlocked = true เมื่อหลุดด้วยสาเหตุที่ลองใหม่ไม่มีประโยชน์ (เช่น Auth ผิด, เต็ม CCU)
    private float nextAutoReconnect, nextAutoProfileRetry;
    private int autoReconnectAttempts;
    private bool automaticRecoveryBlocked;

    // เรียกจาก Update ทุกเฟรม: ระบบเชื่อมต่อใหม่อัตโนมัติ (ไม่ต้องมีปุ่ม Retry)
    // 1) ถ้าโหลดโปรไฟล์เกินกำหนดให้ลองโหลดใหม่ทุก 30 วินาที
    // 2) ถ้าอยู่ในห้องหรือ Lobby แล้ว ถือว่าปกติ รีเซ็ตตัวนับ
    // 3) รอ state ให้นิ่งก่อน ไม่ส่งคำสั่งเชื่อมต่อซ้อนกัน และรอถ้าไม่มีอินเทอร์เน็ต
    // 4) หน่วงแบบ backoff 2, 4, 8 แล้วสูงสุด 15 วินาที แล้วเลือก Rejoin ห้องเดิม / JoinLobby / เชื่อมต่อใหม่
    private void UpdateAutomaticRecovery()
    {
        if (loggingOut || isStartingGame || isLeavingRoom || automaticRecoveryBlocked) return;
        // กำลังตัดเน็ตเพื่อเข้าโหมดเล่นกับบอท: ห้ามต่อเน็ตใหม่แทรก (เดิมแทรกได้ ทำให้กดครั้งแรกแวบแล้วไม่เข้าเกม)
        if (FeatureFlags.SoloStartFix && pendingSolo) return;
        // 1) ลองโหลดโปรไฟล์ใหม่
        if (!profileLoaded && Time.unscaledTime > profileDeadline && Time.unscaledTime >= nextAutoProfileRetry
            && Application.internetReachability != NetworkReachability.NotReachable)
        {
            nextAutoProfileRetry = Time.unscaledTime + 30f;
            LoadLobbyProfile();
        }
        // 2) เชื่อมต่อปกติแล้ว
        if (PhotonNetwork.InRoom || PhotonNetwork.InLobby)
        {
            autoReconnectAttempts = 0;
            return;
        }
        if (roomRequestPending || Time.unscaledTime < nextAutoReconnect) return;
        var state = PhotonNetwork.NetworkClientState;
        // Wait for any existing handshake to finish; never issue overlapping connection requests.
        if (state != ClientState.Disconnected && state != ClientState.PeerCreated && state != ClientState.ConnectedToMasterServer) return;
        // 3) ไม่มีเน็ต รอ 2 วินาทีแล้วเช็คใหม่
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            nextAutoReconnect = Time.unscaledTime + 2f;
            UpdateStatus("Waiting for internet. Reconnection is automatic.");
            return;
        }
        // 4) คำนวณเวลารอ backoff แล้วสั่งเชื่อมต่อตามสถานะปัจจุบัน
        float delay = Mathf.Min(15f, 2f * Mathf.Pow(2, Mathf.Min(autoReconnectAttempts++, 3)));
        nextAutoReconnect = Time.unscaledTime + delay;
        UpdateStatus(reconnecting ? "Reconnecting to your room automatically..." : "Connecting to the lobby automatically...");
        if (state == ClientState.ConnectedToMasterServer)
        {
            if (reconnecting && !string.IsNullOrEmpty(previousRoom))
            {
                if (!PhotonNetwork.RejoinRoom(previousRoom)) RecoveryFailed("Room unavailable. Returning to lobby...");
            }
            else PhotonNetwork.JoinLobby();
        }
        else if (reconnecting && !string.IsNullOrEmpty(previousRoom))
        {
            if (!PhotonNetwork.ReconnectAndRejoin() && !PhotonNetwork.Reconnect()) PhotonNetwork.ConnectUsingSettings();
        }
        else PhotonNetwork.ConnectUsingSettings();
    }

    // ============================
    //  PANEL MANAGEMENT
    // ============================

    // Coroutine ทำแอนิเมชันขยายหน้าต่างจาก 0 เป็นขนาดปกติใน 0.35 วินาที แบบเด้งเกินเล็กน้อย (ease-out-back)
    private System.Collections.IEnumerator ScaleTweenRoutine(Transform targetTransform)
    {
        float duration = 0.35f;
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

    // เปิด panel พร้อมแอนิเมชันขยาย (เฉพาะเมื่อยังปิดอยู่)
    private void ShowPanelAnimated(GameObject targetPanel)
    {
        if (targetPanel != null && !targetPanel.activeSelf)
        {
            targetPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(targetPanel.transform));
        }
    }

    // แสดงหน้าหลักและปิดหน้าอื่น; ถ้ายังอยู่ในห้องหรือกำลังกู้ห้อง จะไปหน้าห้องรอแทน
    public void ShowMainPanel()
    {
        ClosePlayMenu();
        CloseRoomRules();
        if (PhotonNetwork.InRoom || reconnecting) { ShowWaitingRoom(); return; }
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        ShowPanelAnimated(mainPanel);
    }

    // เปิดหน้าคลังยาน/สกิล ได้เฉพาะเมื่อโหลดโปรไฟล์เสร็จและไม่ได้อยู่ในห้องหรือกำลังขอห้อง
    public void ShowInventoryPanel()
    {
        if (!profileLoaded || PhotonNetwork.InRoom || roomRequestPending || reconnecting) return;
        if (mainPanel != null) mainPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        CloseEmbeddedWorkshop();
        ShowPanelAnimated(inventoryPanel);
        UpdateInventoryDisplay(selectedShipIndex);
    }

    // เปิดหน้าสร้าง/ค้นหาห้อง: สุ่มเลขห้อง 6 หลัก และแสดงแม็พที่เลือกไว้
    public void ShowRoomPanel()
    {
        if (PhotonNetwork.InRoom || reconnecting) return;
        if (mainPanel != null) mainPanel.SetActive(false);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        ShowPanelAnimated(roomPanel);

        // สร้างเลขห้องสุ่ม
        if (roomNumberText != null)
            roomNumberText.text = Random.Range(100000, 999999).ToString();
        if (roomModeText != null)
            roomModeText.text = "1 VS 1 (Quick Match)";
        
        // อัปเดตชื่อด่านให้ตรงกับที่เลือกไว้
        if (createRoomMapNameText != null && mapNames != null)
            createRoomMapNameText.text = mapNames[selectedMapIndex];
        RefreshMapCards();
    }

    // เปิดหน้าห้องรอ (Waiting Room) และอัปเดตข้อมูลผู้เล่นทั้งสองฝั่ง
    public void ShowWaitingRoom()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        ShowPanelAnimated(waitingRoomPanel);
        UpdateWaitingRoomUI();
    }

    // ============================
    //  SHIP DISPLAY
    // ============================



    // ออกจากระบบ (เรียกจากปุ่ม LOG OUT ในหน้าตั้งค่า): Logout จาก Firebase/Google, ตัดการเชื่อมต่อ Photon แล้วกลับ LoginScene
    // loggingOut = true กันกดซ้ำ และทำให้ OnDisconnected ไม่พยายามเชื่อมต่อใหม่
    public void OnLogoutButtonClicked()
    {
        if (loggingOut) return;
        loggingOut = true;
        if (FirebaseManager.Instance != null) FirebaseManager.Instance.Logout();
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        SceneManager.LoadScene("LoginScene");
    }

    // ปุ่ม SETTINGS หน้าหลัก: เปิด BattleSettingsPanel โดยส่ง OnLogoutButtonClicked เป็น callback ของปุ่ม LOG OUT
    public void OnSettingsClicked()
    {
        // หน้าตั้งค่าในล็อบบี้มีปุ่ม LOG OUT (ถามยืนยันก่อน)
        BattleSettingsPanel.Show(null, OnLogoutButtonClicked);
        DarkenSettingsShade();
    }

    // ปิดหน้าตั้งค่าแบบเดิม (settingsPanel)
    public void OnCloseSettingsClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // เปิดหน้าวิธีเล่น และใส่ข้อความ BattleHelpText ลงใน label ชื่อ TutDesc
    public void OnTutorialClicked()
    {
        if (tutorialPanel != null)
        {
            foreach (var label in tutorialPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label.name != "TutDesc") continue;
                label.text = BattleHelpText;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18;
                label.fontSizeMax = 24;
                label.alignment = TextAlignmentOptions.TopLeft;
            }
        }
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
    }

    // ข้อความวิธีเล่น (ปุ่มควบคุม สกิล และแม็พ) ใช้ rich text ของ TextMeshPro ใส่สี
    public const string BattleHelpText =
        "<color=#65DFFF>CONTROLS</color>\n" +
        "Left stick: move. Hold FIRE and drag to aim.\nTap the separate SKILL button when ready.\n\n" +
        "<color=#65DFFF>SKILLS</color>\n" +
        "STUN: a seeking wave briefly disables an enemy.\n" +
        "SHIELD: temporary protection and a movement boost.\n" +
        "NOVA: place a mine that explodes after a short delay.\n" +
        "SEEKER: launch a homing missile; solid cover blocks it.\n" +
        "BLINK: warp forward. HEAL: repair 35% hull. CLOAK: vanish for 3 sec.\n\n" +
        "<color=#65DFFF>BATTLEFIELDS</color>\n" +
        "MECH: avoid hot rocks, turret fire and falling meteors.\n" +
        "PRISM: use pillars as cover. Green pools slow you by 30%.\n" +
        "JELLYFISH: escape energy-orb pull and lightning warnings.\n" +
        "The central core boosts firing speed but drains HP.\n" +
        "STATION: dodge meteor strikes (red warning circle).\n" +
        "MOLTEN NEBULA: lava rocks burn on contact.\n\n" +
        "<color=#65DFFF>MORE</color>\n" +
        "Pick up crystals for repair, speed, shield and damage.\n" +
        "Game modes, upgrades and options are in the room and SETTINGS.";

    // ปิดหน้าวิธีเล่น
    public void OnCloseTutorialClicked()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
    }

    // ============================
    //  HELPERS
    // ============================

    // แสดงข้อความสถานะใน Console และบน label สถานะของทุกหน้า (หน้าหลัก ห้องรอ คลังยาน หน้าค้นหาห้อง)
    private void UpdateStatus(string msg)
    {
        Debug.Log(msg);
        if (statusText != null) statusText.text = msg;
        if (lobbyMessage != null) lobbyMessage.text = msg;
        if (hangarMessage != null) hangarMessage.text = msg;
        if (browserMessage != null) browserMessage.text = msg;
    }

    // เปิด/ปิดปุ่มเล่น สร้างห้อง และค้นหาห้อง: จะเปิดได้จริงเฉพาะเมื่อโหลดโปรไฟล์แล้ว อยู่ใน Lobby และไม่มีคำขอค้างอยู่
    private void EnablePlayButtons(bool on)
    {
        on = on && profileLoaded && PhotonNetwork.InLobby && !roomRequestPending && !reconnecting && !pendingSolo;
        RefreshSoloButtons();
        if (playButton != null) playButton.interactable = on;
        if (createRoomButton != null) createRoomButton.interactable = on;
        if (createRoomConfirmButton != null) createRoomConfirmButton.interactable = on;
        if (searchRoomButton != null) searchRoomButton.interactable = on;
    }

    // ============================
    //  WAITING ROOM SYSTEM
    // ============================

    // อัปเดตหน้าห้องรอทั้งหมด (เรียกเมื่อ property เปลี่ยน/ผู้เล่นเข้าออก และทุก 1 วินาทีจาก Update)
    // เรียงผู้เล่นตาม ActorNumber ให้ P1/P2 ตรงกันทุกเครื่อง, ตั้งสถานะปุ่ม Ready/Start/Leave และข้อความแนะนำ
    // busy = ยังไม่อยู่ในห้องหรือกำลังเชื่อมต่อ/ออก/เริ่มเกม ระหว่างนั้นปิดปุ่มทั้งหมด
    private void UpdateWaitingRoomUI()
    {
        bool inRoom = PhotonNetwork.InRoom;
        bool busy = !inRoom || reconnecting || isLeavingRoom || isStartingGame || RoomStarting;
        // ห้องแรงค์: ซ่อนปุ่ม COPY CODE / INVITE FRIENDS (หาคู่แบบสุ่มเท่านั้น)
        bool rankedPrivate = inRoom && RankedPrivate(PhotonNetwork.CurrentRoom);
        var copyCode = waitingRoomPanel != null ? waitingRoomPanel.transform.Find("LobbySurface/CopyCode") : null;
        if (copyCode != null) copyCode.gameObject.SetActive(!rankedPrivate);
        if (inviteFriendsButton != null && rankedPrivate) inviteFriendsButton.gameObject.SetActive(false);
        Player[] players = inRoom ? PhotonNetwork.PlayerList : new Player[0];
        System.Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        RenderPlayer(players.Length > 0 ? players[0] : null, waitP1NameText, waitP1ShipNameText,
            waitP1StatsText, waitP1SkillText, waitP1ShipImage, waitP1ReadyText);
        RenderPlayer(players.Length > 1 ? players[1] : null, waitP2NameText, waitP2ShipNameText,
            waitP2StatsText, waitP2SkillText, waitP2ShipImage, waitP2ReadyText);
        // ห้องหลายคน: ตารางนักบิน 10 ช่องแทนการ์ด 2 ใบ (LobbyManager.Roster.cs)
        RenderRoster(players);
        if (waitRoomNumberText != null) waitRoomNumberText.text = !inRoom ? "ROOM RECOVERY" : rankedPrivate ? "RANKED MATCH" : "ROOM  " + PhotonNetwork.CurrentRoom.Name; // ห้องแรงค์ไม่โชว์รหัส
        if (waitReadyButton != null)
        {
            waitReadyButton.interactable = !busy && profileLoaded && !readyPending;
            SetButtonLabel(waitReadyButton, readyPending ? "SYNCING..." : IsPlayerReady(PhotonNetwork.LocalPlayer) ? "CANCEL READY" : "READY");
        }
        if (waitStartButton != null)
        {
            waitStartButton.gameObject.SetActive(true);
            waitStartButton.interactable = !busy && CanStartGame();
            SetButtonLabel(waitStartButton, RoomStarting || isStartingGame ? "STARTING..." : inRoom && PhotonNetwork.IsMasterClient ? "START BATTLE" : "HOST STARTS");
        }
        if (waitCancelButton != null) waitCancelButton.interactable = !busy;
        int roomSize = inRoom ? PhotonNetwork.CurrentRoom.MaxPlayers : 2;
        bool botFill = inRoom && MatchRules.BotFill(PhotonNetwork.CurrentRoom);
        if (readyHint != null) readyHint.text = busy ? "Connecting or preparing battle..."
            : roomSize > 2 && CanStartGame() ? "All " + players.Length + " pilots ready" + (botFill && players.Length < roomSize ? " (+" + (roomSize - players.Length) + " bots)" : "") + ". Host can launch the battle."
            : roomSize > 2 ? "Pilots " + players.Length + "/" + roomSize + ". Host can start with 2+ ready pilots" + (botFill ? " (empty slots become bots)." : ".")
            : players.Length < 2 && botFill ? "No rival yet: press READY, then START to fight a bot."
            : players.Length < 2 ? "Invite a friend using the room code."
            : CanStartGame() ? "All pilots ready. Host can launch the battle."
            : "Both pilots must confirm READY for the selected battlefield.";
        RefreshMapCards();
        RefreshRoomSettingButtons();
    }

    // ผู้เล่นถือว่า Ready เมื่อยังเชื่อมต่ออยู่และ property Ready ตรงกับแม็พและ MapRevision ปัจจุบันของห้อง
    private bool IsPlayerReady(Player player)
    {
        return PhotonNetwork.InRoom && player != null && !player.IsInactive
            && ReadyPropertiesMatch(player.CustomProperties, ReadMap(PhotonNetwork.CurrentRoom), MapRevision);
    }

    // ตรวจ property ความพร้อม: ต้อง IsReady = true, ReadyMap/ReadyRevision ตรงกับห้อง และโหลด loadout แล้ว
    // ทำให้ถ้า Host เปลี่ยนแม็พ ค่า Ready เดิมจะใช้ไม่ได้ ต้องกด Ready ใหม่
    public static bool ReadyPropertiesMatch(ExitGames.Client.Photon.Hashtable properties, int map, int revision)
    {
        return properties != null && BoolProperty(properties, ReadyProperty)
            && IntProperty(properties, "ReadyRevision", -1) == revision
            && IntProperty(properties, "ReadyMap", -1) == map
            && BoolProperty(properties, "LoadoutLoaded");
    }

    // แสดงการ์ดผู้เล่นหนึ่งฝั่งในห้องรอ: ชื่อ ([HOST]/(YOU)), ยาน ค่าพลัง สกิล รูปยานพร้อมสี และสถานะ Ready
    // ข้อมูลอ่านจาก Custom Properties ของผู้เล่นคนนั้น; ถ้าไม่มีผู้เล่นแสดงเป็นช่องว่าง
    private void RenderPlayer(Player player, TMP_Text nameText, TMP_Text shipText, TMP_Text stats,
        TMP_Text skillText, Image picture, TMP_Text readyText)
    {
        bool present = player != null;
        int ship = present ? Mathf.Clamp(IntProperty(player.CustomProperties, ShipProperty, 0), 0, ships.Length - 1) : 0;
        int skill = present ? Mathf.Clamp(IntProperty(player.CustomProperties, SkillProperty, 0), 0, skills.Length - 1) : 0;
        if (nameText != null)
        {
            nameText.richText = false;
            nameText.text = present ? (player.IsMasterClient ? "[HOST] " : "") + LevelTag(player) + player.NickName + (player.IsLocal ? " (YOU)" : "") : "OPEN SLOT";
        }
        if (shipText != null) shipText.text = present ? ships[ship].name : "Waiting for a pilot";
        if (stats != null) { stats.richText = true; stats.text = present ? RosterStats(ship, player) : "Share the room code to invite a friend"; } // รวมตีบวก + ไอเท็ม (ของคนอื่นอ่านจาก StatBonus)
        if (skillText != null) skillText.text = present ? "EQUIPPED SKILL  /  " + skills[skill].name : "1 VS 1";
        if (picture != null)
        {
            picture.sprite = present ? BattleLoadoutCatalog.ShipSprite(ship) : null;
            picture.color = present ? ShipPaint.For(player) * BattleLoadoutCatalog.ShipTint(ship) : Color.clear;
            picture.preserveAspect = true;
        }
        if (readyText != null)
        {
            bool ready = IsPlayerReady(player);
            readyText.text = !present ? "WAITING" : player.IsInactive ? "RECONNECTING (60s slot)" : ready ? "READY" : "NOT READY";
            readyText.color = ready ? new Color(0.35f, 1f, 0.7f) : new Color(1f, 0.73f, 0.35f);
        }
    }

    // "Lv12 " หน้าชื่อผู้เล่น (ถ้าผู้เล่นคนนั้นส่งเลเวลมา)
    // ห้องแรงค์: แสดงแรงค์แทนเลเวล เช่น "GOLD 1320 "
    private static string LevelTag(Player player)
    {
        if (player == null) return "";
        string guild = FeatureFlags.Guilds && player.CustomProperties.TryGetValue("GuildTag", out object g) && g is string tag && tag.Length > 0 ? "[" + tag + "] " : "";
        return guild + LevelOrRankTag(player);
    }

    // ห้องแรงค์ที่ผู้เล่นมี MMR: คืน "แรงค์ MMR" / ห้องปกติ: คืน "Lv{เลเวล} " ถ้าเปิดระบบเลเวล ไม่งั้นคืน ""
    private static string LevelOrRankTag(Player player)
    {
        if (MatchRules.IsRanked(PhotonNetwork.CurrentRoom) && player.CustomProperties.TryGetValue("MMR", out object m) && m is int mmr)
            return Ranked.TierName(mmr) + " " + mmr + "  ";
        return FeatureFlags.Progression && player.CustomProperties.TryGetValue("Level", out object v) && v is int level ? "Lv" + level + " " : "";
    }

    // ปุ่ม READY (รันบนเครื่องตัวเอง): สลับสถานะพร้อมแล้วส่งเป็น Custom Properties ของผู้เล่นให้ทุกเครื่องเห็น
    // ผูกกับแม็พและ MapRevision ปัจจุบัน; readyPending = true จนกว่า OnPlayerPropertiesUpdate ยืนยันกลับมา
    public void OnReadyButtonClicked()
    {
        if (!PhotonNetwork.InRoom || isLeavingRoom || isStartingGame || RoomStarting || reconnecting || !profileLoaded || readyPending) return;
        readyPending = true;
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [ReadyProperty] = !IsPlayerReady(PhotonNetwork.LocalPlayer),
            ["ReadyMap"] = ReadMap(PhotonNetwork.CurrentRoom),
            ["ReadyRevision"] = MapRevision,
            ["LoadoutLoaded"] = profileLoaded
        };
        string firebaseUid = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetUserId() : "";
        if (!string.IsNullOrWhiteSpace(firebaseUid)) props[FirebaseUidProperty] = firebaseUid;
        if (!PhotonNetwork.LocalPlayer.SetCustomProperties(props)) readyPending = false;
        UpdateWaitingRoomUI();
    }

    // ปุ่ม START BATTLE (ใช้ได้เฉพาะ Master Client เมื่อผู้เล่น 2 คนพร้อม):
    // ตั้ง room property Starting = true แบบมีเงื่อนไข (expected) ว่ายังไม่เริ่มและแม็พไม่ถูกเปลี่ยน
    // เมื่อ server ยืนยัน OnRoomPropertiesUpdate จะเรียก TryLaunchConfirmedRoom เพื่อโหลดเกมจริง
    public void OnStartGameClicked()
    {
        if (!CanStartGame()) { UpdateStatus("Both players must be ready before starting."); return; }
        if (!Application.CanStreamedLevelBeLoaded("SampleScene")) { UpdateStatus("Gameplay scene is missing from Build Profiles."); return; }
        // Wait for the server acknowledgement before loading; a map change invalidates this request.
        var expected = new ExitGames.Client.Photon.Hashtable { ["Starting"] = false, ["MapRevision"] = MapRevision };
        if (PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["Starting"] = true }, expected))
            UpdateStatus("Preparing battle...");
    }

    // เลื่อนเลือกแม็พถัดไป: ในห้องให้ Host เปลี่ยนแม็พของห้อง, นอกห้องเปลี่ยนแม็พที่จะใช้สร้างห้อง
    public void NextMap()
    {
        if (PhotonNetwork.InRoom) { ChangeRoomMap(1); return; }
        selectedMapIndex++;
        if (selectedMapIndex >= MapChoices) selectedMapIndex = 0;
        if (createRoomMapNameText != null) createRoomMapNameText.text = mapNames[selectedMapIndex];
        RefreshMapCards();
    }

    // เลื่อนเลือกแม็พก่อนหน้า (วนกลับไปท้ายรายการ)
    public void PrevMap()
    {
        if (PhotonNetwork.InRoom) { ChangeRoomMap(-1); return; }
        selectedMapIndex--;
        if (selectedMapIndex < 0) selectedMapIndex = MapChoices - 1;
        if (createRoomMapNameText != null) createRoomMapNameText.text = mapNames[selectedMapIndex];
        RefreshMapCards();
    }

    // ปุ่มออกจากห้องรอ: LeaveRoom(false) คือออกถาวร ไม่จองที่ไว้ให้กลับมา
    public void OnLeaveWaitingRoom()
    {
        if (!PhotonNetwork.InRoom || isLeavingRoom || isStartingGame || RoomStarting) return;
        isLeavingRoom = PhotonNetwork.LeaveRoom(false);
        if (isLeavingRoom) UpdateStatus("Leaving room...");
        UpdateWaitingRoomUI();
    }

    // อ่าน index แม็พจาก property ของห้อง ถ้าไม่มีใช้ 2 (หุ่นยนต์) เป็นค่าเริ่มต้น
    private int ReadMap(RoomInfo room)
    {
        return room != null && room.CustomProperties.TryGetValue(MapProperty, out object value) && value is int index
            ? Mathf.Clamp(index, 0, mapNames.Length - 1) : 2;
    }

    // เงื่อนไขเริ่มเกม: เป็น Master, อยู่ในห้อง, ไม่มีงานค้าง, มีผู้เล่นครบ 2 คน และทุกคน Ready
    private bool CanStartGame()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || isStartingGame || isLeavingRoom || RoomStarting || reconnecting) return false;
        // ครบ 2 คน หรือคนเดียวแต่เปิดบอทเติมห้องไว้
        // ห้องหลายคน: 2 คนขึ้นไป (ไม่เกินขนาดห้อง) เริ่มได้
        int players = PhotonNetwork.PlayerList.Length;
        if (players < 2 && !(players == 1 && MatchRules.BotFill(PhotonNetwork.CurrentRoom))) return false;
        foreach (Player player in PhotonNetwork.PlayerList) if (!IsPlayerReady(player)) return false;
        return true;
    }

    // รันบน Master Client หลัง server ยืนยัน Starting = true:
    // ตรวจความพร้อมซ้ำ ถ้ามีคนไม่พร้อมแล้วยกเลิก Starting; ถ้าพร้อมให้ปิดห้อง (ไม่ให้คนอื่นเข้า/ไม่โชว์ในรายการ)
    // สร้าง BattleToken ใหม่ของแมตช์นี้ ตั้ง StartTime = -1 (ยังไม่เริ่มนับเวลา) แล้ว LoadLevel ทุกเครื่องไป SampleScene
    private void TryLaunchConfirmedRoom()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !RoomStarting || isStartingGame) return;
        int humans = PhotonNetwork.PlayerList.Length;
        bool ready = humans >= 2 || (humans == 1 && MatchRules.BotFill(PhotonNetwork.CurrentRoom));
        foreach (Player player in PhotonNetwork.PlayerList) ready &= IsPlayerReady(player);
        if (!ready || isLeavingRoom)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["Starting"] = false });
            UpdateStatus("A pilot is no longer ready. Confirm readiness again.");
            return;
        }
        isStartingGame = true;
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        {
            ["BattleToken"] = System.Guid.NewGuid().ToString("N"),
            ["BattleAborted"] = false,
            ["StartTime"] = -1d,
            // จำนวนคนจริงที่เริ่มแมตช์ และจำนวนบอทที่ต้องสร้าง (เปิดบอทเติมห้อง: ขาดคนกี่ที่เติมบอทเท่านั้น)
            [MatchRules.HumansKey] = humans,
            [MatchRules.BotCountKey] = BotsToAdd(humans)
        });
        // โหมดทีม: แบ่งทีมสุดท้าย (เคารพทีมที่เลือก แต่ให้สองทีมต่างกันไม่เกิน 1 คน) เขียนเป็น T{ActorNumber}
        if (MatchRules.IsCoop(PhotonNetwork.CurrentRoom))
        {
            // Survival: คนจริงทุกคนอยู่ทีม 0 (บอทศัตรูเป็นทีม 1)
            var coop = new ExitGames.Client.Photon.Hashtable();
            foreach (Player player in PhotonNetwork.PlayerList) coop[MatchRules.TeamPrefix + player.ActorNumber] = 0;
            PhotonNetwork.CurrentRoom.SetCustomProperties(coop);
        }
        else if (MatchRules.IsTeamRoom(PhotonNetwork.CurrentRoom) && FeatureFlags.MultiPlayer && PhotonNetwork.CurrentRoom.MaxPlayers >= 4)
            PhotonNetwork.CurrentRoom.SetCustomProperties(MatchRules.AssignTeams(PhotonNetwork.PlayerList, out _, out _));
        PhotonNetwork.LoadLevel("SampleScene");
    }

    // จำนวนบอทที่จะเติม: ห้อง 1 VS 1 = ขาด 1 คนเติม 1 / ห้องหลายคน = เติมจนเต็มขนาดห้อง (ปิดบอทเติมห้อง = 0)
    private int BotsToAdd(int humans)
    {
        var room = PhotonNetwork.CurrentRoom;
        if (!MatchRules.BotFill(room) || MatchRules.IsCoop(room)) return 0; // Survival: บอทมาเป็นระลอกเอง
        int size = FeatureFlags.MultiPlayer ? Mathf.Min(room.MaxPlayers, MatchRules.MaxCombatants) : 2;
        return Mathf.Max(0, size - humans);
    }

    // เปลี่ยนแม็พในห้องไปข้างหน้า/ข้างหลังตาม direction แบบวนรอบ
    private void ChangeRoomMap(int direction)
    {
        SelectLobbyMap((ReadMap(PhotonNetwork.CurrentRoom) + direction + MapChoices) % MapChoices);
    }

    // เลือกแม็พ (กดการ์ดแม็พ): นอกห้องเก็บค่าในเครื่อง; ในห้องเฉพาะ Master เท่านั้นที่เปลี่ยนได้
    // ใช้ expected properties (MapRevision เดิมและยังไม่ Starting) แล้วเพิ่ม MapRevision เพื่อทำให้ Ready เดิมหมดอายุ
    public void SelectLobbyMap(int index)
    {
        if (index < 0 || index >= MapChoices) return;
        if (!PhotonNetwork.InRoom)
        {
            if (roomRequestPending || reconnecting) return;
            selectedMapIndex = index;
            if (createRoomMapNameText != null) createRoomMapNameText.text = mapNames[index];
            RefreshMapCards();
            return;
        }
        if (!PhotonNetwork.IsMasterClient || isStartingGame || isLeavingRoom || RoomStarting || reconnecting || index == ReadMap(PhotonNetwork.CurrentRoom)) return;
        var expected = new ExitGames.Client.Photon.Hashtable { ["MapRevision"] = MapRevision, ["Starting"] = false };
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { [MapProperty] = index, ["MapRevision"] = MapRevision + 1 }, expected);
    }

    // RoomStarting = ห้องกำลังเริ่มเกม, MapRevision = เลขรุ่นของการเลือกแม็พ (เพิ่มขึ้นทุกครั้งที่เปลี่ยน)
    // BoolProperty/IntProperty อ่านค่าจาก Hashtable อย่างปลอดภัย ถ้าไม่มีหรือชนิดไม่ตรงคืนค่า false/fallback
    private bool RoomStarting => PhotonNetwork.InRoom && BoolProperty(PhotonNetwork.CurrentRoom.CustomProperties, "Starting");
    private int MapRevision => PhotonNetwork.InRoom ? IntProperty(PhotonNetwork.CurrentRoom.CustomProperties, "MapRevision", 0) : 0;
    // คืน true เฉพาะเมื่อ key มีอยู่และเป็น bool ที่มีค่า true
    private static bool BoolProperty(ExitGames.Client.Photon.Hashtable props, string key)
        => props.TryGetValue(key, out object value) && value is bool flag && flag;
    // คืนค่า int ของ key ถ้ามีอยู่และเป็น int ไม่งั้นคืน fallback
    private static int IntProperty(ExitGames.Client.Photon.Hashtable props, string key, int fallback)
        => props.TryGetValue(key, out object value) && value is int number ? number : fallback;

    // ส่ง loadout ของเครื่องนี้ (ยาน สกิล สียาน UID) เป็น Custom Properties ให้ทุกเครื่องในห้องเห็น
    // resetReady = true จะตั้ง Ready เป็น false ด้วย (ใช้เมื่อเข้าห้อง/คนออก/เปลี่ยนแม็พ)
    private void PublishLocalLoadout(bool resetReady)
    {
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [ShipProperty] = Mathf.Clamp(equippedShipIndex, 0, ships.Length - 1),
            ["LoadoutLoaded"] = profileLoaded,
            [SkillProperty] = Mathf.Clamp(SkillUnlock.Usable(equippedSkillIndex), 0, skills.Length - 1),
            [ShipPaint.Property] = ShipPaint.Local
        };
        string firebaseUid = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetUserId() : "";
        if (!string.IsNullOrWhiteSpace(firebaseUid)) props[FirebaseUidProperty] = firebaseUid;
        if (resetReady) props[ReadyProperty] = false;
        // เลเวลและฉายาให้คนอื่นในห้องเห็น (เฟส 4)
        if (FeatureFlags.Progression) { props["Level"] = Progression.Level; props["Title"] = Progression.Title; }
        // MMR ให้คู่แข่งใช้คิดคะแนนแรงค์ (เฟส 6)
        if (FeatureFlags.Ranked) props["MMR"] = Ranked.Mmr;
        // ป้ายกิลด์ [TAG] (เฟส 8)
        props["GuildTag"] = FeatureFlags.Guilds ? Social.GuildTag : "";
        // โบนัสตีบวก/ไอเท็มของยานที่ใช้ ให้คนอื่นในห้องรอเห็นค่าพลังจริง (LobbyManager.UpgradeStats.cs)
        if (FeatureFlags.ShowRivalStats) props[StatBonusProperty] = LocalStatBonusText();
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }



    // เริ่มคำขอสร้าง/เข้าห้อง: ตรวจว่าโปรไฟล์โหลดแล้ว อยู่ใน Lobby และไม่มีคำขอค้าง แล้วล็อกปุ่มไว้จนได้ผล
    // คืน false ถ้ายังส่งคำขอไม่ได้
    private bool BeginRoomRequest()
    {
        if (!profileLoaded) { UpdateStatus("Wait for your loadout to finish loading."); return false; }
        if (roomRequestPending || reconnecting || !PhotonNetwork.InLobby || !PhotonNetwork.IsConnectedAndReady) return false;
        roomRequestPending = true;
        EnablePlayButtons(false);
        RefreshMapCards();
        return true;
    }
}

// ============================
//  SHIP DATA
// ============================
