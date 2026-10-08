// MapAmbience.cs — ของขยับ ๆ บนพื้นหลังแม็พใหม่ (ภาพล้วน ไม่มีตัวชน ไม่ส่งผ่านเน็ต แต่ละเครื่องเล่นเอง) (FeatureFlags.MapAmbience)
// แม็พ 3 สถานีอวกาศ: เศษหินลอยหมุนช้า ๆ, ไฟสัญญาณกะพริบ (ฟ้า/แดง), ดาวตกพาดผ่านเป็นระยะ
// แม็พ 4 เนบิวลาลาวา: ประกายไฟลอยขึ้น, หินลาวาเล็กลอยหมุน, แสงร้อนเรืองเป็นจังหวะทั้งแม็พ
// แม็พ 1 ปริซึมรุ่น 3 (FeatureFlags.PrismMapV3): ลำแสงสีรุ้งหมุนรอบเสากลาง, เศษคริสตัลลอยหมุน, ประกายดาวกะพริบ
// ทุกชิ้นอยู่ชั้นหลัง (sortingOrder -5: หน้าพื้นหลัง -6, หลังสิ่งกีดขวาง/ยาน) และวนกลับเข้าสนามเมื่อลอยออกขอบ
// สร้างโดย GameplayManager.ApplySelectedMapLayout → MapAmbience.Build(map, arenaMin, arenaMax)
using System.Collections.Generic;
using UnityEngine;

// ตัวขยับของประดับพื้นหลังแม็พ 1 (ปริซึมรุ่น 3) / 3 / 4 (ติดกับวัตถุ "MapAmbience")
public class MapAmbience : MonoBehaviour
{
    // ของลอย 1 ชิ้น: ความเร็ว ความเร็วหมุน และค่าเฉพาะแบบ (เวลาเริ่ม/อายุ/ความสว่างสูงสุด)
    private class Drifter
    {
        public SpriteRenderer art; // SpriteRenderer ของชิ้นประดับนี้
        public Vector2 velocity; // ความเร็วลอย (หน่วยโลก/วินาที)
        public float spin, phase, life, age, maxAlpha; // ความเร็วหมุน, เฟสเริ่มกะพริบ, คาบ/อายุ, เวลาที่ผ่านไป และความทึบสูงสุด
    }

    private const int Order = -5; // sortingOrder ของของประดับ (หน้าพื้นหลัง หลังสิ่งกีดขวางและยาน)
    private readonly List<Drifter> rocks = new List<Drifter>(); // เศษหิน/คริสตัลที่ลอยหมุนช้า ๆ
    private readonly List<Drifter> lights = new List<Drifter>(); // ไฟสัญญาณที่กะพริบ
    private readonly List<Drifter> embers = new List<Drifter>(); // ประกายไฟลอยขึ้นของแม็พเนบิวลาลาวา
    private readonly List<Drifter> beams = new List<Drifter>(); // ลำแสงสีรุ้งหมุนรอบเสากลางแม็พปริซึม
    private Drifter meteor; // ดาวตกที่พาดผ่านเป็นครั้งคราว (null = ไม่มี)
    private SpriteRenderer heatGlow; // แสงร้อนเรืองของแม็พลาวา
    private Vector2 min, max; // ขอบเขตสนาม ใช้วนของลอยที่หลุดขอบกลับเข้ามา
    private float nextMeteor; // เวลาที่จะปล่อยดาวตกลูกถัดไป
    private static Sprite softDot, beamSprite; // รูปวงกลมนุ่มและรูปลำแสงที่สร้างจากโค้ด (สร้างครั้งเดียวใช้ร่วมกัน)

    // สร้างของประดับของแม็พ map (แม็พ 3, 4 และแม็พ 1 เมื่อเปิดปริซึมรุ่น 3) คืน null ถ้าแม็พอื่น/ปิด Flag
    public static MapAmbience Build(int map, Vector2 arenaMin, Vector2 arenaMax)
    {
        bool prism = map == 1 && FeatureFlags.PrismMapV3;
        if (!FeatureFlags.MapAmbience || (!prism && map != GameplayManager.StationMapIndex && map != GameplayManager.LavaMapIndex)) return null;
        var root = new GameObject("MapAmbience");
        var ambience = root.AddComponent<MapAmbience>();
        ambience.min = arenaMin - Vector2.one * 3f;
        ambience.max = arenaMax + Vector2.one * 3f;
        if (prism) ambience.BuildPrism();
        else if (map == GameplayManager.StationMapIndex) ambience.BuildStation();
        else ambience.BuildLava();
        return ambience;
    }

