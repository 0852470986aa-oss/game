using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

// ส่วน Rooms ของ LobbyManager; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class LobbyManager
{
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
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("BattleAborted", out object aborted) && aborted is bool wasAborted && wasAborted)
            UpdateStatus("Match cancelled: a pilot left or disconnected. Wait for both pilots, then Ready again.");
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
        automaticRecoveryBlocked = cause == DisconnectCause.InvalidAuthentication
            || cause == DisconnectCause.CustomAuthenticationFailed || cause == DisconnectCause.MaxCcuReached
            || cause == DisconnectCause.InvalidRegion || cause == DisconnectCause.ApplicationQuit;
        if (automaticRecoveryBlocked)
        {
            reconnecting = false;
            previousRoom = null;
            ShowMainPanel();
            UpdateStatus("Connection cannot recover automatically: " + cause + ". Check account/server settings.");
            return;
        }
        nextAutoReconnect = Time.unscaledTime + 2f;
        if (!isLeavingRoom && !isStartingGame && !string.IsNullOrEmpty(previousRoom)
            && cause != DisconnectCause.DisconnectByClientLogic)
        {
            if (!reconnecting) reconnectDeadline = Time.unscaledTime + 50f;
            reconnecting = true;
            UpdateStatus("Connection lost. Returning to your room...");
            UpdateWaitingRoomUI();
            return; // The update loop retries with backoff until the room-recovery deadline.
        }
        reconnecting = false;
        previousRoom = null;
        isLeavingRoom = false;
        isStartingGame = false;
        ShowMainPanel();
        UpdateStatus("Connection lost. Reconnecting automatically...");
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
}
