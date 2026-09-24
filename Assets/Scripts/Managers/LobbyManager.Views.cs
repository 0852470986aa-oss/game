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
    private bool profileLoaded;
    private bool shipLoaded, skillLoaded, readyPending, reconnecting;
    private int profileGeneration;
    private string previousRoom;
    private float profileDeadline, reconnectDeadline, nextStatusRefresh;
    private TMP_Text lobbyMessage, browserMessage, connectionText, readyHint, emptyRoomsText;
    private readonly List<RectTransform> fittedRoots = new List<RectTransform>();
    private readonly List<Button[]> mapCardGroups = new List<Button[]>();
    private readonly List<TMP_Text[]> mapLabelGroups = new List<TMP_Text[]>();
    private readonly string[] mapDescriptions = {
        "Cover and side routes.\nEnergy core: trade HP for fire rate.\nHazard: lightning strikes.",
        "Obelisks and crystal cover.\nWide routes for flanking.\nHazard: slowing zones.",
        "12 rocks / 2 turrets / 6 red cores.\nUse cover around the wreck.\nHazard: falling lava asteroids."
    };
    private readonly string[] mapShortNames = { "JELLYFISH CORE", "PRISM PLAINS", "MECH WARZONE" };
    private readonly Color panelColor = new Color(0.035f, 0.065f, 0.12f, 0.97f);
    private readonly Color accentColor = new Color(0.23f, 0.82f, 0.92f);
    private TMP_FontAsset lobbyFont;
    private TMP_Text homeSkillText;
    private TMP_Text hangarMessage;
    private TMP_Text hangarCoinText;

    private void UpdateCoinDisplay(long coins)
    {
        if (coinText != null) coinText.text = "Astronium Coins : " + coins;
        if (hangarCoinText != null) hangarCoinText.text = "ASTRONIUM  /  " + coins.ToString("N0");
    }
    private bool hangarBusy;
    private Button[] hangarSkillCards;
    private TMP_Text[] hangarSkillStates;
    private TMP_Text[] hangarCardLabels;
    private GameObject hangarShipsPage, hangarSkillsPage;

    private void LoadLobbyProfile()
    {
        int generation = ++profileGeneration;
        profileLoaded = shipLoaded = skillLoaded = false;
        if (inventoryButton != null) inventoryButton.interactable = false;
        profileDeadline = Time.unscaledTime + 20f;
        EnablePlayButtons(false);
        UpdateStatus("Loading your ship and skill...");
        if (FirebaseManager.Instance == null)
        {
            // Direct editor entry uses an explicit default loadout.
            equippedShipIndex = equippedSkillIndex = 0;
            shipLoaded = skillLoaded = true;
            FinishProfileLoad();
            return;
        }
        if (playerNameText != null)
        {
            playerNameText.richText = false;
            playerNameText.text = FirebaseManager.Instance.GetUsername();
        }
        FirebaseManager.Instance.GetCoinBalance(coins => {
            if (this == null || generation != profileGeneration) return;
            UpdateCoinDisplay(coins);
        }, error => {
            if (this == null || generation != profileGeneration) return;
            if (coinText != null) coinText.text = "Astronium Coins : unavailable";
            if (hangarCoinText != null) hangarCoinText.text = "ASTRONIUM / unavailable";
            UpdateStatus(error);
        });
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
        FirebaseManager.Instance.GetSelectedSkill(index => {
            if (this == null || generation != profileGeneration) return;
            equippedSkillIndex = Mathf.Clamp(index, 0, skills.Length - 1);
            selectedSkillIndex = equippedSkillIndex;
            skillLoaded = true;
            FinishProfileLoad();
        });
    }

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

    private void RoomRequestFailed()
    {
        roomRequestPending = false;
        EnablePlayButtons(true);
        RenderRoomList();
        UpdateStatus("Could not send the request. Please try again.");
    }

    public void CopyRoomCode()
    {
        if (!PhotonNetwork.InRoom) return;
        GUIUtility.systemCopyBuffer = PhotonNetwork.CurrentRoom.Name;
        UpdateStatus("Room code copied. Share it with your friend.");
    }

    private static void SetButtonLabel(Button button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
    }

    private RectTransform UIRect(string name, Transform parent, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
        return rect;
    }

    private Image UIPanel(string name, Transform parent, float x, float y, float width, float height, Color color)
    {
        Image img = UIRect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text UILabel(string name, Transform parent, string value, float x, float y, float w, float h, int size, Color color)
    {
        var text = UIRect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        if (lobbyFont != null) text.font = lobbyFont;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12, size - 5);
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private Button UIButton(string name, Transform parent, string value, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
    {
        Image image = UIPanel(name, parent, x, y, w, h, new Color(0.08f, 0.26f, 0.36f));
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.65f, 1f, 1f);
        colors.pressedColor = new Color(0.35f, 0.65f, 0.8f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        button.colors = colors;
        button.onClick.AddListener(action);
        button.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Click"); });
        UILabel("Label", image.transform, value, 0, 0, w - 16, h - 8, 20, Color.white);
        return button;
    }

    private RectTransform BuildSurface(GameObject panel)
    {
        foreach (Transform child in panel.transform) child.gameObject.SetActive(false);
        var shade = UIPanel("LobbyBackdrop", panel.transform, 0, 0, 0, 0, new Color(0.015f, 0.025f, 0.06f, 0.98f));
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = Vector2.one;
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
        shade.raycastTarget = true;
        RectTransform root = UIRect("LobbySurface", panel.transform, 0, 0, 1280, 720);
        fittedRoots.Add(root);
        UIPanel("TopLine", root, 0, 274, 1200, 2, accentColor);
        return root;
    }

    private void BuildLobbyUI()
    {
        lobbyFont = statusText != null ? statusText.font : TMP_Settings.defaultFontAsset;
        if (waitingRoomPanel != null)
        {
            RectTransform root = BuildSurface(waitingRoomPanel);
            UILabel("Title", root, "BATTLE PREPARATION", -350, 314, 500, 42, 32, Color.white);
            waitRoomNumberText = UILabel("RoomCode", root, "ROOM", 190, 314, 300, 40, 24, accentColor);
            UIButton("CopyCode", root, "COPY CODE", 485, 314, 200, 48, CopyRoomCode);
            connectionText = UILabel("Connection", root, "CONNECTING", 420, 246, 340, 30, 16, accentColor);
            UILabel("Mode", root, "1 VS 1  /  PILOT LOADOUTS", -390, 246, 420, 30, 18, accentColor);
            BuildPilotCard(root, -306, true);
            BuildPilotCard(root, 306, false);
            UILabel("MapsHeading", root, "SELECT BATTLEFIELD  /  HOST CONTROLS MAP", 0, -37, 900, 28, 17, accentColor);
            BuildMapCards(root, -158, false);
            readyHint = UILabel("ReadyHint", root, "", 0, -288, 1150, 28, 17, Color.white);
            waitCancelButton = UIButton("LeaveRoom", root, "LEAVE ROOM", -420, -334, 270, 52, OnLeaveWaitingRoom);
            waitReadyButton = UIButton("Ready", root, "READY", 0, -334, 270, 52, OnReadyButtonClicked);
            waitStartButton = UIButton("StartBattle", root, "START BATTLE", 420, -334, 270, 52, OnStartGameClicked);
            lobbyMessage = UILabel("Message", root, "", 0, -263, 1160, 24, 15, new Color(1f, 0.77f, 0.43f));
        }
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
                roomSearchInput.transform.SetParent(root, false);
                var rect = roomSearchInput.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(220, 180);
                rect.sizeDelta = new Vector2(340, 52);
                roomSearchInput.characterLimit = 32;
                roomSearchInput.gameObject.SetActive(true);
            }
            searchRoomButton = UIButton("Join", root, "JOIN", 489, 180, 160, 52, OnSearchRoom);
            // Use a masked scroll area; the original inactive template is preserved.
            var viewport = UIPanel("RoomViewport", root, 305, 57, 540, 164, panelColor);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIRect("RoomEntries", viewport.transform, 0, 0, 540, 0);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.spacing = 8; layout.padding = new RectOffset(6, 6, 6, 6);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
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
        if (mainPanel != null)
        {
            BuildHomeScreen();
        }
        if (inventoryPanel != null) BuildHangarScreen();
        FitLobbyUI();
    }

    private void BuildHangarScreen()
    {
        RectTransform root = BuildSurface(inventoryPanel);
        UILabel("HangarTitle", root, "PILOT HANGAR", -355, 315, 480, 45, 33, Color.white);
        hangarCoinText = UILabel("HangarCoins", root, "ASTRONIUM  /  ...", 110, 315, 390, 45, 23, new Color(1f, 0.8f, 0.35f));
        PolishSprites.AddCoinIcon(hangarCoinText);
        backFromInventoryButton = UIButton("Back", root, "BACK", 485, 315, 200, 50, () => { if (!hangarBusy) ShowMainPanel(); });
        UIButton("ShipsTab", root, "SHIPS", -180, 231, 280, 50, () => SetHangarPage(false));
        UIButton("SkillsTab", root, "SKILLS", 180, 231, 280, 50, () => SetHangarPage(true));
        hangarShipsPage = UIRect("ShipsPage", root, 0, -15, 1200, 430).gameObject;
        hangarSkillsPage = UIRect("SkillsPage", root, 0, -15, 1200, 430).gameObject;
        shipButtons = new Button[ships.Length];
        hangarCardLabels = new TMP_Text[ships.Length];
        for (int i = 0; i < ships.Length; i++)
        {
            int index = i;
            var card = UIButton("ShipCard" + i, hangarShipsPage.transform, "", -405, 140 - i * 142, 370, 126, () => SelectShip(index));
            var art = UIPanel("ShipArt", card.transform, -120, 0, 140, 115, Color.white);
            art.sprite = Resources.Load<Sprite>(ships[i].spritePath); art.preserveAspect = true;
            UILabel("Name", card.transform, ships[i].name, 53, 26, 245, 45, 23, Color.white);
            hangarCardLabels[i] = UILabel("Ownership", card.transform, "", 53, -27, 235, 34, 17, accentColor);
            shipButtons[i] = card;
        }
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
        skillDescText = UILabel("SkillDetails", hangarSkillsPage.transform, "", -170, -85, 770, 150, 28, Color.white);
        installSkillButton = UIButton("Install", hangarSkillsPage.transform, "INSTALL", 415, -88, 280, 70, OnInstallSkillClicked);
        installSkillText = installSkillButton.GetComponentInChildren<TMP_Text>();
        UILabel("SkillHint", hangarSkillsPage.transform, "Select a skill to see its effect and cooldown. Install it to change your loadout.", 0, -196, 1150, 32, 17, Color.gray);
        hangarMessage = UILabel("HangarMessage", root, "Choose a ship or open the SKILLS tab.", 0, -288, 1150, 44, 19, Color.white);
        UILabel("HangarFooter", root, "LOADOUT / YOUR NEXT BATTLE STARTS HERE", 0, -340, 1150, 24, 14, Color.gray);
        SetHangarPage(false);
    }

    private void SetHangarPage(bool skillsPage)
    {
        if (hangarBusy) return;
        hangarShipsPage.SetActive(!skillsPage);
        hangarSkillsPage.SetActive(skillsPage);
        UpdateInventoryDisplay(selectedShipIndex);
        UpdateSkillDisplay(selectedSkillIndex);
    }

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

    private void BuildHomeScreen()
    {
        RectTransform root = BuildSurface(mainPanel);
        UILabel("GameTitle", root, "BATTLEFIELD OF THE STARS", -225, 315, 760, 48, 33, Color.white);
        settingsButton = UIButton("Settings", root, "SETTINGS", 465, 315, 220, 50, OnSettingsClicked);
        var profile = UIPanel("PilotProfile", root, -330, 222, 550, 74, panelColor);
        playerNameText = UILabel("PilotName", profile.transform, "LOADING PILOT...", 0, 15, 520, 34, 24, Color.white);
        playerNameText.richText = false;
        playersOnlineText = UILabel("OnlineStatus", profile.transform, "CONNECTING...", 0, -20, 520, 25, 15, accentColor);
        coinText = UILabel("CoinBalance", root, "Astronium Coins : ...", 305, 230, 550, 40, 24, new Color(1f, 0.8f, 0.35f));
        PolishSprites.AddCoinIcon(coinText);
        UILabel("CurrencyHint", root, "YOUR PILOT WALLET", 305, 195, 550, 24, 14, Color.gray);

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

        UILabel("BattleTitle", root, "READY FOR BATTLE?", 395, 127, 370, 44, 26, Color.white);
        playButton = UIButton("QuickMatch", root, "QUICK MATCH", 395, 50, 370, 82, OnPlayButtonClicked);
        playButton.GetComponent<Image>().color = new Color(0.1f, 0.49f, 0.5f);
        createRoomButton = UIButton("CreateJoin", root, "CREATE / JOIN ROOM", 395, -47, 370, 64, OnCreateRoomClicked);
        inventoryButton = UIButton("OpenHangar", root, "SHIPS & SKILLS", 395, -129, 370, 64, ShowInventoryPanel);
        UIButton("HowToPlay", root, "HOW TO PLAY", 395, -205, 370, 52, OnTutorialClicked);
        statusText = UILabel("HomeStatus", root, "Loading pilot data...", -170, -292, 840, 38, 18, Color.white);
        statusText.richText = false;
        // Connection status above reports automatic recovery instead of a manual retry button.
        UILabel("Footer", root, "PILOT HUB  /  1 VS 1 MULTIPLAYER", 0, -339, 1150, 24, 14, Color.gray);
        UpdateShipDisplay(equippedShipIndex);
        UpdateSkillDisplay(equippedSkillIndex);
    }

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

    private void BuildMapCards(Transform parent, float y, bool browser)
    {
        var buttons = new Button[3];
        var labels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            Button button = UIButton("MapCard" + i, parent, "", (i - 1) * 405, y, 392, 202, () => SelectLobbyMap(index));
            Image art = UIPanel("MapPreview", button.transform, -130, 8, 112, 168, Color.white);
            art.sprite = Resources.Load<Sprite>(mapImages[i]); art.preserveAspect = true;
            labels[i] = UILabel("MapName", button.transform, mapShortNames[i], 61, 64, 246, 34, 19, accentColor);
            UILabel("Description", button.transform, mapDescriptions[i], 61, -1, 238, 90, 17, Color.white);
            UILabel("SelectionHint", button.transform, "SELECT MAP", 61, -70, 238, 26, 15, Color.gray);
            buttons[i] = button;
        }
        mapCardGroups.Add(buttons);
        mapLabelGroups.Add(labels);
        RefreshMapCards();
    }

    private void RefreshMapCards()
    {
        int index = PhotonNetwork.InRoom ? ReadMap(PhotonNetwork.CurrentRoom) : selectedMapIndex;
        bool editable = !roomRequestPending && !reconnecting && !isStartingGame && !isLeavingRoom && !RoomStarting
            && (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient);
        for (int group = 0; group < mapCardGroups.Count; group++)
        {
            for (int i = 0; i < 3; i++)
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
