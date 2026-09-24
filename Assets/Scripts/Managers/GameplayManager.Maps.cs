using UnityEngine;
using Photon.Pun;

// ส่วนแม็พของ GameplayManager: เป็นคลาสเดียวกันผ่าน partial ไม่ต้องเพิ่ม Component ใน Scene
// ลำดับแม็พ: 0 แมงกะพรุน, 1 ปริซึม, 2 หุ่นยนต์
// แก้ตำแหน่งสิ่งกีดขวางที่ DecoratePrism / PrepareMechCover
// ขอบเขตสนามและจุดเกิดปลอดภัยอยู่ในไฟล์นี้ด้วย
public partial class GameplayManager
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

    private void ApplySelectedMapLayout(int selectedMapIndex)
    {
        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < 3; i++)
        {
            string layoutName = "Map" + i + "_Layout";
            foreach (GameObject root in roots)
            {
                if (root.name != layoutName) continue;

                Transform background = root.transform.Find("Background");
                SpriteRenderer renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
                FitBackgroundToArena(renderer);
                root.SetActive(i == selectedMapIndex);
                if (i == 0 && i == selectedMapIndex && root.GetComponent<JellyArenaVisuals>() == null)
                    root.AddComponent<JellyArenaVisuals>();
                if (i == 1 && i == selectedMapIndex) DecoratePrism(root.transform);
                // Authored cover needs collision even when procedural generation is disabled.
                if (i == 2 && i == selectedMapIndex) PrepareMechCover(root.transform);

                if (i == selectedMapIndex && renderer != null)
                    backgroundSprite = renderer;
                break;
            }
        }
    }

    // One deterministic solid layout on every client; background artwork stays untouched.
    public static void DecoratePrism(Transform layout)
    {
        if (layout == null || layout.Find("PrismPlayableLayout") != null) return;
        var central = Resources.LoadAll<Sprite>("Images/Obs_PrismPillar_04");
        if (central.Length == 0) { Debug.LogWarning("Prism pillar 04 is missing."); return; }
        foreach (Transform child in layout)
            if (child.name != "Background") child.gameObject.SetActive(false);
        var group = new GameObject("PrismPlayableLayout");
        group.transform.SetParent(layout, false);
        AddPrismCover(group.transform, central[0], Vector2.zero, 18, "CentralPillar_04");
        Vector2[] positions = {
            new Vector2(-22, 14), new Vector2(22, -14),
            new Vector2(22, 14), new Vector2(-22, -14),
            new Vector2(-43, 23), new Vector2(43, -23),
            new Vector2(43, 23), new Vector2(-43, -23),
            new Vector2(-51, 0), new Vector2(51, 0),
            new Vector2(-10, 29), new Vector2(10, -29)
        };
        int[] variants = { 1, 1, 2, 2, 3, 3, 5, 5, 6, 7, 8, 8 };
        for (int i = 0; i < positions.Length; i++)
        {
            var sprites = Resources.LoadAll<Sprite>("Images/Obs_PrismPillar_" + variants[i].ToString("00"));
            if (sprites.Length > 0) AddPrismCover(group.transform, sprites[0], positions[i],
                i < 4 ? 10 : 8, "Pillar_" + i);
        }
        var crystals = Resources.LoadAll<Sprite>("Images/Obs_Crystals");
        System.Array.Sort(crystals, (a,b) => string.CompareOrdinal(a.name,b.name));
        Vector2[] clusters = { new Vector2(-34,5), new Vector2(34,-5), new Vector2(34,5), new Vector2(-34,-5),
            new Vector2(-55,29), new Vector2(55,-29), new Vector2(55,29), new Vector2(-55,-29) };
        for (int i = 0; i < clusters.Length && crystals.Length > 0; i++)
            AddPrismCover(group.transform, crystals[i % crystals.Length], clusters[i], 4.5f, "Crystal_" + i);
        Physics2D.SyncTransforms();
    }

    private static void AddPrismCover(Transform parent, Sprite sprite, Vector2 position, float height, string name)
    {
        var art = PrismSprite(parent, name, sprite, position, height, Color.white, 2);
        var collider = art.gameObject.AddComponent<PolygonCollider2D>();
        collider.isTrigger = false;
        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer >= 0) art.gameObject.layer = layer;
    }

    private static SpriteRenderer PrismSprite(Transform parent, string name, Sprite sprite,
        Vector2 position, float height, Color color, int order)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = position;
        var art = new GameObject("Artwork");
        art.transform.SetParent(holder.transform, false);
        float scale = height / Mathf.Max(sprite.bounds.size.y, 0.01f);
        art.transform.localPosition = -sprite.bounds.center * scale;
        art.transform.localScale = Vector3.one * scale;
        var renderer = art.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static void PrismRing(Transform parent, Vector2 center, float width, float height, Color color, Material material)
    {
        var go = new GameObject("PrismGroundRing");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 48;
        line.widthMultiplier = 0.10f;
        line.startColor = line.endColor = color;
        line.sortingOrder = -4;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * width, Mathf.Sin(angle) * height, 0));
        }
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
        int mapIndex = 1; // ⭐ เปลี่ยนเป็น 1 (Map1 - Obelisk Plains) เพื่อทดสอบ
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
            // ใช้เลย์เอาต์สมมาตร 180 องศา เพื่อให้ผู้เล่นทั้งสองฝั่งมี cover เท่ากัน
            // ==========================================
            
            // === CENTRAL TOWER (หอตรงกลาง) ===
            obstacles.Add((new Vector2(0, 0), new Vector2(5.5f, 8), "tower"));
            
            // === CRYSTAL COVERS ===
            obstacles.Add((new Vector2(-20, 12), new Vector2(5, 4), "crystal"));
            obstacles.Add((new Vector2(20, -12), new Vector2(5, 4), "crystal"));
            obstacles.Add((new Vector2(20, 12), new Vector2(5, 4), "crystal"));
            obstacles.Add((new Vector2(-20, -12), new Vector2(5, 4), "crystal"));
            obstacles.Add((new Vector2(-32, 7), new Vector2(4.5f, 4), "crystal"));
            obstacles.Add((new Vector2(32, -7), new Vector2(4.5f, 4), "crystal"));
            obstacles.Add((new Vector2(32, 7), new Vector2(4.5f, 4), "crystal"));
            obstacles.Add((new Vector2(-32, -7), new Vector2(4.5f, 4), "crystal"));
            
            // === DOME SHIELDS ===
            obstacles.Add((new Vector2(-40, 23), new Vector2(7, 5), "dome"));
            obstacles.Add((new Vector2(40, -23), new Vector2(7, 5), "dome"));
            obstacles.Add((new Vector2(40, 23), new Vector2(7, 5), "dome"));
            obstacles.Add((new Vector2(-40, -23), new Vector2(7, 5), "dome"));
            
            // === ENERGY BARRIERS ===
            obstacles.Add((new Vector2(-16, 0), new Vector2(10, 4.5f), "platform"));
            obstacles.Add((new Vector2(16, 0), new Vector2(10, 4.5f), "platform"));
            
            // Cover หลังจุดเกิด โดยเว้นแกนกลาง y = +/-16 ให้บินออกได้
            obstacles.Add((new Vector2(0, 29), new Vector2(8, 4), "platform"));
            obstacles.Add((new Vector2(0, -29), new Vector2(8, 4), "platform"));
            
            // Cover รอบนอก
            obstacles.Add((new Vector2(-52, 25), new Vector2(8, 4.5f), "platform"));
            obstacles.Add((new Vector2(52, -25), new Vector2(8, 4.5f), "platform"));
            obstacles.Add((new Vector2(52, 25), new Vector2(8, 4.5f), "platform"));
            obstacles.Add((new Vector2(-52, -25), new Vector2(8, 4.5f), "platform"));
            
            // เสาพลังงานคุมทางอ้อมซ้าย/ขวา
            obstacles.Add((new Vector2(-31, 16), new Vector2(4.5f, 5.5f), "platform"));
            obstacles.Add((new Vector2(31, -16), new Vector2(4.5f, 5.5f), "platform"));
            obstacles.Add((new Vector2(31, 16), new Vector2(4.5f, 5.5f), "platform"));
            obstacles.Add((new Vector2(-31, -16), new Vector2(4.5f, 5.5f), "platform"));
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
            obstacles.Add((new Vector2(-14f, 10f), new Vector2(9f, 7f), "asteroid"));
            obstacles.Add((new Vector2(14f, -10f), new Vector2(9f, 7f), "asteroid"));
            obstacles.Add((new Vector2(15f, 11f), new Vector2(8f, 6f), "asteroid"));
            obstacles.Add((new Vector2(-15f, -11f), new Vector2(8f, 6f), "asteroid"));

            // Outer ring provides cover without sealing the map edges.
            obstacles.Add((new Vector2(-34f, 30f), new Vector2(16f, 12f), "asteroid"));
            obstacles.Add((new Vector2(34f, 30f), new Vector2(16f, 12f), "asteroid"));
            obstacles.Add((new Vector2(-34f, -30f), new Vector2(16f, 12f), "asteroid"));
            obstacles.Add((new Vector2(34f, -30f), new Vector2(16f, 12f), "asteroid"));
            obstacles.Add((new Vector2(-11f, 32f), new Vector2(11f, 8f), "asteroid"));
            obstacles.Add((new Vector2(11f, -32f), new Vector2(11f, 8f), "asteroid"));
            obstacles.Add((new Vector2(-36f, 6f), new Vector2(10f, 8f), "asteroid"));
            obstacles.Add((new Vector2(36f, -6f), new Vector2(10f, 8f), "asteroid"));

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
        GameObject map1Layout = GameObject.Find("Map1_Layout");
        bool hasAuthoredMap1Obstacles = map1Layout != null && map1Layout.transform.Find("Map1Obstacles") != null;

        foreach (var obs in obstacles)
        {
            if (mapIndex == 0 && obs.type != "wall") continue;
            if (mapIndex == 1 && map1Layout != null && map1Layout.transform.Find("PrismPlayableLayout") != null && obs.type != "wall") continue;
            // Use the visible sprite's collider, never an additional invisible box with a different size.
            if (mapIndex == 2 && ((obs.type == "asteroid" && hasAuthoredMap2Rocks)
                || (obs.type == "turret" && hasAuthoredMap2Turrets)
                || (obs.type == "core" && hasAuthoredMap2RedCores))) continue;
            // สร้าง Core (ตัวกำแพงหลัก)
            GameObject box = new GameObject("Wall_" + obs.type + "_" + id);
            box.transform.position = new Vector3(obs.pos.x, obs.pos.y, 0);
            box.transform.SetParent(obstaclesContainer.transform);
            
            // Add Collider
            BoxCollider2D col = box.AddComponent<BoxCollider2D>();
            col.size = obs.scale;
            if (mapIndex == 1)
            {
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer >= 0) box.layer = obstacleLayer;
            }

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
                if (obs.type == "asteroid") box.AddComponent<MoltenContactSurface>();
                if (obs.type != "wall" && obs.type != "mech" && !usesAuthoredVisual)
                    CreateMechWarzoneLayerVisual(box.transform, obs.type, obs.scale, id, asteroidSprites, turretSprites, coreSprites);
            }
            else if (mapIndex == 1 && obs.type != "wall")
            {
                // Artwork is authored under Map1_Layout so it is visible and editable in the scene.
                // These runtime objects only provide the authoritative collision layout.
                if (!hasAuthoredMap1Obstacles)
                    CreateMap1ObstacleVisual(box.transform, obs.type, obs.scale, id, crystalSprites, pillarSprites);
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
        if (mapIndex == 1 && !hasAuthoredMap1Obstacles && crystalSprites != null && crystalSprites.Length > 0)
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

    private static Sprite FindNumberedSprite(Sprite[] sprites, string prefix, int number)
    {
        if (sprites == null) return null;
        string targetName = prefix + "_" + number;
        foreach (Sprite sprite in sprites)
        {
            if (sprite != null && sprite.name == targetName) return sprite;
        }
        return null;
    }

    private void CreateMap1ObstacleVisual(Transform parent, string type, Vector2 collisionSize, int id,
        Sprite[] crystalSprites, Sprite[] pillarSprites)
    {
        Sprite sprite = null;
        float rotation = 0f;
        int sortingOrder = type == "platform" ? 0 : 1;

        if (type == "tower")
        {
            sprite = FindNumberedSprite(pillarSprites, "Obs_Pillars", 14);
            sortingOrder = 2;
        }
        else if (type == "crystal")
        {
            int[] spriteNumbers = { 2, 2, 3, 3, 0, 0, 4, 4 };
            float[] rotations = { -8f, -8f, 8f, 8f, -12f, -12f, 12f, 12f };
            int index = Mathf.Clamp(id - 6, 0, spriteNumbers.Length - 1);
            sprite = FindNumberedSprite(crystalSprites, "Obs_Crystals", spriteNumbers[index]);
            rotation = rotations[index];
        }
        else if (type == "dome")
        {
            int index = Mathf.Clamp(id - 14, 0, 3);
            sprite = FindNumberedSprite(pillarSprites, "Obs_Pillars", index % 2 == 0 ? 12 : 13);
            rotation = 0f;
        }
        else if (type == "platform")
        {
            int[] spriteNumbers = { 0, 0, 2, 2, 4, 4, 0, 0, 14, 14, 14, 14 };
            float[] rotations = { 0f, 0f, 0f, 0f, -8f, -8f, 8f, 8f, 0f, 0f, 0f, 0f };
            int index = Mathf.Clamp(id - 18, 0, spriteNumbers.Length - 1);
            sprite = FindNumberedSprite(pillarSprites, "Obs_Pillars", spriteNumbers[index]);
            rotation = rotations[index];
        }

        if (sprite == null) return;

        GameObject artwork = new GameObject("Artwork");
        artwork.transform.SetParent(parent, false);
        Vector2 spriteSize = sprite.bounds.size;
        float scale = Mathf.Max(
            collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
            collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
        artwork.transform.localPosition = -(Vector3)sprite.bounds.center * scale;
        artwork.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        artwork.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer renderer = artwork.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
    }

    public static void PrepareMechCover(Transform layout)
    {
        if (layout == null) return;
        var starfield = GameObject.Find("Starfield");
        var stars = starfield != null ? starfield.GetComponent<ParticleSystem>() : null;
        if (stars != null)
        {
            stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            stars.transform.position = Vector3.zero;
            stars.transform.rotation = Quaternion.identity;
            stars.transform.localScale = Vector3.one;
            var main = stars.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0;
            main.startLifetime = 20;
            main.maxParticles = 500;
            main.prewarm = true;
            main.loop = true;
            var shape = stars.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            Vector2 area = GetArenaMax(2) - GetArenaMin(2);
            shape.scale = new Vector3(area.x, area.y, .1f);
            var emission = stars.emission;
            emission.rateOverTime = 20;
            stars.Play();
        }
        var rocks = layout.Find("RockObstacles");
        // Stagger the inner cover around an open central route and two outer flanking routes.
        Vector2[] innerRocks = {
            new Vector2(-15, 11), new Vector2(15, -11), new Vector2(15, 11), new Vector2(-15, -11),
            new Vector2(-35, 31), new Vector2(35, 31), new Vector2(-35, -31), new Vector2(35, -31),
            new Vector2(-11, 33), new Vector2(11, -33), new Vector2(-36, 4), new Vector2(36, -4)
        };
        if (rocks != null)
            for (int i = 0; i < innerRocks.Length; i++)
            {
                var rock = rocks.Find("RockObstacle_" + (i + 1).ToString("00"));
                if (rock != null)
                {
                    rock.localPosition = innerRocks[i];
                    FitMechObstacle(rock, i < 4 ? new Vector2(7, 6) : i < 8 ? new Vector2(10, 8) : new Vector2(6, 6));
                }
            }
        var cores = layout.Find("RedCoreObstacles");
        if (cores != null)
            foreach (Transform core in cores)
            {
                FitMechObstacle(core, new Vector2(2.8f, 2.8f));
                // Keep side cores within existing cover clusters, not across the outer flight lanes.
                if (Mathf.Abs(core.localPosition.x) > 20f)
                    core.localPosition = new Vector3(Mathf.Sign(core.localPosition.x) * 15, Mathf.Sign(core.localPosition.x) * 11, 0);
                if (Mathf.Abs(core.localPosition.x) < 1f && Mathf.Abs(core.localPosition.y) > 15f)
                    core.localPosition = new Vector3(0, Mathf.Sign(core.localPosition.y) * 25f, 0);
            }
        var turrets = layout.Find("TurretObstacles");
        if (turrets != null)
            foreach (Transform turret in turrets)
            {
                turret.localPosition = new Vector3(Mathf.Sign(turret.localPosition.x) * 30, Mathf.Sign(turret.localPosition.x) * 18, 0);
                FitMechObstacle(turret, new Vector2(4, 3.5f));
                if (turret.GetComponent<AutoTurret>() == null) turret.gameObject.AddComponent<AutoTurret>();
            }
        // Preserve authored art and rotations; attach solid collision to those exact objects.
        foreach (string groupName in new[] { "RockObstacles", "TurretObstacles", "RedCoreObstacles" })
        {
            var group = layout.Find(groupName);
            if (group == null) continue;
            foreach (Transform obstacle in group)
            {
                var renderer = obstacle.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.sprite == null) continue;
                var collider = obstacle.GetComponent<PolygonCollider2D>();
                if (collider == null) collider = obstacle.gameObject.AddComponent<PolygonCollider2D>();
                foreach (var other in obstacle.GetComponents<Collider2D>())
                    if (other != collider) other.enabled = false;
                collider.isTrigger = false;
                collider.enabled = true;
                // No extra damage on turrets or red cores: they are permanent cover only.
                if (groupName == "RockObstacles" && obstacle.GetComponent<MoltenContactSurface>() == null)
                    obstacle.gameObject.AddComponent<MoltenContactSurface>();
            }
        }
        // Tiny decorative rocks used to look solid while ships could pass through them.
        // Hide that visual-only layer so the playable lanes remain clear and unambiguous.
        var debris = layout.Find("RockDecorations");
        if (debris != null) debris.gameObject.SetActive(false);
        Physics2D.SyncTransforms();
    }

    private static void FitMechObstacle(Transform obstacle, Vector2 maximumSize)
    {
        var art = obstacle.GetComponent<SpriteRenderer>();
        if (art == null || art.sprite == null) return;
        Vector2 size = art.sprite.bounds.size;
        float angle = obstacle.localEulerAngles.z * Mathf.Deg2Rad;
        float width = Mathf.Abs(Mathf.Cos(angle)) * size.x + Mathf.Abs(Mathf.Sin(angle)) * size.y;
        float height = Mathf.Abs(Mathf.Sin(angle)) * size.x + Mathf.Abs(Mathf.Cos(angle)) * size.y;
        float scale = Mathf.Min(maximumSize.x / Mathf.Max(.01f, width), maximumSize.y / Mathf.Max(.01f, height));
        obstacle.localScale = new Vector3(scale, scale, 1);
    }

    private static readonly Vector2[] MechSpawnPads = {
        new Vector2(-23, -20), new Vector2(23, 20), new Vector2(23, -20), new Vector2(-23, 20),
        new Vector2(0, -14), new Vector2(0, 14), new Vector2(-24, -12), new Vector2(24, 12)
    };

    public static bool TryFindMechSpawn(GameObject ship, bool lowerSide, out Vector2 position)
        => TryFindSafeSpawn(ship, 2, lowerSide, out position);

    public static bool TryFindSafeSpawn(GameObject ship, int mapIndex, bool lowerSide, out Vector2 position)
    {
        // Clearance includes the entire solid hull around its pivot, even while the dead hull is disabled.
        float radius = 2.5f;
        if (ship != null)
            foreach (var polygon in ship.GetComponentsInChildren<PolygonCollider2D>(true))
                for (int path = 0; path < polygon.pathCount; path++)
                    foreach (Vector2 point in polygon.GetPath(path))
                        radius = Mathf.Max(radius, ((Vector2)polygon.transform.TransformPoint(point + polygon.offset) - (Vector2)ship.transform.position).magnitude);
        radius += .65f;
        Vector2 min = GetArenaMin(mapIndex);
        Vector2 max = GetArenaMax(mapIndex);
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        float best = float.NegativeInfinity;
        position = default;
        // Pads first, then a deterministic fallback grid; never return an unchecked center position.
        for (int i = 0; i < MechSpawnPads.Length + 121; i++)
        {
            int grid = i - MechSpawnPads.Length;
            Vector2 candidate = i < MechSpawnPads.Length ? MechSpawnPads[i] : new Vector2(-30 + (grid % 11) * 6, -30 + (grid / 11) * 6);
            if (mapIndex != 2 && grid >= 0)
                candidate = new Vector2(Mathf.Lerp(min.x + radius, max.x - radius, (grid % 11) / 10f),
                    Mathf.Lerp(min.y + radius, max.y - radius, (grid / 11) / 10f));
            if (candidate.x - radius < min.x || candidate.x + radius > max.x
                || candidate.y - radius < min.y || candidate.y + radius > max.y) continue;
            bool blocked = false;
            foreach (var hit in Physics2D.OverlapCircleAll(candidate, radius))
            {
                if (ship != null && hit.transform.IsChildOf(ship.transform)) continue;
                if (!hit.isTrigger || hit.GetComponentInParent<HazardController>() != null) { blocked = true; break; }
            }
            if (blocked) continue;
            float nearest = 60;
            foreach (var player in players)
                if (!player.isDead && player.gameObject != ship)
                    nearest = Mathf.Min(nearest, Vector2.Distance(candidate, player.transform.position));
            if (nearest < 14) continue;
            float score = nearest + (i < MechSpawnPads.Length ? 8 : 0) + ((candidate.y < 0) == lowerSide ? 3 : 0);
            if (score > best) { best = score; position = candidate; }
        }
        return !float.IsNegativeInfinity(best);
    }

    private System.Collections.IEnumerator SpawnMechPlayerWhenClear(string prefabName, Quaternion rotation)
    {
        GameObject prefab = GetPrefab(prefabName);
        Physics2D.SyncTransforms();
        while (PhotonNetwork.InRoom && !isMatchEnding)
        {
            if (TryFindMechSpawn(prefab, PhotonNetwork.IsMasterClient, out Vector2 position))
            {
                PhotonNetwork.Instantiate(prefabName, position, rotation);
                yield break;
            }
            yield return new WaitForSeconds(.5f);
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
}
