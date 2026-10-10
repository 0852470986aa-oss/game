// ButtonSkin.cs — แต่งหน้าตาปุ่มให้สวยขึ้นโดยไม่ย้ายตำแหน่งหรือเปลี่ยนขนาดปุ่ม (สวิตช์ FeatureFlags.FancyButtons)
// เดิม: ปุ่มเป็นสี่เหลี่ยมสีทึบเรียบ ๆ
// ใหม่: 1) มุมโค้ง + ไล่สีบนสว่าง-ล่างเข้ม (ยังใช้สีเดิมของปุ่มแต่ละอัน เพราะภาพเป็นสีขาวแล้วคูณด้วยสีของ Image)
//       2) ขอบเรืองแสงบาง ๆ + เงาเงาวาวครึ่งบน (วาดอยู่ "หลัง" ตัวหนังสือ/รูปในปุ่ม)
//       3) เงาตกด้านล่าง  4) กดแล้วปุ่มยุบลงเล็กน้อย ปล่อยแล้วเด้งกลับ
// ภาพทั้งหมดสร้างด้วยโค้ดตอนเริ่มเกม (ไม่มีไฟล์รูปเพิ่ม) ใช้ texture แผ่นเดียวกันทุกปุ่ม (วาดรวมกันได้ ไม่เพิ่ม draw call มาก)
// ปุ่มที่มีรูปอยู่แล้ว (Image มี sprite) หรือปุ่มโปร่งใส จะไม่ถูกเปลี่ยน
// ปิดสวิตช์ = ปุ่มสี่เหลี่ยมเรียบแบบเดิมทุกอย่าง (ต้องปิดก่อนเข้าฉาก)
using UnityEngine;
using UnityEngine.EventSystems;

