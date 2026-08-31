using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviourPunCallbacks
{
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
    }

    void Start()
    {
        EnablePlayButtons(false);
        ShowMainPanel();

        // PHASE 5: เล่นเพลงหน้าเมนู
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BGM_Lobby");
        }
        
        // แสดงข้อมูลผู้เล่น
        if (FirebaseManager.Instance != null)
        {
            string username = FirebaseManager.Instance.GetUsername();
            if (playerNameText != null)
                playerNameText.text = "Player\n" + username;

            FirebaseManager.Instance.GetCoinBalance(coins =>
            {
                if (coinText != null)
                    coinText.text = "Astronium Coins : " + coins;
            });

            // โหลดข้อมูลคลังยานจากฐานข้อมูล
            FirebaseManager.Instance.GetUnlockedShips(ships =>
            {
                unlockedShips = ships;
                
                // หลังจากโหลดยานที่ปลดล็อกแล้ว โลหดยานที่สวมใส่ต่อ
                FirebaseManager.Instance.GetSelectedShip(shipIdx =>
                {
                    equippedShipIndex = shipIdx;
                    selectedShipIndex = shipIdx;
                    UpdateShipDisplay(selectedShipIndex);
                });
            });

            // โหลดสกิลที่ติดตั้งล่าสุด
            FirebaseManager.Instance.GetSelectedSkill(skillIdx =>
            {
                equippedSkillIndex = skillIdx;
                selectedSkillIndex = skillIdx;
                UpdateSkillDisplay(selectedSkillIndex);
            });
        }
        else
        {
            UpdateStatus("Failed to load user data!");
        }

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
            UpdateStatus("Connecting to server...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else if (PhotonNetwork.InLobby)
        {
            UpdateStatus("Ready! Press button to enter room");
            EnablePlayButtons(true);
        }
        else
        {
            UpdateStatus("Entering Lobby...");
            PhotonNetwork.JoinLobby();
        }

    }

    void Update()
    {
        if (playersOnlineText != null && PhotonNetwork.IsConnected)
            playersOnlineText.text = "Players Online: " + PhotonNetwork.CountOfPlayers;
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
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        ShowPanelAnimated(mainPanel);
    }

    public void ShowInventoryPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        ShowPanelAnimated(inventoryPanel);
        UpdateInventoryDisplay(selectedShipIndex);
    }

    public void ShowRoomPanel()
    {
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
        UpdateStatus("Connected successfully!");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        UpdateStatus("Ready! Press button to enter room");
        EnablePlayButtons(true);
    }

    public override void OnJoinedRoom()
    {
        UpdateStatus($"Joined room! ({PhotonNetwork.CurrentRoom.PlayerCount}/2)");
        
        // ตั้งค่าเริ่มต้น: ยังไม่พร้อม
        ExitGames.Client.Photon.Hashtable readyProps = new ExitGames.Client.Photon.Hashtable();
        readyProps["IsReady"] = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(readyProps);
        
        // เปิดหน้า Waiting Room แทนการโหลดเกมทันที
        ShowWaitingRoom();
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        UpdateWaitingRoomUI();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        UpdateWaitingRoomUI();
    }

    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        UpdateWaitingRoomUI();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
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

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        UpdateStatus("No room found. Creating a new one...");
        string roomId = Random.Range(100000, 999999).ToString();

        ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
        // ถ้า Quick Match หาห้องไม่เจอ ให้ใช้ map ที่เลือกไว้ หรือสุ่มก็ได้ (ที่นี่เราใช้อันที่เลือกไว้)
        roomProps.Add("MapIndex", selectedMapIndex);

        RoomOptions options = new RoomOptions 
        { 
            MaxPlayers = 2, 
            IsVisible = true, 
            IsOpen = true,
            CustomRoomProperties = roomProps,
            CustomRoomPropertiesForLobby = new string[] { "MapIndex" }
        };
        PhotonNetwork.CreateRoom(roomId, options, TypedLobby.Default);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        UpdateStatus("Disconnected...");
        EnablePlayButtons(false);
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // อัปเดตรายการห้อง (สำหรับหน้า Room Panel)
        Debug.Log($"Found {roomList.Count} rooms");

        if (roomListContent == null || roomItemPrefab == null) return;

        // ลบรายการเดิมทั้งหมด (ยกเว้น RoomItemPrefab ถ้ามันอยู่ในนั้นด้วย)
        foreach (Transform child in roomListContent)
        {
            Destroy(child.gameObject);
        }

        foreach (RoomInfo room in roomList)
        {
            if (room.RemovedFromList) continue; // ข้ามห้องที่โดนลบไปแล้ว
            if (!room.IsOpen || !room.IsVisible) continue; // ข้ามห้องที่ปิดหรือซ่อนอยู่

            GameObject roomItem = Instantiate(roomItemPrefab, roomListContent);
            roomItem.SetActive(true);

            // ค้นหา Text ภายในปุ่ม
            TMP_Text[] texts = roomItem.GetComponentsInChildren<TMP_Text>();
            foreach (TMP_Text t in texts)
            {
                if (t.name == "RoomNameText") t.text = "Room: " + room.Name;
                else if (t.name == "RoomPlayersText") t.text = room.PlayerCount + " / " + room.MaxPlayers;
            }

            // ผูกปุ่ม Join
            Button joinBtn = roomItem.GetComponentInChildren<Button>();
            if (joinBtn != null)
            {
                joinBtn.onClick.AddListener(() => JoinRoomByName(room.Name));
            }
        }
    }

    public void JoinRoomByName(string roomName)
    {
        UpdateStatus("Joining room " + roomName + "...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", equippedSkillIndex);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        PhotonNetwork.JoinRoom(roomName);
    }

    // ============================
    //  BUTTON FUNCTIONS
    // ============================

    public void OnPlayButtonClicked()
    {
        EnablePlayButtons(false);
        UpdateStatus("Searching for room (Quick Match)...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", equippedSkillIndex);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        PhotonNetwork.JoinRandomRoom();
    }

    public void OnCreateRoomClicked()
    {
        ShowRoomPanel();
    }

    public void OnCreateRoomConfirm()
    {
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

        RoomOptions options = new RoomOptions 
        { 
            MaxPlayers = 2, 
            IsVisible = true, 
            IsOpen = true,
            CustomRoomProperties = roomProps,
            CustomRoomPropertiesForLobby = new string[] { "MapIndex" }
        };
        
        PhotonNetwork.CreateRoom(roomId, options, TypedLobby.Default);
    }

    public void OnSearchRoom()
    {
        if (roomSearchInput == null || string.IsNullOrEmpty(roomSearchInput.text)) return;
        UpdateStatus("Searching for room " + roomSearchInput.text + "...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", equippedSkillIndex);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        PhotonNetwork.JoinRoom(roomSearchInput.text);
    }

    public void OnLogoutButtonClicked()
    {
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
    }

    private void EnablePlayButtons(bool on)
    {
        if (playButton != null) playButton.interactable = on;
        if (createRoomButton != null) createRoomButton.interactable = on;
    }

    // ============================
    //  WAITING ROOM SYSTEM
    // ============================

    private void UpdateWaitingRoomUI()
    {
        if (waitingRoomPanel == null || !waitingRoomPanel.activeSelf) return;

        // แสดงเลขห้อง
        if (waitRoomNumberText != null && PhotonNetwork.CurrentRoom != null)
            waitRoomNumberText.text = "Room: " + PhotonNetwork.CurrentRoom.Name;

        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;

        // Player 1 (Master Client / ผู้เล่นคนแรก)
        if (players.Length >= 1)
        {
            Photon.Realtime.Player p1 = players[0];
            if (waitP1NameText != null) waitP1NameText.text = p1.NickName;

            int p1Ship = p1.CustomProperties.ContainsKey("ShipType") ? (int)p1.CustomProperties["ShipType"] : 0;
            int p1Skill = p1.CustomProperties.ContainsKey("SkillType") ? (int)p1.CustomProperties["SkillType"] : 0;
            bool p1Ready = p1.CustomProperties.ContainsKey("IsReady") && (bool)p1.CustomProperties["IsReady"];

            if (p1Ship >= 0 && p1Ship < ships.Length)
            {
                if (waitP1ShipNameText != null) waitP1ShipNameText.text = ships[p1Ship].name;
                if (waitP1StatsText != null) waitP1StatsText.text = ships[p1Ship].hp + " HP  |  ATK " + ships[p1Ship].atk + "  |  SPD " + ships[p1Ship].spd;
                if (waitP1ShipImage != null)
                {
                    Sprite sp = Resources.Load<Sprite>(ships[p1Ship].spritePath);
                    if (sp != null) { waitP1ShipImage.sprite = sp; waitP1ShipImage.color = Color.white; }
                }
            }
            if (p1Skill >= 0 && p1Skill < skills.Length)
            {
                if (waitP1SkillText != null) waitP1SkillText.text = skills[p1Skill].name;
            }
            if (waitP1ReadyText != null)
                waitP1ReadyText.text = p1Ready ? "READY" : "NOT READY";
        }

        // Player 2 (ผู้เล่นคนที่สอง)
        if (players.Length >= 2)
        {
            Photon.Realtime.Player p2 = players[1];
            if (waitP2NameText != null) waitP2NameText.text = p2.NickName;

            int p2Ship = p2.CustomProperties.ContainsKey("ShipType") ? (int)p2.CustomProperties["ShipType"] : 0;
            int p2Skill = p2.CustomProperties.ContainsKey("SkillType") ? (int)p2.CustomProperties["SkillType"] : 0;
            bool p2Ready = p2.CustomProperties.ContainsKey("IsReady") && (bool)p2.CustomProperties["IsReady"];

            if (p2Ship >= 0 && p2Ship < ships.Length)
            {
                if (waitP2ShipNameText != null) waitP2ShipNameText.text = ships[p2Ship].name;
                if (waitP2StatsText != null) waitP2StatsText.text = ships[p2Ship].hp + " HP  |  ATK " + ships[p2Ship].atk + "  |  SPD " + ships[p2Ship].spd;
                if (waitP2ShipImage != null)
                {
                    Sprite sp = Resources.Load<Sprite>(ships[p2Ship].spritePath);
                    if (sp != null) { waitP2ShipImage.sprite = sp; waitP2ShipImage.color = Color.white; }
                }
            }
            if (p2Skill >= 0 && p2Skill < skills.Length)
            {
                if (waitP2SkillText != null) waitP2SkillText.text = skills[p2Skill].name;
            }
            if (waitP2ReadyText != null)
                waitP2ReadyText.text = p2Ready ? "READY" : "NOT READY";
        }
        else
        {
            // ยังไม่มีผู้เล่นคนที่ 2
            if (waitP2NameText != null) waitP2NameText.text = "Waiting...";
            if (waitP2ShipNameText != null) waitP2ShipNameText.text = "";
            if (waitP2StatsText != null) waitP2StatsText.text = "";
            if (waitP2SkillText != null) waitP2SkillText.text = "";
            if (waitP2ReadyText != null) waitP2ReadyText.text = "";
            if (waitP2ShipImage != null) waitP2ShipImage.color = new Color(1, 1, 1, 0.2f);
        }

        // อัปเดตข้อมูลด่าน (Map) ในหน้า Waiting Room
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("MapIndex", out object mapProp))
        {
            int mapIdx = (int)mapProp;

            // พยายามหาจากชื่อในฉาก ถ้าคุณลืมลากใส่ช่อง
            if (waitMapNameText == null)
            {
                TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                foreach (var t in allTexts)
                {
                    if (t.name.Contains("MapNameText") || t.text.Contains("Prism") || t.text.Contains("Mech"))
                    {
                        waitMapNameText = t;
                        break;
                    }
                }
            }

            // ดึงค่า MapName Text และ Map Image ที่อยู่ใน Waiting Room
            if (waitMapNameText != null) waitMapNameText.text = mapNames[mapIdx];
            
            if (waitMapImage != null)
            {
                Sprite s = Resources.Load<Sprite>(mapImages[mapIdx]);
                if (s != null) waitMapImage.sprite = s;
            }
        }

        // ตรวจสอบความพร้อมเพื่อเปิดปุ่ม Start Game (เฉพาะ MasterClient) + ทุกคนพร้อม)
        if (waitStartButton != null)
        {
            bool allReady = true;
            foreach (Photon.Realtime.Player p in players)
            {
                if (!p.CustomProperties.ContainsKey("IsReady") || !(bool)p.CustomProperties["IsReady"])
                {
                    allReady = false;
                    break;
                }
            }
            waitStartButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
            waitStartButton.interactable = allReady && players.Length >= 2;
        }

        // ปุ่ม Ready ของตัวเอง
        if (waitReadyButton != null)
        {
            bool myReady = PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("IsReady")
                && (bool)PhotonNetwork.LocalPlayer.CustomProperties["IsReady"];
            TMP_Text readyBtnText = waitReadyButton.GetComponentInChildren<TMP_Text>();
            if (readyBtnText != null)
                readyBtnText.text = myReady ? "Cancel Ready" : "Ready";
            waitReadyButton.GetComponent<Image>().color = myReady ? new Color(0.8f, 0.5f, 0.2f) : new Color(0.2f, 0.7f, 0.3f);
        }
    }

    public void OnReadyButtonClicked()
    {
        bool currentReady = PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("IsReady")
            && (bool)PhotonNetwork.LocalPlayer.CustomProperties["IsReady"];
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props["IsReady"] = !currentReady;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public void OnStartGameClicked()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        PhotonNetwork.LoadLevel("SampleScene");
    }

    public void NextMap()
    {
        selectedMapIndex++;
        if (selectedMapIndex >= mapNames.Length) selectedMapIndex = 0;
        if (createRoomMapNameText != null) createRoomMapNameText.text = mapNames[selectedMapIndex];
    }

    public void PrevMap()
    {
        selectedMapIndex--;
        if (selectedMapIndex < 0) selectedMapIndex = mapNames.Length - 1;
        if (createRoomMapNameText != null) createRoomMapNameText.text = mapNames[selectedMapIndex];
    }

    public void OnLeaveWaitingRoom()
    {
        PhotonNetwork.LeaveRoom();
        ShowMainPanel();
        EnablePlayButtons(true);
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
