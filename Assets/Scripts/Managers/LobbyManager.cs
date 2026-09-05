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
    private ShipData[] ships = new ShipData[]
    {
        new ShipData("Nebula Ghost", 70, 0.8f, 7.5f, "STUN", 0, "Images/ship1"),
        new ShipData("Comet Crusher", 90, 1.0f, 5.0f, "SHIELD", 2800, "Images/ship2"),
        new ShipData("Stellar Striker", 55, 1.5f, 9.0f, "NOVA", 3089, "Images/ship3"),
    };
    // ข้อมูลผู้เล่นจากฐานข้อมูล
    private List<int> unlockedShips = new List<int> { 0 };
    private int equippedShipIndex = 0;
    
    // ข้อมูลสกิล
    private int selectedSkillIndex = 0;
    private int equippedSkillIndex = 0;
    private SkillData[] skills = new SkillData[]
    {
        new SkillData("STUN", "Paralyze wave\nCooldown 12 sec", "Images/icon_stun"),
        new SkillData("SHIELD", "Invincibility Bubble\nCooldown 20 sec", "Images/icon_shield"),
        new SkillData("NOVA", "AoE Explosion\nCooldown 15 sec", "Images/icon_nova"),
        new SkillData("SEEKER", "Homing Missile\nCooldown 10 sec", "Images/icon_seeker")
    };

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
                musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
                musicSlider.onValueChanged.AddListener(vol => AudioManager.Instance.SetMusicVolume(vol));
            }
            if (sfxSlider != null) 
            {
                sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
                sfxSlider.onValueChanged.AddListener(vol => AudioManager.Instance.SetSFXVolume(vol));
            }
        }

        // ตั้งชื่อ Photon
        PhotonNetwork.NickName = FirebaseManager.Instance != null
            ? FirebaseManager.Instance.GetUsername()
            : "Player_" + Random.Range(1000, 9999);

        // เชื่อมต่อ Photon
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
                UpdateStatus("Loadout is taking longer than expected. Check connection, then Retry.");
        }
        if (reconnecting && Time.unscaledTime > reconnectDeadline)
        {
            reconnecting = false;
            previousRoom = null;
            PhotonNetwork.Disconnect();
            ShowMainPanel();
            UpdateStatus("Room recovery timed out. Reconnect to find a new room.");
        }
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

    private void UpdateShipDisplay(int index)
    {
        if (index < 0 || index >= ships.Length) return;
        ShipData ship = ships[index];

        if (shipNameText != null) shipNameText.text = ship.name;
        if (shipHPText != null) shipHPText.text = "HP: " + ship.hp;
        if (shipATKText != null) shipATKText.text = "ATK: " + ship.atk;
        if (shipSPDText != null) shipSPDText.text = "SPD: " + ship.spd;
        if (shipSkillText != null) shipSkillText.text = ""; // ลบระบบ Skill เดิมทิ้ง
        if (shipImage != null)
        {
            Sprite sp = Resources.Load<Sprite>(ship.spritePath);
            if (sp != null)
            {
                shipImage.sprite = sp;
                shipImage.color = Color.white;
            }
        }
    }

    private void UpdateInventoryDisplay(int index)
    {
        if (index < 0 || index >= ships.Length) return;
        ShipData ship = ships[index];

        if (inventoryShipName != null) inventoryShipName.text = ship.name;
        if (inventoryShipHP != null) inventoryShipHP.text = "HP: " + ship.hp;
        if (inventoryShipATK != null) inventoryShipATK.text = "ATK: " + ship.atk;
        if (inventoryShipSPD != null) inventoryShipSPD.text = "SPD: " + ship.spd;
        if (inventoryShipSkill != null) inventoryShipSkill.text = ""; // ลบระบบ Skill เดิมทิ้ง
        if (inventoryShipImage != null)
        {
            Sprite sp = Resources.Load<Sprite>(ship.spritePath);
            if (sp != null)
            {
                inventoryShipImage.sprite = sp;
                inventoryShipImage.color = Color.white;
            }
        }

        UpdateInventoryActionButton(index);
    }

    private void UpdateInventoryActionButton(int index)
    {
        if (inventoryActionButton == null) return;
        
        if (inventoryActionText == null)
            inventoryActionText = inventoryActionButton.GetComponentInChildren<TMP_Text>();

        // ถ้าปลดล็อกแล้ว
        if (unlockedShips.Contains(index))
        {
            if (index == equippedShipIndex)
            {
                inventoryActionButton.interactable = false;
                inventoryActionButton.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
                if (inventoryActionText != null) inventoryActionText.text = "Equipped";
            }
            else
            {
                inventoryActionButton.interactable = true;
                inventoryActionButton.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.3f);
                if (inventoryActionText != null) inventoryActionText.text = "Equip";
            }
        }
        else // ถ้ายังไม่ปลดล็อก
        {
            inventoryActionButton.interactable = true;
            inventoryActionButton.GetComponent<Image>().color = new Color(0.8f, 0.6f, 0.1f);
            if (inventoryActionText != null) inventoryActionText.text = "Buy (" + ships[index].price + ")";
        }
    }

    public void SelectShip(int index)
    {
        selectedShipIndex = index;
        UpdateInventoryDisplay(index);
    }

    public void OnInventoryActionClicked()
    {
        int index = selectedShipIndex;
        if (index < 0 || index >= ships.Length) return;

        // สวมใส่ยาน
        if (unlockedShips.Contains(index))
        {
            equippedShipIndex = index;
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.SaveSelectedShip(index);
            }
            UpdateShipDisplay(equippedShipIndex); // อัปเดตหน้าล็อบบี้
            UpdateInventoryActionButton(index);   // อัปเดตปุ่ม
        }
        // ซื้อยาน
        else
        {
            ShipData ship = ships[index];
            if (FirebaseManager.Instance != null)
            {
                inventoryActionButton.interactable = false; // ป้องกันการกดเบิ้ล
                FirebaseManager.Instance.GetCoinBalance(coins =>
                {
                    if (coins >= ship.price)
                    {
                        // หักเงิน
                        FirebaseManager.Instance.UpdateCoinBalance(coins - ship.price, success =>
                        {
                            if (success)
                            {
                                // ปลดล็อกยาน
                                FirebaseManager.Instance.UnlockShip(index, unlockSuccess =>
                                {
                                    if (unlockSuccess)
                                    {
                                        unlockedShips.Add(index);
                                        UpdateInventoryActionButton(index);
                                        
                                        // อัปเดตเงินใน UI ด่วน
                                        if (coinText != null)
                                            coinText.text = "Astronium Coins : " + (coins - ship.price);
                                            
                                        UpdateStatus("Purchased " + ship.name + "!");
                                    }
                                    else
                                    {
                                        FirebaseManager.Instance.UpdateCoinBalance(coins);
                                        inventoryActionButton.interactable = true;
                                        UpdateStatus("Purchase failed; coins were restored.");
                                    }
                                });
                            }
                            else
                            {
                                inventoryActionButton.interactable = true;
                                UpdateStatus("Could not update coin balance.");
                            }
                        });
                    }
                    else
                    {
                        UpdateStatus("Not enough coins!");
                        inventoryActionButton.interactable = true;
                    }
                });
            }
        }
    }

    // ============================
    //  SKILL FUNCTIONS
    // ============================

    public void SelectSkill(int index)
    {
        if (index < 0 || index >= skills.Length) return;
        selectedSkillIndex = index;
        UpdateSkillDisplay(index);
    }

    private void UpdateSkillDisplay(int index)
    {
        if (index < 0 || index >= skills.Length) return;
        SkillData skill = skills[index];

        if (skillDescText != null)
            skillDescText.text = skill.name + " - " + skill.description;

        if (installSkillButton == null) return;
        if (installSkillText == null) installSkillText = installSkillButton.GetComponentInChildren<TMP_Text>();

        if (index == equippedSkillIndex)
        {
            installSkillButton.interactable = false;
            installSkillButton.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
            if (installSkillText != null) installSkillText.text = "Installed";
        }
        else
        {
            installSkillButton.interactable = true;
            installSkillButton.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.9f);
            if (installSkillText != null) installSkillText.text = "Install";
        }
    }

    public void OnInstallSkillClicked()
    {
        equippedSkillIndex = selectedSkillIndex;
        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.SaveSelectedSkill(equippedSkillIndex);
        }
        UpdateSkillDisplay(equippedSkillIndex);
        UpdateStatus("Installed " + skills[equippedSkillIndex].name + " Skill!");
    }

    // ============================
    //  PHOTON CALLBACKS
    // ============================

    public override void OnConnectedToMaster()
    {
        if (loggingOut) return;
        if (reconnecting && !string.IsNullOrEmpty(previousRoom))
        {
            if (!PhotonNetwork.RejoinRoom(previousRoom)) RecoveryFailed("Could not return to the room.");
            return;
        }
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        roomRequestPending = false;
        cachedRooms.Clear();
        RenderRoomList();
        UpdateStatus(profileLoaded ? "Ready! Create or join a room." : "Loading your ship and skill...");
        EnablePlayButtons(true);
    }

    public override void OnJoinedRoom()
    {
        roomRequestPending = false;
        readyPending = false;
        reconnecting = false;
        previousRoom = PhotonNetwork.CurrentRoom.Name;
        isLeavingRoom = false;
        isStartingGame = false;
        UpdateStatus($"Joined room! ({PhotonNetwork.CurrentRoom.PlayerCount}/2)");
        
        // ตั้งค่าเริ่มต้น: ยังไม่พร้อม
        PublishLocalLoadout(true);
        
        // เปิดหน้า Waiting Room แทนการโหลดเกมทันที
        ShowWaitingRoom();
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        UpdateStatus(newPlayer.NickName + " joined the room");
        UpdateWaitingRoomUI();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        PublishLocalLoadout(true);
        UpdateStatus(otherPlayer.NickName + (otherPlayer.IsInactive ? " lost connection. Slot reserved for 60 seconds." : " left the room"));
        UpdateWaitingRoomUI();
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (RoomStarting) { TryLaunchConfirmedRoom(); return; }
        PublishLocalLoadout(true);
        UpdateStatus(newMasterClient.IsLocal
            ? "You are now the room host"
            : newMasterClient.NickName + " is now the room host");
        UpdateWaitingRoomUI();
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("Starting") && RoomStarting && PhotonNetwork.IsMasterClient && !isStartingGame)
        {
            TryLaunchConfirmedRoom();
        }
        if (propertiesThatChanged.ContainsKey(MapProperty))
        {
            PublishLocalLoadout(true);
            UpdateWaitingRoomUI();
        }
    }

    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (targetPlayer.IsLocal) readyPending = false;
        UpdateWaitingRoomUI();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        roomRequestPending = false;
        if (reconnecting) { RecoveryFailed("The room expired or the slot is no longer available."); return; }
        Debug.LogError($"Join Room Failed: {returnCode} - {message}");
        
        // ErrorCode 32765 is GameFull, 32758 is GameDoesNotExist (Photon ErrorCodes)
        if (returnCode == 32765)
        {
            UpdateStatus("Room is full!");
        }
        else
        {
            UpdateStatus("Failed to join room: " + message);
        }

        EnablePlayButtons(true);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        roomRequestPending = false;
        Debug.LogError($"Create Room Failed: {returnCode} - {message}");
        UpdateStatus("Failed to create room: " + message);
        EnablePlayButtons(true);
        ShowRoomPanel();
    }

    public override void OnLeftRoom()
    {
        previousRoom = null;
        roomRequestPending = false;
        isLeavingRoom = false;
        isStartingGame = false;
        UpdateStatus("Left room successfully");
        ShowMainPanel();
        EnablePlayButtons(PhotonNetwork.InLobby);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (!roomRequestPending || !PhotonNetwork.IsConnectedAndReady) return;
        UpdateStatus("No room found. Creating a new one...");
        string roomId = Random.Range(100000, 999999).ToString();

        ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
        // ถ้า Quick Match หาห้องไม่เจอ ให้ใช้ map ที่เลือกไว้ หรือสุ่มก็ได้ (ที่นี่เราใช้อันที่เลือกไว้)
        roomProps.Add("MapIndex", selectedMapIndex);
        roomProps.Add("MapRevision", 0);
        roomProps.Add("Starting", false);

        RoomOptions options = new RoomOptions 
        { 
            PlayerTtl = 60000,
            EmptyRoomTtl = 60000,
            MaxPlayers = 2, 
            IsVisible = true, 
            IsOpen = true,
            CustomRoomProperties = roomProps,
            CustomRoomPropertiesForLobby = new string[] { "MapIndex" }
        };
        if (!PhotonNetwork.CreateRoom(roomId, options, TypedLobby.Default)) RoomRequestFailed();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        roomRequestPending = false;
        readyPending = false;
        EnablePlayButtons(false);
        cachedRooms.Clear();
        RenderRoomList();
        if (loggingOut) return;
        if (!isLeavingRoom && !isStartingGame && !string.IsNullOrEmpty(previousRoom)
            && cause != DisconnectCause.DisconnectByClientLogic)
        {
            if (!reconnecting) reconnectDeadline = Time.unscaledTime + 50f;
            reconnecting = true;
            UpdateStatus("Connection lost. Returning to your room...");
            UpdateWaitingRoomUI();
            if (PhotonNetwork.ReconnectAndRejoin()) return;
            if (PhotonNetwork.Reconnect()) return;
        }
        reconnecting = false;
        previousRoom = null;
        isLeavingRoom = false;
        isStartingGame = false;
        ShowMainPanel();
        UpdateStatus("Disconnected (" + cause + "). Press Reconnect.");
    }

    private void RecoveryFailed(string message)
    {
        reconnecting = false;
        previousRoom = null;
        ShowMainPanel();
        UpdateStatus(message);
        if (PhotonNetwork.IsConnectedAndReady) PhotonNetwork.JoinLobby();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // อัปเดตรายการห้อง (สำหรับหน้า Room Panel)
        foreach (RoomInfo update in roomList)
        {
            if (update.RemovedFromList) cachedRooms.Remove(update.Name);
            else cachedRooms[update.Name] = update;
        }

        RenderRoomList();
    }

    private void RenderRoomList()
    {
        if (roomListContent == null || roomItemPrefab == null) return;

        // ลบรายการเดิมทั้งหมด (ยกเว้น RoomItemPrefab ถ้ามันอยู่ในนั้นด้วย)
        foreach (Transform child in roomListContent)
        {
            if (child.gameObject != roomItemPrefab)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        int visibleCount = 0;
        var rooms = new List<RoomInfo>(cachedRooms.Values);
        rooms.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        foreach (RoomInfo room in rooms)
        {
            if (room.RemovedFromList) continue; // ข้ามห้องที่โดนลบไปแล้ว
            if (!room.IsOpen || !room.IsVisible) continue; // ข้ามห้องที่ปิดหรือซ่อนอยู่

            visibleCount++;
            GameObject roomItem = Instantiate(roomItemPrefab, roomListContent);
            roomItem.SetActive(true);
            LayoutElement rowLayout = roomItem.GetComponent<LayoutElement>();
            if (rowLayout != null) rowLayout.minHeight = rowLayout.preferredHeight = 76;

            // ค้นหา Text ภายในปุ่ม
            TMP_Text[] texts = roomItem.GetComponentsInChildren<TMP_Text>();
            foreach (TMP_Text t in texts)
            {
                t.richText = false;
                if (t.name == "RoomNameText") t.text = "Room: " + room.Name;
                else if (t.name == "RoomPlayersText") t.text = room.PlayerCount + " / " + room.MaxPlayers;
                else if (t.name == "RoomModeMapText") t.text = "1 VS 1 | " + mapNames[ReadMap(room)];
                if (t.name == "RoomNameText" || t.name == "RoomModeMapText")
                {
                    t.rectTransform.anchoredPosition = new Vector2(-82, t.name == "RoomNameText" ? 15 : -15);
                    t.rectTransform.sizeDelta = new Vector2(322, 28);
                    t.enableAutoSizing = true; t.fontSizeMin = 12; t.fontSizeMax = 17;
                    t.overflowMode = TextOverflowModes.Ellipsis;
                }
                else if (t.name == "RoomPlayersText")
                {
                    t.rectTransform.anchoredPosition = new Vector2(115, 0);
                    t.rectTransform.sizeDelta = new Vector2(60, 30);
                }
            }

            // ผูกปุ่ม Join
            Button joinBtn = roomItem.GetComponentInChildren<Button>();
            if (joinBtn != null)
            {
                joinBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(205, 0);
                joinBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(84, 50);
                joinBtn.interactable = !roomRequestPending && profileLoaded && PhotonNetwork.InLobby && room.PlayerCount < room.MaxPlayers;
                joinBtn.onClick.AddListener(() => JoinRoomByName(room.Name));
            }
        }
        if (emptyRoomsText != null) emptyRoomsText.gameObject.SetActive(visibleCount == 0);
    }

    public void JoinRoomByName(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName) || !BeginRoomRequest()) return;
        roomName = roomName.Trim();
        UpdateStatus("Joining room " + roomName + "...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", equippedSkillIndex);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        if (!PhotonNetwork.JoinRoom(roomName)) RoomRequestFailed();
    }

    // ============================
    //  BUTTON FUNCTIONS
    // ============================

    public void OnPlayButtonClicked()
    {
        if (!BeginRoomRequest()) return;
        EnablePlayButtons(false);
        UpdateStatus("Searching for room (Quick Match)...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", equippedSkillIndex);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        if (!PhotonNetwork.JoinRandomRoom()) RoomRequestFailed();
    }

    public void OnCreateRoomClicked()
    {
        ShowRoomPanel();
    }

    public void OnCreateRoomConfirm()
    {
        if (!BeginRoomRequest()) return;
        string roomId = roomNumberText != null ? roomNumberText.text : Random.Range(100000, 999999).ToString();
        UpdateStatus("Creating room " + roomId + "...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", equippedSkillIndex);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // ใส่ข้อมูลด่านลงไปในห้อง
        ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
        roomProps.Add("MapIndex", selectedMapIndex);
        roomProps.Add("MapRevision", 0);
        roomProps.Add("Starting", false);

        RoomOptions options = new RoomOptions 
        { 
            PlayerTtl = 60000,
            EmptyRoomTtl = 60000,
            MaxPlayers = 2, 
            IsVisible = true, 
            IsOpen = true,
            CustomRoomProperties = roomProps,
            CustomRoomPropertiesForLobby = new string[] { "MapIndex" }
        };
        
        if (!PhotonNetwork.CreateRoom(roomId, options, TypedLobby.Default)) RoomRequestFailed();
    }

    public void OnSearchRoom()
    {
        if (roomSearchInput == null || string.IsNullOrWhiteSpace(roomSearchInput.text))
        {
            UpdateStatus("Enter a room code first.");
            return;
        }
        JoinRoomByName(roomSearchInput.text);
    }

    public void OnLogoutButtonClicked()
    {
        loggingOut = true;
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        SceneManager.LoadScene("LoginScene");
    }

    public void OnSettingsClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnCloseSettingsClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnTutorialClicked()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
    }

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
[System.Serializable]
public class ShipData
{
    public string name;
    public int hp;
    public float atk;
    public float spd;
    public string skill;
    public int price;
    public string spritePath;

    public ShipData(string name, int hp, float atk, float spd, string skill, int price, string spritePath)
    {
        this.name = name;
        this.hp = hp;
        this.atk = atk;
        this.spd = spd;
        this.skill = skill;
        this.price = price;
        this.spritePath = spritePath;
    }
}

[System.Serializable]
public class SkillData
{
    public string name;
    public string description;
    public string iconPath;

    public SkillData(string name, string desc, string iconPath)
    {
        this.name = name;
        this.description = desc;
        this.iconPath = iconPath;
    }
}



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
            if (coinText != null) coinText.text = "Astronium Coins : " + coins;
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
            UIButton("RetryBrowser", root, "RECONNECT / RETRY", 0, -345, 250, 44, RetryLobbyConnection);
        }
        // Common recovery action remains visible when the main menu is disconnected.
        if (mainPanel != null)
        {
            Button retry = UIButton("RetryConnection", mainPanel.transform, "RECONNECT / RETRY", 0, 0, 240, 48, RetryLobbyConnection);
            RectTransform rect = retry.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 85);
        }
        FitLobbyUI();
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
