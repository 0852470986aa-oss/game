// ไฟล์ PrismArenaVisuals.cs: ภาพและเอฟเฟกต์ตกแต่งแม็พปริซึม (แม็พ 1) + คลาส PrismFx สร้างภาพด้วยโค้ด
// GameplayManager.ApplySelectedMapLayout (GameplayManager.Maps.cs) เป็นคนเพิ่มคอมโพเนนต์นี้ให้ Map1_Layout
// รันแยกบนแต่ละเครื่อง ไม่ซิงก์ผ่าน Photon (ใช้ Random ได้ เพราะไม่มีผลกับเกมเพลย์)
using UnityEngine;
using System.Collections.Generic;

// ภาพบรรยากาศแม็พปริซึม (เฉพาะภาพ ไม่มีผลต่อเกม): ถูกเพิ่มให้ Map1_Layout ตอนเริ่มแมตช์
// - พื้นสนามมุมบนแบบสร้างด้วยโค้ด (แทนภาพวิวมุมข้างที่มีท้องฟ้า) + ลายตาข่ายคริสตัล + ขอบมืด
// - ละอองแสงลอยในอากาศ
// - แสงเรืองที่รูนบนเสา และแสงรอบกลุ่มคริสตัล (บอกว่าคริสตัลสะท้อนกระสุนได้)
// - รุ่น 3 (FeatureFlags.PrismMapV3): พื้นหลังเนบิวลามุมบน, แสงเรืองที่เสากลาง/กองคริสตัล, แท่นวาร์ปหมุน, เศษคริสตัลโคจรเป็นวงกลม
// คอมโพเนนต์หลักของภาพบรรยากาศแม็พปริซึม ติดอยู่ที่ Map1_Layout
public class PrismArenaVisuals : MonoBehaviour
{
    // ข้อมูลแสงเรืองที่กะพริบเป็นจังหวะ (ภาพเรือง, สี, เฟส, ความทึบสูงสุด, ความเร็วกะพริบ)
    private struct Pulse
    {
        public SpriteRenderer glow; // ภาพแสงเรืองที่กะพริบ
        public Color color; // สีของแสงเรือง
        public float phase, alpha, speed; // เฟสเริ่ม, ความทึบสูงสุด, ความเร็วกะพริบ
    }
    private readonly List<Pulse> pulses = new List<Pulse>(); // แสงเรืองทั้งหมดที่ต้องกะพริบทุกเฟรม

    // ของที่ขยับ (ภาพล้วน ไม่มีตัวชน ไม่ส่งผ่านเน็ต)
    // Bob = ของลอยขึ้นลง, Shard = เศษคริสตัลโคจรรอบเสากลาง, Mist = หมอกลอยผ่านสนาม
    private struct Bob { public Transform item; public Vector3 origin; public float phase; }
    // เศษคริสตัล 1 ชิ้น: รัศมีวงโคจร ความเร็วโคจร มุมเริ่มต้น ความเร็วหมุนตัว และเฟสการลอยขึ้นลง
    private struct Shard { public SpriteRenderer art; public float radius, speed, angle, spin, bob; }
    // หมอก 1 ก้อน: ความเร็วเลื่อนไปทางขวา ความทึบสูงสุด และเฟสของการจางเข้าออก
    private struct Mist { public SpriteRenderer art; public float speed, alpha, phase; }
    private readonly List<Bob> bobs = new List<Bob>(); // ของที่ลอยขึ้นลง
    private readonly List<Shard> shards = new List<Shard>(); // เศษคริสตัลที่โคจรรอบเสากลาง
    private readonly List<Mist> mists = new List<Mist>(); // หมอกที่ลอยผ่านสนาม
    private readonly List<SpriteRenderer> sparkleTargets = new List<SpriteRenderer>(); // ภาพคริสตัลที่สุ่มเกิดประกายแสง
    // เฟส 7B: หินลอยวนรอบเสาแต่ละต้น (ปิดได้ด้วย FeatureFlags.PillarRocks)
    private struct PillarRock { public SpriteRenderer art; public Vector2 center; public float radius, speed, angle, spin, bob, baseOrder; }
    private readonly List<PillarRock> pillarRocks = new List<PillarRock>(); // หินลอยวนรอบเสาแต่ละต้น
    private readonly List<Bounds> pillarBounds = new List<Bounds>(); // ขอบเขตเสาแต่ละต้น ใช้วางหินลอยรอบเสา
    private Vector2 arenaMin, arenaMax; // มุมล่างซ้ายและบนขวาของสนาม
    private float nextSparkle; // เวลาที่จะเกิดประกายแสงครั้งถัดไป
    // รุ่น 3: ภาพที่หมุนอยู่กับที่ (ลายวนบนแท่นวาร์ป) และโหมดมุมบน (เศษคริสตัลโคจรเป็นวงกลม ไม่แบนแบบมุมเอียง)
    private struct Spinner { public Transform item; public float speed; }
    private readonly List<Spinner> spinners = new List<Spinner>(); // ภาพที่หมุนอยู่กับที่ เช่น ลายบนแท่นวาร์ป
    private bool topDown; // true = แม็พรุ่น 3 แบบมุมบน

