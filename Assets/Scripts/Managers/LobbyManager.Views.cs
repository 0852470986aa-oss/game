// LobbyManager.Views.cs — ส่วนหน้าจอของ LobbyManager (partial class เดียวกับ LobbyManager.cs) ใน LobbyScene
// สร้าง UI ทุกหน้าด้วยโค้ด: หน้าหลัก, คลังยาน/สกิล (Hangar) + เลือกสียาน, หน้าค้นหาห้อง, ห้องรอ และการ์ดแม็พ
// โหลดโปรไฟล์ผู้เล่น (เหรียญ ยาน สกิล สถิติ) จาก FirebaseManager มาแสดง
// เมธอด UIRect/UIPanel/UILabel/UIButton จะหา object เดิมตามชื่อก่อน ทำให้แก้หน้าตาใน Editor แล้วไม่ถูกสร้างทับ
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

// หน้าหลัก คลังยาน และการโหลดข้อมูลเพื่อแสดงผล; ยังใช้ LobbyManager เดิม
// Runtime view keeps existing scene button bindings and inventory intact.
public partial class LobbyManager
{
    // สถานะการโหลดโปรไฟล์: profileLoaded = true เมื่อโหลดทั้งยาน (shipLoaded) และสกิล (skillLoaded) เสร็จ
    // readyPending = รอ server ยืนยันการกด Ready, reconnecting = กำลังพยายามกลับห้องเดิม
    private bool profileLoaded;
    private bool shipLoaded, skillLoaded, readyPending, reconnecting;
    // เลขรอบการโหลดโปรไฟล์ ใช้ทิ้งผลลัพธ์เก่าที่ตอบกลับมาช้าหลังเริ่มโหลดรอบใหม่แล้ว
    private int profileGeneration;
    // ชื่อห้องล่าสุดที่อยู่ ใช้ Rejoin เมื่อหลุด
    private string previousRoom;
    // เวลา (Time.unscaledTime) ที่หมดเขตโหลดโปรไฟล์, หมดเขตกู้ห้อง และครั้งถัดไปที่จะรีเฟรชสถานะ
    private float profileDeadline, reconnectDeadline, nextStatusRefresh;
    private TMP_Text lobbyMessage, browserMessage, connectionText, readyHint, emptyRoomsText;
    // รากของแต่ละหน้า (ขนาดออกแบบ 1280x720) ที่ FitLobbyUI ต้องย่อ/ขยายตามจอ
    private readonly List<RectTransform> fittedRoots = new List<RectTransform>();
    // กลุ่มการ์ดแม็พ (มีทั้งในหน้าค้นหาห้องและห้องรอ) เก็บไว้ให้ RefreshMapCards อัปเดตพร้อมกัน
    private readonly List<Button[]> mapCardGroups = new List<Button[]>();
    private readonly List<TMP_Text[]> mapLabelGroups = new List<TMP_Text[]>();
    // คำอธิบายแม็พบนการ์ด เรียงตาม index แม็พ (0 แมงกะพรุน, 1 ปริซึม, 2 หุ่นยนต์)
    private readonly string[] mapDescriptions = {
        "Cover and side routes.\nEnergy core: trade HP for fire rate.\nHazard: lightning strikes.",
        "Crystals bounce your shots.\nWarp gates link the corners.\nHazard: slowing pools.",
        "12 rocks / 2 turrets / 6 red cores.\nUse cover around the wreck.\nHazard: falling lava asteroids.",
        "Station ring in an asteroid field.\nEnergy gates guard the lanes.\nHazard: meteor strikes.",
        "Molten rocks burn on contact.\nOpen lava field, fast fights.\nHazard: falling lava asteroids."
    };
    private readonly string[] mapShortNames = { "JELLYFISH CORE", "PRISM PLAINS", "MECH WARZONE", "ASTEROID STATION", "MOLTEN NEBULA" };
    // สีพื้น panel และสีเน้น (ฟ้า) ของ UI ล็อบบี้
    private readonly Color panelColor = new Color(0.035f, 0.065f, 0.12f, 0.97f);
    private readonly Color accentColor = new Color(0.23f, 0.82f, 0.92f);
    private TMP_FontAsset lobbyFont;
    private TMP_Text homeSkillText;
    private TMP_Text hangarMessage;
    private TMP_Text hangarCoinText;

    // แสดงยอดเหรียญทั้งในหน้าหลักและหน้า Hangar
    private void UpdateCoinDisplay(long coins)
    {
        if (coinText != null) coinText.text = "Astronium Coins : " + coins;
        if (hangarCoinText != null) hangarCoinText.text = "ASTRONIUM  /  " + coins.ToString("N0");
    }
    // hangarBusy = กำลังรอผลการซื้อยาน (ล็อกไม่ให้สลับหน้า/เลือกยานอื่น); ที่เหลือคือ UI ของหน้า Hangar ที่สร้างตอนรัน
    private bool hangarBusy;
    private Button[] hangarSkillCards;
    private TMP_Text[] hangarSkillStates;
    private TMP_Text[] hangarCardLabels;
    private GameObject hangarShipsPage, hangarSkillsPage;

