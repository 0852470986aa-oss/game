// ไฟล์ GameplayManager.Maps.cs: ส่วนจัดการแม็พของ GameplayManager (partial class เดียวกับ GameplayManager.cs)
// หน้าที่: เลือก layout แม็พที่ห้องเลือก, ขอบสนาม, สร้างสิ่งกำบัง/กำแพง, คริสตัลสะท้อนกระสุน, แท่นวาร์ป และหาจุดเกิดที่ปลอดภัย
// ถูกเรียกจาก GameplayManager.Start (ApplySelectedMapLayout / GenerateMapObstacles) และ PlayerController.Health (TryFindSafeSpawn ตอนเกิดใหม่)
// ทำงานเหมือนกันทุกเครื่อง (ไม่ส่งผ่านเน็ต) เพราะตำแหน่งทุกอย่างกำหนดตายตัว ทำให้ตำแหน่งชนตรงกันทุกเครื่อง
using UnityEngine;
using Photon.Pun;

// ส่วนแม็พของ GameplayManager: เป็นคลาสเดียวกันผ่าน partial ไม่ต้องเพิ่ม Component ใน Scene
// ลำดับแม็พ: 0 แมงกะพรุน, 1 ปริซึม, 2 หุ่นยนต์
// แก้ตำแหน่งสิ่งกีดขวางที่ DecoratePrism / PrepareMechCover
// ขอบเขตสนามและจุดเกิดปลอดภัยอยู่ในไฟล์นี้ด้วย
public partial class GameplayManager
{
    // ครึ่งความสูงของสนาม (หน่วยโลก) ใช้เท่ากันทุกแม็พ → สนามสูงรวม 73 หน่วย
    public const float ArenaHalfHeight = 36.5f;
    // แม็พปริซึมเดิมกว้าง 128 (ครึ่งละ 64) ใหญ่เกินสำหรับ 1v1 → ลดเหลือ 92 ให้เจอกันเร็วขึ้น
    // ครึ่งความกว้างสนามแยกตามแม็พ (index 0,1,2) หน่วยโลก ใช้คำนวณขอบสนาม/กำแพง/จุดเกิด
    private static readonly float[] ArenaHalfWidths = { 36.3f, 46f, 39f, 37f, 37f }; // 3, 4 = แม็พใหม่ (GameplayManager.NewMaps.cs)