    // false = ใช้ภาพพื้นหลังเดิมของแม็พ (Map_ObeliskPlains) / true = ใช้พื้นมุมบนที่สร้างด้วยโค้ด
    public static bool UseGeneratedGround = false;
    // ใช้ภาพเดิม: เอาส่วนล่างของภาพ (พื้นทะเลสาบ) มาเป็นพื้นสนาม ไม่ให้เห็นท้องฟ้า
    // .45 = ใช้ 45% ล่างของภาพ / เพิ่มค่า = เห็นภาพมากขึ้น (มีภูเขา/ท้องฟ้า) แต่คมขึ้น
    public static float GroundFraction = .45f;

    // เรียกครั้งเดียวตอนเริ่ม: จัดพื้นหลัง, สร้างละอองแสง, ตกแต่งชิ้นส่วนใน PrismPlayableLayout_v2/_v3, สร้างเศษคริสตัลและหมอก
    private void Start()
    {
        int mapIndex = GameplayManager.GetCurrentMapIndex();
        Vector2 min = GameplayManager.GetArenaMin(mapIndex), max = GameplayManager.GetArenaMax(mapIndex);
        Vector2 arena = max - min;
        topDown = FeatureFlags.PrismMapV3;
        bool nebula = topDown && UseNebulaBackground(min, max);
        if (!nebula && UseGeneratedGround) BuildGround(arena);
        else if (!nebula) PlaceBackgroundOnGround(min, max);
        BuildMotes(arena);
        var layout = transform.Find(GameplayManager.PrismLayoutName);
        if (layout != null)
            foreach (Transform item in layout) Decorate(item);
        arenaMin = min;
        arenaMax = max;
        BuildOrbitingShards();
        BuildMist();
        BuildPillarRocks();
    }

    // หินคริสตัลเล็ก 3 ก้อนลอยวนรอบเสาทุกต้น (วงรีแบนให้ดูเหมือนมุมเอียง ครึ่งบนอยู่หลังเสา ครึ่งล่างอยู่หน้าเสา)
    private void BuildPillarRocks()
    {
        if (!FeatureFlags.PillarRocks || pillarBounds.Count == 0) return;
        var sheet = Resources.LoadAll<Sprite>("Images/Obs_Crystals");
        var pieces = new List<Sprite>();
        foreach (var sprite in sheet)
            if (sprite.name == "Obs_Crystals_8" || sprite.name == "Obs_Crystals_10" || sprite.name == "Obs_Crystals_0") pieces.Add(sprite);
        if (pieces.Count == 0) return;
        foreach (var bounds in pillarBounds)
            for (int i = 0; i < 3; i++)
            {
                var piece = pieces[(i + pillarRocks.Count) % pieces.Count];
                var art = PrismFx.Sprite("PillarRock", transform, piece, 3);
                float size = Random.Range(.35f, .7f);
                art.transform.localScale = Vector3.one * size / Mathf.Max(piece.bounds.size.y, .01f) / Mathf.Max(.001f, transform.lossyScale.x);
                art.color = new Color(.8f, .92f, 1f, .95f);
                pillarRocks.Add(new PillarRock
                {
                    art = art,
                    center = new Vector2(bounds.center.x, bounds.min.y + bounds.size.y * .55f),
                    radius = Mathf.Max(1.2f, bounds.extents.x * 1.25f),
                    speed = Random.Range(.6f, 1.1f) * (Random.value < .5f ? -1f : 1f),
                    angle = i * Mathf.PI * 2f / 3f + Random.value,
                    spin = Random.Range(-90f, 90f),
                    bob = Random.Range(0, 6f),
                    baseOrder = 3
                });
            }
    }

