// ไฟล์ MapHazardManager.cs — อยู่ใน Scene เกมเพลย์ (ติดกับ GameObject ของ GameplayManager โดย SceneSetupTool)
// MapHazardManager: Master Client สุ่มสร้าง Hazard ตามแม็พเป็นระยะ ผ่าน PhotonNetwork.InstantiateRoomObject
// (ตัว Hazard แต่ละชิ้นทำงานใน HazardController.cs) แม็พ 0 = สายฟ้า+แกนพลังงาน, 1 = บึงชะลอ, 2 = อุกกาบาตลาวา
// JellyArenaVisuals: ของประดับ/แอนิเมชันแม็พแมงกะพรุน (แม็พ 0) และให้ค่าแรงดูดหลุมแรงโน้มถ่วงกับ PlayerController
using UnityEngine;
using Photon.Pun;
using System.Collections;

// Local presentation only. Gravity is evaluated by each ship owner, then normal ship sync replicates it.
// คลาสภาพประกอบแม็พแมงกะพรุน (แม็พ 0) ถูกเพิ่มโดย GameplayManager.Maps.cs: สร้าง/หมุนแกนกลาง หลุมแรงโน้มถ่วง
// แมงกะพรุนลอย และเศษหินหมุนวน รวมถึงคำนวณแรงดูด PullAt() ที่ยานใช้ตอนเคลื่อนที่
public class JellyArenaVisuals : MonoBehaviour
{
    // ตัวที่กำลังทำงานอยู่ (มีได้ตัวเดียว) ให้ PullAt แบบ static เข้าถึงได้
    private static JellyArenaVisuals active;
    // ตำแหน่ง local ของหลุมแรงโน้มถ่วง 6 จุด วางสมมาตรกัน
    private static readonly Vector2[] WellPoints = { new Vector2(-17, 10), new Vector2(17,-10),
        new Vector2(19,16), new Vector2(-19,-16), new Vector2(-27,0), new Vector2(27,0) };
    // ตำแหน่ง local ของแมงกะพรุนลอยประดับ 3 ตัว
    private static readonly Vector2[] JellyfishPoints = { new Vector2(-16,18), new Vector2(18,-17), new Vector2(22,14) };
    private readonly Vector2[] wells = WellPoints;
    // รายการ SpriteRenderer ของประดับ (ลำดับ: แกนกลาง -> หลุม -> แมงกะพรุน) และตำแหน่งเริ่มต้นของแต่ละชิ้น
    private readonly System.Collections.Generic.List<SpriteRenderer> ornaments = new System.Collections.Generic.List<SpriteRenderer>();
    private readonly System.Collections.Generic.List<Vector3> origins = new System.Collections.Generic.List<Vector3>();
    // เศษหิน 24 ชิ้นที่หมุนวนเข้าหาศูนย์กลาง (แอนิเมชันอย่างเดียว)
    private readonly System.Collections.Generic.List<SpriteRenderer> debris = new System.Collections.Generic.List<SpriteRenderer>();
    // สร้าง GameObject ลูกที่มี SpriteRenderer วางที่ point และปรับสเกลให้ด้านยาวสุดเท่ากับ size หน่วย
    // ตัวแรกใช้ transform ของตัวเองเป็นพ่อ ตัว static ใช้ parent ที่ส่งมา (ใช้ตอนสร้างใน Editor ด้วย)
    private SpriteRenderer Add(Sprite sprite, Vector2 point, float size, string label, int order)
        => Add(transform, sprite, point, size, label, order);
    // ตัวทำงานจริงของ Add: สร้างลูกใต้ parent ที่ส่งมา (ใช้ได้ทั้งตอนเล่นและใน Editor)
    private static SpriteRenderer Add(Transform parent, Sprite sprite, Vector2 point, float size, string label, int order)
    {
        var item = new GameObject(label);
// ถ้าเรียกตอนอยู่ใน Editor (ไม่ได้กด Play) ให้บันทึก Undo ไว้ กด Ctrl+Z ย้อนได้
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(item, "Build Editable Map Layouts");
#endif
        item.transform.SetParent(parent, false);
        item.transform.localPosition = point;
        var art = item.AddComponent<SpriteRenderer>();
        art.sprite = sprite;
        art.sortingOrder = order;
        item.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        return art;
    }
    // ภาพแมงกะพรุน/ลูกพลังงานที่ตัดแยกไฟล์แล้ว (ไม่มีเศษภาพข้างเคียงติดขอบ) ถ้าไม่มีไฟล์หรือปิด Flag ใช้ภาพจากแผ่นรวมเดิม
    private static Sprite CleanSprite(string file, Sprite fallback)
    {
        if (!FeatureFlags.CleanJellyArt) return fallback;
        var clean = Resources.Load<Sprite>("Images/Maps/Jelly/" + file);
        return clean != null ? clean : fallback;
    }
    // เปลี่ยนภาพของประดับที่บันทึกไว้ใน Scene เป็นภาพที่ตัดใหม่ โดยคงขนาดที่เห็นบนจอไว้เท่าเดิม
    private static void SwapToClean(SpriteRenderer art, Sprite clean)
    {
        if (art == null || art.sprite == null || clean == null || art.sprite == clean) return;
        float before = Mathf.Max(art.sprite.bounds.size.x, art.sprite.bounds.size.y);
        float after = Mathf.Max(clean.bounds.size.x, clean.bounds.size.y);
        art.sprite = clean;
        if (after > 0.0001f) art.transform.localScale *= before / after;
    }
    // ของประดับที่บันทึกลง Scene ได้ (แก้ขนาด/ตำแหน่งแกนกลางและแมงกะพรุนได้)
    // GravityWell ผูกกับแรงดึงจริงในเกม จึงถูกวางกลับตำแหน่งเดิมทุกครั้งที่เริ่มเกม
    private static bool IsEditableOrnament(string name)
        => name == "EnergyCoreArtwork" || name == "GravityWell" || name == "FloatingJellyfish";
    // Unity เรียกตอนเริ่ม (ทุกเครื่องแยกกัน เป็นภาพล้วน): โหลด Sprite แมงกะพรุน ซ่อนของเดิมใน Layout ยกเว้นพื้นหลัง
    // ถ้า Scene มีของประดับที่บันทึกไว้ (authored) ให้ใช้ของนั้น ไม่งั้นสร้างด้วยโค้ด แล้วสร้างเศษหินหมุน 24 ชิ้น
    void Awake()
    {
        var sheet = SkillSheetVisual.Load("Props_Jellyfish");
        var jelly = CleanSprite("jellyfish", System.Array.Find(sheet, s => s.name == "Props_Jellyfish_1"));
        var orb = CleanSprite("energy_orb", System.Array.Find(sheet, s => s.name == "Props_Jellyfish_2"));
        if (jelly == null || orb == null) { enabled = false; return; }
        // authored = true ถ้า Scene มีของประดับที่บันทึกไว้แล้ว (มีลูกชื่อ EnergyCoreArtwork)
        bool authored = transform.Find("EnergyCoreArtwork") != null;
        foreach (Transform child in transform)
            if (child.name != "Background" && !(authored && IsEditableOrnament(child.name))) child.gameObject.SetActive(false);
        active = this;
        if (authored && BindAuthoredOrnaments())
        {
            // ของประดับที่บันทึกไว้ใน Scene ยังชี้ภาพจากแผ่นรวม: เปลี่ยนเป็นภาพที่ตัดใหม่ (ลำดับ แกนกลาง → หลุม → แมงกะพรุน)
            for (int i = 0; i < ornaments.Count; i++)
                SwapToClean(ornaments[i], i <= wells.Length ? orb : jelly);
        }
        else
        {
            // A visible core anchors the map visually and matches the EnergyCore zone.
            var core = Add(orb, Vector2.zero, 10, "EnergyCoreArtwork", -1);
            ornaments.Add(core); origins.Add(core.transform.localPosition);
            foreach (Vector2 point in wells)
            {
                var art = Add(orb, point, 5, "GravityWell", -1);
                ornaments.Add(art); origins.Add(art.transform.localPosition);
            }
            foreach (Vector2 point in JellyfishPoints)
            {
                var art = Add(jelly, point, 10, "FloatingJellyfish", -2);
                ornaments.Add(art); origins.Add(art.transform.localPosition);
            }
        }
        // เศษหินประดับ (ใช้ Sprite "Obs_Asteroids_1") ตำแหน่งจริงถูกคำนวณใน Update
        var rocks = SkillSheetVisual.Load("Obs_Asteroids");
        var rock = System.Array.Find(rocks, s => s.name == "Obs_Asteroids_1");
        if (rock != null)
            for (int i=0; i<24; i++) debris.Add(Add(rock, Vector2.zero, 1, "VortexDecoration", -6));
    }