    // อ่านเลขแม็พจาก Custom Property "MapIndex" ของห้อง Photon (ทุกเครื่องอ่านค่าเดียวกัน)
    // ถ้ายังไม่อยู่ในห้องหรือไม่มีค่า จะคืน 2 (แม็พหุ่นยนต์) เป็นค่าเริ่มต้น
    public static int GetCurrentMapIndex()
    {
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("MapIndex", out object value))
            return Mathf.Clamp((int)value, 0, ArenaHalfWidths.Length - 1);
        return 2;
    }

    // คืนชื่อแม็พปัจจุบันเป็นข้อความ (ใช้แสดงผลบน UI)
    public static string GetCurrentMapName()
    {
        string[] names = { "Electric Jellyfish Core", "Obelisk Plains of Prism", "Abandoned Mech Warzone", "Asteroid Station", "Molten Nebula" };
        return names[GetCurrentMapIndex()];
    }

    // มุมซ้ายล่างของสนามตามแม็พ (ใช้จำกัดขอบ/วาดมินิแม็พ/หาจุดเกิด)
    public static Vector2 GetArenaMin(int mapIndex)
    {
        mapIndex = Mathf.Clamp(mapIndex, 0, ArenaHalfWidths.Length - 1);
        return new Vector2(-ArenaHalfWidths[mapIndex], -ArenaHalfHeight);
    }

    // มุมขวาบนของสนามตามแม็พ
    public static Vector2 GetArenaMax(int mapIndex)
    {
        mapIndex = Mathf.Clamp(mapIndex, 0, ArenaHalfWidths.Length - 1);
        return new Vector2(ArenaHalfWidths[mapIndex], ArenaHalfHeight);
    }

    // ย่อ/ขยายภาพพื้นหลังให้สูง 75 หน่วยโลก (ครอบสนามสูง 73 พอดี) โดยคงสัดส่วนภาพ
    private static void FitBackgroundToArena(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return;
        const float targetWorldHeight = 75f;
        float scale = targetWorldHeight / Mathf.Max(renderer.sprite.bounds.size.y, 0.01f);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    // เปิดเฉพาะ MapX_Layout ของแม็พที่เลือก ปิดแม็พอื่น แล้วติดภาพ/สิ่งกีดขวางเฉพาะแม็พนั้น
    // เรียกจาก GameplayManager.Start ทุกเครื่อง (รวมโหมดทดสอบออฟไลน์) ก่อนสร้างยานผู้เล่น
    private void ApplySelectedMapLayout(int selectedMapIndex)
    {
        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        // วนแม็พ 0-2 แล้วหา root object ชื่อ Map{i}_Layout ใน Scene
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
                if (i == 1 && i == selectedMapIndex)
                {
                    DecoratePrism(root.transform);
                    if (root.GetComponent<PrismArenaVisuals>() == null) root.AddComponent<PrismArenaVisuals>();
                }
                // Authored cover needs collision even when procedural generation is disabled.
                if (i == 2 && i == selectedMapIndex) PrepareMechCover(root.transform);

                if (i == selectedMapIndex && renderer != null)
                    backgroundSprite = renderer;
                break;
            }
        }
        // แม็พใหม่ (3, 4) ไม่มี Layout ใน Scene: สร้างพื้นหลังด้วยโค้ด
        BuildRuntimeMapBackground(selectedMapIndex);
        // แม็พ 3/4: ของขยับบนพื้นหลัง (เศษหิน ไฟสัญญาณ ดาวตก / ประกายไฟ แสงร้อน) — MapAmbience.cs
        MapAmbience.Build(selectedMapIndex, GetArenaMin(selectedMapIndex), GetArenaMax(selectedMapIndex));
        SetupStarfield(selectedMapIndex);
    }

    // ตั้ง ParticleSystem "Starfield" (เม็ดดาวขาว) ให้กระจายทั่วทั้งสนามของแม็พที่เล่น + เผื่อขอบที่กล้องมองเห็น
    // จำนวนดาวตามพื้นที่ (ความหนาแน่นเท่ากันทุกแม็พ) / เดิมตั้งเฉพาะแม็พ 2 แม็พอื่นดาวกองอยู่กลางจอ
    // ปิด FeatureFlags.FullStarfield = ใช้ค่าที่ตั้งใน Scene ตามเดิม
    private void SetupStarfield(int mapIndex)
    {
        if (!FeatureFlags.FullStarfield) return;
        ParticleSystem stars = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.name == "Starfield") { stars = ps; break; }
            if (stars != null) break;
        }
        if (stars == null) return;
        // ย้ายออกมาเป็น root (ถ้าซ่อนอยู่ใต้ Layout ของแม็พอื่น ดาวจะถูกปิดไปด้วย)
        if (stars.transform.parent != null) stars.transform.SetParent(null, true);
        stars.gameObject.SetActive(true);
        stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        stars.transform.position = new Vector3(0, 0, stars.transform.position.z);
        stars.transform.rotation = Quaternion.identity;
        stars.transform.localScale = Vector3.one;
        const float margin = 14f, density = .055f, life = 20f; // ดาวต่อตารางหน่วย
        Vector2 area = GetArenaMax(mapIndex) - GetArenaMin(mapIndex) + Vector2.one * margin * 2f;
        int count = Mathf.Clamp(Mathf.RoundToInt(area.x * area.y * density), 200, 1500);
        var main = stars.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startSpeed = 0;
        main.duration = life; // prewarm จำลองแค่ 1 รอบ duration: ให้เท่าอายุดาว ดาวจะเต็มตั้งแต่เริ่มแมตช์
        main.startLifetime = life;
        main.maxParticles = count;
        main.prewarm = true;
        main.loop = true;
        var shape = stars.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box; // Box = กระจายทั้งกล่อง (ไม่ใช่แค่ขอบ)
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = new Vector3(area.x, area.y, .1f);
        var emission = stars.emission;
        emission.enabled = true;
        emission.rateOverTime = count / life;
        var renderer = stars.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) renderer.maxParticleSize = Mathf.Max(renderer.maxParticleSize, .5f);
        stars.Play();
    }

    // One deterministic solid layout on every client; background artwork stays untouched.
    // รุ่น v2: สนามแคบลง (กว้าง 92) + คริสตัลสะท้อนกระสุน + แท่นวาร์ป 4 มุม
    // ชื่อกลุ่มเลย์เอาต์ (PrismLayoutName) อยู่ใน GameplayManager.PrismV3.cs: รุ่น 3 เมื่อเปิด FeatureFlags.PrismMapV3
    private const string LegacyPrismLayoutName = "PrismPlayableLayout";

    // สร้างเลย์เอาต์แม็พปริซึม (เสา, หินคริสตัลห้อย, คริสตัลสะท้อนกระสุน, แท่นวาร์ป) ใต้ Map1_Layout
    // ถ้ามีเลย์เอาต์ v2 อยู่แล้ว (เช่นบันทึกไว้ใน Scene) จะไม่สร้างซ้ำ / เรียกจาก ApplySelectedMapLayout และเมนู Editable
    public static void DecoratePrism(Transform layout)
    {
        if (layout == null || layout.Find(PrismLayoutName) != null) return;
        // รุ่น 3: ชิ้นส่วนมุมบนชุดใหม่ (GameplayManager.PrismV3.cs)
        if (FeatureFlags.PrismMapV3) { DecoratePrismV3(layout); return; }
        var central = Resources.LoadAll<Sprite>("Images/Obs_PrismPillar_04");
        if (central.Length == 0) { Debug.LogWarning("Prism pillar 04 is missing."); return; }
        // เลย์เอาต์รุ่นเก่าออกแบบไว้สำหรับสนามกว้าง 128 ใช้กับสนามใหม่ไม่ได้
        var legacy = layout.Find(LegacyPrismLayoutName);
        if (legacy != null)
        {
            legacy.gameObject.SetActive(false);
            if (Application.isPlaying) Object.Destroy(legacy.gameObject);
        }
        // 1) ซ่อนของเดิมใน layout ทั้งหมด ยกเว้นภาพพื้นหลัง แล้วสร้างกลุ่มใหม่ + เสากลางสูง 18 หน่วย
        foreach (Transform child in layout)
            if (child.name != "Background") child.gameObject.SetActive(false);
        var group = new GameObject(PrismLayoutName);
        group.transform.SetParent(layout, false);
        AddPrismCover(group.transform, central[0], Vector2.zero, 18, "CentralPillar_04");
        // สมมาตร 180 องศา: ทั้งสองฝั่งได้ cover เท่ากัน
        Vector2[] positions = {
            new Vector2(-18, 13), new Vector2(18, -13),
            new Vector2(18, 13), new Vector2(-18, -13),
            new Vector2(-32, 22), new Vector2(32, -22),
            new Vector2(32, 22), new Vector2(-32, -22),
            new Vector2(-40, 0), new Vector2(40, 0),
            new Vector2(-9, 28), new Vector2(9, -28)
        };
        int[] variants = { 1, 1, 2, 2, 3, 3, 5, 5, 6, 7, 8, 8 };
        // 2) วางเสารอบสนาม: 4 ต้นแรกสูง 10 หน่วย ที่เหลือสูง 8 หน่วย (มีตัวชน)
        for (int i = 0; i < positions.Length; i++)
        {
            var sprites = Resources.LoadAll<Sprite>("Images/Obs_PrismPillar_" + variants[i].ToString("00"));
            if (sprites.Length > 0) AddPrismCover(group.transform, sprites[0], positions[i],
                i < 4 ? 10 : 8, "Pillar_" + i);
        }
        // 3) โหลดภาพคริสตัลแล้วเรียงตามชื่อ ให้ทุกเครื่องได้ลำดับเดียวกัน
        var crystals = Resources.LoadAll<Sprite>("Images/Obs_Crystals");
        System.Array.Sort(crystals, (a,b) => string.CompareOrdinal(a.name,b.name));
        // Hang blue crystal-rock clusters upside down beneath the floating obelisks.
        // These are decorative only (no collider), so they cannot alter the arena layout.
        int[] hangingRockVariants = { 0, 2, 3, 4 };
        for (int i = 0; i < positions.Length && crystals.Length > 0; i++)
        {
            Sprite rock = FindNumberedSprite(crystals, "Obs_Crystals", hangingRockVariants[i % hangingRockVariants.Length]);
            if (rock == null) continue;
            float pillarHeight = i < 4 ? 10f : 8f;
            float rockHeight = i < 4 ? 3.2f : 2.6f;
            float pillarBottom = positions[i].y - pillarHeight * 0.5f;
            Vector2 rockPosition = new Vector2(positions[i].x, pillarBottom - rockHeight * 0.5f + 1.4f);
            SpriteRenderer hangingRock = PrismSprite(group.transform, "HangingCrystalRock_" + i,
                rock, rockPosition, rockHeight, Color.white, 1);
            hangingRock.flipY = true;
        }
        // กลุ่มคริสตัล = กระจกสะท้อนกระสุน (PrismReflector) ยิงอ้อมมุมได้
        Vector2[] clusters = { new Vector2(-27,6), new Vector2(27,-6), new Vector2(27,6), new Vector2(-27,-6),
            new Vector2(-20,30), new Vector2(20,-30), new Vector2(20,30), new Vector2(-20,-30) };
        // เลือกภาพคริสตัลตามหมายเลข (เดิมเลือกตามลำดับในชีต เลยได้เศษชิ้นเล็กและภาพระเบิดปนมา)
        int[] clusterVariants = { 3, 3, 2, 2, 4, 4, 11, 11 };
        // 4) สร้างคริสตัลสะท้อนกระสุน 8 จุด (4 จุดในสูง 5, 4 จุดนอกสูง 4.5) แล้วติด PrismReflector
        for (int i = 0; i < clusters.Length && crystals.Length > 0; i++)
        {
            Sprite cluster = FindNumberedSprite(crystals, "Obs_Crystals", clusterVariants[i]) ?? crystals[0];
            var art = AddPrismCover(group.transform, cluster, clusters[i], i < 4 ? 5f : 4.5f, "Crystal_" + i);
            art.gameObject.AddComponent<PrismReflector>();
        }
        // 5) เพิ่มแท่นวาร์ป แล้วซิงก์ตำแหน่ง Collider กับ Physics2D ทันที
        AddWarpPads(group.transform);
        Physics2D.SyncTransforms();
    }

    // แท่นวาร์ป: คู่ A ซ้ายล่าง ↔ ขวาบน (ฟ้า), คู่ B ขวาล่าง ↔ ซ้ายบน (ชมพู) สมมาตรทั้งสองฝั่ง
    private static void AddWarpPads(Transform parent)
    {
        // รุ่น 3 ใช้ภาพแท่นวงกลมมุมบน (pr_warp กว้าง 5.2 หน่วย) / รุ่นเดิมใช้ภาพในชุด Props_Prism (กว้าง 6 หน่วย)
        Sprite padSprite = FeatureFlags.PrismMapV3 ? Resources.Load<Sprite>(PrismArtFolder + "pr_warp") : null;
        float padWidth = padSprite != null ? 5.2f : 6f;
        if (padSprite == null)
        {
            var sheet = Resources.LoadAll<Sprite>("Images/Props_Prism");
            padSprite = System.Array.Find(sheet, s => s.name == "Props_Prism_83");
        }
        var a1 = CreateWarpPad(parent, "WarpPad_A1", new Vector2(-40, -30), padSprite, WarpPad.ColorA, padWidth);
        var a2 = CreateWarpPad(parent, "WarpPad_A2", new Vector2(40, 30), padSprite, WarpPad.ColorA, padWidth);
        var b1 = CreateWarpPad(parent, "WarpPad_B1", new Vector2(40, -30), padSprite, WarpPad.ColorB, padWidth);
        var b2 = CreateWarpPad(parent, "WarpPad_B2", new Vector2(-40, 30), padSprite, WarpPad.ColorB, padWidth);
        a1.partner = a2; a2.partner = a1;
        b1.partner = b2; b2.partner = b1;
    }

    // สร้างแท่นวาร์ป 1 อัน: ภาพกว้าง width หน่วย ย้อมสีตามคู่ + Trigger วงกลมรัศมี 2.1 หน่วย + คอมโพเนนต์ WarpPad
    private static WarpPad CreateWarpPad(Transform parent, string name, Vector2 position, Sprite padSprite, Color tint, float width = 6f)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = position;
        if (padSprite != null)
        {
            var art = new GameObject("Artwork");
            art.transform.SetParent(holder.transform, false);
            float scale = width / Mathf.Max(padSprite.bounds.size.x, 0.01f);
            art.transform.localScale = Vector3.one * scale;
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = padSprite;
            renderer.color = Color.Lerp(Color.white, tint, .45f);
            renderer.sortingOrder = -2;
        }
        var trigger = holder.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 2.1f;
        var pad = holder.AddComponent<WarpPad>();
        pad.tint = tint;
        return pad;
    }

    // สร้างสิ่งกำบังของแม็พปริซึม: ภาพ + PolygonCollider2D ตามรูปภาพ (ชนได้ บังกระสุน) และย้ายไป Layer "Obstacle"
    private static SpriteRenderer AddPrismCover(Transform parent, Sprite sprite, Vector2 position, float height, string name)
    {
        var art = PrismSprite(parent, name, sprite, position, height, Color.white, 2);
        var collider = art.gameObject.AddComponent<PolygonCollider2D>();
        collider.isTrigger = false;
        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer >= 0) art.gameObject.layer = layer;
        return art;
    }

    // สร้าง GameObject ภาพ 1 ชิ้น ย่อ/ขยายให้สูงเท่า height (หน่วยโลก) และจัดให้จุดกึ่งกลางภาพอยู่ที่ position
    // คืน SpriteRenderer ของภาพ (ตัวชนเพิ่มทีหลังได้ถ้าต้องการ)
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

    // วาดวงรีบนพื้นด้วย LineRenderer 48 จุด (กว้าง width สูง height) เป็นภาพตกแต่งอย่างเดียว
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

    // สร้างกำแพงขอบสนาม 4 ด้าน + สิ่งกำบังตามแม็พด้วยโค้ด (ตำแหน่งตายตัว ทุกเครื่องได้เหมือนกัน)
    // เรียกจาก GameplayManager.Start ทุกเครื่องเมื่อเชื่อมต่อ Photon และเปิด autoGenerateMap
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

        // แม็พ 0: เพิ่มรายการ cover (แต่ตอนสร้างจริงข้ามทุกอย่างยกเว้นกำแพงขอบ ดูลูป foreach ข้างล่าง)
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
            // แม็พ 1: รายการนี้ใช้เฉพาะเมื่อยังไม่มี PrismPlayableLayout_v2 (ถ้ามี จะสร้างแค่กำแพงขอบ)
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
            // แม็พ 2: ถ้าใน Scene มีหิน/ป้อม/แกนพลังงานที่จัดไว้แล้ว จะข้ามชนิดนั้น (ใช้ตัวชนของภาพใน Scene แทน)
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
        else if (!IsRuntimeMap(mapIndex))
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

        // คัดภาพอุกกาบาตที่ลงท้าย _9, _10, _11 ออก (ถ้าคัดแล้วไม่เหลือ ใช้ทั้งหมด)
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

        // ตรวจว่าใน Scene มีสิ่งกีดขวางที่จัดวางไว้เองแล้วหรือยัง เพื่อไม่สร้างตัวชนซ้อนกัน
        GameObject map2Layout = GameObject.Find("Map2_Layout");
        bool hasAuthoredMap2Rocks = map2Layout != null && map2Layout.transform.Find("RockObstacles") != null;
        bool hasAuthoredMap2Turrets = map2Layout != null && map2Layout.transform.Find("TurretObstacles") != null;
        bool hasAuthoredMap2RedCores = map2Layout != null && map2Layout.transform.Find("RedCoreObstacles") != null;
        GameObject map1Layout = GameObject.Find("Map1_Layout");
        bool hasAuthoredMap1Obstacles = map1Layout != null && map1Layout.transform.Find("Map1Obstacles") != null;

        // 5. วนสร้างสิ่งกีดขวางทีละชิ้น: BoxCollider2D ขนาด obs.scale + ภาพตามชนิด
        foreach (var obs in obstacles)
        {
            if (mapIndex == 0 && obs.type != "wall") continue;
            if (mapIndex == 1 && map1Layout != null && map1Layout.transform.Find(PrismLayoutName) != null && obs.type != "wall") continue;
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
                else if (mapIndex == StationMapIndex) { baseCore = new Color(0.12f, 0.1f, 0.22f, 1f); baseOutline = new Color(0.55f, 0.4f, 1f, 1f); }
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

        // แม็พใหม่ (3, 4): สิ่งกีดขวางจากรูปชุดใหม่
        BuildRuntimeMapObstacles(mapIndex, obstaclesContainer.transform);

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

    // หา Sprite ที่ชื่อ "{prefix}_{number}" ในชุดภาพ (เช่น Obs_Crystals_3) ไม่เจอคืน null
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

    // สร้างภาพให้สิ่งกีดขวางแม็พปริซึมแบบเก่า (ใช้เมื่อ Scene ไม่มี Map1Obstacles) เลือกภาพ/มุมเอียงตามชนิดและลำดับ id
    // ย่อ/ขยายภาพให้ครอบขนาดกล่องชน
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

    // เตรียมแม็พหุ่นยนต์: ตั้งดาวพื้นหลัง, จัดตำแหน่ง cover, ติดป้อมปืน AutoTurret และตัวชนให้หิน/ป้อม/แกนพลังงาน
    // เรียกจาก ApplySelectedMapLayout ทุกเครื่องเมื่อเลือกแม็พ 2
    public static void PrepareMechCover(Transform layout)
    {
        if (layout == null) return;
        // 1) ดาวพื้นหลัง: ตั้งให้ทุกแม็พใน SetupStarfield (เรียกจาก ApplySelectedMapLayout)
        // มี EditableLayout = ตำแหน่ง/ขนาดที่จัดไว้ใน Scene คือของจริง ไม่ย้ายกลับ
        if (layout.GetComponent<EditableLayout>() == null) ApplyMechCoverPlacement(layout);
        // 2) ติด AutoTurret ให้ป้อมทุกอันที่ยังไม่มี
        var turrets = layout.Find("TurretObstacles");
        if (turrets != null)
            foreach (Transform turret in turrets)
                if (turret.GetComponent<AutoTurret>() == null) turret.gameObject.AddComponent<AutoTurret>();
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

    // ตำแหน่ง/ขนาดสิ่งกีดขวางแม็พหุ่นยนต์ที่โค้ดกำหนด (ใช้ทั้งตอนรันและตอนบันทึกลง Scene)
    private static void ApplyMechCoverPlacement(Transform layout)
    {
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
            }
    }

#if UNITY_EDITOR
    // คลิกขวาที่ GameplayManager ใน Inspector แล้วเลือกเมนูนี้ (ตอนไม่ได้กด Play)
    // สร้าง/จัดสิ่งกีดขวางของทั้ง 3 แม็พลง Scene ตามที่โค้ดจะทำตอนรัน แล้วแก้ในหน้า Edit ได้
    // กำแพงขอบสนามและกล่องชนที่มองไม่เห็นยังสร้างตอนรัน เพราะเป็นกติกาเกม (ขนาดสนาม)
    [ContextMenu("Editable/2. Build Map Layouts into Scene")]
    private void EditableBuildMapLayouts()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before building map layouts into the scene."); return; }
        int group = UnityEditor.Undo.GetCurrentGroup();
        var built = new System.Collections.Generic.List<string>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name == "Map0_Layout" && JellyArenaVisuals.BuildEditableOrnaments(root.transform))
                built.Add(root.name);
            else if (root.name == "Map1_Layout" && root.transform.Find(PrismLayoutName) == null)
            {
                UnityEditor.Undo.RegisterFullObjectHierarchyUndo(root, "Build Editable Map Layouts");
                // ลบเลย์เอาต์รุ่นเก่า (สนามกว้าง 128) ก่อนสร้างรุ่นใหม่
                var legacy = root.transform.Find(LegacyPrismLayoutName);
                if (legacy != null) UnityEditor.Undo.DestroyObjectImmediate(legacy.gameObject);
                DecoratePrism(root.transform);
                var prism = root.transform.Find(PrismLayoutName);
                if (prism != null)
                {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(prism.gameObject, "Build Editable Map Layouts");
                    built.Add(root.name);
                }
            }
            else if (root.name == "Map2_Layout" && root.GetComponent<EditableLayout>() == null)
            {
                UnityEditor.Undo.RegisterFullObjectHierarchyUndo(root, "Build Editable Map Layouts");
                ApplyMechCoverPlacement(root.transform);
                var debris = root.transform.Find("RockDecorations");
                if (debris != null) debris.gameObject.SetActive(false);
                UnityEditor.Undo.AddComponent<EditableLayout>(root);
                built.Add(root.name);
            }
        }
        UnityEditor.Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log(built.Count > 0 ? "Editable map layouts built: " + string.Join(", ", built)
            : "Nothing to build: map layouts are already editable in this scene.", this);
    }
