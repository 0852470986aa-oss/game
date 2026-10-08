// LobbyManager.Rooms.cs — ส่วนห้อง Photon ของ LobbyManager (partial class เดียวกัน) ใน LobbyScene
// รับ callback ของ Photon (เชื่อมต่อ, เข้า/ออกห้อง, property เปลี่ยน, หลุด) และสั่ง Quick Match / สร้างห้อง / เข้าห้องด้วยรหัส
// callback ทั้งหมดรันบนเครื่องของผู้เล่นแต่ละคนเมื่อ Photon แจ้งเหตุการณ์ (ไม่ใช่ RPC)
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
    // Photon เรียกเมื่อเชื่อมต่อ Master Server สำเร็จ: ถ้ากำลังกู้ห้องให้ Rejoin ห้องเดิม ไม่งั้นเข้า Lobby
    public override void OnConnectedToMaster()
    {
        if (loggingOut) return;
        // โหมดออฟไลน์ (เล่นคนเดียว): สร้างห้อง SOLO แทนการเข้า Lobby
        if (PhotonNetwork.OfflineMode) { if (pendingSolo) CreateSoloRoom(); return; }
        if (reconnecting && !string.IsNullOrEmpty(previousRoom))
        {
            if (!PhotonNetwork.RejoinRoom(previousRoom)) RecoveryFailed("Could not return to the room.");
            return;
        }
        PhotonNetwork.JoinLobby();
    }

    // Photon เรียกเมื่อเข้า Lobby แล้ว: ล้างแคชห้อง (Photon จะส่งรายการใหม่มา) และเปิดปุ่มเล่น
    public override void OnJoinedLobby()
    {
        roomRequestPending = false;
        pendingRanked = false;
        cachedRooms.Clear();
        RenderRoomList();
        UpdateStatus(profileLoaded ? "Ready! Create or join a room." : "Loading your ship and skill...");
        EnablePlayButtons(true);
    }

    // Photon เรียกบนเครื่องเราเมื่อเข้าห้องสำเร็จ (ทั้งสร้างเอง เข้าห้อง หรือ Rejoin): ล้างธงสถานะ จำชื่อห้องไว้กู้
    // ส่ง loadout ของเราแบบยังไม่ Ready แล้วเปิดหน้าห้องรอ; ถ้าแมตช์ก่อนถูกยกเลิก (BattleAborted) แจ้งผู้เล่น
    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.OfflineMode) { LaunchSoloRoom(); return; }
        // กันหลุดเข้าห้องแรงค์ทางอื่นที่ไม่ใช่การหาคู่แรงค์ (เช่นจอยชื่อห้องตรง ๆ) — กลับเข้าห้องเดิมหลังเน็ตหลุดยังได้
        if (RankedPrivate(PhotonNetwork.CurrentRoom) && !pendingRanked && !reconnecting)
        {
            roomRequestPending = false;
            isLeavingRoom = PhotonNetwork.LeaveRoom(false);
            UpdateStatus("Ranked matches can't be joined with a code or invite. Use FIND RANKED MATCH.");
            EnablePlayButtons(true);
            return;
        }
        pendingRanked = false;
        roomRequestPending = false;
        readyPending = false;
        reconnecting = false;
        previousRoom = PhotonNetwork.CurrentRoom.Name;
        isLeavingRoom = false;
        isStartingGame = false;
        UpdateStatus($"Joined room! ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
        
        // ตั้งค่าเริ่มต้น: ยังไม่พร้อม
        PublishLocalLoadout(true);
        
        // เปิดหน้า Waiting Room แทนการโหลดเกมทันที
        ShowWaitingRoom();
        // ถูก Host เตะไว้แล้ว = ออกทันที
        CheckKicked();
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("BattleAborted", out object aborted) && aborted is bool wasAborted && wasAborted)
            UpdateStatus("Match cancelled: a pilot left or disconnected. Wait for both pilots, then Ready again.");
    }

    // Photon เรียกบนทุกเครื่องในห้องเมื่อมีผู้เล่นใหม่เข้ามา
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        UpdateStatus(newPlayer.NickName + " joined the room");
        UpdateWaitingRoomUI();
    }

    // Photon เรียกเมื่ออีกฝ่ายออกหรือหลุด: รีเซ็ต Ready ของเรา (ต้องกดใหม่)
    // ถ้า IsInactive = หลุดชั่วคราว ห้องจองที่ไว้ 60 วินาที (PlayerTtl)
    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        PublishLocalLoadout(true);
        UpdateStatus(otherPlayer.NickName + (otherPlayer.IsInactive ? " lost connection. Slot reserved for 60 seconds." : " left the room"));
        UpdateWaitingRoomUI();
    }

    // Photon เรียกเมื่อ Master Client เปลี่ยน (Host เดิมออก): ถ้ากำลังเริ่มเกมอยู่ Master ใหม่จะพยายามเริ่มต่อ
    // ไม่งั้นรีเซ็ต Ready และแจ้งว่าใครเป็น Host ใหม่
    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (RoomStarting) { TryLaunchConfirmedRoom(); return; }
        PublishLocalLoadout(true);
        UpdateStatus(newMasterClient.IsLocal
            ? "You are now the room host"
            : newMasterClient.NickName + " is now the room host");
        UpdateWaitingRoomUI();
    }

    // Photon เรียกบนทุกเครื่องเมื่อ room property เปลี่ยน:
    // Starting = true -> เฉพาะ Master เรียก TryLaunchConfirmedRoom เพื่อโหลดเกม; MapIndex เปลี่ยน -> ทุกคนรีเซ็ต Ready
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
        if (propertiesThatChanged.ContainsKey(KickedProperty)) CheckKicked();
        // Host เปลี่ยนกติกาห้อง (Kill/เวลา/อันตราย) -> รีเฟรชปุ่มและสถานะ Ready
        else if (propertiesThatChanged.ContainsKey("MapRevision"))
        {
            UpdateWaitingRoomUI();
        }
    }

    // Photon เรียกเมื่อ property ของผู้เล่นเปลี่ยน: ถ้าเป็นของเราแปลว่า server ยืนยันแล้ว จึงปลด readyPending
    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (targetPlayer.IsLocal) readyPending = false;
        UpdateWaitingRoomUI();
    }

    // Photon เรียกเมื่อเข้าห้องไม่สำเร็จ (ห้องเต็ม/ไม่มีห้อง); ถ้ากำลังกู้ห้องถือว่ากู้ไม่สำเร็จ
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        roomRequestPending = false;
        pendingRanked = false;
        if (reconnecting) { RecoveryFailed("The room expired or the slot is no longer available."); return; }
        Debug.LogError($"Join Room Failed: {returnCode} - {message}");
        
        // ErrorCode 32765 = GameFull, 32758 = GameDoesNotExist, 32764 = GameClosed (Photon ErrorCodes)
        if (returnCode == 32765)
        {
            UpdateStatus("Room is full!");
        }
        else if (returnCode == 32758)
        {
            UpdateStatus("Room not found. Check the code - the room may have closed.");
        }
        else if (returnCode == 32764)
        {
            UpdateStatus("That room is closed or the match has already started.");
        }
        else
        {
            UpdateStatus("Failed to join room: " + message);
        }

        EnablePlayButtons(true);
    }

    // Photon เรียกเมื่อสร้างห้องไม่สำเร็จ (เช่นเลขห้องซ้ำ) แล้วกลับไปหน้าสร้างห้องซึ่งจะสุ่มเลขใหม่
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        roomRequestPending = false;
        pendingRanked = false;
        Debug.LogError($"Create Room Failed: {returnCode} - {message}");
        UpdateStatus("Failed to create room: " + message);
        EnablePlayButtons(true);
        ShowRoomPanel();
    }

    // Photon เรียกเมื่อออกจากห้องแล้ว: ล้างสถานะห้องและกลับหน้าหลัก
    public override void OnLeftRoom()
    {
        previousRoom = null;
        roomRequestPending = false;
        isLeavingRoom = false;
        isStartingGame = false;
        UpdateStatus(wasKicked ? "The host removed you from that room." : "Left room successfully");
        wasKicked = false;
        ShowMainPanel();
        EnablePlayButtons(PhotonNetwork.InLobby);
    }

    // Photon เรียกเมื่อ Quick Match (JoinRandomRoom) หาห้องว่างไม่เจอ: สร้างห้องใหม่เลขสุ่ม 6 หลักเอง
    // ห้อง 2 คน, PlayerTtl/EmptyRoomTtl = 60000 ms (จองที่/เก็บห้องไว้ 60 วินาทีเมื่อหลุด), ส่ง MapIndex ให้เห็นในรายการห้อง
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (!roomRequestPending || !PhotonNetwork.IsConnectedAndReady) return;
        // หาห้องแรงค์ไม่เจอ = สร้างห้องแรงค์ใหม่ (LobbyManager.Ranked.cs)
        if (pendingRanked) { CreateRankedRoom(); return; }
        UpdateStatus("No room found. Creating a new one...");
        string roomId = Random.Range(100000, 999999).ToString();

        ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
        // ถ้า Quick Match หาห้องไม่เจอ ให้ใช้ map ที่เลือกไว้ หรือสุ่มก็ได้ (ที่นี่เราใช้อันที่เลือกไว้)
        roomProps.Add("MapIndex", selectedMapIndex);
        roomProps.Add("MapRevision", 0);
        roomProps.Add("Starting", false);
        roomProps.Add(MatchRules.RankedKey, false); // ห้องปกติ (ไม่ใช่แรงค์)

        RoomOptions options = new RoomOptions 
        { 
            PlayerTtl = 60000,
            EmptyRoomTtl = 60000,
            MaxPlayers = 2, 
            IsVisible = true, 
            IsOpen = true,
            CustomRoomProperties = roomProps,
            CustomRoomPropertiesForLobby = new string[] { "MapIndex", MatchRules.TeamsKey, MatchRules.RankedKey }
        };
        if (!PhotonNetwork.CreateRoom(roomId, options, TypedLobby.Default)) RoomRequestFailed();
    }

    // Photon เรียกเมื่อหลุดการเชื่อมต่อ ขั้นตอน:
    // 1) ปิดปุ่มและล้างรายการห้อง (ถ้าออกจากระบบอยู่ให้หยุดแค่นี้)
    // 2) ถ้าสาเหตุแก้ด้วยการลองใหม่ไม่ได้ (Auth ผิด, CCU เต็ม, Region ผิด, ปิดแอป) ให้หยุดระบบเชื่อมต่ออัตโนมัติ
    // 3) ถ้าหลุดขณะอยู่ในห้องรอโดยไม่ได้ตั้งใจ ตั้ง reconnecting ให้ Update พยายามกลับห้องเดิมภายใน 50 วินาที
    // 4) กรณีอื่นกลับหน้าหลักแล้วให้ระบบเชื่อมต่อ Lobby ใหม่อัตโนมัติ
    public override void OnDisconnected(DisconnectCause cause)
    {
        roomRequestPending = false;
        readyPending = false;
        EnablePlayButtons(false);
        cachedRooms.Clear();
        RenderRoomList();
        if (loggingOut) return;
        // ตั้งใจตัดเน็ตเพื่อเข้าโหมดเล่นคนเดียว
        if (pendingSolo)
        {
            // เดิมเข้าโหมดออฟไลน์ทันทีในตัว callback นี้ -> Photon ยังเก็บกวาดการตัดเน็ตไม่เสร็จ ห้อง SOLO ที่เพิ่งสร้างโดนล้าง
            // (กดครั้งแรกแวบแล้วเด้งกลับ ต้องกดใหม่) ตอนนี้รอ 1 เฟรมก่อน (FeatureFlags.SoloStartFix)
            if (FeatureFlags.SoloStartFix) StartCoroutine(EnterOfflineSoloNextFrame());
            else EnterOfflineSolo();
            return;
        }
        // 2) สาเหตุที่ลองใหม่ไม่ช่วย
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
        // 3) พยายามกลับห้องเดิม
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
        // 4) กลับหน้าหลัก
        reconnecting = false;
        previousRoom = null;
        isLeavingRoom = false;
        isStartingGame = false;
        ShowMainPanel();
        UpdateStatus("Connection lost. Reconnecting automatically...");
    }

    // เลิกกู้ห้องเดิม: กลับหน้าหลัก แสดงเหตุผล และเข้า Lobby ถ้ายังเชื่อมต่ออยู่
    private void RecoveryFailed(string message)
    {
        reconnecting = false;
        previousRoom = null;
        ShowMainPanel();
        UpdateStatus(message);
        if (PhotonNetwork.IsConnectedAndReady) PhotonNetwork.JoinLobby();
    }

    // Photon เรียกเมื่อรายการห้องใน Lobby เปลี่ยน (ส่งมาเฉพาะห้องที่เปลี่ยน): รวมเข้าแคชแล้ววาดรายการใหม่
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

    // วาดรายการห้องในหน้าค้นหาห้อง: ลบแถวเดิม แล้วสร้างแถวใหม่จาก roomItemPrefab สำหรับห้องที่เปิดและมองเห็นได้
    // เรียงตามชื่อห้อง แต่ละแถวแสดงรหัสห้อง จำนวนคน แม็พ และปุ่ม Join (กดได้เมื่อห้องยังไม่เต็ม)
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
            if (RankedPrivate(room)) continue; // ห้องแรงค์เข้าได้จากปุ่ม FIND RANKED MATCH เท่านั้น

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
                else if (t.name == "RoomModeMapText") t.text = (MatchRules.IsRanked(room) ? "RANKED 1V1" : MatchRules.ModeName(room.MaxPlayers, MatchRules.IsTeamRoom(room))) + " | " + mapNames[ReadMap(room)];
                if (t.name == "RoomNameText" || t.name == "RoomModeMapText")
                {
                    t.rectTransform.anchoredPosition = new Vector2(-60, t.name == "RoomNameText" ? 15 : -15);
                    t.rectTransform.sizeDelta = new Vector2(300, 28);
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

    // เข้าห้องตามชื่อ/รหัส (จากปุ่ม Join ในรายการ หรือช่องค้นหา): ส่งยาน สกิล สียานเป็น property ก่อน แล้ว JoinRoom
    public void JoinRoomByName(string roomName)
    {
        roomName = CleanRoomCode(roomName);
        if (string.IsNullOrEmpty(roomName)) return;
        if (FeatureFlags.RankedNoInvite && IsRankedRoomName(roomName))
        {
            UpdateStatus("Ranked matches can't be joined with a code or invite. Use FIND RANKED MATCH.");
            return;
        }
        if (!BeginRoomRequest())
        {
            // เดิมกดแล้วเงียบ: บอกเหตุผลที่ยังจอยไม่ได้
            if (!roomRequestPending && profileLoaded) UpdateStatus("Connecting to the server... try again in a moment.");
            return;
        }
        UpdateStatus("Joining room " + roomName + "...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", SkillUnlock.Usable(equippedSkillIndex)); // สกิลที่ยังล็อก = ใช้สกิลแรก (SkillUnlock.cs)
        props.Add(ShipPaint.Property, ShipPaint.Local);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        if (!PhotonNetwork.JoinRoom(roomName)) RoomRequestFailed();
    }

    // ============================
    //  BUTTON FUNCTIONS
    // ============================

    // ปุ่ม QUICK MATCH: ส่ง loadout เป็น property แล้วสุ่มเข้าห้องที่ว่าง (ไม่เจอจะไป OnJoinRandomFailed เพื่อสร้างห้อง)
    public void OnPlayButtonClicked()
    {
        if (!BeginRoomRequest()) return;
        EnablePlayButtons(false);
        UpdateStatus("Searching for room (Quick Match)...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", SkillUnlock.Usable(equippedSkillIndex)); // สกิลที่ยังล็อก = ใช้สกิลแรก (SkillUnlock.cs)
        props.Add(ShipPaint.Property, ShipPaint.Local);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // Quick Match เข้าเฉพาะห้องปกติ (ไม่เข้าห้องแรงค์)
        if (!PhotonNetwork.JoinRandomRoom(new ExitGames.Client.Photon.Hashtable { [MatchRules.RankedKey] = false }, 0)) RoomRequestFailed();
    }

    // ปุ่ม CREATE / JOIN ROOM ในหน้าหลัก: เปิดหน้าค้นหา/สร้างห้อง
    public void OnCreateRoomClicked()
    {
        ShowRoomPanel();
    }

    // ปุ่มยืนยันสร้างห้อง: ใช้เลขห้องที่สุ่มไว้บนจอ ส่ง loadout แล้วสร้างห้องพร้อมแม็พที่เลือก
    // ตั้งค่าห้องเหมือน OnJoinRandomFailed (2 คน, จองที่ 60 วินาที, MapRevision = 0, Starting = false)
    public void OnCreateRoomConfirm()
    {
        if (!BeginRoomRequest()) return;
        string roomId = roomNumberText != null ? roomNumberText.text : Random.Range(100000, 999999).ToString();
        UpdateStatus("Creating room " + roomId + "...");

        // Set chosen ship & skill for Gameplay spawn
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("ShipType", equippedShipIndex);
        props.Add("SkillType", SkillUnlock.Usable(equippedSkillIndex)); // สกิลที่ยังล็อก = ใช้สกิลแรก (SkillUnlock.cs)
        props.Add(ShipPaint.Property, ShipPaint.Local);
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // ใส่ข้อมูลด่านลงไปในห้อง
        ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
        roomProps.Add("MapIndex", selectedMapIndex);
        roomProps.Add("MapRevision", 0);
        roomProps.Add("Starting", false);
        roomProps.Add(MatchRules.RankedKey, false); // ห้องปกติ (ไม่ใช่แรงค์)

        RoomOptions options = new RoomOptions 
        { 
            PlayerTtl = 60000,
            EmptyRoomTtl = 60000,
            MaxPlayers = 2, 
            IsVisible = true, 
            IsOpen = true,
            CustomRoomProperties = roomProps,
            CustomRoomPropertiesForLobby = new string[] { "MapIndex", MatchRules.TeamsKey, MatchRules.RankedKey }
        };
        
        if (!PhotonNetwork.CreateRoom(roomId, options, TypedLobby.Default)) RoomRequestFailed();
    }

    // ปุ่ม JOIN ในหน้าค้นหาห้อง: เข้าห้องตามรหัสที่พิมพ์ (ถ้าว่างแจ้งให้ใส่รหัสก่อน)
    public void OnSearchRoom()
    {
        string code = roomSearchInput != null ? CleanRoomCode(roomSearchInput.text) : "";
        if (string.IsNullOrEmpty(code))
        {
            UpdateStatus("Enter a room code first.");
            return;
        }
        roomSearchInput.text = code;
        JoinRoomByName(code);
    }

    // ทำความสะอาดเลขห้องที่พิมพ์/วางมา: เก็บแค่ตัวอักษรอังกฤษกับตัวเลข (ตัดช่องว่าง ขีด # ขึ้นบรรทัดใหม่ ที่ติดมาตอนคัดลอก)
    public static string CleanRoomCode(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var code = new System.Text.StringBuilder();
        foreach (char c in raw) if (c < 128 && char.IsLetterOrDigit(c)) code.Append(c);
        return code.ToString();
    }
}