    // ใช้ของประดับที่บันทึกไว้ใน Scene; ลำดับต้องเป็น แกนกลาง → หลุมแรงโน้มถ่วง → แมงกะพรุน (Update ใช้ลำดับนี้)
    private bool BindAuthoredOrnaments()
    {
        var core = transform.Find("EnergyCoreArtwork").GetComponent<SpriteRenderer>();
        var wellArt = new System.Collections.Generic.List<SpriteRenderer>();
        var jellyArt = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (Transform child in transform)
        {
            var art = child.GetComponent<SpriteRenderer>();
            if (art == null || art.sprite == null) continue;
            if (child.name == "GravityWell") wellArt.Add(art);
            else if (child.name == "FloatingJellyfish") jellyArt.Add(art);
        }
        if (core == null || core.sprite == null || wellArt.Count != wells.Length)
        {
            // ไม่ครบ: ซ่อนของที่บันทึกไว้แล้วกลับไปสร้างด้วยโค้ดแบบเดิม
            Debug.LogWarning("Map0_Layout ornaments are incomplete (need EnergyCoreArtwork and " + wells.Length
                + " GravityWell); using the code-built ornaments.", this);
            foreach (Transform child in transform)
                if (IsEditableOrnament(child.name)) child.gameObject.SetActive(false);
            return false;
        }
        ornaments.Add(core); origins.Add(core.transform.localPosition);
        for (int i = 0; i < wellArt.Count; i++)
        {
            wellArt[i].transform.localPosition = wells[i];
            ornaments.Add(wellArt[i]); origins.Add(wellArt[i].transform.localPosition);
        }
        foreach (var art in jellyArt) { ornaments.Add(art); origins.Add(art.transform.localPosition); }
        return true;
    }

// ส่วนนี้คอมไพล์เฉพาะใน Unity Editor
#if UNITY_EDITOR
    // ใช้จากเมนู Editable/2 บน GameplayManager: สร้างของประดับแม็พแมงกะพรุนลง Scene ให้แก้ได้
    // (เศษหินที่หมุนวนเป็นแอนิเมชันล้วน จึงยังสร้างตอนรัน)
    public static bool BuildEditableOrnaments(Transform layout)
    {
        if (layout == null || layout.Find("EnergyCoreArtwork") != null) return false;
        var sheet = SkillSheetVisual.Load("Props_Jellyfish");
        var jelly = CleanSprite("jellyfish", System.Array.Find(sheet, s => s.name == "Props_Jellyfish_1"));
        var orb = CleanSprite("energy_orb", System.Array.Find(sheet, s => s.name == "Props_Jellyfish_2"));
        if (jelly == null || orb == null) { Debug.LogWarning("Props_Jellyfish sprites are missing."); return false; }
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Build Editable Map Layouts");
        foreach (Transform child in layout)
            if (child.name != "Background") child.gameObject.SetActive(false);
        Add(layout, orb, Vector2.zero, 10, "EnergyCoreArtwork", -1);
        foreach (Vector2 point in WellPoints) Add(layout, orb, point, 5, "GravityWell", -1);
        foreach (Vector2 point in JellyfishPoints) Add(layout, jelly, point, 10, "FloatingJellyfish", -2);
        return true;
    }
#endif
    // คืนแรงดูดของหลุมแรงโน้มถ่วงที่ตำแหน่ง position (เรียกจาก PlayerController.cs ตอนคำนวณการเคลื่อนที่บนเครื่องเจ้าของยาน)
    // หลุมมีผลในระยะ 0.15–7 หน่วย ยิ่งใกล้ยิ่งแรง และแรงรวมถูกจำกัดไม่เกิน 2.8; ถ้าไม่ได้อยู่แม็พ 0 คืนศูนย์
    public static Vector2 PullAt(Vector2 position)
    {
        if (active == null || !active.isActiveAndEnabled) return Vector2.zero;
        Vector2 pull = Vector2.zero;
        foreach (Vector2 point in active.wells)
        {
            Vector2 delta = (Vector2)active.transform.TransformPoint(point) - position;
            float distance = delta.magnitude;
            if (distance > .15f && distance < 7)
                pull += delta.normalized * (2.8f * (1-distance/7) * Mathf.Min(1,distance));
        }
        return Vector2.ClampMagnitude(pull, 2.8f);
    }
    // ทุกเฟรม: ขยับประดับตาม PhotonNetwork.Time (ทุกเครื่องเห็นจังหวะใกล้เคียงกัน)
    // แกนกลาง/หลุมหมุนและกระพริบ, แมงกะพรุนลอยขึ้นลง, เศษหินหมุนเป็นเกลียวเข้าหาศูนย์กลางรอบละ 18 วิ
    void Update()
    {
        float time = (float)(PhotonNetwork.Time % 10000);
        for (int i=0;i<ornaments.Count;i++)
        {
            var art = ornaments[i];
            // Index 0 is the core, followed by the gravity wells.
            if (i <= wells.Length) art.transform.localRotation = Quaternion.Euler(0,0,time*12+i*47);
            else art.transform.localPosition = origins[i] + Vector3.up * Mathf.Sin(time*.65f+i)*.65f;
            art.color = new Color(1,1,1,i <= wells.Length ? .78f+.14f*Mathf.Sin(time*2+i) : .78f);
        }
        for (int i=0;i<debris.Count;i++)
        {
            float progress = Mathf.Repeat(time/18f+i/(float)debris.Count,1);
            float radius = 30*(1-progress);
            float angle = i*2.4f+progress*Mathf.PI*4;
            var art = debris[i];
            art.transform.localPosition = new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0);
            art.transform.localRotation = Quaternion.Euler(0,0,time*24+i*37);
            float size = Mathf.Lerp(1.3f,.08f,progress)/Mathf.Max(art.sprite.bounds.size.x,art.sprite.bounds.size.y);
            art.transform.localScale = Vector3.one*size;
            art.color = new Color(.55f,.8f,1,.35f*Mathf.Min(progress*8,(1-progress)*5));
        }
    }
    // เปิด/ปิดคอมโพเนนต์: อัปเดตตัวแปร active ให้ PullAt รู้ว่าแม็พนี้ทำงานอยู่หรือไม่
    void OnDisable() { if (active == this) active = null; }
    // เปิดคอมโพเนนต์: ตั้งตัวนี้เป็น active ให้ PullAt ใช้คิดแรงดูด
    void OnEnable() { active = this; }
}