    // ทุกเฟรม: อัปเดตแอนิเมชันภาพล้วน (แสงกะพริบ, หินลอย, เศษคริสตัลโคจร, หมอกเลื่อน, ประกายสุ่มบนคริสตัล)
    private void Update()
    {
        float time = Time.time;
        // 1) แสงเรืองกะพริบตามคลื่น sin
        foreach (var pulse in pulses)
        {
            if (pulse.glow == null) continue;
            float wave = .62f + .38f * Mathf.Sin(time * pulse.speed + pulse.phase);
            pulse.glow.color = new Color(pulse.color.r, pulse.color.g, pulse.color.b, pulse.alpha * wave);
        }
        // 2) หินคริสตัลห้อยลอยขึ้นลง ±0.3 หน่วย
        foreach (var bob in bobs)
            if (bob.item != null)
                bob.item.localPosition = bob.origin + Vector3.up * Mathf.Sin(time * 1.3f + bob.phase) * .3f;
        // 3) เศษคริสตัลโคจรเป็นวงรีรอบกลางสนาม
        for (int i = 0; i < shards.Count; i++)
        {
            var shard = shards[i];
            if (shard.art == null) continue;
            float angle = shard.angle + time * shard.speed;
            if (topDown)
            {
                // รุ่น 3 มองจากด้านบน: โคจรเป็นวงกลมรอบเสากลาง รัศมีขยับเข้าออกเล็กน้อย อยู่ชั้นบนเสมอ
                float radius = shard.radius + Mathf.Sin(time * 1.3f + shard.bob) * .4f;
                shard.art.transform.position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                shard.art.transform.rotation = Quaternion.Euler(0, 0, time * shard.spin);
                continue;
            }
            // วงรีแบน ให้ดูเหมือนวนรอบเสาในมุมเอียง; ครึ่งบนอยู่หลังเสา ครึ่งล่างอยู่หน้าเสา
            Vector3 orbit = new Vector3(Mathf.Cos(angle) * shard.radius, Mathf.Sin(angle) * shard.radius * .45f + 1.5f
                + Mathf.Sin(time * 1.7f + shard.bob) * .35f, 0);
            shard.art.transform.position = orbit;
            shard.art.transform.rotation = Quaternion.Euler(0, 0, time * shard.spin);
            shard.art.sortingOrder = Mathf.Sin(angle) > 0 ? 1 : 3;
        }
        // 3.5) หินลอยวนรอบเสา
        foreach (var rock in pillarRocks)
        {
            if (rock.art == null) continue;
            float a = rock.angle + time * rock.speed;
            rock.art.transform.position = new Vector3(rock.center.x + Mathf.Cos(a) * rock.radius,
                rock.center.y + Mathf.Sin(a) * rock.radius * .35f + Mathf.Sin(time * 2f + rock.bob) * .2f, 0);
            rock.art.transform.rotation = Quaternion.Euler(0, 0, time * rock.spin);
            rock.art.sortingOrder = Mathf.Sin(a) > 0 ? 1 : 6;
        }
        // 3.7) รุ่น 3: ลายวนบนแท่นวาร์ปหมุนช้า ๆ
        foreach (var spinner in spinners)
            if (spinner.item != null) spinner.item.Rotate(0, 0, spinner.speed * Time.deltaTime);
        // 4) หมอกเลื่อนไปทางขวา เลยขอบแล้ววนกลับมาฝั่งซ้าย
        float width = arenaMax.x - arenaMin.x;
        foreach (var mist in mists)
        {
            if (mist.art == null) continue;
            var position = mist.art.transform.position;
            position.x += mist.speed * Time.deltaTime;
            if (position.x > arenaMax.x + 10f) position.x -= width + 20f;
            mist.art.transform.position = position;
            mist.art.color = new Color(1, 1, 1, mist.alpha * (.7f + .3f * Mathf.Sin(time * .4f + mist.phase)));
        }
        // 5) ทุก 0.25-0.6 วิ สุ่มประกายแสงบนคริสตัลสักก้อน
        if (time >= nextSparkle && sparkleTargets.Count > 0)
        {
            nextSparkle = time + Random.Range(.25f, .6f);
            var target = sparkleTargets[Random.Range(0, sparkleTargets.Count)];
            if (target != null)
            {
                Bounds bounds = target.bounds;
                // มุมบน: สุ่มทั้งก้อน (ย่อขอบเข้ามา 30%) / มุมข้าง: ครึ่งบนของภาพ
                if (topDown) bounds.extents *= .7f;
                Vector2 point = new Vector2(Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(topDown ? bounds.min.y : bounds.center.y, bounds.max.y));
                PrismFx.Burst(point, new Color(.85f, 1f, 1f), Random.Range(.6f, 1.1f));
            }
        }
    }