    // ===== แม็พปริซึม (รุ่น 3) =====
    private void BuildPrism()
    {
        // ลำแสง 6 เส้นออกจากเสากลาง (สีรุ้ง) หมุนช้า ๆ และสว่างขึ้นลงคนละจังหวะ
        Color[] rainbow = { new Color(.45f, .95f, 1f), new Color(.75f, .5f, 1f), new Color(1f, .5f, .85f),
            new Color(.5f, 1f, .8f), new Color(.55f, .7f, 1f), new Color(1f, .8f, .5f) };
        for (int i = 0; i < rainbow.Length; i++)
        {
            var beam = MakeDrifter("PrismBeam", BeamSprite(), 1f, new Color(rainbow[i].r, rainbow[i].g, rainbow[i].b, 0f));
            beam.art.transform.position = Vector3.zero;
            beam.art.transform.rotation = Quaternion.Euler(0, 0, i * 60f + 15f);
            Vector2 size = beam.art.sprite.bounds.size;
            beam.art.transform.localScale = new Vector3(56f / size.x, 4.5f / size.y, 1f); // ยาว 56 กว้าง 4.5 หน่วยโลก
            beam.spin = 5f;
            beam.phase = i * 1.1f;
            beam.maxAlpha = .12f;
            beams.Add(beam);
        }
        // เศษคริสตัลลอยหมุน (ภาพ pr_shard_a/b/c)
        string[] shardNames = { "pr_shard_a", "pr_shard_b", "pr_shard_c" };
        for (int i = 0; i < 15; i++)
        {
            var sprite = Resources.Load<Sprite>(GameplayManager.PrismArtFolder + shardNames[i % shardNames.Length]);
            if (sprite == null) continue;
            var shard = MakeDrifter("CrystalShard", sprite, Random.Range(.7f, 1.5f), new Color(.85f, .9f, 1f, .5f));
            shard.velocity = Random.insideUnitCircle.normalized * Random.Range(.2f, .5f);
            shard.spin = Random.Range(-30f, 30f);
            rocks.Add(shard);
        }
        // ประกายดาวกะพริบ (ฟ้า/ขาว/ชมพู)
        Color[] twinkle = { new Color(.6f, .95f, 1f), Color.white, new Color(1f, .6f, .9f) };
        for (int i = 0; i < 24; i++)
        {
            var light = MakeDrifter("Twinkle", SoftDot(), Random.Range(.3f, .55f), twinkle[i % twinkle.Length]);
            light.phase = Random.Range(0f, 6.28f);
            light.life = Random.Range(1.6f, 3.2f);
            light.maxAlpha = Random.Range(.5f, .85f);
            lights.Add(light);
        }
    }

