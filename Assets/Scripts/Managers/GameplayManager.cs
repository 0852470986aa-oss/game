using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class GameplayManager : MonoBehaviourPunCallbacks
{
    public const float ArenaHalfHeight = 36.5f;
    private static readonly float[] ArenaHalfWidths = { 36.3f, 64f, 39f };

    public static int GetCurrentMapIndex()
    {
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("MapIndex", out object value))
            return Mathf.Clamp((int)value, 0, ArenaHalfWidths.Length - 1);
        return 2;
    }

    public static Vector2 GetArenaMin(int mapIndex)
    {
        mapIndex = Mathf.Clamp(mapIndex, 0, ArenaHalfWidths.Length - 1);
        return new Vector2(-ArenaHalfWidths[mapIndex], -ArenaHalfHeight);
    }

    public static Vector2 GetArenaMax(int mapIndex)
    {
        mapIndex = Mathf.Clamp(mapIndex, 0, ArenaHalfWidths.Length - 1);
        return new Vector2(ArenaHalfWidths[mapIndex], ArenaHalfHeight);
    }

    private static void FitBackgroundToArena(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return;
        const float targetWorldHeight = 75f;
        float scale = targetWorldHeight / Mathf.Max(renderer.sprite.bounds.size.y, 0.01f);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
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

        // PHASE 5: เล่นเพลงตอนสู้
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BGM_Battle");
        }

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

            // Set StartTime
            if (PhotonNetwork.IsMasterClient)
            {
                ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
                roomProps.Add("StartTime", PhotonNetwork.Time);
                PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
            }

            // Spawn Player (Vertical Layout - ห่างกันมากขึ้นเพื่อไม่ให้โดนยิงทันที)
            Vector3 spawnPos = PhotonNetwork.IsMasterClient ? new Vector3(0f, -16f, 0f) : new Vector3(0f, 16f, 0f);
            Quaternion spawnRot = PhotonNetwork.IsMasterClient ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);
            PhotonNetwork.Instantiate(prefabName, spawnPos, spawnRot);

            if (playerInfoText != null)
                playerInfoText.text = "Player: " + PhotonNetwork.NickName;
        }
    }

    private float remoteFindTimer = 0f;

    void Update()
    {
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

        UpdateMatchTimerAndScore();
        UpdateHealthBars();
        UpdateSkillUI();
    }

    // แคช Sprite สำหรับกำแพง (สร้างครั้งเดียว ใช้ซ้ำ)
    private static Sprite wallCoreSprite;
    private static Sprite wallOutlineSprite;

    private void GenerateMapObstacles()
    {
        // 1. สร้าง Sprite สำหรับกำแพง (สีเทาเข้มมีมิติ แทนสีชมพู placeholder)
        if (wallCoreSprite == null)
        {
            Texture2D coreTex = new Texture2D(4, 4);
            Color coreColor = new Color(0.15f, 0.18f, 0.25f, 1f); // เทาน้ำเงินเข้ม (เหล็กอวกาศ)
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    coreTex.SetPixel(x, y, coreColor);
            coreTex.filterMode = FilterMode.Point;
            coreTex.Apply();
            wallCoreSprite = Sprite.Create(coreTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
        if (wallOutlineSprite == null)
        {
            Texture2D outTex = new Texture2D(4, 4);
            Color outColor = new Color(0.3f, 0.5f, 0.7f, 1f); // ฟ้าอ่อน (ขอบเรืองแสง)
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    outTex.SetPixel(x, y, outColor);
            outTex.filterMode = FilterMode.Point;
            outTex.Apply();
            wallOutlineSprite = Sprite.Create(outTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        GameObject obstaclesContainer = new GameObject("Obstacles");

        // 2. อ่านค่าว่าห้องนี้เลือกด่านอะไรมา
        int mapIndex = 2; // เปลี่ยน default เป็น 2 (ด่านหุ่นยนต์) เพื่อให้เทสต์ในหน้า SampleScene ได้เลย
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("MapIndex", out object mapIdxProp))
        {
            mapIndex = (int)mapIdxProp;
        }

        // จัดการเปิด/ปิด Map2_Visuals ในฉาก (ถ้ามี)
        GameObject map2Vis = GameObject.Find("Map2_Visuals");
        if (map2Vis != null)
        {
            map2Vis.SetActive(mapIndex == 2);
        }

        // 3. กำหนดโครงสร้างกำแพงตามแม็พ
        //    type: "wall" = กำแพงขอบ, "large" = Cover ใหญ่, "medium" = Cover กลาง, "small" = เสาเล็ก
        var obstacles = new System.Collections.Generic.List<(Vector2 pos, Vector2 scale, string type)>();

        // === กำแพงขอบแม็พ 4 ด้าน (ทุกแม็พใช้ร่วมกัน) ===
        float wallHalfWidth = ArenaHalfWidths[Mathf.Clamp(mapIndex, 0, ArenaHalfWidths.Length - 1)] + 1f;
        float wallHalfHeight = ArenaHalfHeight + 1f;
        obstacles.Add((new Vector2(0, wallHalfHeight), new Vector2(wallHalfWidth * 2f, 2), "wall"));
        obstacles.Add((new Vector2(0, -wallHalfHeight), new Vector2(wallHalfWidth * 2f, 2), "wall"));
        obstacles.Add((new Vector2(-wallHalfWidth, 0), new Vector2(2, wallHalfHeight * 2f), "wall"));
        obstacles.Add((new Vector2(wallHalfWidth, 0), new Vector2(2, wallHalfHeight * 2f), "wall"));

        if (mapIndex == 0)
        {
            // ==========================================
            // แม็พ 0: Electric Jellyfish Core
            // แนวคิด: ป่าหลบซ่อน มี Cover 4 จุดรอบกลาง + ทางอ้อมซ้าย/ขวา
            // ==========================================
            // COVER รอบ Open Combat (4 จุดรอบตรงกลาง — หลบ Missile/Block Line of Sight)
            obstacles.Add((new Vector2(-11, 9), new Vector2(5, 5), "large"));
            obstacles.Add((new Vector2(11, 9), new Vector2(5, 5), "large"));
            obstacles.Add((new Vector2(-11, -9), new Vector2(5, 5), "large"));
            obstacles.Add((new Vector2(11, -9), new Vector2(5, 5), "large"));
            // SIDE ROUTE กำแพงยาวซ้าย/ขวา (บังคับให้อ้อม ไม่ให้ยิงตรงข้ามแม็พ)
            obstacles.Add((new Vector2(-25, 0), new Vector2(5, 14), "large"));
            obstacles.Add((new Vector2(25, 0), new Vector2(5, 14), "large"));
            // มุมขอบ (ตกแต่ง + กั้นไม่ให้แคมป์มุม)
            obstacles.Add((new Vector2(-32, 27), new Vector2(8, 5), "medium"));
            obstacles.Add((new Vector2(32, 27), new Vector2(8, 5), "medium"));
            obstacles.Add((new Vector2(-32, -27), new Vector2(8, 5), "medium"));
            obstacles.Add((new Vector2(32, -27), new Vector2(8, 5), "medium"));
            // เสาเล็ก Transition Zone (เปลี่ยนจังหวะ)
            obstacles.Add((new Vector2(-19, 20), new Vector2(3, 3), "small"));
            obstacles.Add((new Vector2(19, -20), new Vector2(3, 3), "small"));
            obstacles.Add((new Vector2(8, 27), new Vector2(3, 3), "small"));
            obstacles.Add((new Vector2(-8, -27), new Vector2(3, 3), "small"));
        }
        else if (mapIndex == 1)
        {
            // ==========================================
            // แม็พ 1: Obelisk Plains of Prism
            // แนวคิด: กำแพงแนวนอนกลางแม็พแบ่ง 2 ฝั่ง มีช่องตรงกลางให้ลอดผ่าน (Chokepoint)
            // ==========================================
            // กำแพงแนวนอนซ้าย/ขวา (แบ่งแม็พ ช่องว่างตรงกลาง 6 units ให้ทุกยานลอดได้)
            obstacles.Add((new Vector2(-20, 0), new Vector2(16, 3.5f), "large"));
            obstacles.Add((new Vector2(20, 0), new Vector2(16, 3.5f), "large"));
            // เสาฝั่งบน/ล่าง (Flank Route)
            obstacles.Add((new Vector2(-13, 16), new Vector2(5, 10), "large"));
            obstacles.Add((new Vector2(13, -16), new Vector2(5, 10), "large"));
            // Cover ใกล้ Spawn (ที่หลบหลัง Respawn)
            obstacles.Add((new Vector2(0, 28), new Vector2(7, 4), "medium"));
            obstacles.Add((new Vector2(0, -28), new Vector2(7, 4), "medium"));
            // เสาเล็กกระจาย (เปลี่ยนทิศทาง)
            obstacles.Add((new Vector2(-30, 17), new Vector2(4, 4), "small"));
            obstacles.Add((new Vector2(30, -17), new Vector2(4, 4), "small"));
            obstacles.Add((new Vector2(14, 19), new Vector2(4, 4), "small"));
            obstacles.Add((new Vector2(-14, -19), new Vector2(4, 4), "small"));
            // Wide outer wings unique to the panoramic Map 1 background.
            obstacles.Add((new Vector2(-49, 12), new Vector2(9, 5), "medium"));
            obstacles.Add((new Vector2(49, -12), new Vector2(9, 5), "medium"));
            obstacles.Add((new Vector2(-52, -22), new Vector2(6, 8), "large"));
            obstacles.Add((new Vector2(52, 22), new Vector2(6, 8), "large"));
            obstacles.Add((new Vector2(-43, 28), new Vector2(5, 4), "small"));
            obstacles.Add((new Vector2(43, -28), new Vector2(5, 4), "small"));
        }
        else if (mapIndex == 2)
        {
            // ==========================================
            // แม็พ 2: Abandoned Mech Warzone
            // ซากหุ่นยักษ์อยู่กลางสนาม, อุกกาบาตลาวาเป็น cover และป้อมพัง
            // วางแบบตายตัวเพื่อให้ตำแหน่งชนตรงกันทุกเครื่องใน multiplayer
            // ==========================================
            // Central wreck with four open flank routes around it.
            obstacles.Add((new Vector2(0, 0), new Vector2(6.5f, 3.5f), "mech"));

            // Inner asteroid ring. Opposite pairs keep both spawn sides fair.
            obstacles.Add((new Vector2(-14f, 10f), new Vector2(7.5f, 5.8f), "asteroid"));
            obstacles.Add((new Vector2(14f, -10f), new Vector2(7.5f, 5.8f), "asteroid"));
            obstacles.Add((new Vector2(15f, 11f), new Vector2(6.5f, 5f), "asteroid"));
            obstacles.Add((new Vector2(-15f, -11f), new Vector2(6.5f, 5f), "asteroid"));

            // Outer ring provides cover without sealing the map edges.
            obstacles.Add((new Vector2(-34f, 30f), new Vector2(13f, 9f), "asteroid"));
            obstacles.Add((new Vector2(34f, 30f), new Vector2(13f, 9f), "asteroid"));
            obstacles.Add((new Vector2(-34f, -30f), new Vector2(13f, 9f), "asteroid"));
            obstacles.Add((new Vector2(34f, -30f), new Vector2(13f, 9f), "asteroid"));
            obstacles.Add((new Vector2(-11f, 32f), new Vector2(9f, 6f), "asteroid"));
            obstacles.Add((new Vector2(11f, -32f), new Vector2(9f, 6f), "asteroid"));
            obstacles.Add((new Vector2(-36f, 6f), new Vector2(8f, 6f), "asteroid"));
            obstacles.Add((new Vector2(36f, -6f), new Vector2(8f, 6f), "asteroid"));

            obstacles.Add((new Vector2(-31f, 29f), new Vector2(6f, 4.8f), "turret"));
            obstacles.Add((new Vector2(31f, 29f), new Vector2(6f, 4.8f), "turret"));
            // Energy cores เป็น cover ขนาดเล็กสำหรับหยุดจังหวะยิง ไม่ใช่จุดเกิดของผู้เล่น
            obstacles.Add((new Vector2(-9f, -3f), new Vector2(3.8f, 3.8f), "core"));
            obstacles.Add((new Vector2(9f, 3f), new Vector2(3.8f, 3.8f), "core"));
            obstacles.Add((new Vector2(0, 20f), new Vector2(3.5f, 3.5f), "core"));
            obstacles.Add((new Vector2(0, -20f), new Vector2(3.5f, 3.5f), "core"));
            obstacles.Add((new Vector2(-24f, 0f), new Vector2(3.2f, 3.2f), "core"));
            obstacles.Add((new Vector2(24f, 0f), new Vector2(3.2f, 3.2f), "core"));
        }
        else
        {
            // fallback
            obstacles.Add((new Vector2(0, 0), new Vector2(4, 4), "large"));
            obstacles.Add((new Vector2(-10, 0), new Vector2(3, 10), "large"));
            obstacles.Add((new Vector2(10, 0), new Vector2(3, 10), "large"));
        }

        // 4. สร้าง Obstacle ตามลิสต์ — พร้อม Visual ที่ดูเป็น "กำแพงเหล็กอวกาศ"
        int id = 1;
        
        // Load Sprites
        Sprite[] asteroidSprites = null;
        Sprite[] turretSprites = null;
        Sprite[] coreSprites = null;
        
        Sprite[] crystalSprites = null;
        Sprite[] pillarSprites = null;

        if (mapIndex == 2)
        {
            asteroidSprites = Resources.LoadAll<Sprite>("Images/Obs_Asteroids");
            turretSprites = Resources.LoadAll<Sprite>("Images/Obs_Turrets");
            coreSprites = Resources.LoadAll<Sprite>("Images/Obs_RedCores");
        }
        else if (mapIndex == 1)
        {
            crystalSprites = Resources.LoadAll<Sprite>("Images/Obs_Crystals");
            pillarSprites = Resources.LoadAll<Sprite>("Images/Obs_Pillars");
        }

        System.Collections.Generic.List<Sprite> validAsteroids = new System.Collections.Generic.List<Sprite>();
        if (asteroidSprites != null)
        {
            foreach (var sp in asteroidSprites)
            {
                if (!sp.name.EndsWith("_9") && !sp.name.EndsWith("_10") && !sp.name.EndsWith("_11"))
                {
                    validAsteroids.Add(sp);
                }
            }
            if (validAsteroids.Count == 0) validAsteroids.AddRange(asteroidSprites);
        }

        GameObject map2Layout = GameObject.Find("Map2_Layout");
        bool hasAuthoredMap2Rocks = map2Layout != null && map2Layout.transform.Find("RockObstacles") != null;
        bool hasAuthoredMap2Turrets = map2Layout != null && map2Layout.transform.Find("TurretObstacles") != null;
        bool hasAuthoredMap2RedCores = map2Layout != null && map2Layout.transform.Find("RedCoreObstacles") != null;

        foreach (var obs in obstacles)
        {
            // สร้าง Core (ตัวกำแพงหลัก)
            GameObject box = new GameObject("Wall_" + obs.type + "_" + id);
            box.transform.position = new Vector3(obs.pos.x, obs.pos.y, 0);
            box.transform.SetParent(obstaclesContainer.transform);
            
            // Add Collider
            BoxCollider2D col = box.AddComponent<BoxCollider2D>();
            col.size = obs.scale;

            // Visual Rendering
            if (mapIndex == 2)
            {
                // พื้นหลังมีหุ่นยักษ์เป็นฉากฐาน ส่วนอุกกาบาต/ป้อม/แกนพลังงานคือชั้นเกมเพลย์
                // ที่วางทับขึ้นมาและชนได้เหมือน cover ใน Mini Militia
                // Asteroid art is authored under Map2_Layout so it is visible while editing.
                // Runtime objects still provide the authoritative colliders for multiplayer.
                bool usesAuthoredVisual =
                    (obs.type == "asteroid" && hasAuthoredMap2Rocks) ||
                    (obs.type == "turret" && hasAuthoredMap2Turrets) ||
                    (obs.type == "core" && hasAuthoredMap2RedCores);
                if (obs.type != "wall" && obs.type != "mech" && !usesAuthoredVisual)
                    CreateMechWarzoneLayerVisual(box.transform, obs.type, obs.scale, id, asteroidSprites, turretSprites, coreSprites);
            }
            else if (mapIndex == 1 && obs.type != "wall" && crystalSprites != null && crystalSprites.Length > 0)
            {
                // [NEW] Cluster Spawning for Map 1 (Prism Crystals & Pillars)
                float area = obs.scale.x * obs.scale.y;
                int count = Mathf.Max(1, Mathf.RoundToInt(area / 2.5f)); 

                for (int i = 0; i < count; i++)
                {
                    GameObject crystal = new GameObject("Crystal_Vis");
                    crystal.transform.SetParent(box.transform);
                    
                    float rx = Random.Range(-obs.scale.x / 2.2f, obs.scale.x / 2.2f);
                    float ry = Random.Range(-obs.scale.y / 2.2f, obs.scale.y / 2.2f);
                    crystal.transform.localPosition = new Vector3(rx, ry, 0);
                    
                    SpriteRenderer sr = crystal.AddComponent<SpriteRenderer>();
                    sr.sprite = crystalSprites[Random.Range(0, crystalSprites.Length)];
                    sr.sortingOrder = -2;
                    
                    float rScale = Random.Range(0.7f, 1.3f);
                    crystal.transform.localScale = new Vector3(rScale, rScale, 1f); 
                    crystal.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
                }

                if (obs.type == "large" && pillarSprites != null && pillarSprites.Length > 0)
                {
                    GameObject pillar = new GameObject("Pillar_Vis");
                    pillar.transform.SetParent(box.transform);
                    pillar.transform.localPosition = new Vector3(0, 0, 0); 
                    SpriteRenderer psr = pillar.AddComponent<SpriteRenderer>();
                    psr.sprite = pillarSprites[Random.Range(0, pillarSprites.Length)];
                    psr.sortingOrder = -1;
                    float pScale = Random.Range(0.9f, 1.4f);
                    pillar.transform.localScale = new Vector3(pScale, pScale, 1f);
                    pillar.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-20f, 20f));
                }
            }
            else
            {
                // [OLD] วาดกล่องสี่เหลี่ยมเรืองแสง สำหรับขอบสนามและแม็พอื่นๆ
                box.transform.localScale = new Vector3(obs.scale.x, obs.scale.y, 1f);
                col.size = Vector2.one; // คืนค่าเพราะ scale ถูกเปลี่ยน

                Color baseCore = Color.white, baseOutline = Color.white;
                if (mapIndex == 0) { baseCore = new Color(0.1f, 0.12f, 0.22f, 1f); baseOutline = new Color(0.1f, 0.7f, 1f, 1f); }
                else if (mapIndex == 1) { baseCore = new Color(0.15f, 0.08f, 0.2f, 1f); baseOutline = new Color(1f, 0.3f, 0.8f, 1f); }
                else { baseCore = new Color(0.2f, 0.18f, 0.15f, 1f); baseOutline = new Color(1f, 0.5f, 0.1f, 1f); }

                Color coreColor = baseCore; Color outlineColor = baseOutline; float outlinePad = 0.2f;
                if (obs.type == "wall") { coreColor = Color.Lerp(baseCore, Color.white, 0.4f); coreColor.a = 0.8f; outlineColor.a = 0.9f; outlinePad = 0.3f; }
                else if (obs.type == "large") { coreColor = Color.Lerp(baseCore, Color.white, 0.5f); coreColor.a = 0.8f; outlineColor.a = 0.9f; outlinePad = 0.25f; }
                else if (obs.type == "medium") { coreColor = Color.Lerp(baseCore, Color.white, 0.6f); coreColor.a = 0.8f; outlineColor.a = 0.9f; outlinePad = 0.2f; }
                else { coreColor = Color.Lerp(baseCore, Color.white, 0.7f); coreColor.a = 0.8f; outlineColor.a = 0.9f; outlinePad = 0.15f; }

                SpriteRenderer sr = box.AddComponent<SpriteRenderer>();
                sr.sprite = wallCoreSprite;
                sr.color = coreColor;
                sr.sortingOrder = -2;

                if (obs.type != "wall")
                {
                    GameObject outline = new GameObject("Outline");
                    outline.transform.SetParent(box.transform, false);
                    outline.transform.localPosition = Vector3.zero;
                    float oxScale = 1f + (outlinePad * 2f / obs.scale.x);
                    float oyScale = 1f + (outlinePad * 2f / obs.scale.y);
                    outline.transform.localScale = new Vector3(oxScale, oyScale, 1f);

                    SpriteRenderer outSr = outline.AddComponent<SpriteRenderer>();
                    outSr.sprite = wallOutlineSprite;
                    outSr.color = outlineColor;
                    outSr.sortingOrder = -3;
                }
            }

            id++;
        }

        // --- เพิ่มระบบตกแต่งฉากหลัง (Background Decorator) ---
        if (mapIndex == 1 && crystalSprites != null && crystalSprites.Length > 0)
        {
            GameObject bgDeco = new GameObject("BackgroundDecorations");
            bgDeco.transform.SetParent(obstaclesContainer.transform);

            // โปรยคริสตัลประดับฉาก 35 ก้อน
            for (int i = 0; i < 35; i++)
            {
                GameObject crystal = new GameObject("Crystal_BG");
                crystal.transform.SetParent(bgDeco.transform);
                crystal.transform.position = new Vector3(Random.Range(-36f, 36f), Random.Range(-33f, 33f), 0);
                
                SpriteRenderer sr = crystal.AddComponent<SpriteRenderer>();
                sr.sprite = crystalSprites[Random.Range(0, crystalSprites.Length)];
                sr.sortingOrder = -4; 
                sr.color = new Color(0.6f, 0.8f, 1f, 0.8f); // ออกฟ้าๆ โปร่งใสนิดๆ
                
                float rScale = Random.Range(0.4f, 0.9f); 
                crystal.transform.localScale = new Vector3(rScale, rScale, 1f);
                crystal.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
            }

            // แทรกเสาหินประดับฉาก (Pillars) 5 ต้น
            if (pillarSprites != null && pillarSprites.Length > 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    GameObject pillar = new GameObject("Pillar_BG");
                    pillar.transform.SetParent(bgDeco.transform);
                    pillar.transform.position = new Vector3(Random.Range(-34f, 34f), Random.Range(-31f, 31f), 0);
                    
                    SpriteRenderer sr = pillar.AddComponent<SpriteRenderer>();
                    sr.sprite = pillarSprites[Random.Range(0, pillarSprites.Length)];
                    sr.sortingOrder = -5; // อยู่ลึกๆ
                    sr.color = new Color(0.5f, 0.5f, 0.8f, 1f);
                    
                    float rScale = Random.Range(0.8f, 1.5f);
                    pillar.transform.localScale = new Vector3(rScale, rScale, 1f);
                    pillar.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-10f, 10f));
                }
            }
        }

        // ปรับพื้นหลังให้อยู่ลึกสุด
        if (backgroundSprite != null)
        {
            backgroundSprite.sortingOrder = -6; // ถอยพื้นหลังไปอีกเพื่อหลบ Background Decorator
        }
    }

    private void CreateMechWarzoneLayerVisual(Transform parent, string type, Vector2 collisionSize, int id,
        Sprite[] asteroidSprites, Sprite[] turretSprites, Sprite[] coreSprites)
    {
        Sprite[] spriteSet = asteroidSprites;
        if (type == "turret") spriteSet = turretSprites;
        else if (type == "core") spriteSet = coreSprites;
        if (spriteSet == null || spriteSet.Length == 0) return;

        GameObject visual = new GameObject(type == "core" ? "EnergyCore_Cover" : type + "_Cover");
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, type == "asteroid" ? id * 31f : (id % 2 == 0 ? 20f : -20f));

        float sizeMultiplier = type == "core" ? 0.85f : 0.65f;
        visual.transform.localScale = new Vector3(collisionSize.x * sizeMultiplier, collisionSize.y * sizeMultiplier, 1f);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = spriteSet[id % spriteSet.Length];
        renderer.color = Color.white;
        renderer.sortingOrder = -1;
    }

    private void CreateMatchUI()
    {
        Canvas canvas = null;
        if (playerInfoText != null) canvas = playerInfoText.canvas;
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        if (canvas != null)
        {
            GameObject timerObj = new GameObject("MatchTimerText");
            timerObj.transform.SetParent(canvas.transform, false);
            matchTimerText = timerObj.AddComponent<TextMeshProUGUI>();
            RectTransform timerRect = timerObj.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0, -120); // ขยับลงมาไม่ให้บังปุ่มออก
            timerRect.sizeDelta = new Vector2(200, 40);
            matchTimerText.alignment = TextAlignmentOptions.Center;
            matchTimerText.fontSize = 24;
            matchTimerText.color = Color.white;
            matchTimerText.outlineWidth = 0.2f;
            matchTimerText.outlineColor = Color.black;

            GameObject scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(canvas.transform, false);
            scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
            RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.pivot = new Vector2(0.5f, 1f);
            scoreRect.anchoredPosition = new Vector2(0, -160); // ขยับลงมาตามเวลา
            scoreRect.sizeDelta = new Vector2(300, 40);
            scoreText.alignment = TextAlignmentOptions.Center;
            scoreText.fontSize = 20;
            scoreText.color = Color.yellow;
            scoreText.outlineWidth = 0.2f;
            scoreText.outlineColor = Color.black;
        }
    }

    public int targetKills = 3; // PHASE 5: ใครถึง 3 Kills ก่อน ชนะ

    private void UpdateMatchTimerAndScore()
    {
        if (isMatchEnding || PhotonNetwork.CurrentRoom == null) return;

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
            scoreText.text = $"Me: {myKills} - Enemy: {enemyKills}";
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

    private void ShowDrawResultScreen(string remotePlayerName)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(resultPanel.transform));
        }
        if (resultRoomNumber != null && PhotonNetwork.CurrentRoom != null) resultRoomNumber.text = PhotonNetwork.CurrentRoom.Name;
        if (localResultName != null) localResultName.text = PhotonNetwork.NickName;
        if (remoteResultName != null) remoteResultName.text = remotePlayerName;
        if (localResultStatus != null) { localResultStatus.text = "เสมอ"; localResultStatus.color = Color.white; }
        if (remoteResultStatus != null) { remoteResultStatus.text = "เสมอ"; remoteResultStatus.color = Color.white; }
        if (localResultCoins != null) localResultCoins.text = "0";
        if (remoteResultCoins != null) remoteResultCoins.text = "0";
        if (btnReturnToMenu != null)
        {
            btnReturnToMenu.onClick.RemoveAllListeners();
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
    }

    public void ShowKillMessage()
    {
        // Simple floating text in the center
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            GameObject txtObj = new GameObject("KillMessage");
            txtObj.transform.SetParent(canvas.transform, false);
            TextMeshProUGUI msgText = txtObj.AddComponent<TextMeshProUGUI>();
            RectTransform rect = txtObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 100);
            rect.sizeDelta = new Vector2(300, 100);
            msgText.alignment = TextAlignmentOptions.Center;
            msgText.fontSize = 50;
            msgText.color = Color.red;
            msgText.text = "KILL +1";
            msgText.outlineWidth = 0.2f;
            msgText.outlineColor = Color.black;

            Destroy(txtObj, 1.5f); // Automatically destroy after 1.5 seconds
        }
    }

    private void UpdateSkillUI()
    {
        if (localPlayer != null && skillCooldownImage != null)
        {
            if (localPlayer.currentCooldown > 0)
            {
                skillCooldownImage.fillAmount = localPlayer.currentCooldown / localPlayer.maxCooldown;
            }
            else
            {
                skillCooldownImage.fillAmount = 0f;
            }
        }
    }

    public void SetLocalPlayer(PlayerController player)
    {
        localPlayer = player;
        if (playerInfoText != null)
            playerInfoText.text = "Player: " + PhotonNetwork.NickName + " | Ship: " + player.gameObject.name.Replace("(Clone)","");

        // โหลดรูปไอคอนสกิล
        if (skillIconImage != null)
        {
            string iconName = "icon_stun";
            if (player.skillType == 1) iconName = "icon_shield";
            else if (player.skillType == 2) iconName = "icon_nova";
            else if (player.skillType == 3) iconName = "icon_seeker";

            Sprite sp = Resources.Load<Sprite>("Images/" + iconName);
            if (sp != null) skillIconImage.sprite = sp;
        }
    }

    private void UpdateHealthBars()
    {
        if (localPlayer != null)
        {
            float hpRatio = localPlayer.currentHp / localPlayer.maxHp;
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
            float hpRatio2 = remotePlayer.currentHp / remotePlayer.maxHp;
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
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("LobbyScene");
    }

    public void ShowResultScreen(bool isWinner, string localShipName, string remoteShipName, string remotePlayerName)
    {
        if (resultPanel != null) 
        {
            resultPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(resultPanel.transform));
        }

        if (resultRoomNumber != null && PhotonNetwork.CurrentRoom != null)
        {
            resultRoomNumber.text = PhotonNetwork.CurrentRoom.Name;
        }

        // Local Player Settings
        if (localResultName != null) localResultName.text = PhotonNetwork.NickName;
        if (localResultShip != null) 
        {
            // Try load ship sprite, using name or mapping (for now we assume 1,2,3 logic or just hide it if null)
        }

        if (isWinner)
        {
            if (localResultOutline != null) localResultOutline.effectColor = new Color(0.2f, 0.8f, 0.4f); // Green
            if (localResultStatus != null) { localResultStatus.text = "ชัยชนะ +"; localResultStatus.color = new Color(1f, 0.8f, 0.2f); }
            if (localResultCoins != null) localResultCoins.text = "190";
            
            if (remoteResultOutline != null) remoteResultOutline.effectColor = new Color(0.8f, 0.2f, 0.2f); // Red
            if (remoteResultStatus != null) { remoteResultStatus.text = "พ่ายแพ้ +"; remoteResultStatus.color = new Color(1f, 0.4f, 0.4f); }
            if (remoteResultCoins != null) remoteResultCoins.text = "10";

            // Update Firebase
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.RecordMatchResult(true, remotePlayerName, 190);
            }
        }
        else
        {
            if (localResultOutline != null) localResultOutline.effectColor = new Color(0.8f, 0.2f, 0.2f); // Red
            if (localResultStatus != null) { localResultStatus.text = "พ่ายแพ้ +"; localResultStatus.color = new Color(1f, 0.4f, 0.4f); }
            if (localResultCoins != null) localResultCoins.text = "10";
            
            if (remoteResultOutline != null) remoteResultOutline.effectColor = new Color(0.2f, 0.8f, 0.4f); // Green
            if (remoteResultStatus != null) { remoteResultStatus.text = "ชัยชนะ +"; remoteResultStatus.color = new Color(1f, 0.8f, 0.2f); }
            if (remoteResultCoins != null) remoteResultCoins.text = "190";

            // Update Firebase
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.RecordMatchResult(false, remotePlayerName, 10);
            }
        }

        if (remoteResultName != null) remoteResultName.text = remotePlayerName;

        if (btnReturnToMenu != null)
        {
            btnReturnToMenu.onClick.RemoveAllListeners();
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
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