    // สร้างเศษคริสตัล 12 ชิ้น (สูง 0.7-1.4 หน่วย) โคจรรอบกลางสนามรัศมี 6.5-10 หน่วย
    private void BuildOrbitingShards()
    {
        var pieces = new List<Sprite>();
        // รุ่น 3: เศษคริสตัลมุมบนชุดใหม่ (pr_shard_a/b/c)
        if (topDown)
            foreach (var key in new[] { "pr_shard_a", "pr_shard_b", "pr_shard_c" })
            {
                var sprite = Resources.Load<Sprite>(GameplayManager.PrismArtFolder + key);
                if (sprite != null) pieces.Add(sprite);
            }
        if (pieces.Count == 0)
            foreach (var sprite in Resources.LoadAll<Sprite>("Images/Obs_Crystals"))
                if (sprite.name == "Obs_Crystals_8" || sprite.name == "Obs_Crystals_10" || sprite.name == "Obs_Crystals_0")
                    pieces.Add(sprite);
        if (pieces.Count == 0) return;
        for (int i = 0; i < 12; i++)
        {
            var piece = pieces[i % pieces.Count];
            var art = PrismFx.Sprite("OrbitingShard", transform, piece, 3);
            float size = Random.Range(.7f, 1.4f);
            art.transform.localScale = Vector3.one * size / Mathf.Max(piece.bounds.size.y, .01f) / Mathf.Max(.001f, transform.lossyScale.x);
            art.color = new Color(.85f, .95f, 1f, .9f);
            // มุมบน: เสากลางกว้าง 12 หน่วย ให้โคจรนอกแท่น (รัศมี 7.5-9.5)
            float orbitMin = topDown ? 7.5f : 6.5f, orbitMax = topDown ? 9.5f : 10f;
            shards.Add(new Shard { art = art, radius = Random.Range(orbitMin, orbitMax), speed = Random.Range(.18f, .35f),
                angle = i * Mathf.PI * 2f / 12f, spin = Random.Range(-40f, 40f), bob = Random.Range(0, 6f) });
        }
    }

    // สร้างหมอก 7 ก้อน (กว้าง 12-20 สูง 3.5-6 หน่วย) วางสุ่มในสนาม ความทึบต่ำ
    private void BuildMist()
    {
        for (int i = 0; i < 7; i++)
        {
            var art = PrismFx.Sprite("LakeMist", transform, PrismFx.Dot(), -1);
            art.transform.position = new Vector3(Random.Range(arenaMin.x, arenaMax.x), Random.Range(arenaMin.y + 4, arenaMax.y - 4), 0);
            art.transform.localScale = new Vector3(Random.Range(12f, 20f), Random.Range(3.5f, 6f), 1) / PrismFx.DotWorldSize
                / Mathf.Max(.001f, transform.lossyScale.x);
            mists.Add(new Mist { art = art, speed = Random.Range(.4f, .9f), alpha = Random.Range(.07f, .13f), phase = Random.Range(0, 6f) });
        }
    }