    // ภาพลำแสง (กว้าง 1 หน่วยโลก) จุดหมุนอยู่ขอบซ้าย: เข้มกลางเส้น จางไปขอบบนล่าง และจางลงไปทางปลาย (สร้างครั้งเดียว)
    private static Sprite BeamSprite()
    {
        if (beamSprite != null) return beamSprite;
        const int w = 128, h = 32; // ขนาดเท็กซ์เจอร์ลำแสง กว้าง x สูง (พิกเซล)
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = Mathf.Abs((y + .5f) / h * 2f - 1f);
                float along = (x + .5f) / w;
                float a = Mathf.Exp(-across * across * 5f) * Mathf.SmoothStep(0f, 1f, along * 6f) * (1f - along) * (1f - along);
                texture.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        texture.Apply();
        beamSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0f, .5f), w);
        return beamSprite;
    }

    // ===== แม็พสถานี =====
    private void BuildStation()
    {
        string[] rockNames = { "Station/st_rock_a", "Station/st_rock_b", "Station/st_rock_c" };
        for (int i = 0; i < 16; i++)
        {
            var sprite = Resources.Load<Sprite>("Images/Maps/" + rockNames[i % rockNames.Length]);
            if (sprite == null) continue;
            var rock = MakeDrifter("Debris", sprite, Random.Range(.9f, 2.4f), new Color(.5f, .55f, .68f, .55f));
            rock.velocity = Random.insideUnitCircle.normalized * Random.Range(.25f, .7f);
            rock.spin = Random.Range(-22f, 22f);
            rocks.Add(rock);
        }
        Color[] beacon = { new Color(.35f, .9f, 1f), new Color(1f, .35f, .3f) };
        for (int i = 0; i < 14; i++)
        {
            var light = MakeDrifter("Beacon", SoftDot(), Random.Range(.35f, .6f), beacon[i % 2]);
            light.phase = Random.Range(0f, 6.28f);
            light.life = Random.Range(1.2f, 2.6f); // คาบกะพริบ (วินาที)
            light.maxAlpha = Random.Range(.55f, .9f);
            lights.Add(light);
        }
        meteor = MakeDrifter("ShootingStar", SoftDot(), 1f, new Color(.8f, .9f, 1f, 0f));
        nextMeteor = Time.time + Random.Range(2f, 4f);
    }

    // ===== แม็พลาวา =====
    private void BuildLava()
    {
        string[] rockNames = { "Lava/lv_small_a", "Lava/lv_small_b", "Lava/lv_small_c", "Lava/lv_small_d" };
        for (int i = 0; i < 12; i++)
        {
            var sprite = Resources.Load<Sprite>("Images/Maps/" + rockNames[i % rockNames.Length]);
            if (sprite == null) continue;
            var rock = MakeDrifter("LavaDebris", sprite, Random.Range(.8f, 2f), new Color(.75f, .45f, .35f, .5f));
            rock.velocity = Random.insideUnitCircle.normalized * Random.Range(.2f, .55f);
            rock.spin = Random.Range(-18f, 18f);
            rocks.Add(rock);
        }
        float area = (max.x - min.x) * (max.y - min.y);
        int count = Mathf.Clamp(Mathf.RoundToInt(area * .018f), 40, 110);
        for (int i = 0; i < count; i++)
        {
            var ember = MakeDrifter("Ember", SoftDot(), Random.Range(.18f, .42f), new Color(1f, Random.Range(.45f, .75f), .2f, 0f));
            ResetEmber(ember, true);
            embers.Add(ember);
        }
        // แสงร้อนเรืองทั้งแม็พ (ส้มโปร่ง ค่อย ๆ หายใจเข้าออก)
        heatGlow = MakeDrifter("HeatGlow", SoftDot(), Mathf.Max(max.x - min.x, max.y - min.y) * 1.4f, new Color(1f, .4f, .1f, 0f)).art;
        heatGlow.transform.position = Vector3.zero;
    }

    // ประกายไฟ 1 เม็ด: เกิดใหม่ที่ตำแหน่งสุ่ม ลอยขึ้นพร้อมส่ายซ้ายขวา อายุ 4-8 วิ
    private void ResetEmber(Drifter ember, bool anywhere)
    {
        float y = anywhere ? Random.Range(min.y, max.y) : min.y + Random.Range(0f, (max.y - min.y) * .4f);
        ember.art.transform.position = new Vector3(Random.Range(min.x, max.x), y, 0);
        ember.velocity = new Vector2(Random.Range(-.25f, .25f), Random.Range(.6f, 1.6f));
        ember.phase = Random.Range(0f, 6.28f);
        ember.life = Random.Range(4f, 8f);
        ember.age = anywhere ? Random.Range(0f, ember.life) : 0f;
        ember.maxAlpha = Random.Range(.45f, .9f);
    }

    // สร้าง SpriteRenderer ชั้นหลัง ขนาด size หน่วยโลก ที่ตำแหน่งสุ่มในสนาม
    private Drifter MakeDrifter(string name, Sprite sprite, float size, Color color)
    {
        var item = new GameObject(name);
        item.transform.SetParent(transform, false);
        item.transform.position = new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), 0);
        item.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        var art = item.AddComponent<SpriteRenderer>();
        art.sprite = sprite;
        art.color = color;
        art.sortingOrder = Order;
        float longest = Mathf.Max(.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        item.transform.localScale = Vector3.one * (size / longest);
        return new Drifter { art = art, maxAlpha = color.a };
    }

    // วงกลมนุ่ม (ขาวตรงกลาง จางไปขอบ) สร้างครั้งเดียว ใช้เป็นไฟ/ประกาย/แสงเรือง
    private static Sprite SoftDot()
    {
        if (softDot != null) return softDot;
        const int size = 64; // ขนาดเท็กซ์เจอร์วงกลมนุ่ม (พิกเซล)
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size - 1, size - 1) * .5f) / (size * .5f);
                float a = Mathf.Clamp01(1f - d);
                texture.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
        texture.Apply();
        softDot = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        return softDot;
    }

    // ทุกเฟรม: ขยับ/หมุน/กะพริบ แล้ววนของที่ลอยออกขอบกลับเข้าอีกฝั่ง
    void Update()
    {
        float dt = Time.deltaTime, t = Time.time;
        foreach (var rock in rocks)
        {
            var tr = rock.art.transform;
            tr.position += (Vector3)(rock.velocity * dt);
            tr.Rotate(0, 0, rock.spin * dt);
            tr.position = Wrap(tr.position);
        }
        foreach (var light in lights)
        {
            float wave = Mathf.Clamp01(Mathf.Sin(t * 6.283f / light.life + light.phase) * 1.4f - .2f);
            var c = light.art.color; c.a = light.maxAlpha * wave; light.art.color = c;
        }
        foreach (var ember in embers)
        {
            ember.age += dt;
            if (ember.age >= ember.life || ember.art.transform.position.y > max.y) { ResetEmber(ember, false); continue; }
            var tr = ember.art.transform;
            tr.position += (Vector3)(new Vector2(ember.velocity.x + Mathf.Sin(t * 1.7f + ember.phase) * .35f, ember.velocity.y) * dt);
            float p = ember.age / ember.life;
            var c = ember.art.color; c.a = ember.maxAlpha * Mathf.Sin(p * Mathf.PI); ember.art.color = c;
        }
        foreach (var beam in beams)
        {
            beam.art.transform.Rotate(0, 0, beam.spin * dt);
            var c = beam.art.color; c.a = beam.maxAlpha * (.55f + .45f * Mathf.Sin(t * .7f + beam.phase)); beam.art.color = c;
        }
        if (heatGlow != null)
        {
            var c = heatGlow.color; c.a = .07f + .06f * Mathf.Sin(t * 1.1f); heatGlow.color = c;
        }
        UpdateMeteor(t, dt);
    }

    // ดาวตก: ทุก 3-7 วิ พุ่งเฉียงข้ามสนาม 1.2 วิ (เส้นยาวโปร่ง)
    private void UpdateMeteor(float t, float dt)
    {
        if (meteor == null) return;
        var tr = meteor.art.transform;
        if (meteor.age <= 0f)
        {
            if (t < nextMeteor) return;
            meteor.age = 1.2f;
            Vector2 start = new Vector2(Random.Range(min.x, max.x), max.y - Random.Range(0f, 8f));
            float angle = Random.Range(200f, 250f) * Mathf.Deg2Rad;
            meteor.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 26f;
            tr.position = start;
            tr.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            tr.localScale = new Vector3(6f, .18f, 1f); // ยืดเป็นเส้น
        }
        meteor.age -= dt;
        tr.position += (Vector3)(meteor.velocity * dt);
        var c = meteor.art.color; c.a = Mathf.Clamp01(meteor.age / 1.2f) * .8f; meteor.art.color = c;
        if (meteor.age <= 0f) { c.a = 0; meteor.art.color = c; nextMeteor = t + Random.Range(3f, 7f); }
    }

    // ลอยออกขอบสนาม = โผล่อีกฝั่ง
    private Vector3 Wrap(Vector3 p)
    {
        if (p.x < min.x) p.x = max.x; else if (p.x > max.x) p.x = min.x;
        if (p.y < min.y) p.y = max.y; else if (p.y > max.y) p.y = min.y;
        return p;
    }
}
