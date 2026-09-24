using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

public partial class LobbyManager : MonoBehaviourPunCallbacks
{
    private const string ReadyProperty = "IsReady";
    private const string ShipProperty = "ShipType";
    private const string SkillProperty = "SkillType";
    private const string MapProperty = "MapIndex";

    private readonly Dictionary<string, RoomInfo> cachedRooms = new Dictionary<string, RoomInfo>();
    private bool isLeavingRoom;
    private bool isStartingGame;
    private bool loggingOut;
    private bool roomRequestPending;

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
    private string[] mapNames = { "Electric Jellyfish Core", "Obelisk Plains of Prism", "Abandoned Mech Warzone" };
    private string[] mapImages = { "Images/Map_ThunderJellyfish", "Images/Map_ObeliskPlains", "Images/Map_AncientMech" };

    // ข้อมูลยาน
    private int selectedShipIndex = 0;
    private ShipData[] ships = BattleLoadoutCatalog.Ships;
    // ข้อมูลผู้เล่นจากฐานข้อมูล
    private List<int> unlockedShips = new List<int> { 0 };
    private int equippedShipIndex = 0;
    
    // ข้อมูลสกิล
    private int selectedSkillIndex = 0;
    private int equippedSkillIndex = 0;
    private SkillData[] skills = BattleLoadoutCatalog.Skills;