    // รุ่น 3: เปลี่ยนพื้นหลังเป็นเนบิวลามุมบน (Map_PrismNebula) ขยายให้คลุมสนาม + ขอบ 6 หน่วยทุกด้าน วางกลางสนาม
    // คืน false ถ้าไม่มีภาพ/ไม่มี Background (จะใช้ภาพเดิมแทน)
    private bool UseNebulaBackground(Vector2 min, Vector2 max)
    {
        var background = transform.Find("Background");
        var art = background != null ? background.GetComponent<SpriteRenderer>() : null;
        var sprite = Resources.Load<Sprite>(GameplayManager.PrismV3Background);
        if (art == null || sprite == null) return false;
        art.sprite = sprite;
        art.drawMode = SpriteDrawMode.Simple;
        art.flipX = art.flipY = false;
        const float margin = 6f; // ขยายพื้นหลังเกินขอบสนามกันเห็นขอบภาพ
        Vector2 cover = max - min + Vector2.one * margin * 2f;
        Vector2 spriteSize = sprite.bounds.size;
        float scale = Mathf.Max(cover.x / Mathf.Max(.01f, spriteSize.x), cover.y / Mathf.Max(.01f, spriteSize.y));
        Vector3 parentScale = background.parent != null ? background.parent.lossyScale : Vector3.one;
        background.localRotation = Quaternion.identity;
        background.localScale = new Vector3(scale / Mathf.Max(.001f, parentScale.x), scale / Mathf.Max(.001f, parentScale.y), 1);
        Bounds bounds = art.bounds;
        background.position += new Vector3((min.x + max.x) * .5f - bounds.center.x, (min.y + max.y) * .5f - bounds.center.y, 0);
        return true;
    }

    // รุ่น 3: ตกแต่งชิ้นที่มีเฉพาะมุมบน คืน true = จัดการแล้ว (ปริซึม Crystal_ ใช้ทางเดิมด้านล่าง)
    // CentralCore = แสงที่ยอดโอเบลิสก์ + วงรูนเรือง, Cluster_ = แสงเรืองใต้กอง + ประกาย, WarpPad_ = ลายวนหมุน
    private bool DecorateTopDown(Transform item, SpriteRenderer art, Bounds bounds)
    {
        if (item.name == "CentralCore")
        {
            var cyan = new Color(.45f, .95f, 1f);
            var apex = PrismFx.CreateGlow(item, bounds.center, bounds.size.x * .32f, cyan, art.sortingOrder + 1);
            pulses.Add(new Pulse { glow = apex, color = cyan, alpha = .55f, speed = 2.2f, phase = 0 });
            var ring = PrismFx.CreateGlow(item, bounds.center, bounds.size.x * 1.15f, cyan, art.sortingOrder - 1);
            pulses.Add(new Pulse { glow = ring, color = cyan, alpha = .22f, speed = 1.1f, phase = 1.5f });
            sparkleTargets.Add(art);
            return true;
        }
        if (item.name.StartsWith("Cluster_"))
        {
            sparkleTargets.Add(art);
            // กองโทนม่วง (cluster_b, แนวสัน) เรืองม่วง ที่เหลือเรืองฟ้า
            bool violet = art.sprite.name.Contains("cluster_b") || art.sprite.name.Contains("ridge");
            var tint = violet ? new Color(.75f, .55f, 1f) : new Color(.45f, .9f, 1f);
            var glow = PrismFx.CreateGlow(item, bounds.center, Mathf.Max(bounds.size.x, bounds.size.y) * 1.25f, tint, art.sortingOrder - 1);
            pulses.Add(new Pulse { glow = glow, color = tint, alpha = .2f, speed = 1.3f, phase = item.position.x * .2f + item.position.y * .1f });
            return true;
        }
        if (item.name.StartsWith("WarpPad_") && art.sprite.name == "pr_warp")
        {
            spinners.Add(new Spinner { item = art.transform, speed = -40f });
            return true;
        }
        return false;
    }

    // ใช้ภาพพื้นหลังเดิม: ขยายให้ส่วนล่างของภาพ (GroundFraction) ครอบสนาม + ขอบ 1.5 หน่วย
    private void PlaceBackgroundOnGround(Vector2 min, Vector2 max)
    {
        var background = transform.Find("Background");
        var art = background != null ? background.GetComponent<SpriteRenderer>() : null;
        if (art == null || art.sprite == null) return;
        const float margin = 1.5f; // ระยะเผื่อรอบสนามตอนขยายพื้นหลัง
        Vector2 spriteSize = art.sprite.bounds.size;
        float worldHeight = (max.y - min.y + margin * 2) / Mathf.Clamp(GroundFraction, .2f, 1f);
        float scale = Mathf.Max(worldHeight / spriteSize.y, (max.x - min.x + margin * 2) / spriteSize.x);
        Vector3 parentScale = background.parent != null ? background.parent.lossyScale : Vector3.one;
        background.localScale = new Vector3(scale / Mathf.Max(.001f, parentScale.x), scale / Mathf.Max(.001f, parentScale.y), 1);
        // ขอบล่างของภาพชิดขอบล่างสนาม และอยู่กึ่งกลางแนวนอน
        Bounds bounds = art.bounds;
        background.position += new Vector3((min.x + max.x) * .5f - bounds.center.x, (min.y - margin) - bounds.min.y, 0);
    }