// คลาสสุ่มสร้าง Hazard ระหว่างแมตช์ ทำงานเฉพาะบน Master Client (เครื่องอื่นได้รับ Hazard ผ่าน Photon)
public class MapHazardManager : MonoBehaviourPunCallbacks
{
    // แม็พของห้องนี้ อ่านจาก Room Custom Property "MapIndex"
    private int mapIndex = 0;
    // Coroutine วนสร้าง Hazard ที่กำลังรัน (เก็บไว้เพื่อหยุดเมื่อเปลี่ยน Master)
    private Coroutine hazardLoop;

    // Photon Callback เรียกทุกเครื่องเมื่อ Master Client เปลี่ยน (เช่น Master เดิมหลุด)
    // หยุดลูปเดิม แล้วให้เครื่องที่เป็น Master คนใหม่เริ่มลูปสร้าง Hazard ต่อ
    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (hazardLoop != null) StopCoroutine(hazardLoop);
        hazardLoop = PhotonNetwork.IsMasterClient && MatchRules.Hazards(PhotonNetwork.CurrentRoom) ? StartCoroutine(SpawnHazardRoutine()) : null;
    }

    // Unity เรียกตอนเริ่ม Scene: อ่านเลขแม็พจากห้อง แล้วถ้าเป็น Master ให้สร้าง Hazard ถาวรและเริ่มลูปสุ่ม Hazard
    void Start()
    {
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("MapIndex", out object mapProp))
        {
            mapIndex = (int)mapProp;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            SpawnStaticHazards();
            // Host ปิด "อันตรายในแม็พ" ในห้องรอ = ไม่สุ่มสายฟ้า/บึง/อุกกาบาต (ของถาวรของแม็พยังอยู่)
            if (MatchRules.Hazards(PhotonNetwork.CurrentRoom)) hazardLoop = StartCoroutine(SpawnHazardRoutine());
        }
    }

    // สร้าง Hazard ถาวรตอนเริ่มแมตช์ (รันบน Master): แม็พ 0 สร้างแกนพลังงานไว้กลางแม็พ
    private void SpawnStaticHazards()
    {
        if (mapIndex == 1)
        {
            // Spawn only after layout and players have finished initializing.
        }
        // Obstacle ทั้งหมด (กำแพง, เสาหิน, Cover) ถูกสร้างใน GameplayManager.GenerateMapObstacles() แล้ว
        // ที่นี่เหลือแค่ Hazard พิเศษที่ต้อง Sync ผ่าน Network เท่านั้น
        if (mapIndex == 0)
        {
            // Energy Core ตรงกลาง (ดูดเลือด + บูสต์ Fire Rate)
            PhotonNetwork.InstantiateRoomObject("Hazard_EnergyCore", Vector3.zero, Quaternion.identity);
        }
    }

    // Coroutine หลักบน Master: รอจนแมตช์เริ่ม (MatchInputAllowed) แล้วรออีก 5 วิ
    // จากนั้นวนสร้าง Hazard ตามแม็พทุกช่วงเวลาสุ่ม จนกว่าจะออกจากห้องหรือไม่ได้เป็น Master แล้ว
    private IEnumerator SpawnHazardRoutine()
    {
        while (PhotonNetwork.InRoom && GameplayManager.Instance != null && !GameplayManager.Instance.MatchInputAllowed)
            yield return null;
        // Wait a few seconds before hazards start
        yield return new WaitForSeconds(5f);

        // ถ้า MatchInputAllowed เป็น false (ยังเล่นไม่ได้) ให้รอเฟรมถัดไป; ค่าเริ่มต้น: รอ 10 วิ, ตำแหน่งสุ่มในแม็พ
        while (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            if (GameplayManager.Instance == null || !GameplayManager.Instance.MatchInputAllowed)
            { yield return null; continue; }
            float waitTime = 10f;
            string hazardPrefabName = "";
            float hazardHalfWidth = mapIndex == 1 ? 40f : 30f;
            Vector3 spawnPos = new Vector3(Random.Range(-hazardHalfWidth, hazardHalfWidth), Random.Range(-28f, 28f), 0);

            // แม็พ 0: สายฟ้าผ่าใกล้ยานที่ยังไม่ตายแบบสุ่ม 1 ลำ (เยื้องสุ่มไม่เกิน 2 หน่วย, ไม่ชิดขอบ) ทุก 3–8 วิ
            if (mapIndex == 0) // Electric Jellyfish Core
            {
                var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                var active = System.Array.FindAll(players, p => !p.isDead);
                if (active.Length > 0)
                {
                    Vector2 target = active[Random.Range(0, active.Length)].transform.position;
                    target += Random.insideUnitCircle * 2f;
                    Vector2 min = GameplayManager.GetArenaMin(mapIndex);
                    Vector2 max = GameplayManager.GetArenaMax(mapIndex);
                    spawnPos = new Vector3(Mathf.Clamp(target.x, min.x + 2, max.x - 2), Mathf.Clamp(target.y, min.y + 2, max.y - 2), 0);
                }
                hazardPrefabName = "Hazard_Lightning";
                waitTime = Random.Range(3f, 8f);
            }
            // แม็พ 1: เติมบึงชะลอ (ใน SpawnPrismSwamp) ตรวจทุก 4–6 วิ
            else if (mapIndex == 1) // Obelisk Plains
            {
                SpawnPrismSwamp();
                hazardPrefabName = ""; // Permanent authored swamp regions are created once above.
                waitTime = Random.Range(4f, 6f);
            }
            // แม็พ 2: อุกกาบาตลาวาเกิดเหนือขอบบนของแม็พ 3 หน่วย ตำแหน่ง x สุ่ม ทุก 3–6 วิ
            else if (mapIndex == 2) // Abandoned Mech Warzone
            {
                hazardPrefabName = "Hazard_MoltenAsteroid";
                Vector2 min = GameplayManager.GetArenaMin(mapIndex);
                Vector2 max = GameplayManager.GetArenaMax(mapIndex);
                spawnPos = new Vector3(Random.Range(min.x + 1f, max.x - 1f), max.y + 3f, 0);
                waitTime = Random.Range(3f, 6f); // เกิดถี่หน่อย
            }

            // แม็พ 3 (สถานีอวกาศ): อุกกาบาตตกจุดสุ่มทุก 5–9 วิ
            else if (mapIndex == GameplayManager.StationMapIndex)
            {
                hazardPrefabName = "Hazard_Meteor";
                Vector2 min = GameplayManager.GetArenaMin(mapIndex), max = GameplayManager.GetArenaMax(mapIndex);
                spawnPos = new Vector3(Random.Range(min.x + 4f, max.x - 4f), Random.Range(min.y + 4f, max.y - 4f), 0);
                waitTime = Random.Range(5f, 9f);
            }
            // แม็พ 4 (ลาวา): อุกกาบาตลาวาร่วงจากขอบบนเหมือนแม็พหุ่นยนต์
            else if (mapIndex == GameplayManager.LavaMapIndex)
            {
                hazardPrefabName = "Hazard_MoltenAsteroid";
                Vector2 min = GameplayManager.GetArenaMin(mapIndex), max = GameplayManager.GetArenaMax(mapIndex);
                spawnPos = new Vector3(Random.Range(min.x + 1f, max.x - 1f), max.y + 3f, 0);
                waitTime = Random.Range(3.5f, 6.5f);
            }

            // สร้าง Hazard เป็น Room Object (เป็นของห้อง ไม่หายเมื่อคนสร้างหลุด) ทุกเครื่องเห็นเหมือนกัน
            if (!string.IsNullOrEmpty(hazardPrefabName))
            {
                PhotonNetwork.InstantiateRoomObject(hazardPrefabName, spawnPos, Quaternion.identity);
            }

            yield return new WaitForSeconds(waitTime);
        }
    }

    // สร้างบึงชะลอบนแม็พปริซึม (รันบน Master): ถ้ามีบึงอยู่ครบ 6 แล้วไม่สร้างเพิ่ม
    // ไม่งั้นสุ่มจุดในครึ่งล่างของแม็พ (สูงสุด 60 ครั้ง) ที่ไม่ใกล้จุด (0,-16) และไม่ทับสิ่งของในรัศมี 3
    // แล้วสร้างบึง 2 อันที่จุดนั้นและจุดสะท้อน 180 องศา (data: [false = ไม่ถาวร, variant ภาพ 85–87])
    private void SpawnPrismSwamp()
    {
        int count = 0;
        foreach (var hazard in FindObjectsByType<HazardController>(FindObjectsSortMode.None))
            if (hazard.type == HazardController.HazardType.SlowZone) count++;
        if (count >= 6) return;
        Vector2 min = GameplayManager.GetArenaMin(1), max = GameplayManager.GetArenaMax(1);
        for (int attempt = 0; attempt < 60; attempt++)
        {
            // เกิดเป็นคู่สมมาตร 180 องศา (เหมือนเลย์เอาต์) เดิมเกิดแค่ครึ่งล่าง = ฝั่ง Host เสียเปรียบ
            Vector2 point = new Vector2(Random.Range(min.x + 6, max.x - 6), Random.Range(min.y + 6, -9));
            Vector2 mirror = -point;
            if (Vector2.Distance(point, new Vector2(0,-16)) < 9) continue;
            if (Physics2D.OverlapCircleAll(point, 3).Length > 0 || Physics2D.OverlapCircleAll(mirror, 3).Length > 0) continue;
            int variant = Random.Range(85,88);
            PhotonNetwork.InstantiateRoomObject("Hazard_SlowZone", point, Quaternion.identity, 0,
                new object[] { false, variant });
            PhotonNetwork.InstantiateRoomObject("Hazard_SlowZone", mirror, Quaternion.identity, 0,
                new object[] { false, variant });
            break;
        }
    }
}
