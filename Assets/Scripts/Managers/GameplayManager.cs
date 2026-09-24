using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public partial class GameplayManager : MonoBehaviourPunCallbacks
{
    public static string RecoveryRoom;
    private string battleRoomName;
    private bool returningToRoom;
    private bool intentionalLeave;
    private bool requestedRematch;
    private TMP_Text rematchLabel;


    private void StopInterruptedBattle()
    {
        isMatchEnding = true;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            ship.SetMatchEndedRPC();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        if (intentionalLeave || resultShown) return;
        StopInterruptedBattle();
        ReturnToWaitingRoom();
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (intentionalLeave || resultShown || !PhotonNetwork.InRoom) return;
        int active = 0;
        foreach (var pilot in PhotonNetwork.PlayerList) if (!pilot.IsInactive) active++;
        if (active < 2) { StopInterruptedBattle(); ReturnToWaitingRoom(); }
    }

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

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        StopInterruptedBattle();
        RecoveryRoom = !intentionalLeave && cause != Photon.Realtime.DisconnectCause.DisconnectByClientLogic
            ? battleRoomName : null;
        SceneManager.LoadScene("LobbyScene");
    }

    public static GameplayManager Instance;


    [Header("UI Controls")]
    public UIJoystick joystick;
    public UIButton fireButton;
    public UIButton skillButton;
    public Image skillCooldownImage;
    public Image skillIconImage;

    [Header("UI Text")]
    public TMP_Text pingText;
    public TMP_Text playerInfoText;

    [Header("Players")]
    public PlayerController localPlayer;
    public PlayerController remotePlayer;

    [Header("Match Settings")]
    public float matchDuration = 180f; // 3 minutes
    private float matchTimer;
    private bool matchStarted = false;
    private TMP_Text battleCountdownText;
    private RectTransform damageDirectionRoot;
    private Image damageDirectionMarker;
    private float damageDirectionUntil;
    private Vector3 damageSourcePosition;

    public void ShowIncomingDamage(int shooterId)
    {
        if (localPlayer == null || battleHud == null || isMatchEnding || shooterId <= 0) return;
        PlayerController source = null;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (ship.photonView.OwnerActorNr == shooterId && ship != localPlayer) { source = ship; break; }
        if (source == null) return; // Do not invent a direction for environmental damage.
        damageSourcePosition = source.transform.position;
        damageDirectionUntil = Time.unscaledTime + 1.1f;
        if (damageDirectionRoot == null)
        {
            damageDirectionRoot = BattleRect("IncomingDamage", battleHud, 0, 0, 1, 1);
            damageDirectionMarker = BattlePanel("Direction", damageDirectionRoot, 0, 118, 34, 7, new Color(1, .2f, .1f));
            BattlePanel("Tip", damageDirectionRoot, 0, 127, 10, 10, new Color(1, .45f, .15f));
        }
    }

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
    private TMP_Text hitConfirmationText;
    private float hitConfirmationUntil;

    public void ShowConfirmedHit(bool shield)
    {
        if (battleHud == null || isMatchEnding) return;
        if (hitConfirmationText == null)
            hitConfirmationText = BattleLabel("HitConfirmation", battleHud, "", 0, -100, 260, 38, 22);
        hitConfirmationText.text = shield ? "SHIELD HIT" : "HIT";
        hitConfirmationText.color = shield ? new Color(.3f, .85f, 1f) : new Color(1f, .8f, .3f);
        hitConfirmationUntil = Time.unscaledTime + .3f;
        hitConfirmationText.gameObject.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(shield ? "SFX_ShieldHit" : "SFX_Hit");
    }
    private Image respawnPanel;
    private TMP_Text respawnReasonText;
    private TMP_Text respawnTimerText;

    private void UpdateRespawnHUD()
    {
        bool visible = localPlayer != null && localPlayer.isDead && !isMatchEnding && !localPlayer.HasMatchEnded;
        if (visible && respawnPanel == null && battleHud != null)
        {
            respawnPanel = BattlePanel("RespawnPanel", battleHud, 0, 5, 650, 160, new Color(.035f, .055f, .12f, .94f));
            BattleLabel("Title", respawnPanel.transform, "SHIP DESTROYED", 0, 48, 610, 38, 30);
            respawnReasonText = BattleLabel("Reason", respawnPanel.transform, "", 0, 6, 610, 32, 20);
            respawnReasonText.richText = false;
            respawnTimerText = BattleLabel("Countdown", respawnPanel.transform, "", 0, -43, 610, 36, 25);
            respawnTimerText.color = new Color(.25f, .95f, 1f);
        }
        if (respawnPanel == null) return;
        respawnPanel.gameObject.SetActive(visible);
        if (!visible) return;
        respawnReasonText.text = localPlayer.DeathReason;
        int seconds = Mathf.CeilToInt(localPlayer.RespawnReadyAt - Time.unscaledTime);
        respawnTimerText.text = seconds > 0 ? "RESPAWNING IN " + seconds : "FINDING A SAFE SPAWN...";
    }
    private float nextReadyUpdate;
    public bool MatchInputAllowed => !isMatchEnding && (!PhotonNetwork.InRoom || matchStarted);

    private void UpdateBattleStart()
    {
        if (!PhotonNetwork.InRoom || isMatchEnding) return;
        var room = PhotonNetwork.CurrentRoom;
        room.CustomProperties.TryGetValue("BattleToken", out object tokenValue);
        string token = tokenValue as string;
        double start = room.CustomProperties.TryGetValue("StartTime", out object value) && value is double timestamp
            ? timestamp : -1d;
        bool bothReady = !string.IsNullOrEmpty(token) && PhotonNetwork.PlayerList.Length == 2;
        foreach (var pilot in PhotonNetwork.PlayerList)
            bothReady &= !pilot.IsInactive && pilot.CustomProperties.TryGetValue("LoadedBattleToken", out object loaded)
                && Equals(loaded, token);

        if (!matchStarted && Time.unscaledTime >= nextReadyUpdate)
        {
            nextReadyUpdate = Time.unscaledTime + 1f;
            if (localPlayer != null && !string.IsNullOrEmpty(token))
            {
                if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("LoadedBattleToken", out object loaded)
                    || !Equals(loaded, token))
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["LoadedBattleToken"] = token });
            }
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
        if (bothReady && start >= 0 && PhotonNetwork.Time >= start) matchStarted = true;
        if (battleCountdownText == null && battleHud != null)
            battleCountdownText = BattleLabel("BattleCountdown", battleHud, "", 0, 40, 800, 100, 48);
        if (battleCountdownText != null)
        {
            battleCountdownText.gameObject.SetActive(!matchStarted || PhotonNetwork.Time - start < 1d);
            battleCountdownText.text = matchStarted ? "GO!" : !bothReady || start < 0
                ? "WAITING FOR PILOTS" : Mathf.Max(1, Mathf.CeilToInt((float)(start - PhotonNetwork.Time))).ToString();
        }
    }
    private bool isMatchEnding = false;

    private TMP_Text matchTimerText;
    private TMP_Text scoreText;

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
    
    [Header("Map UI")]
    public SpriteRenderer backgroundSprite;
    public GameObject lowHPWarning; // ขอบจอแดงเมื่อเลือดต่ำ
    public bool autoGenerateMap = true; // เปิด/ปิด การเสกอุกกาบาตอัตโนมัติ

    private TMP_Text p1HpText, p2HpText;
    private RectTransform p1HpFill, p2HpFill;

    void Awake()
    {
        Instance = this;
    }

    // --- PREFAB CACHE ---
    private static System.Collections.Generic.Dictionary<string, GameObject> prefabCache = new System.Collections.Generic.Dictionary<string, GameObject>();

    public static GameObject GetPrefab(string name)
    {
        if (!prefabCache.ContainsKey(name))
        {
            prefabCache[name] = Resources.Load<GameObject>(name);
        }
        return prefabCache[name];
    }
    // --------------------

    void Start()
    {
        if (PhotonNetwork.InRoom) battleRoomName = PhotonNetwork.CurrentRoom.Name;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BGM_Battle");
        }

        // (CreateMatchUI ถูกเรียกด้านล่างแล้ว ไม่ต้องเรียกซ้ำ)

        // Attach Minimap
        gameObject.AddComponent<RadarMinimap>();

        // Attach CameraFollow explicitly by searching for Camera
        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = FindObjectOfType<Camera>();
        
        if (mainCam != null && mainCam.GetComponent<CameraFollow>() == null)
        {
            mainCam.gameObject.AddComponent<CameraFollow>();
        }

        // ค้นหา UI อัตโนมัติจากชื่อ
        var p1txt = GameObject.Find("Player1HUD/HP_BG/HP_Text");
        var p1fill = GameObject.Find("Player1HUD/HP_BG/HP_Fill");
        if(p1txt) p1HpText = p1txt.GetComponent<TMP_Text>();
        if(p1fill) p1HpFill = p1fill.GetComponent<RectTransform>();

        var p2txt = GameObject.Find("Player2HUD/HP_BG/HP_Text");
        var p2fill = GameObject.Find("Player2HUD/HP_BG/HP_Fill");
        if(p2txt) p2HpText = p2txt.GetComponent<TMP_Text>();
        if(p2fill) p2HpFill = p2fill.GetComponent<RectTransform>();

        CreateMatchUI();
        StyleBattleHUD();
        BuildResultUI();

        // PHASE 5: เล่นเพลงตอนสู้
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BGM_Battle");
        }

        // Scene setup must also run in offline previews, before spawning any player.
        ApplySelectedMapLayout(GetCurrentMapIndex());

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
            string prefabName = "ShipPrefabs/Ship" + (shipIndex + 1);

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
                    string[] mapImages = { "Images/Map_ThunderJellyfish", "Images/Map_ObeliskPlains", "Images/Map_AncientMech" };
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

            // Initialize Kills
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props.Add("Kills", 0);
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);


            // Spawn Player (Vertical Layout - ห่างกันมากขึ้นเพื่อไม่ให้โดนยิงทันที)
            Vector3 spawnPos = PhotonNetwork.IsMasterClient ? new Vector3(0f, -16f, 0f) : new Vector3(0f, 16f, 0f);
            Quaternion spawnRot = PhotonNetwork.IsMasterClient ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);
            if (GetCurrentMapIndex() == 2) StartCoroutine(SpawnMechPlayerWhenClear(prefabName, spawnRot));
            else PhotonNetwork.Instantiate(prefabName, spawnPos, spawnRot);

            if (playerInfoText != null)
                playerInfoText.text = "Player: " + PhotonNetwork.NickName;
        }
    }

    private float remoteFindTimer = 0f;

    void Update()
    {
        CheckSameRoomReturn();
        if (PhotonNetwork.InRoom)
        {
            battleRoomName = PhotonNetwork.CurrentRoom.Name;
            int activePilots = 0;
            foreach (var pilot in PhotonNetwork.PlayerList) if (!pilot.IsInactive) activePilots++;
            if (activePilots < 2 && !intentionalLeave && !resultShown)
            {
                StopInterruptedBattle();
                ReturnToWaitingRoom();
                return;
            }
        }
        FitBattleHUD();
        if (pingText != null && PhotonNetwork.IsConnected)
        {
            pingText.text = "Ping: " + PhotonNetwork.GetPing() + " ms";
        }

        // ค้นหาศัตรูถ้ายังไม่เจอ (เช็คทุก 1 วินาที แทน ทุกเฟรม เพื่อประหยัดเปอร์ฟอร์แมนซ์)
        if (remotePlayer == null && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > 1)
        {
            remoteFindTimer -= Time.deltaTime;
            if (remoteFindTimer <= 0f)
            {
                remoteFindTimer = 1f;
                PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
                foreach(var p in allPlayers)
                {
                    if (!p.photonView.IsMine) { remotePlayer = p; break; }
                }
            }
        }

        UpdateBattleStart();
        UpdateRespawnHUD();
        UpdateDamageDirection();
        if (hitConfirmationText != null && Time.unscaledTime >= hitConfirmationUntil)
            hitConfirmationText.gameObject.SetActive(false);
        UpdateMatchTimerAndScore();
        UpdateHealthBars();
        UpdateSkillUI();
    }



    public int targetKills = 3; // PHASE 5: ใครถึง 3 Kills ก่อน ชนะ

    private void UpdateMatchTimerAndScore()
    {
        if (isMatchEnding || PhotonNetwork.CurrentRoom == null) return;
        if (!matchStarted)
        {
            if (matchTimerText != null) matchTimerText.text = string.Format("{0:00}:{1:00}",
                Mathf.FloorToInt(matchDuration / 60f), Mathf.FloorToInt(matchDuration % 60f));
            return;
        }

        // Update Score Text first so we have the latest kills
        int myKills = 0;
        int enemyKills = 0;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Kills", out object kills)) myKills = (int)kills;
        
        foreach (var p in PhotonNetwork.PlayerListOthers)
        {
            if (p.CustomProperties.TryGetValue("Kills", out object eKills)) enemyKills = (int)eKills;
        }

        if (scoreText != null)
        {
            scoreText.text = $"YOU  {myKills}  :  {enemyKills}  RIVAL";
        }

        // เช็คเงื่อนไขจบเกม: First to 3 Kills
        if (myKills >= targetKills || enemyKills >= targetKills)
        {
            isMatchEnding = true;
            EndMatch();
            return;
        }

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

    private void EndMatch()
    {
        if (localPlayer != null)
        {
            localPlayer.photonView.RPC("SetMatchEndedRPC", RpcTarget.All);
        }

        int myKills = 0;
        int enemyKills = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Kills", out object kills)) myKills = (int)kills;
        foreach (var p in PhotonNetwork.PlayerListOthers)
        {
            if (p.CustomProperties.TryGetValue("Kills", out object eKills)) enemyKills = (int)eKills;
        }

        bool isDraw = myKills == enemyKills;
        bool isWinner = myKills > enemyKills;

        string myShip = localPlayer != null ? localPlayer.gameObject.name.Replace("(Clone)", "") : "MyShip";
        string enemyName = "Enemy";
        string enemyShip = "Unknown";
        if (remotePlayer != null)
        {
            enemyName = remotePlayer.photonView.Owner.NickName;
            enemyShip = remotePlayer.gameObject.name.Replace("(Clone)", "");
        }
        else if (PhotonNetwork.PlayerListOthers.Length > 0)
        {
            enemyName = PhotonNetwork.PlayerListOthers[0].NickName;
        }

        if (isDraw)
            ShowDrawResultScreen(enemyName);
        else
            ShowResultScreen(isWinner, myShip, enemyShip, enemyName);
    }



    public void SetLocalPlayer(PlayerController player)
    {
        localPlayer = player;
        if (playerInfoText != null)
            playerInfoText.text = "YOU / " + PhotonNetwork.NickName;

        // โหลดรูปไอคอนสกิล
        if (skillIconImage != null)
        {
            string iconName = "icon_stun";
            if (player.skillType == 1) iconName = "icon_shield";
            else if (player.skillType == 2) iconName = "icon_nova";
            else if (player.skillType == 3) iconName = "icon_seeker";

            Sprite sp = Resources.Load<Sprite>("Images/" + iconName);
            skillIconImage.sprite = sp;
            skillIconImage.enabled = sp != null;
        }
    }

    private void UpdateHealthBars()
    {
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

    public void LeaveRoom()
    {
        if (intentionalLeave) return;
        intentionalLeave = true;
        StopInterruptedBattle();
        if (!PhotonNetwork.InRoom) SceneManager.LoadScene("LobbyScene");
        else if (!PhotonNetwork.LeaveRoom(false)) intentionalLeave = false;
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("LobbyScene");
    }


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