    // โหลดโปรไฟล์ผู้เล่นจาก Firebase (เรียกจาก Start และระบบลองใหม่อัตโนมัติ)
    // ทุก callback เช็ค generation และ this == null เพื่อทิ้งผลที่มาช้าหรือมาหลังเปลี่ยน Scene
    private void LoadLobbyProfile()
    {
        // 1) เริ่มรอบใหม่: รีเซ็ตสถานะ ปิดปุ่ม และตั้งเวลาหมดเขต 20 วินาที
        int generation = ++profileGeneration;
        profileLoaded = shipLoaded = skillLoaded = false;
        if (inventoryButton != null) inventoryButton.interactable = false;
        profileDeadline = Time.unscaledTime + 20f;
        EnablePlayButtons(false);
        UpdateStatus("Loading your ship and skill...");
        // เฟส 4: โหลดเลเวล/ภารกิจ (Progression.cs)
        LoadProgression();
        // เฟส 5: โหลดตีบวก/ไอเท็ม (Economy.cs)
        Economy.Load();
        // เฟส 6: โหลดแรงค์ (Ranked.cs)
        Ranked.Load();
        // ไม่มี FirebaseManager (เช่นกด Play ใน Editor ตรงจาก LobbyScene) ใช้ยาน/สกิล 0
        if (FirebaseManager.Instance == null)
        {
            // Direct editor entry uses an explicit default loadout.
            equippedShipIndex = equippedSkillIndex = 0;
            shipLoaded = skillLoaded = true;
            FinishProfileLoad();
            return;
        }
        // 2) แสดงชื่อผู้เล่น
        if (playerNameText != null)
        {
            playerNameText.richText = false;
            playerNameText.text = FirebaseManager.Instance.GetUsername();
        }
        // 3) อ่านสถิติ (คะแนนสูงสุด ชนะ แพ้) และยอดเหรียญ
        FirebaseManager.Instance.GetPlayerStats((highScore, wins, losses) =>
        {
            if (this == null || generation != profileGeneration || winsText == null) return;
            winsText.text = $"HIGH SCORE  {highScore:N0}     W  {wins:N0}  /  L  {losses:N0}";
        });
        FirebaseManager.Instance.GetCoinBalance(coins => {
            if (this == null || generation != profileGeneration) return;
            UpdateCoinDisplay(coins);
        }, error => {
            if (this == null || generation != profileGeneration) return;
            if (coinText != null) coinText.text = "Astronium Coins : unavailable";
            if (hangarCoinText != null) hangarCoinText.text = "ASTRONIUM / unavailable";
            UpdateStatus(error);
        });
        // 4) อ่านยานที่ปลดล็อก (ยาน 0 มีเสมอ) แล้วอ่านยานที่ใส่อยู่ ถ้าไม่ได้ครอบครองให้กลับเป็นยาน 0
        FirebaseManager.Instance.GetUnlockedShips(result => {
            if (this == null || generation != profileGeneration) return;
            unlockedShips = result ?? new List<int> { 0 };
            if (!unlockedShips.Contains(0)) unlockedShips.Add(0);
            FirebaseManager.Instance.GetSelectedShip(index => {
                if (this == null || generation != profileGeneration) return;
                equippedShipIndex = index >= 0 && index < ships.Length && unlockedShips.Contains(index) ? index : 0;
                selectedShipIndex = equippedShipIndex;
                shipLoaded = true;
                FinishProfileLoad();
            });
        });
        // 5) อ่านสกิลที่ใส่อยู่ เมื่อโหลดครบทั้งยานและสกิลแล้ว FinishProfileLoad จะเปิดปุ่มต่าง ๆ
        FirebaseManager.Instance.GetSelectedSkill(index => {
            if (this == null || generation != profileGeneration) return;
            equippedSkillIndex = Mathf.Clamp(index, 0, skills.Length - 1);
            selectedSkillIndex = equippedSkillIndex;
            skillLoaded = true;
            FinishProfileLoad();
        });
    }

    // เรียกหลังโหลดยานหรือสกิลเสร็จแต่ละอย่าง: ถ้าครบทั้งสองแล้วจึงแสดงผล เปิดปุ่มเล่น
    // และถ้าอยู่ในห้องอยู่แล้วจะส่ง loadout ใหม่ให้อีกฝ่ายเห็น
    private void FinishProfileLoad()
    {
        profileLoaded = shipLoaded && skillLoaded;
        if (!profileLoaded) return;
        if (inventoryButton != null) inventoryButton.interactable = true;
        UpdateShipDisplay(equippedShipIndex);
        UpdateSkillDisplay(equippedSkillIndex);
        if (PhotonNetwork.InRoom) PublishLocalLoadout(true);
        EnablePlayButtons(true);
        RenderRoomList();
        UpdateStatus(PhotonNetwork.IsConnectedAndReady ? "Loadout ready. Create or join a room." : "Loadout ready. Connecting...");
    }

    // public method สำหรับสั่งโหลดโปรไฟล์/เชื่อมต่อ Photon ใหม่ด้วยตนเอง (ปกติระบบใน Update ลองใหม่อัตโนมัติอยู่แล้ว)
    public void RetryLobbyConnection()
    {
        if (roomRequestPending || reconnecting || PhotonNetwork.InRoom) return;
        if (!profileLoaded) LoadLobbyProfile();
        if (!PhotonNetwork.IsConnected)
        {
            UpdateStatus("Connecting...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
    }

    // เรียกเมื่อส่งคำสั่งสร้าง/เข้าห้องไม่สำเร็จตั้งแต่ฝั่งเครื่อง (Photon คืน false) ปลดล็อกปุ่มให้ลองใหม่
    private void RoomRequestFailed()
    {
        roomRequestPending = false;
        EnablePlayButtons(true);
        RenderRoomList();
        UpdateStatus("Could not send the request. Please try again.");
    }

    // ปุ่ม COPY CODE ในห้องรอ: คัดลอกรหัสห้องไปคลิปบอร์ดเพื่อส่งให้เพื่อน
    public void CopyRoomCode()
    {
        if (!PhotonNetwork.InRoom) return;
        if (RankedPrivate(PhotonNetwork.CurrentRoom)) { UpdateStatus("Ranked rooms can't be shared."); return; }
        GUIUtility.systemCopyBuffer = PhotonNetwork.CurrentRoom.Name;
        UpdateStatus("Room code copied. Share it with your friend.");
    }

    // เปลี่ยนข้อความบนปุ่ม (TMP_Text ลูกตัวแรก)
    private static void SetButtonLabel(Button button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
    }

    // หา RectTransform ลูกตามชื่อ ถ้าไม่มีจึงสร้างใหม่ที่ตำแหน่ง/ขนาดที่กำหนด (หน่วยพิกเซลของจอออกแบบ 1280x720)
    // ถ้ามีอยู่แล้วจะไม่แก้ตำแหน่ง ทำให้ปรับ layout ใน Editor แล้วค่าไม่หาย
    private RectTransform UIRect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null) rect = FindNavigationControl(parent, name);
        if (rect == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create editable lobby UI");
#endif
            UiLayout.Placed(rect); // ชิ้นที่สร้างใหม่ (รวมการ์ดในโรงเก็บยาน) ใช้ตำแหน่งที่บันทึกเองถ้ามี
        }
        return rect;
    }