    // โหมดพื้นสร้างด้วยโค้ด (UseGeneratedGround = true): ปิดภาพเดิม แล้ววางพื้น + ลายตาข่าย + ขอบมืด
    private void BuildGround(Vector2 arena)
    {
        // ปิดภาพวิวมุมข้าง (ท้องฟ้า/ภูเขา) ของแม็พนี้ แล้ววางพื้นมุมบนแทน
        var background = transform.Find("Background");
        var backgroundArt = background != null ? background.GetComponent<SpriteRenderer>() : null;
        if (backgroundArt != null) backgroundArt.enabled = false;

        Vector2 cover = arena + Vector2.one * 8f;
        var floor = PrismFx.Sprite("PrismFloor", transform, PrismFx.FloorSprite(), -9);
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(cover.x / floor.sprite.bounds.size.x, cover.y / floor.sprite.bounds.size.y, 1)
            / Mathf.Max(.001f, transform.lossyScale.x);

        var lattice = PrismFx.Sprite("PrismLattice", transform, PrismFx.LatticeSprite(), -8);
        lattice.transform.position = Vector3.zero;
        lattice.transform.localScale = Vector3.one / Mathf.Max(.001f, transform.lossyScale.x);
        lattice.drawMode = SpriteDrawMode.Tiled;
        lattice.tileMode = SpriteTileMode.Continuous;
        lattice.size = cover;
        lattice.color = new Color(.45f, .9f, 1f, .07f);

        var vignette = PrismFx.Sprite("PrismVignette", transform, PrismFx.VignetteSprite(), -7);
        vignette.transform.position = Vector3.zero;
        vignette.transform.localScale = new Vector3(arena.x / vignette.sprite.bounds.size.x, arena.y / vignette.sprite.bounds.size.y, 1)
            / Mathf.Max(.001f, transform.lossyScale.x);
    }