    [Header("=== Skill UI ===")]
    public TMP_Text skillDescText;
    public Button installSkillButton;
    public TMP_Text installSkillText;

    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        BuildLobbyUI();
    }

    void Start()
    {
        Time.timeScale = 1f;
        EnablePlayButtons(false);
        ShowMainPanel();

        // PHASE 5: เล่นเพลงหน้าเมนู
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BGM_Lobby");
        }
        
        LoadLobbyProfile();

        // Initialize Audio Settings
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

    void Update()
    {
        FitLobbyUI();
        UpdateAutomaticRecovery();
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

    private float nextAutoReconnect, nextAutoProfileRetry;
    private int autoReconnectAttempts;
    private bool automaticRecoveryBlocked;

    private void UpdateAutomaticRecovery()
    {
        if (loggingOut || isStartingGame || isLeavingRoom || automaticRecoveryBlocked) return;
        if (!profileLoaded && Time.unscaledTime > profileDeadline && Time.unscaledTime >= nextAutoProfileRetry
            && Application.internetReachability != NetworkReachability.NotReachable)
        {
            nextAutoProfileRetry = Time.unscaledTime + 30f;
            LoadLobbyProfile();
        }
        if (PhotonNetwork.InRoom || PhotonNetwork.InLobby)
        {
            autoReconnectAttempts = 0;
            return;
        }
        if (roomRequestPending || Time.unscaledTime < nextAutoReconnect) return;
        var state = PhotonNetwork.NetworkClientState;
        // Wait for any existing handshake to finish; never issue overlapping connection requests.
        if (state != ClientState.Disconnected && state != ClientState.PeerCreated && state != ClientState.ConnectedToMasterServer) return;
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            nextAutoReconnect = Time.unscaledTime + 2f;
            UpdateStatus("Waiting for internet. Reconnection is automatic.");
            return;
        }
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

    private void ShowPanelAnimated(GameObject targetPanel)
    {
        if (targetPanel != null && !targetPanel.activeSelf)
        {
            targetPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(targetPanel.transform));
        }
    }

    public void ShowMainPanel()
    {
        if (PhotonNetwork.InRoom || reconnecting) { ShowWaitingRoom(); return; }
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        ShowPanelAnimated(mainPanel);
    }

    public void ShowInventoryPanel()
    {
        if (!profileLoaded || PhotonNetwork.InRoom || roomRequestPending || reconnecting) return;
        if (mainPanel != null) mainPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        ShowPanelAnimated(inventoryPanel);
        UpdateInventoryDisplay(selectedShipIndex);
    }

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



    public void OnLogoutButtonClicked()
    {
        loggingOut = true;
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        SceneManager.LoadScene("LoginScene");
    }

    public void OnSettingsClicked()
    {
        BattleSettingsPanel.Show();
    }

    public void OnCloseSettingsClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

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

    public const string BattleHelpText =
        "<color=#65DFFF>CONTROLS</color>\n" +
        "Left stick: move. Hold FIRE and drag to aim.\nTap the separate SKILL button when ready.\n\n" +
        "<color=#65DFFF>SKILLS</color>\n" +
        "STUN: a seeking wave briefly disables an enemy.\n" +
        "SHIELD: temporary protection and a movement boost.\n" +
        "NOVA: place a mine that explodes after a short delay.\n" +
        "SEEKER: launch a homing missile; solid cover blocks it.\n\n" +
        "<color=#65DFFF>BATTLEFIELDS</color>\n" +
        "MECH: avoid hot rocks, turret fire and falling meteors.\n" +
        "PRISM: use pillars as cover. Green pools slow you by 30%.\n" +
        "JELLYFISH: escape energy-orb pull and lightning warnings.\n" +
        "The central core boosts firing speed but drains HP.";

    public void OnCloseTutorialClicked()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
    }

    // ============================
    //  HELPERS
    // ============================

    private void UpdateStatus(string msg)
    {
        Debug.Log(msg);
        if (statusText != null) statusText.text = msg;
        if (lobbyMessage != null) lobbyMessage.text = msg;
        if (hangarMessage != null) hangarMessage.text = msg;
        if (browserMessage != null) browserMessage.text = msg;
    }

    private void EnablePlayButtons(bool on)
    {
        on = on && profileLoaded && PhotonNetwork.InLobby && !roomRequestPending && !reconnecting;
        if (playButton != null) playButton.interactable = on;
        if (createRoomButton != null) createRoomButton.interactable = on;
        if (createRoomConfirmButton != null) createRoomConfirmButton.interactable = on;
        if (searchRoomButton != null) searchRoomButton.interactable = on;
    }

    // ============================
    //  WAITING ROOM SYSTEM
    // ============================

    private void UpdateWaitingRoomUI()
    {
        bool inRoom = PhotonNetwork.InRoom;
        bool busy = !inRoom || reconnecting || isLeavingRoom || isStartingGame || RoomStarting;
        Player[] players = inRoom ? PhotonNetwork.PlayerList : new Player[0];
        System.Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        RenderPlayer(players.Length > 0 ? players[0] : null, waitP1NameText, waitP1ShipNameText,
            waitP1StatsText, waitP1SkillText, waitP1ShipImage, waitP1ReadyText);
        RenderPlayer(players.Length > 1 ? players[1] : null, waitP2NameText, waitP2ShipNameText,
            waitP2StatsText, waitP2SkillText, waitP2ShipImage, waitP2ReadyText);
        if (waitRoomNumberText != null) waitRoomNumberText.text = inRoom ? "ROOM  " + PhotonNetwork.CurrentRoom.Name : "ROOM RECOVERY";
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
        if (readyHint != null) readyHint.text = busy ? "Connecting or preparing battle..."
            : players.Length < 2 ? "Invite a friend using the room code."
            : CanStartGame() ? "All pilots ready. Host can launch the battle."
            : "Both pilots must confirm READY for the selected battlefield.";
        RefreshMapCards();
    }

    private bool IsPlayerReady(Player player)
    {
        return PhotonNetwork.InRoom && player != null && !player.IsInactive
            && ReadyPropertiesMatch(player.CustomProperties, ReadMap(PhotonNetwork.CurrentRoom), MapRevision);
    }

    public static bool ReadyPropertiesMatch(ExitGames.Client.Photon.Hashtable properties, int map, int revision)
    {
        return properties != null && BoolProperty(properties, ReadyProperty)
            && IntProperty(properties, "ReadyRevision", -1) == revision
            && IntProperty(properties, "ReadyMap", -1) == map
            && BoolProperty(properties, "LoadoutLoaded");
    }

    private void RenderPlayer(Player player, TMP_Text nameText, TMP_Text shipText, TMP_Text stats,
        TMP_Text skillText, Image picture, TMP_Text readyText)
    {
        bool present = player != null;
        int ship = present ? Mathf.Clamp(IntProperty(player.CustomProperties, ShipProperty, 0), 0, ships.Length - 1) : 0;
        int skill = present ? Mathf.Clamp(IntProperty(player.CustomProperties, SkillProperty, 0), 0, skills.Length - 1) : 0;
        if (nameText != null)
        {
            nameText.richText = false;
            nameText.text = present ? (player.IsMasterClient ? "[HOST] " : "") + player.NickName + (player.IsLocal ? " (YOU)" : "") : "OPEN SLOT";
        }
        if (shipText != null) shipText.text = present ? ships[ship].name : "Waiting for a pilot";
        if (stats != null) stats.text = present ? ships[ship].hp + " HP   /   ATK " + ships[ship].atk + "   /   SPD " + ships[ship].spd : "Share the room code to invite a friend";
        if (skillText != null) skillText.text = present ? "EQUIPPED SKILL  /  " + skills[skill].name : "1 VS 1";
        if (picture != null)
        {
            picture.sprite = present ? Resources.Load<Sprite>(ships[ship].spritePath) : null;
            picture.color = present ? Color.white : Color.clear;
            picture.preserveAspect = true;
        }
        if (readyText != null)
        {
            bool ready = IsPlayerReady(player);
            readyText.text = !present ? "WAITING" : player.IsInactive ? "RECONNECTING (60s slot)" : ready ? "READY" : "NOT READY";
            readyText.color = ready ? new Color(0.35f, 1f, 0.7f) : new Color(1f, 0.73f, 0.35f);
        }
    }

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
        if (!PhotonNetwork.LocalPlayer.SetCustomProperties(props)) readyPending = false;
        UpdateWaitingRoomUI();
    }

    public void OnStartGameClicked()
    {
        if (!CanStartGame()) { UpdateStatus("Both players must be ready before starting."); return; }
        if (!Application.CanStreamedLevelBeLoaded("SampleScene")) { UpdateStatus("Gameplay scene is missing from Build Profiles."); return; }
        // Wait for the server acknowledgement before loading; a map change invalidates this request.
        var expected = new ExitGames.Client.Photon.Hashtable { ["Starting"] = false, ["MapRevision"] = MapRevision };
        if (PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["Starting"] = true }, expected))
            UpdateStatus("Preparing battle...");
    }

    public void NextMap()
    {
        if (PhotonNetwork.InRoom) { ChangeRoomMap(1); return; }
        selectedMapIndex++;
        if (selectedMapIndex >= mapNames.Length) selectedMapIndex = 0;
        if (createRoomMapNameText != null) createRoomMapNameText.text = mapNames[selectedMapIndex];
        RefreshMapCards();
    }

    public void PrevMap()
    {
        if (PhotonNetwork.InRoom) { ChangeRoomMap(-1); return; }
        selectedMapIndex--;
        if (selectedMapIndex < 0) selectedMapIndex = mapNames.Length - 1;
        if (createRoomMapNameText != null) createRoomMapNameText.text = mapNames[selectedMapIndex];
        RefreshMapCards();
    }

    public void OnLeaveWaitingRoom()
    {
        if (!PhotonNetwork.InRoom || isLeavingRoom || isStartingGame || RoomStarting) return;
        isLeavingRoom = PhotonNetwork.LeaveRoom(false);
        if (isLeavingRoom) UpdateStatus("Leaving room...");
        UpdateWaitingRoomUI();
    }

    private int ReadMap(RoomInfo room)
    {
        return room != null && room.CustomProperties.TryGetValue(MapProperty, out object value) && value is int index
            ? Mathf.Clamp(index, 0, mapNames.Length - 1) : 2;
    }

    private bool CanStartGame()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || isStartingGame || isLeavingRoom || RoomStarting || reconnecting || PhotonNetwork.PlayerList.Length != 2) return false;
        foreach (Player player in PhotonNetwork.PlayerList) if (!IsPlayerReady(player)) return false;
        return true;
    }

    private void TryLaunchConfirmedRoom()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !RoomStarting || isStartingGame) return;
        bool ready = PhotonNetwork.PlayerList.Length == 2;
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
            ["StartTime"] = -1d
        });
        PhotonNetwork.LoadLevel("SampleScene");
    }

    private void ChangeRoomMap(int direction)
    {
        SelectLobbyMap((ReadMap(PhotonNetwork.CurrentRoom) + direction + mapNames.Length) % mapNames.Length);
    }

    public void SelectLobbyMap(int index)
    {
        if (index < 0 || index >= mapNames.Length) return;
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

    private bool RoomStarting => PhotonNetwork.InRoom && BoolProperty(PhotonNetwork.CurrentRoom.CustomProperties, "Starting");
    private int MapRevision => PhotonNetwork.InRoom ? IntProperty(PhotonNetwork.CurrentRoom.CustomProperties, "MapRevision", 0) : 0;
    private static bool BoolProperty(ExitGames.Client.Photon.Hashtable props, string key)
        => props.TryGetValue(key, out object value) && value is bool flag && flag;
    private static int IntProperty(ExitGames.Client.Photon.Hashtable props, string key, int fallback)
        => props.TryGetValue(key, out object value) && value is int number ? number : fallback;

    private void PublishLocalLoadout(bool resetReady)
    {
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [ShipProperty] = Mathf.Clamp(equippedShipIndex, 0, ships.Length - 1),
            ["LoadoutLoaded"] = profileLoaded,
            [SkillProperty] = Mathf.Clamp(equippedSkillIndex, 0, skills.Length - 1)
        };
        if (resetReady) props[ReadyProperty] = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }



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