#endif

    // ย่อ/ขยายสิ่งกีดขวางให้กรอบ (คิดรวมมุมหมุนแล้ว) ไม่เกิน maximumSize หน่วยโลก โดยคงสัดส่วน
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

    // จุดเกิดที่ลองก่อนในแม็พหุ่นยนต์ (หน่วยโลก) ถ้าไม่ว่างจะไปไล่หาจากตารางสำรอง
    private static readonly Vector2[] MechSpawnPads = {
        new Vector2(-23, -20), new Vector2(23, 20), new Vector2(23, -20), new Vector2(-23, 20),
        new Vector2(0, -14), new Vector2(0, 14), new Vector2(-24, -12), new Vector2(24, 12)
    };

    // ระยะห่างขั้นต่ำจากยานอื่นตอนหาจุดเกิด (1v1 = 14 หน่วย, ห้องหลายคนลดลงเพราะยานเยอะ) ตั้งใน GameplayManager.Start
    public static float SafeSpawnDistance = 14f;
    // คะแนนพิเศษถ้าจุดเกิดอยู่ฝั่งที่ต้องการ (โหมดทีมให้สูง เพื่อให้เกิดฝั่งทีมตัวเองเกือบเสมอ)
    public static float SpawnSideBonus = 3f;

    // ทางลัดหาจุดเกิดในแม็พหุ่นยนต์ (mapIndex = 2)
    public static bool TryFindMechSpawn(GameObject ship, bool lowerSide, out Vector2 position)
        => TryFindSafeSpawn(ship, 2, lowerSide, out position);

    // หาจุดเกิดที่ไม่ทับสิ่งกีดขวาง/Hazard และห่างผู้เล่นที่ยังมีชีวิต >= 14 หน่วย; lowerSide = อยากเกิดครึ่งล่าง (Master) หรือบน
    // เรียกบนเครื่องเจ้าของยาน (ตอนเกิดครั้งแรกในแม็พ 2 และตอนเกิดใหม่) คืน false ถ้ายังไม่มีจุดว่าง ผู้เรียกจะรอแล้วลองใหม่
    // ให้คะแนน: ระยะห่างคู่แข่ง + 8 ถ้าเป็นจุดเกิดที่กำหนด + 3 ถ้าอยู่ฝั่งที่ต้องการ แล้วเลือกจุดคะแนนสูงสุด
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
            // ข้ามจุดที่ตัวยานจะล้นขอบสนาม
            if (candidate.x - radius < min.x || candidate.x + radius > max.x
                || candidate.y - radius < min.y || candidate.y + radius > max.y) continue;
            // ข้ามจุดที่ทับตัวชนแข็ง หรือทับ Hazard (ไม่นับ Trigger อื่นและชิ้นส่วนของยานตัวเอง)
            bool blocked = false;
            foreach (var hit in Physics2D.OverlapCircleAll(candidate, radius))
            {
                if (ship != null && hit.transform.IsChildOf(ship.transform)) continue;
                if (!hit.isTrigger || hit.GetComponentInParent<HazardController>() != null) { blocked = true; break; }
            }
            if (blocked) continue;
            // หาระยะถึงผู้เล่นที่ยังไม่ตายที่ใกล้ที่สุด ถ้าใกล้กว่า 14 หน่วยถือว่าไม่ปลอดภัย
            float nearest = 60;
            foreach (var player in players)
                if (!player.isDead && player.gameObject != ship)
                    nearest = Mathf.Min(nearest, Vector2.Distance(candidate, player.transform.position));
            if (nearest < SafeSpawnDistance) continue;
            float score = nearest + (i < MechSpawnPads.Length ? 8 : 0) + ((candidate.y < 0) == lowerSide ? SpawnSideBonus : 0);
            if (score > best) { best = score; position = candidate; }
        }
        return !float.IsNegativeInfinity(best);
    }

    // Coroutine สร้างยานผู้เล่นในแม็พหุ่นยนต์: วนหาจุดเกิดปลอดภัยทุก 0.5 วิ จนเจอแล้ว PhotonNetwork.Instantiate
    // รันบนเครื่องของผู้เล่นเอง (เรียกจาก GameplayManager.Start) Master เกิดฝั่งล่าง อีกคนฝั่งบน
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

    // สร้างภาพให้สิ่งกีดขวางแม็พหุ่นยนต์ที่สร้างด้วยโค้ด (อุกกาบาต/ป้อม/แกนพลังงาน) เลือกภาพและมุมหมุนจาก id
    // ภาพมีขนาด 65% ของกล่องชน (แกนพลังงาน 85%) เป็นภาพอย่างเดียว ตัวชนคือ BoxCollider2D ของ parent
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