    // สร้าง ParticleSystem ละอองแสงลอยขึ้นช้าๆ ทั่วสนาม (สูงสุด 160 จุด, ปล่อย 16 จุด/วิ, อยู่ 6-10 วิ)
    private void BuildMotes(Vector2 arena)
    {
        var holder = new GameObject("PrismMotes");
        holder.transform.SetParent(transform, false);
        holder.transform.position = Vector3.zero;
        var motes = holder.AddComponent<ParticleSystem>();
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // 1) ค่าหลักของอนุภาค: วนซ้ำ, ไม่มีความเร็วเริ่ม, ขนาด/สีสุ่ม, อยู่ในพิกัดโลก
        var main = motes.main;
        main.loop = true;
        main.duration = 10f;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.12f, .34f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.5f, .95f, 1f, .6f), new Color(.85f, .6f, 1f, .55f));
        main.maxParticles = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // 2) จุดปล่อยเป็นกล่องเท่าขนาดสนาม
        var emission = motes.emission;
        emission.rateOverTime = 16f;
        var shape = motes.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(arena.x, arena.y, .1f);
        // 3) ลอยซ้าย-ขวาเล็กน้อยและขึ้นข้างบน
        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.25f, .25f);
        velocity.y = new ParticleSystem.MinMaxCurve(.05f, .4f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        // 4) ค่อยๆ ปรากฏแล้วจางหายตามอายุ
        var fade = motes.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .25f), new GradientAlphaKey(1f, .7f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);
        // 5) ใช้วัสดุจุดแสงฟุ้งแล้วเริ่มเล่น
        var renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.material = PrismFx.DotMaterial();
        renderer.sortingOrder = -1;
        motes.Play();
    }

    // ตกแต่งชิ้นส่วน 1 ชิ้นในเลย์เอาต์ตามชื่อ: หินห้อย → ลอยขึ้นลง, คริสตัล → แสงเรือง+ประกาย+เติม PrismReflector, เสา → แสงรูน
    private void Decorate(Transform item)
    {
        var artTransform = item.Find("Artwork");
        var art = artTransform != null ? artTransform.GetComponent<SpriteRenderer>() : null;
        if (art == null || art.sprite == null) return;
        Bounds bounds = art.bounds;
        if (topDown && DecorateTopDown(item, art, bounds)) return;
        if (item.name.StartsWith("HangingCrystalRock_"))
        {
            bobs.Add(new Bob { item = item, origin = item.localPosition, phase = item.position.x * .2f + item.position.y });
            return;
        }
        if (item.name.StartsWith("Crystal_"))
        {
            sparkleTargets.Add(art);
            // เลย์เอาต์ที่บันทึกใน Scene ก่อนมีระบบสะท้อน: เติมให้
            if (art.GetComponent<PrismReflector>() == null && art.GetComponent<Collider2D>() != null)
                art.gameObject.AddComponent<PrismReflector>();
            // มุมบน: ปริซึมเล็กกว่ารุ่นเดิม แสงเรือง 1.5 เท่าพอ (รุ่นเดิม 2 เท่า)
            var glow = PrismFx.CreateGlow(item, bounds.center, Mathf.Max(bounds.size.x, bounds.size.y) * (topDown ? 1.5f : 2f),
                PrismReflector.FlashColor, art.sortingOrder - 1);
            pulses.Add(new Pulse { glow = glow, color = PrismReflector.FlashColor, alpha = .45f, speed = 1.6f, phase = item.position.x });
        }
        else if (item.name.StartsWith("Pillar_") || item.name.StartsWith("CentralPillar"))
        {
            pillarBounds.Add(bounds);
            Vector3 rune = bounds.center + Vector3.up * bounds.size.y * .04f;
            var color = new Color(.4f, .9f, 1f);
            var glow = PrismFx.CreateGlow(item, rune, bounds.size.x * 1.1f, color, art.sortingOrder + 1);
            pulses.Add(new Pulse { glow = glow, color = color, alpha = .5f, speed = 2.2f, phase = item.position.x * .3f + item.position.y });
        }
    }
}

// ภาพที่สร้างด้วยโค้ด (ไม่ต้องมีไฟล์ภาพ) ใช้ร่วมกันในแม็พปริซึม
public static class PrismFx
{
    public const float DotWorldSize = 1f; // ขนาดจุดแสงในหน่วยโลก
    private static Sprite dot, floor, lattice, vignette; // Sprite ที่สร้างด้วยโค้ด (แคชไว้สร้างครั้งเดียว)
    private static Material dotMaterial; // Material ของจุดแสงสำหรับ Particle