    // สร้าง/หา panel ที่มี Image สี; ตั้งสีเฉพาะตอนสร้างครั้งแรก
    private Image UIPanel(string name, Transform parent, float x, float y, float width, float height, Color color)
    {
        bool initializeStyle = parent.Find(name) == null && FindNavigationControl(parent, name) == null;
        RectTransform rect = UIRect(name, parent, x, y, width, height);
        Image img = rect.GetComponent<Image>();
        if (img == null)
        {
            img = rect.gameObject.AddComponent<Image>();
            initializeStyle = true;
        }
        if (initializeStyle)
        {
            img.color = color;
            img.raycastTarget = false;
        }
        return img;
    }

    // สร้าง/หา label TextMeshPro; ตั้งข้อความทุกครั้ง แต่ตั้งฟอนต์ ขนาด สี การจัดวางเฉพาะตอนสร้างครั้งแรก
    private TMP_Text UILabel(string name, Transform parent, string value, float x, float y, float w, float h, int size, Color color)
    {
        RectTransform rect = UIRect(name, parent, x, y, w, h);
        var text = rect.GetComponent<TextMeshProUGUI>();
        bool initializeStyle = text == null;
        if (initializeStyle) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (text.font == null)
        {
            if (lobbyFont != null) text.font = lobbyFont;
            initializeStyle = true;
        }
        text.text = value;
        if (initializeStyle)
        {
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12, size - 5);
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }
        return text;
    }

    // สร้าง/หาปุ่ม: ผูก action ใหม่ทุกครั้ง (ล้าง listener เดิมก่อน) และเพิ่มเสียงคลิก SFX_Click
    private Button UIButton(string name, Transform parent, string value, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
    {
        Image image = UIPanel(name, parent, x, y, w, h, new Color(0.08f, 0.26f, 0.36f));
        image.raycastTarget = true;
        Button button = image.GetComponent<Button>();
        bool initializeStyle = button == null;
        if (initializeStyle) button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (initializeStyle)
        {
            var colors = button.colors;
            colors.highlightedColor = new Color(0.65f, 1f, 1f);
            colors.pressedColor = new Color(0.35f, 0.65f, 0.8f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            button.colors = colors;
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        button.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Click"); });
        UILabel("Label", image.transform, value, 0, 0, w - 16, h - 8, 20, Color.white);
        return button;
    }

    // สร้างพื้นผิวหลักของ panel ("LobbySurface" ขนาด 1280x720): ครั้งแรกจะซ่อนลูกเดิมของ panel และใส่พื้นหลังทึบ
    // แล้วลงทะเบียนให้ FitLobbyUI ปรับขนาดตามจอ
    private RectTransform BuildSurface(GameObject panel)
    {
        RectTransform root = panel.transform.Find("LobbySurface") as RectTransform;
        if (root == null)
        {
            foreach (Transform child in panel.transform) child.gameObject.SetActive(false);
            var shade = UIPanel("LobbyBackdrop", panel.transform, 0, 0, 0, 0, new Color(0.015f, 0.025f, 0.06f, 0.98f));
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.raycastTarget = true;
            root = UIRect("LobbySurface", panel.transform, 0, 0, 1280, 720);
        }
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
        if (!fittedRoots.Contains(root)) fittedRoots.Add(root);
        UIPanel("TopLine", root, 0, 274, 1200, 2, accentColor);
        return root;
    }

    // สร้าง UI ของทุกหน้าในล็อบบี้ (เรียกจาก Awake และจากเมนู Editor) ขั้นตอน:
    // 1) ห้องรอ  2) หน้าค้นหา/สร้างห้อง  3) หน้าหลัก  4) Hangar  แล้วปรับขนาดตามจอ
    private void BuildLobbyUI()
    {
        fittedRoots.Clear();
        mapCardGroups.Clear();
        mapLabelGroups.Clear();
        lobbyFont = statusText != null ? statusText.font : TMP_Settings.defaultFontAsset;
        // 1) ห้องรอ: รหัสห้อง, การ์ดผู้เล่น 2 ใบ, การ์ดแม็พ, ปุ่ม LEAVE / READY / START
        if (waitingRoomPanel != null)
        {
            RectTransform root = BuildSurface(waitingRoomPanel);
            UILabel("Title", root, "BATTLE PREPARATION", -350, 314, 500, 42, 32, Color.white);
            waitRoomNumberText = UILabel("RoomCode", root, "ROOM", 190, 314, 300, 40, 24, accentColor);
            UIButton("CopyCode", root, "COPY CODE", 485, 314, 200, 48, CopyRoomCode);
            connectionText = UILabel("Connection", root, "CONNECTING", 420, 246, 340, 30, 16, accentColor);
            // ปุ่มโหมด/จำนวนคน (กดวน 1 VS 1 -> FFA 4P -> ... -> 10P) อยู่ใต้ป้าย Mode เดิม ดู LobbyManager.RoomSettings.cs
            BuildRoomModeButton(root);
            roomModeLabel = UILabel("Mode", root, "1 VS 1", -510, 246, 180, 30, 18, accentColor);
            roomModeLabel.raycastTarget = false;
            roomModeLabel.transform.SetAsLastSibling();
            // ปุ่มตั้งค่าห้อง (Host กดวนค่าได้ คนอื่นดูได้อย่างเดียว) ดู LobbyManager.RoomSettings.cs
            BuildRoomSettingButtons(root);
            BuildRoomUpgradesButton(root);
            BuildRoomPowerUpsButton(root);
            BuildRoomGameModeButton(root);
            BuildSocialWaiting(root);
            BuildPilotCard(root, -306, true);
            BuildPilotCard(root, 306, false);
            // ตารางนักบิน 10 ช่อง สำหรับห้องหลายคน (ซ่อนไว้ ใช้แทนการ์ด 2 ใบ) ดู LobbyManager.Roster.cs
            BuildRoster(root);
            UILabel("MapsHeading", root, "SELECT BATTLEFIELD  /  HOST CONTROLS MAP", 0, -37, 900, 28, 17, accentColor);
            BuildMapCards(root, -158, false);
            readyHint = UILabel("ReadyHint", root, "", 0, -288, 1150, 28, 17, Color.white);
            waitCancelButton = UIButton("LeaveRoom", root, "LEAVE ROOM", -420, -334, 270, 52, OnLeaveWaitingRoom);
            waitReadyButton = UIButton("Ready", root, "READY", 0, -334, 270, 52, OnReadyButtonClicked);
            waitStartButton = UIButton("StartBattle", root, "START BATTLE", 420, -334, 270, 52, OnStartGameClicked);
            lobbyMessage = UILabel("Message", root, "", 0, -263, 1160, 24, 15, new Color(1f, 0.77f, 0.43f));
            BuildRoomNavigation(root);
            LayoutPrepRoom(root); // หน้าเตรียมพร้อมรบแบบใหม่ (LobbyManager.NewLayout.cs)
        }
        // 2) หน้าค้นหาห้อง: ฝั่งซ้ายสร้างห้อง ฝั่งขวาใส่รหัสเข้าห้องและรายการห้องแบบเลื่อนได้ ด้านล่างเป็นการ์ดแม็พ
        if (roomPanel != null)
        {
            RectTransform root = BuildSurface(roomPanel);
            UILabel("Title", root, "FIND YOUR BATTLE", -340, 314, 540, 42, 32, Color.white);
            backFromRoomButton = UIButton("Back", root, "BACK", 490, 314, 190, 48, ShowMainPanel);
            UILabel("CreateTitle", root, "CREATE ROOM", -305, 235, 540, 36, 24, accentColor);
            roomNumberText = UILabel("RoomNumber", root, "000000", -305, 189, 540, 36, 28, Color.white);
            roomModeText = UILabel("RoomMode", root, "1 VS 1", -305, 150, 540, 28, 18, Color.white);
            createRoomMapNameText = UILabel("SelectedMap", root, "", -305, 105, 540, 36, 20, accentColor);
            createRoomConfirmButton = UIButton("Create", root, "CREATE ROOM", -305, 46, 510, 54, OnCreateRoomConfirm);
            UILabel("JoinTitle", root, "JOIN WITH ROOM CODE", 305, 235, 540, 36, 24, accentColor);
            // Reuse the existing input field so keyboard and font setup are retained.
            if (roomSearchInput != null)
            {
                if (roomSearchInput.transform.parent != root)
                {
                    roomSearchInput.transform.SetParent(root, false);
                    var rect = roomSearchInput.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(220, 180);
                    rect.sizeDelta = new Vector2(340, 52);
                }
                roomSearchInput.characterLimit = 32;
                roomSearchInput.gameObject.SetActive(true);
            }
            searchRoomButton = UIButton("Join", root, "JOIN", 489, 180, 160, 52, OnSearchRoom);
            // Use a masked scroll area; the original inactive template is preserved.
            var viewport = UIPanel("RoomViewport", root, 305, 57, 540, 164, panelColor);
            viewport.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIRect("RoomEntries", viewport.transform, 0, 0, 540, 0);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.spacing = 8; layout.padding = new RectOffset(6, 6, 6, 6);
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>();
            if (scroll == null) scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            if (roomItemPrefab != null) { roomItemPrefab.transform.SetParent(root, false); roomItemPrefab.SetActive(false); }
            roomListContent = content;
            emptyRoomsText = UILabel("EmptyRooms", viewport.transform, "No open rooms yet. Create one or enter a code.", 0, 0, 500, 70, 18, Color.gray);
            UILabel("MapHeading", root, "CHOOSE YOUR BATTLEFIELD", 0, -53, 1100, 30, 18, accentColor);
            BuildMapCards(root, -175, true);
            browserMessage = UILabel("BrowserMessage", root, "", 0, -307, 1140, 32, 18, Color.white);
            // Connection recovery is automatic; no retry button is needed.
        }
        // 3) หน้าหลัก และ 4) หน้า Hangar
        if (mainPanel != null)
        {
            BuildHomeScreen();
        }
        if (inventoryPanel != null) { BuildHangarScreen(); BuildHangarNavigation(); }
        ApplyLobbyIcons(); // ไอคอนหน้าข้อความปุ่ม (LobbyManager.Icons.cs) — ทำหลังจัดหน้าเสร็จทุกอย่าง
        FitLobbyUI();
        if (Application.isPlaying) UiLayout.ApplyAll(); // ตำแหน่งที่บันทึกเองของทุกชิ้น (UiLayout.cs)
    }

#if UNITY_EDITOR
    // เมนูคลิกขวาใน Inspector (เฉพาะ Editor ไม่ได้รันในเกม): สร้าง/อัปเดต UI ล็อบบี้ลง Scene เพื่อดูและแก้หน้าตา
    [ContextMenu("UI Preview/Build or Update Lobby Screens")]
    private void BuildEditableLobbyScreens()
    {
        if (Application.isPlaying) return;
        if (mainPanel == null)
        {
            Debug.LogError("Lobby UI preview needs the Main Panel reference assigned on LobbyManager.", this);
            return;
        }

        BuildLobbyUI();
        ShowLobbyEditorPreview(mainPanel);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // เมนู Editor: แสดงหน้าหลักเพื่อดูตัวอย่าง
    [ContextMenu("UI Preview/Show Home")]
    private void PreviewLobbyHome() => ShowLobbyEditorPreview(mainPanel);

    // เมนู Editor: แสดงหน้า Hangar
    [ContextMenu("UI Preview/Show Hangar")]
    private void PreviewLobbyHangar() => ShowLobbyEditorPreview(inventoryPanel);

    // เมนู Editor: แสดงหน้าค้นหาห้อง
    [ContextMenu("UI Preview/Show Room Browser")]
    private void PreviewLobbyRoomBrowser() => ShowLobbyEditorPreview(roomPanel);

    // เมนู Editor: แสดงหน้าห้องรอ
    [ContextMenu("UI Preview/Show Waiting Room")]
    private void PreviewLobbyWaitingRoom() => ShowLobbyEditorPreview(waitingRoomPanel);

    // เมนู Editor: แสดงหน้าตั้งค่า
    [ContextMenu("UI Preview/Show Settings")]
    private void PreviewLobbySettings() => ShowLobbyEditorPreview(settingsPanel);

    // เมนู Editor: แสดงหน้าวิธีเล่น
    [ContextMenu("UI Preview/Show How to Play")]
    private void PreviewLobbyTutorial() => ShowLobbyEditorPreview(tutorialPanel);

    // สร้างต้นแบบหน้าตั้งค่า (ปุ่มเฟือง) ลง Scene ให้แก้หน้าตาได้; ตอนรันจะคัดลอกต้นแบบนี้ไปใช้
    [ContextMenu("UI Preview/Build Settings Panel Template")]
    private void BuildLobbySettingsTemplate()
    {
        if (Application.isPlaying) return;
        BattleSettingsPanel.BuildEditableTemplate(false);
    }

    // เปิดเฉพาะ panel ที่เลือกแล้วปิดที่เหลือ และ mark Scene ว่ามีการแก้ไขเพื่อให้บันทึกได้
    private void ShowLobbyEditorPreview(GameObject selectedPanel)
    {
        if (Application.isPlaying || selectedPanel == null) return;
        foreach (GameObject panel in new[] { mainPanel, inventoryPanel, roomPanel, settingsPanel, waitingRoomPanel, tutorialPanel })
            if (panel != null) panel.SetActive(panel == selectedPanel);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    // ---------------- สียาน (PAINT) ----------------
    // แถวปุ่มสีใต้กล่องรายละเอียดยาน ใช้กับยานทุกลำ ฟรี ไม่ต้องซื้อ
    private Button[] paintSwatches;

    // สร้างปุ่มสีเรียงแนวนอนตามจำนวนสีใน ShipPaint.Colors
    private void BuildPaintPicker()
    {
        UILabel("PaintTitle", hangarShipsPage.transform, "PAINT", -40, -229, 120, 30, 17, accentColor);
        paintSwatches = new Button[ShipPaint.Colors.Length];
        for (int i = 0; i < paintSwatches.Length; i++)
        {
            int paint = i;
            var swatch = UIButton("Paint" + i, hangarShipsPage.transform, "", 40 + i * 52, -229, 40, 40, () => SelectPaint(paint));
            swatch.GetComponent<Image>().color = ShipPaint.Colors[i];
            paintSwatches[i] = swatch;
        }
        RefreshPaintSwatches();
    }

    // เลือกสียาน: บันทึกใน PlayerPrefs ผ่าน ShipPaint.Local แล้วเปลี่ยนสีรูปยานในหน้า Hangar และหน้าหลักทันที
    private void SelectPaint(int paint)
    {
        ShipPaint.Local = paint;
        RefreshPaintSwatches();
        if (inventoryShipImage != null) inventoryShipImage.color = ShipPaint.LocalColor;
        if (shipImage != null) shipImage.color = ShipPaint.LocalColor;
        if (hangarMessage != null) hangarMessage.text = "Paint: " + ShipPaint.Names[ShipPaint.Local] + "  /  applies to every ship.";
    }

    // ขยายปุ่มสีที่เลือกอยู่ 1.25 เท่าให้เห็นชัด
    private void RefreshPaintSwatches()
    {
        if (paintSwatches == null) return;
        for (int i = 0; i < paintSwatches.Length; i++)
            if (paintSwatches[i] != null)
                paintSwatches[i].transform.localScale = Vector3.one * (i == ShipPaint.Local ? 1.25f : 1f);
    }

    // สร้างหน้า Hangar (คลังยาน/สกิล) ขั้นตอน:
    // 1) หัวข้อ เหรียญ ปุ่ม BACK และแท็บ SHIPS/SKILLS  2) การ์ดยานทางซ้าย  3) กล่องรายละเอียดยาน + ปุ่ม EQUIP/BUY + สียาน
    // 4) การ์ดสกิล + คำอธิบาย + ปุ่ม INSTALL  5) ข้อความสถานะด้านล่าง
    private void BuildHangarScreen()
    {
        RectTransform root = BuildSurface(inventoryPanel);
        UILabel("HangarTitle", root, "PILOT HANGAR", -355, 315, 480, 45, 33, Color.white);
        hangarCoinText = UILabel("HangarCoins", root, "ASTRONIUM  /  ...", 110, 315, 390, 45, 23, new Color(1f, 0.8f, 0.35f));
        PolishSprites.AddCoinIcon(hangarCoinText);
        backFromInventoryButton = UIButton("Back", root, "BACK", 485, 315, 200, 50, () => { if (!hangarBusy) ShowMainPanel(); });
        // 1) แท็บสลับหน้า SHIPS / SKILLS
        UIButton("ShipsTab", root, "SHIPS", -180, 231, 280, 50, () => SetHangarPage(false));
        UIButton("SkillsTab", root, "SKILLS", 180, 231, 280, 50, () => SetHangarPage(true));
        hangarShipsPage = UIRect("ShipsPage", root, 0, -15, 1200, 430).gameObject;
        hangarSkillsPage = UIRect("SkillsPage", root, 0, -15, 1200, 430).gameObject;
        // 2) การ์ดยานแต่ละลำ (รูป ชื่อ สถานะครอบครอง)
        shipButtons = new Button[ships.Length];
        hangarCardLabels = new TMP_Text[ships.Length];
        for (int i = 0; i < ships.Length; i++)
        {
            int index = i;
            var card = UIButton("ShipCard" + i, hangarShipsPage.transform, "", -405, 140 - i * 142, 370, 126, () => SelectShip(index));
            var art = UIPanel("ShipArt", card.transform, -120, 0, 140, 115, Color.white);
            art.sprite = BattleLoadoutCatalog.ShipSprite(i); art.preserveAspect = true;
            art.color = BattleLoadoutCatalog.ShipTint(i);
            UILabel("Name", card.transform, ships[i].name, 53, 26, 245, 45, 23, Color.white);
            hangarCardLabels[i] = UILabel("Ownership", card.transform, "", 53, -27, 235, 34, 17, accentColor);
            shipButtons[i] = card;
        }
        // 3) กล่องรายละเอียดยานที่เลือก
        var details = UIPanel("ShipDetails", hangarShipsPage.transform, 200, 0, 760, 415, panelColor);
        inventoryShipName = UILabel("Name", details.transform, "", 0, 165, 700, 50, 32, Color.white);
        inventoryShipImage = UIPanel("Preview", details.transform, -192, -12, 350, 275, Color.white);
        inventoryShipImage.preserveAspect = true;
        inventoryShipHP = UILabel("HP", details.transform, "", 170, 88, 310, 40, 25, Color.white);
        inventoryShipATK = UILabel("ATK", details.transform, "", 170, 33, 310, 40, 25, Color.white);
        inventoryShipSPD = UILabel("SPD", details.transform, "", 170, -22, 310, 40, 25, Color.white);
        inventoryShipSkill = null;
        UILabel("Tip", details.transform, "Choose a ship, then equip it for your next battle.", 0, -170, 690, 32, 17, Color.gray);
        inventoryActionButton = UIButton("EquipBuy", details.transform, "EQUIP", 170, -98, 320, 62, OnInventoryActionClicked);
        inventoryActionText = inventoryActionButton.GetComponentInChildren<TMP_Text>();
        BuildPaintPicker();
        // 4) การ์ดสกิลเรียงแนวนอน
        hangarSkillCards = new Button[skills.Length];
        hangarSkillStates = new TMP_Text[skills.Length];
        for (int i = 0; i < skills.Length; i++)
        {
            int index = i;
            var card = UIButton("SkillCard" + i, hangarSkillsPage.transform, "", -435 + i * 290, 116, 270, 160, () => SelectSkill(index));
            hangarSkillCards[i] = card;
            hangarSkillStates[i] = UILabel("State", card.transform, "", 0, 65, 245, 22, 13, Color.white);
            var icon = UIPanel("Icon", card.transform, 0, 20, 88, 88, Color.white);
            icon.sprite = Resources.Load<Sprite>(skills[i].iconPath); icon.preserveAspect = true;
            UILabel("SkillName", card.transform, skills[i].name, 0, -55, 245, 35, 22, accentColor);
        }
        // เฟส 7: ยาน/สกิลเกิน 3/4 อัน → จัดการ์ดใหม่ให้พอดีจอ (LobbyManager.Content.cs)
        LayoutExtraHangarCards();
        skillDescText = UILabel("SkillDetails", hangarSkillsPage.transform, "", -170, -85, 770, 150, 28, Color.white);
        installSkillButton = UIButton("Install", hangarSkillsPage.transform, "INSTALL", 415, -88, 280, 70, OnInstallSkillClicked);
        installSkillText = installSkillButton.GetComponentInChildren<TMP_Text>();
        UILabel("SkillHint", hangarSkillsPage.transform, "Select a skill to see its effect and cooldown. Install it to change your loadout.", 0, -196, 1150, 32, 17, Color.gray);
        // 5) ข้อความสถานะและ footer
        hangarMessage = UILabel("HangarMessage", root, "Choose a ship or open the SKILLS tab.", 0, -288, 1150, 44, 19, Color.white);
        UILabel("HangarFooter", root, "LOADOUT / YOUR NEXT BATTLE STARTS HERE", 0, -340, 1150, 24, 14, Color.gray);
        SetHangarPage(false);
    }

    // สลับแท็บ SHIPS/SKILLS (ห้ามสลับระหว่างรอผลการซื้อ) แล้วอัปเดตข้อมูลบนหน้านั้น
    private void SetHangarPage(bool skillsPage)
    {
        if (hangarBusy) return;
        CloseEmbeddedWorkshop(false);
        hangarShipsPage.SetActive(!skillsPage);
        hangarSkillsPage.SetActive(skillsPage);
        StyleHangarTabs(skillsPage);
        UpdateInventoryDisplay(selectedShipIndex);
        UpdateSkillDisplay(selectedSkillIndex);
    }

    // อัปเดตป้ายบนการ์ดยาน (EQUIPPED / OWNED / ราคา) และไฮไลต์การ์ดที่เลือกอยู่
    private void RefreshHangarCards()
    {
        if (hangarCardLabels == null) return;
        for (int i = 0; i < ships.Length; i++)
        {
            hangarCardLabels[i].text = i == equippedShipIndex ? "EQUIPPED"
                : unlockedShips.Contains(i) ? "OWNED" : ships[i].price + " COINS";
            shipButtons[i].GetComponent<Image>().color = i == selectedShipIndex
                ? new Color(0.1f, 0.38f, 0.46f) : panelColor;
        }
    }

    // สร้างหน้าหลัก: โปรไฟล์นักบิน (ชื่อ สถิติ สถานะออนไลน์), ยอดเหรียญ, ยานที่ใช้อยู่, ปุ่ม QUICK MATCH / สร้างห้อง / Hangar / วิธีเล่น
    private void BuildHomeScreen()
    {
        RectTransform root = BuildSurface(mainPanel);
        UILabel("GameTitle", root, "BATTLEFIELD OF THE STARS", -225, 315, 760, 48, 33, Color.white);
        settingsButton = UIButton("Settings", root, "SETTINGS", 465, 315, 220, 50, OnSettingsClicked);
        // โปรไฟล์นักบินมุมซ้ายบน และยอดเหรียญมุมขวาบน
        var profile = UIPanel("PilotProfile", root, -330, 222, 550, 94, panelColor);
        Image pilotPortrait = UIPanel("PilotPortrait", profile.transform, -222, 0, 58, 82, Color.white);
        pilotPortrait.sprite = Resources.Load<Sprite>("Images/หน้าตัวละครเอก");
        pilotPortrait.preserveAspect = true;
        pilotPortrait.raycastTarget = false;
        playerNameText = UILabel("PilotName", profile.transform, "LOADING PILOT...", 35, 25, 420, 32, 23, Color.white);
        playerNameText.richText = false;
        winsText = UILabel("PilotStats", profile.transform, "HIGH SCORE  0     W  0  /  L  0", 35, -5, 420, 25, 15, new Color(1f, 0.8f, 0.35f));
        playersOnlineText = UILabel("OnlineStatus", profile.transform, "CONNECTING...", 35, -32, 420, 22, 13, accentColor);
        coinText = UILabel("CoinBalance", root, "Astronium Coins : ...", 305, 230, 550, 40, 24, new Color(1f, 0.8f, 0.35f));
        PolishSprites.AddCoinIcon(coinText);
        UILabel("CurrencyHint", root, "YOUR PILOT WALLET", 305, 195, 550, 24, 14, Color.gray);

        // กล่องยานและสกิลที่ใส่อยู่
        var hangar = UIPanel("ActiveShip", root, -235, -45, 740, 400, panelColor);
        UILabel("HangarTitle", hangar.transform, "ACTIVE SHIP / HANGAR", 0, 167, 680, 32, 18, accentColor);
        shipNameText = UILabel("ShipName", hangar.transform, "Loading ship...", 0, 120, 680, 45, 34, Color.white);
        // Imported art contains transparent margins; reserve a large preview area.
        shipImage = UIPanel("ShipPreview", hangar.transform, -190, -30, 340, 280, Color.clear);
        shipImage.preserveAspect = true;
        shipHPText = UILabel("HP", hangar.transform, "HP: --", 150, 55, 290, 38, 25, Color.white);
        shipATKText = UILabel("ATK", hangar.transform, "ATK: --", 150, 4, 290, 38, 25, Color.white);
        shipSPDText = UILabel("SPD", hangar.transform, "SPD: --", 150, -47, 290, 38, 25, Color.white);
        homeSkillText = UILabel("ActiveSkill", hangar.transform, "LOADING SKILL...", 150, -104, 310, 38, 19, accentColor);
        shipSkillText = null;
        UILabel("LoadoutHint", hangar.transform, "Change your ship and skill in the hangar.", 0, -169, 670, 26, 16, Color.gray);

        // ปุ่มเริ่มเล่นทางขวา
        UILabel("BattleTitle", root, "READY FOR BATTLE?", 395, 127, 370, 44, 26, Color.white);
        bool initializeQuickMatchStyle = root.Find("QuickMatch") == null;
        playButton = UIButton("QuickMatch", root, "QUICK MATCH", 395, 50, 370, 82, OnPlayButtonClicked);
        if (initializeQuickMatchStyle) playButton.GetComponent<Image>().color = new Color(0.1f, 0.49f, 0.5f);
        createRoomButton = UIButton("CreateJoin", root, "CREATE / JOIN ROOM", 395, -47, 370, 64, OnCreateRoomClicked);
        inventoryButton = UIButton("OpenHangar", root, "SHIPS & SKILLS", 395, -129, 370, 64, ShowInventoryPanel);
        UIButton("HowToPlay", root, "HOW TO PLAY", 395, -205, 370, 52, OnTutorialClicked);
        // แถวเล่นคนเดียว: ความยากบอท / VS BOT / TRAINING (ดู LobbyManager.Solo.cs)
        BuildSoloButtons(root);
        // เลเวล / ภารกิจ / โปรไฟล์ (LobbyManager.Progress.cs)
        BuildProgressUI(root);
        // โรงซ่อม: ตีบวก / ไอเท็ม / ร้านค้า (LobbyManager.Workshop.cs)
        BuildWorkshopUI(root);
        // แรงค์ / ตารางอันดับ (LobbyManager.Ranked.cs)
        BuildRankedUI(root);
        // เพื่อน / แชท / กิลด์ (LobbyManager.Social.cs)
        BuildSocialHome(root);
        // ข้อความสถานะด้านล่าง แล้วแสดงยาน/สกิลปัจจุบัน
        statusText = UILabel("HomeStatus", root, "Loading pilot data...", -215, -292, 750, 38, 18, Color.white);
        statusText.richText = false;
        // Connection status above reports automatic recovery instead of a manual retry button.
        UILabel("Footer", root, FeatureFlags.MultiPlayer ? "PILOT HUB  /  1 VS 1  &  FREE-FOR-ALL UP TO 10 PILOTS" : "PILOT HUB  /  1 VS 1 MULTIPLAYER", 0, -339, 1150, 24, 14, Color.gray);
        UpdateShipDisplay(equippedShipIndex);
        UpdateSkillDisplay(equippedSkillIndex);
        BuildHomeNavigation(root);
    }

    // สร้างการ์ดผู้เล่นในห้องรอ (first = ฝั่ง P1 ซ้าย, ไม่ใช่ = P2 ขวา) แล้วเก็บ reference ไว้ให้ RenderPlayer ใช้
    private void BuildPilotCard(Transform parent, float x, bool first)
    {
        Image card = UIPanel(first ? "PilotOne" : "PilotTwo", parent, x, 108, 586, 222, panelColor);
        UIPanel("Accent", card.transform, -286, 0, 3, 214, accentColor);
        TMP_Text name = UILabel("PilotName", card.transform, "", 0, 85, 548, 38, 23, Color.white);
        Image ship = UIPanel("ShipPreview", card.transform, -187, -15, 270, 196, Color.clear);
        TMP_Text shipName = UILabel("ShipName", card.transform, "", 85, 40, 340, 35, 24, accentColor);
        TMP_Text stats = UILabel("Stats", card.transform, "", 85, 4, 340, 30, 16, Color.white);
        TMP_Text skill = UILabel("Skill", card.transform, "", 85, -30, 340, 30, 16, Color.white);
        TMP_Text ready = UILabel("ReadyState", card.transform, "", 85, -75, 340, 32, 19, accentColor);
        if (first)
        {
            waitP1NameText = name; waitP1ShipNameText = shipName; waitP1StatsText = stats;
            waitP1SkillText = skill; waitP1ShipImage = ship; waitP1ReadyText = ready;
        }
        else
        {
            waitP2NameText = name; waitP2ShipNameText = shipName; waitP2StatsText = stats;
            waitP2SkillText = skill; waitP2ShipImage = ship; waitP2ReadyText = ready;
        }
    }

    // สร้างการ์ดแม็พ 3 ใบเรียงแนวนอน (กดแล้วเรียก SelectLobbyMap) และเก็บไว้ในกลุ่มให้ RefreshMapCards อัปเดต
    // browser = เป็นการ์ดของหน้าค้นหาห้อง (ไม่ได้ใช้ค่าในเมธอดนี้)
    private void BuildMapCards(Transform parent, float y, bool browser)
    {
        int count = MapChoices;
        var buttons = new Button[count];
        var labels = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            int index = i;
            if (count <= 3)
            {
                // 3 แม็พ: การ์ดใหญ่แบบเดิม
                Button button = UIButton("MapCard" + i, parent, "", (i - 1) * 405, y, 392, 202, () => SelectLobbyMap(index));
                Image art = UIPanel("MapPreview", button.transform, -130, 8, 112, 168, Color.white);
                art.sprite = Resources.Load<Sprite>(MapImage(i)); art.preserveAspect = true;
                labels[i] = UILabel("MapName", button.transform, mapShortNames[i], 61, 64, 246, 34, 19, accentColor);
                UILabel("Description", button.transform, mapDescriptions[i], 61, -1, 238, 90, 17, Color.white);
                UILabel("SelectionHint", button.transform, "SELECT MAP", 61, -70, 238, 26, 15, Color.gray);
                buttons[i] = button;
            }
            else
            {
                // 5 แม็พ: การ์ดเล็กเรียง 5 ใบ (รูปบน ชื่อ คำใบ้ล่าง)
                float spacing = 1220f / count;
                Button button = UIButton("MapCard" + i, parent, "", (i - (count - 1) * .5f) * spacing, y, spacing - 10, 202, () => SelectLobbyMap(index));
                Image art = UIPanel("MapPreview", button.transform, 0, 30, spacing - 30, 120, Color.white);
                art.sprite = Resources.Load<Sprite>(MapImage(i)); art.preserveAspect = true;
                labels[i] = UILabel("MapName", button.transform, mapShortNames[i], 0, -46, spacing - 16, 28, 16, accentColor);
                UILabel("SelectionHint", button.transform, "SELECT MAP", 0, -76, spacing - 16, 24, 14, Color.gray);
                // การ์ด 3 ใบแรกอาจถูกบันทึกใน Scene ด้วยขนาดใหญ่แบบเดิม: บังคับขนาด/ตำแหน่งใหม่ทุกใบ
                LayoutCompactMapCard(button, (i - (count - 1) * .5f) * spacing, y, spacing - 10);
                buttons[i] = button;
            }
        }
        mapCardGroups.Add(buttons);
        mapLabelGroups.Add(labels);
        RefreshMapCards();
    }

    // อัปเดตการ์ดแม็พทุกกลุ่ม: ไฮไลต์แม็พที่เลือก (ในห้องอ่านจาก property ห้อง)
    // กดได้เฉพาะตอนไม่ได้อยู่ในห้อง หรือเป็น Host ในห้อง; อีกฝ่ายจะเห็นข้อความ HOST SELECTS
    private void RefreshMapCards()
    {
        int index = PhotonNetwork.InRoom ? ReadMap(PhotonNetwork.CurrentRoom) : selectedMapIndex;
        bool editable = !roomRequestPending && !reconnecting && !isStartingGame && !isLeavingRoom && !RoomStarting
            && (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient);
        for (int group = 0; group < mapCardGroups.Count; group++)
        {
            for (int i = 0; i < mapCardGroups[group].Length; i++)
            {
                Button button = mapCardGroups[group][i];
                button.interactable = editable;
                button.GetComponent<Image>().color = i == index ? new Color(0.10f, 0.38f, 0.46f) : panelColor;
                mapLabelGroups[group][i].text = mapShortNames[i];
                TMP_Text hint = button.transform.Find("SelectionHint").GetComponent<TMP_Text>();
                hint.text = i == index ? "SELECTED" : editable ? "SELECT MAP" : "HOST SELECTS";
                hint.color = i == index ? new Color(0.4f, 1f, 0.73f) : Color.gray;
            }
        }
    }

    // ย่อ/ขยาย UI ขนาดออกแบบ 1280x720 ให้พอดีกับ Safe Area ของจอ (เว้นขอบ 24 พิกเซล) และจัดให้อยู่กลาง Safe Area
    // เรียกทุกเฟรมจาก Update เพื่อรองรับการหมุนจอ/เปลี่ยนขนาดจอ
    private void FitLobbyUI()
    {
        Rect safe = Screen.safeArea;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        foreach (RectTransform root in fittedRoots)
        {
            RectTransform parent = root.parent as RectTransform;
            if (parent == null) continue;
            float w = parent.rect.width * safe.width / Screen.width;
            float h = parent.rect.height * safe.height / Screen.height;
            float scale = Mathf.Min((w - 24f) / 1280f, (h - 24f) / 720f);
            root.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            root.anchoredPosition = new Vector2(
                (safe.center.x / Screen.width - 0.5f) * parent.rect.width,
                (safe.center.y / Screen.height - 0.5f) * parent.rect.height);
        }
    }
}