// ตัวแต่งปุ่ม: เรียก ButtonSkin.Apply(รูปพื้นของปุ่ม) หลังสร้างปุ่ม เรียกซ้ำได้ (ไม่แต่งซ้ำ)
public class ButtonSkin : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    // ===== ค่าที่ปรับหน้าตาได้ =====
    private const int Tex = 64;                 // ขนาดภาพต้นแบบแต่ละชิ้น (พิกเซล)
    private const int Radius = 18;              // รัศมีมุมโค้งในภาพต้นแบบ (พิกเซล)
    private const float CornerScale = 1.8f;     // หารขนาดมุมบนจอ: 18 / 1.8 = มุมโค้งราว 10 หน่วย
    private const float TopShade = 1f;          // ความสว่างขอบบนของพื้นปุ่ม (1 = สีเดิมเต็ม)
    private const float BottomShade = .72f;     // ความสว่างขอบล่างของพื้นปุ่ม (ไล่เข้มลง)
    private const float RimAlpha = .30f;        // ความชัดของขอบเรืองแสง
    private const float GlossAlpha = .14f;      // ความชัดของเงาวาวครึ่งบน
    private const float PressScale = .95f;      // ขนาดตอนกดค้าง (95%)
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, .45f); // สีเงาตกใต้ปุ่ม
    private static readonly Vector2 ShadowOffset = new Vector2(0f, -3f);     // ระยะเงาตก (ลงล่าง 3 หน่วย)

    // ภาพที่สร้างครั้งเดียวแล้วใช้ซ้ำทุกปุ่ม: พื้นปุ่ม / ขอบเรืองแสง / เงาวาว (อยู่ใน texture แผ่นเดียวกัน)
    private static Sprite fillSprite, rimSprite, glossSprite;

    private UnityEngine.UI.Image body;          // รูปพื้นของปุ่ม (ตัวที่ Button ใช้เปลี่ยนสีตอนกด)
    private UnityEngine.UI.Image rim;           // ขอบเรืองแสง (ลูกของปุ่ม อยู่หลังตัวหนังสือ)
    private UnityEngine.UI.Image gloss;         // เงาวาวครึ่งบน (ลูกของปุ่ม อยู่หลังตัวหนังสือ)
    private UnityEngine.UI.Selectable selectable; // ปุ่ม (ใช้ดูว่ากดได้อยู่ไหม)
    private Vector3 restScale = Vector3.one;    // ขนาดปกติก่อนกด (คืนค่านี้ตอนปล่อย)
    private bool pressed;                        // กำลังกดค้างอยู่หรือไม่
    private float lastAlpha = -1f;               // ค่าความทึบล่าสุดของพื้นปุ่ม (ใช้ดูว่าต้องอัปเดตขอบ/เงาวาวไหม)
    private bool lastVisible = true;             // พื้นปุ่มแสดงอยู่หรือไม่ ครั้งล่าสุด

    // แต่งปุ่มจากรูปพื้นของปุ่ม: ข้ามถ้าปิดสวิตช์ / มีรูปอยู่แล้ว / โปร่งใส / แต่งไปแล้ว
    public static void Apply(UnityEngine.UI.Image image)
    {
        // แต่งเฉพาะตอนเล่น: ภาพสร้างด้วยโค้ด ถ้าแต่งตอนใช้เมนูสร้าง UI ใน Editor แล้วเซฟฉาก ภาพจะหายเป็นสี่เหลี่ยมขาว
        if (!FeatureFlags.FancyButtons || !Application.isPlaying || image == null) return;
        if (image.GetComponent<ButtonSkin>() != null) return;
        EnsureSprites();
        if (!IsPlain(image.sprite) || image.color.a < .05f) return;
        image.sprite = fillSprite;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = CornerScale;
        image.fillCenter = true;
        UnityEngine.UI.Shadow shadow = null; // หา Shadow ตัวจริง (Outline ก็สืบทอดจาก Shadow ห้ามไปแก้ตัวนั้น)
        foreach (var effect in image.GetComponents<UnityEngine.UI.Shadow>())
            if (effect.GetType() == typeof(UnityEngine.UI.Shadow)) { shadow = effect; break; }
        if (shadow == null) shadow = image.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = ShadowColor;
        shadow.effectDistance = ShadowOffset;
        shadow.useGraphicAlpha = true; // ปุ่มจาง/ซ่อน เงาก็จางตาม
        var skin = image.gameObject.AddComponent<ButtonSkin>();
        skin.Init(image);
    }

    // แต่งทุกปุ่ม (Button) ที่อยู่ใต้ root รวมที่ซ่อนอยู่ — ใช้กับหน้าที่สร้างปุ่มหลายแบบ เช่น หน้าตั้งค่า
    public static void ApplyUnder(Transform root)
    {
        if (!FeatureFlags.FancyButtons || !Application.isPlaying || root == null) return;
        foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            Apply(button.targetGraphic as UnityEngine.UI.Image ?? button.GetComponent<UnityEngine.UI.Image>());
    }

    // รูปพื้นที่ยอมให้แต่งทับ: ไม่มีรูป / รูปสี่เหลี่ยมมาตรฐานของ Unity (UISprite) / รูปพื้นปุ่มของเราที่ถูกคัดลอกมาจากปุ่มอื่น
    private static bool IsPlain(Sprite sprite) => sprite == null || sprite == fillSprite || sprite.name == "UISprite";

    // สร้างลูก 2 ชิ้น (เงาวาว + ขอบ) วางหลังทุกอย่างในปุ่ม และจำขนาดปกติไว้ใช้ตอนกด
    private void Init(UnityEngine.UI.Image image)
    {
        body = image;
        selectable = GetComponent<UnityEngine.UI.Selectable>();
        restScale = transform.localScale;
        gloss = Layer("SkinGloss", glossSprite, UnityEngine.UI.Image.Type.Sliced, new Vector2(0f, .42f), Vector2.one, GlossAlpha);
        rim = Layer("SkinRim", rimSprite, UnityEngine.UI.Image.Type.Sliced, Vector2.zero, Vector2.one, RimAlpha);
        // ลำดับ: ขอบ (ล่างสุด) -> เงาวาว -> ของเดิมในปุ่ม (ตัวหนังสือ/รูป อยู่บนสุด)
        gloss.transform.SetAsFirstSibling();
        rim.transform.SetAsFirstSibling();
        Sync(true);
    }

    // สร้างรูปลูกแบบยืดเต็มช่วง anchor ที่กำหนด ไม่รับการคลิก (การกดยังไปที่ปุ่มตามเดิม)
    private UnityEngine.UI.Image Layer(string name, Sprite sprite, UnityEngine.UI.Image.Type type, Vector2 min, Vector2 max, float alpha)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.type = type;
        image.pixelsPerUnitMultiplier = CornerScale;
        image.color = new Color(1f, 1f, 1f, alpha);
        image.raycastTarget = false;
        return image;
    }

    // ขอบและเงาวาวจางตามพื้นปุ่ม (ปุ่มถูกซ่อน/ทำให้โปร่งใสภายหลัง ขอบก็หายตาม) และจางลงเมื่อปุ่มกดไม่ได้
    private void LateUpdate() => Sync(false);
    private void Sync(bool force)
    {
        if (body == null) return;
        // มีโค้ดอื่นเปลี่ยนรูปพื้นปุ่มเป็นภาพอื่นภายหลัง = ซ่อนขอบ/เงาวาว ไม่ให้ทับภาพนั้น
        bool visible = body.enabled && body.gameObject.activeInHierarchy && body.sprite == fillSprite;
        float alpha = body.color.a * (selectable != null && !selectable.interactable ? .5f : 1f);
        if (!force && visible == lastVisible && Mathf.Approximately(alpha, lastAlpha)) return;
        lastVisible = visible; lastAlpha = alpha;
        if (rim != null) { rim.enabled = visible; rim.color = new Color(1f, 1f, 1f, RimAlpha * alpha); }
        if (gloss != null) { gloss.enabled = visible; gloss.color = new Color(1f, 1f, 1f, GlossAlpha * alpha); }
    }

    // กดลง: ยุบปุ่มเล็กน้อย (ตำแหน่งเดิม เพราะยุบรอบจุดกึ่งกลางปุ่ม)
    public void OnPointerDown(PointerEventData eventData)
    {
        if (selectable != null && !selectable.IsInteractable()) return;
        if (!pressed) restScale = transform.localScale;
        pressed = true;
        transform.localScale = restScale * PressScale;
    }
    // ปล่อยนิ้ว / ลากนิ้วออกนอกปุ่ม: คืนขนาดเดิม
    public void OnPointerUp(PointerEventData eventData) => Release();
    public void OnPointerExit(PointerEventData eventData) => Release();
    private void OnDisable() => Release();
    private void Release()
    {
        if (!pressed) return;
        pressed = false;
        transform.localScale = restScale;
    }

    // ===== สร้างภาพต้นแบบ 3 ชิ้นใน texture แผ่นเดียว (กว้าง 3 ช่อง) =====
    private static void EnsureSprites()
    {
        if (fillSprite != null && rimSprite != null && glossSprite != null) return;
        var texture = new Texture2D(Tex * 3, Tex, TextureFormat.RGBA32, false)
        {
            name = "ButtonSkin", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[Tex * 3 * Tex];
        for (int y = 0; y < Tex; y++)
            for (int x = 0; x < Tex; x++)
            {
                float edge = Mathf.Clamp01(.5f - Distance(x, y));       // ความทึบตามรูปทรงมุมโค้ง (ขอบเรียบไม่หยัก)
                float v = y / (float)(Tex - 1);                          // 0 = ล่าง, 1 = บน
                // ช่อง 1 พื้นปุ่ม: ไล่สีจากบนสว่างไปล่างเข้ม (สีจริงมาจาก Image.color)
                float shade = Mathf.Lerp(BottomShade, TopShade, v);
                pixels[y * Tex * 3 + x] = Px(shade, edge);
                // ช่อง 2 ขอบเรืองแสง: เส้นหนาราว 2 พิกเซลด้านใน ด้านบนสว่างกว่าด้านล่าง
                float d = Distance(x, y);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d + 1.5f) / 1.5f) * Mathf.Lerp(.55f, 1f, v);
                pixels[y * Tex * 3 + Tex + x] = Px(1f, ring * edge);
                // ช่อง 3 เงาวาว: ทึบที่ขอบบนแล้วจางหายลงล่าง
                float fade = v * v;
                pixels[y * Tex * 3 + Tex * 2 + x] = Px(1f, fade * edge);
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true); // ไม่ต้องเก็บสำเนาไว้ใน RAM อีก
        var border = new Vector4(Radius + 2, Radius + 2, Radius + 2, Radius + 2);
        fillSprite = Sprite.Create(texture, new Rect(0, 0, Tex, Tex), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
        rimSprite = Sprite.Create(texture, new Rect(Tex, 0, Tex, Tex), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
        glossSprite = Sprite.Create(texture, new Rect(Tex * 2, 0, Tex, Tex), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    // ระยะจากจุด (x,y) ถึงขอบสี่เหลี่ยมมุมโค้ง (ค่าติดลบ = อยู่ด้านใน) ใช้ทำขอบมนแบบนุ่ม
    private static float Distance(int x, int y)
    {
        float half = Tex * .5f;
        float px = Mathf.Abs(x + .5f - half) - (half - Radius);
        float py = Mathf.Abs(y + .5f - half) - (half - Radius);
        float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(px, py), 0f) - Radius;
    }

    // สีพิกเซลขาวเทาตามความสว่าง + ความทึบ
    private static Color32 Px(float shade, float alpha)
    {
        byte s = (byte)Mathf.RoundToInt(Mathf.Clamp01(shade) * 255f);
        return new Color32(s, s, s, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
    }
}