    // สร้าง GameObject ที่มี SpriteRenderer พร้อมลำดับการวาด (sortingOrder)
    public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, int order)
    {
        var item = new GameObject(name);
        item.transform.SetParent(parent, false);
        var renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    // สร้างแสงเรือง (จุดฟุ้ง) ขนาด worldSize หน่วยโลก ณ ตำแหน่งที่กำหนด ชดเชย scale ของ parent
    public static SpriteRenderer CreateGlow(Transform parent, Vector3 worldPosition, float worldSize, Color color, int order)
    {
        var glow = Sprite("Glow", parent, Dot(), order);
        glow.transform.position = worldPosition;
        glow.transform.localScale = Vector3.one * (worldSize / DotWorldSize) / Mathf.Max(.001f, parent.lossyScale.x);
        glow.color = new Color(color.r, color.g, color.b, .3f);
        return glow;
    }

    // สร้างแสงวาบชั่วคราวที่ตำแหน่ง (ใช้ตอนกระสุนเด้งคริสตัล/วาร์ป) คอมโพเนนต์ PrismBurst จะลบตัวเองเมื่อจบ
    public static void Burst(Vector2 position, Color color, float worldSize, int order = 20)
    {
        var flash = Sprite("PrismBurst", null, Dot(), order);
        flash.transform.position = position;
        flash.gameObject.AddComponent<PrismBurst>().Begin(color, worldSize);
    }

    // วัสดุสำหรับ ParticleSystem ที่ใช้ภาพจุดฟุ้ง (สร้างครั้งเดียวแล้วแคชไว้)
    public static Material DotMaterial()
    {
        if (dotMaterial == null)
        {
            dotMaterial = new Material(Shader.Find("Sprites/Default"));
            dotMaterial.mainTexture = Dot().texture;
        }
        return dotMaterial;
    }

    // จุดแสงฟุ้งกลม (ขนาด 1 หน่วยโลก)
    public static Sprite Dot()
    {
        if (dot != null) return dot;
        const int size = 64; // ขนาดภาพจุดแสง (พิกเซล)
        var texture = NewTexture(size, size);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                float fade = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1, 1, 1, fade * fade);
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        dot = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size / DotWorldSize, 0, SpriteMeshType.FullRect);
        return dot;
    }

    // พื้นสนาม: น้ำเงินอมม่วงเข้ม มีแอ่งแสงฟ้า/ม่วงนุ่มๆ วางสมมาตร 180 องศา (ภาพเบลอได้ ไม่แตกเมื่อขยาย)
    public static Sprite FloorSprite()
    {
        if (floor != null) return floor;
        const int w = 256, h = 204; // ขนาดภาพพื้นสนาม (พิกเซล)
        var texture = NewTexture(w, h);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[w * h];
        Color edge = new Color(.035f, .03f, .09f), middle = new Color(.05f, .07f, .14f);
        var pools = new (Vector2 center, float radius, Color color)[] {
            (new Vector2(.5f, .5f), .30f, new Color(.07f, .26f, .30f)),
            (new Vector2(.2f, .25f), .22f, new Color(.20f, .07f, .28f)),
            (new Vector2(.8f, .75f), .22f, new Color(.20f, .07f, .28f)),
            (new Vector2(.82f, .22f), .18f, new Color(.05f, .12f, .30f)),
            (new Vector2(.18f, .78f), .18f, new Color(.05f, .12f, .30f)),
        };
        float aspect = (float)w / h;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 uv = new Vector2((x + .5f) / w, (y + .5f) / h);
                Color color = Color.Lerp(middle, edge, Mathf.Abs(uv.y - .5f) * 2f);
                foreach (var pool in pools)
                {
                    Vector2 delta = uv - pool.center;
                    delta.x *= aspect;
                    float d = delta.magnitude / pool.radius;
                    color += pool.color * Mathf.Exp(-d * d * 2.2f);
                }
                color.a = 1;
                pixels[y * w + x] = color;
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        floor = UnityEngine.Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
        return floor;
    }

    // ลายตาข่ายสามเหลี่ยม (เหมือนผิวคริสตัล) ต่อกันได้ไม่มีรอย ช่องละ 5 หน่วยโลก
    public static Sprite LatticeSprite()
    {
        if (lattice != null) return lattice;
        const int w = 64; // ความกว้างภาพลายตาข่าย (พิกเซล)
        int h = Mathf.RoundToInt(w * 1.7320508f);
        float spacing = w * .8660254f;
        var texture = NewTexture(w, h);
        texture.wrapMode = TextureWrapMode.Repeat;
        var pixels = new Color32[w * h];
        Vector2[] normals = { new Vector2(0, 1), new Vector2(.8660254f, .5f), new Vector2(-.8660254f, .5f) };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f);
                float nearest = float.MaxValue;
                foreach (var n in normals)
                {
                    float units = Vector2.Dot(p, n) / spacing;
                    nearest = Mathf.Min(nearest, Mathf.Abs(units - Mathf.Round(units)) * spacing);
                }
                pixels[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01(1.3f - nearest));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        lattice = UnityEngine.Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), w / 5f, 0, SpriteMeshType.FullRect);
        return lattice;
    }

    // ขอบสนามมืดลง ให้สายตาโฟกัสกลางจอ
    public static Sprite VignetteSprite()
    {
        if (vignette != null) return vignette;
        const int size = 128; // ขนาดภาพขอบมืด (พิกเซล)
        var texture = NewTexture(size, size);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                pixels[y * size + x] = new Color(0, 0, 0, Mathf.SmoothStep(0, .6f, Mathf.InverseLerp(.45f, 1f, r)));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        vignette = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
        return vignette;
    }

    // สร้าง Texture แบบ RGBA32 ไม่มี mipmap กรองแบบ Bilinear
    private static Texture2D NewTexture(int width, int height)
        => new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
}
