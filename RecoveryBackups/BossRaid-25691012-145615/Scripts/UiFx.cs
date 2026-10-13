// UiFx.cs — ภาพเอฟเฟกต์แบบนุ่ม ๆ สำหรับฉาก UI (เปิดกล่องเสบียง / ฉากฉลองเลเวลอัป-แรงค์อัป-Achievement) สวิตช์ FeatureFlags.SoftUiFx
// เดิม: แสงรัศมีเป็นแท่งสี่เหลี่ยมทึบ ประกายเป็นสี่เหลี่ยมหมุน 45° แสงวาบขาวเต็มจอ — โปรเจกต์ใช้ Linear color space
//       ทำให้สีโปร่งแสงดูสว่างจัดกว่าตัวเลข (แท่งแสง 30% ดูเหมือนทึบ) และพื้นหลังมืด 85–90% ยังเห็นหน้าข้างหลังชัด
// ใหม่: ภาพสร้างด้วยโค้ดครั้งเดียว (ไม่มีไฟล์รูปเพิ่ม): ลำแสงเรียวปลายจาง / แสงเรืองกลม / วงคลื่นกระแทก / ประกายรูปดาว 4 แฉก
//       + UiSpin หมุนชั้นแสงสวนทางกันให้ดูมีมิติ
using UnityEngine;

// คลังภาพเอฟเฟกต์ (static) สร้างครั้งแรกที่เรียกแล้วใช้ซ้ำ
public static class UiFx
{
    private static Sprite rays12, rays20, glow, ring, spark; // ภาพที่สร้างแล้ว (cache)

    // ลำแสงเรียว 12 เส้น (หนา) / 20 เส้น (บาง) — ปลายจางหาย กลางว่าง ให้แสงเรืองเติมกลางแทน
    public static Sprite Rays12 => rays12 != null ? rays12 : (rays12 = Make(256, (x, y) => RayAlpha(x, y, 12, 7f)));
    public static Sprite Rays20 => rays20 != null ? rays20 : (rays20 = Make(256, (x, y) => RayAlpha(x, y, 20, 16f)));
    // แสงเรืองกลมนุ่ม (สว่างกลาง จางออกขอบ)
    public static Sprite Glow => glow != null ? glow : (glow = Make(128, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        return r >= 1f ? 0f : Mathf.Exp(-r * r * 4.5f) * (1f - r);
    }));
    // วงแหวนบาง ๆ ขอบนุ่ม (ใช้เป็นคลื่นกระแทกที่ขยายออก)
    public static Sprite Ring => ring != null ? ring : (ring = Make(128, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float d = (r - .86f) / .07f;
        return r >= 1f ? 0f : Mathf.Exp(-d * d);
    }));
    // ประกายรูปดาว 4 แฉก + แกนกลางสว่าง
    public static Sprite Spark => spark != null ? spark : (spark = Make(64, (x, y) =>
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y), r = Mathf.Sqrt(x * x + y * y);
        if (r >= 1f) return 0f;
        float arms = Mathf.Exp(-ax * 16f) * Mathf.Exp(-ay * 2.6f) + Mathf.Exp(-ay * 16f) * Mathf.Exp(-ax * 2.6f);
        return Mathf.Clamp01(arms * (1f - r) + Mathf.Exp(-r * r * 40f));
    }));

    // ความทึบของลำแสงที่จุด (x,y) ช่วง -1..1: n เส้น, sharp = ความคมของเส้น (เส้นเรียวลงเมื่อห่างจากกลาง)
    private static float RayAlpha(float x, float y, int n, float sharp)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        if (r >= 1f) return 0f;
        float angle = Mathf.Atan2(y, x);
        float beam = Mathf.Pow(.5f + .5f * Mathf.Cos(angle * n), sharp + 30f * r); // ยิ่งไกลยิ่งเรียว
        float fade = Mathf.Pow(1f - r, 1.6f) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - .04f) / .22f));
        return beam * fade;
    }

    // สร้าง sprite สีขาวขนาด size×size จากฟังก์ชันความทึบ (พิกัด -1..1) — สีจริงมาจาก Image.color
    private static Sprite Make(int size, System.Func<float, float, float> alpha)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "UiFx", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = (x + .5f) / size * 2f - 1f, fy = (y + .5f) / size * 2f - 1f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(fx, fy)) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
    }

    // สร้างรูปเอฟเฟกต์กลางพ่อแม่ (ไม่รับการคลิก)
    public static UnityEngine.UI.Image Layer(string name, Transform parent, Sprite sprite, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}

// หมุนวัตถุ UI ต่อเนื่อง (ใช้เวลาจริง ไม่สนการหยุดเกม) — ใช้หมุนชั้นลำแสงสวนทางกัน
public class UiSpin : MonoBehaviour
{
    public float degreesPerSecond = -12f; // ความเร็วหมุน (องศา/วินาที) ค่าลบ = หมุนตามเข็มนาฬิกา
    void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
}
