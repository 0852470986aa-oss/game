// CelebrationOverlay.cs — ฉากฉลองเต็มจอ (เลเวลอัป / แรงค์อัป / ได้ Achievement) แสดงทับหน้าผลแมตช์
// ลำดับ: พื้นหลังมืด → แสงรัศมีหมุน + ประกายกระจาย → รูป (ดาว/ป้ายแรงค์/ถ้วย) เด้งขึ้น → หัวข้อใหญ่ + รายละเอียด → "แตะเพื่อไปต่อ"
// มีหลายอย่างพร้อมกัน = แสดงต่อกันทีละอัน (แตะเพื่อไปอันถัดไป) / ปิด FeatureFlags.Celebrations = ไม่แสดง
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ตัวจัดคิวและเล่นฉากฉลองเต็มจอ (สร้าง GameObject เองครั้งแรกที่เรียก Show)
public class CelebrationOverlay : MonoBehaviour
{
    // หนึ่งฉาก: หัวข้อ, บรรทัดรอง, รายละเอียด, รูป, สีหลัก
    private struct Scene { public string title, subtitle, detail; public Sprite sprite; public Color color; }

    private static CelebrationOverlay instance; // ตัวเล่นฉากฉลองตัวเดียวของเกม (สร้างตอน Show ครั้งแรก)
    private readonly Queue<Scene> queue = new Queue<Scene>(); // คิวฉากฉลองที่รอแสดงทีละฉาก
    private Transform canvas; // Canvas ที่ใช้วางฉากฉลอง
    private TMP_FontAsset font; // ฟอนต์ของข้อความในฉากฉลอง
    private bool playing, tapped; // playing = กำลังเล่นคิวอยู่, tapped = ผู้เล่นแตะเพื่อไปต่อแล้ว
    private RectTransform current; // ฉากที่กำลังแสดง (null = ไม่มี)

    // เพิ่มฉากฉลองเข้าคิว (canvas = Canvas ที่จะแสดง, font = ฟอนต์ที่ใช้)
    public static void Show(Transform canvas, TMP_FontAsset font, string title, string subtitle, string detail, Sprite sprite, Color color)
    {
        if (!FeatureFlags.Celebrations || canvas == null) return;
        if (instance == null)
        {
            var go = new GameObject("CelebrationRunner");
            instance = go.AddComponent<CelebrationOverlay>();
        }
        instance.canvas = canvas;
        if (font != null) instance.font = font;
        instance.queue.Enqueue(new Scene { title = title, subtitle = subtitle, detail = detail, sprite = sprite, color = color });
        if (!instance.playing) instance.StartCoroutine(instance.PlayAll());
    }

    // ปิดฉากที่แสดงอยู่ (ปุ่มย้อนกลับ) คืน true ถ้ามีฉากเปิดอยู่
    public static bool HandleBack()
    {
        if (instance == null || instance.current == null) return false; // ยังไม่มีฉากขึ้นจอ = ไม่กลืนปุ่ม
        instance.tapped = true;
        return true;
    }

    // Unity เรียกตอนถูกทำลาย (เปลี่ยนฉาก): ล้าง instance ให้สร้างใหม่ได้
    void OnDestroy() { if (instance == this) instance = null; }

    // Coroutine เล่นทุกฉากในคิวทีละฉาก (รอหน้าผลขึ้นก่อน 0.8 วิ)
    private IEnumerator PlayAll()
    {
        playing = true;
        // รอให้หน้าผลขึ้นก่อนนิดหนึ่ง
        yield return new WaitForSecondsRealtime(.8f);
        while (queue.Count > 0 && canvas != null) yield return PlayOne(queue.Dequeue());
        playing = false;
    }

    // Coroutine เล่น 1 ฉาก: สร้าง UI → เด้งเข้า 1 วิ → ค้างจนผู้เล่นแตะ → ลบทิ้ง
    private IEnumerator PlayOne(Scene scene)
    {
        tapped = false;
        var root = Rect("Celebration", canvas, Vector2.zero);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
        current = root;
        var shade = Img("Shade", root, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
        shade.raycastTarget = true;
        shade.gameObject.AddComponent<Button>().onClick.AddListener(() => tapped = true);
        var stage = Rect("Stage", root, Vector2.zero);

        var rays = Rect("Rays", stage, new Vector2(0, 40));
        var rayImages = new List<Image>();
        for (int i = 0; i < 18; i++)
        {
            var ray = Img("Ray" + i, rays, Vector2.zero, new Vector2(i % 2 == 0 ? 30 : 14, 1000), new Color(1, 1, 1, 0));
            ray.rectTransform.localRotation = Quaternion.Euler(0, 0, i * 10f);
            rayImages.Add(ray);
        }
        var picture = Img("Picture", stage, new Vector2(0, 40), new Vector2(220, 220), Color.white);
        if (scene.sprite != null) { picture.sprite = scene.sprite; picture.preserveAspect = true; }
        else picture.color = scene.color;
        picture.rectTransform.localScale = Vector3.zero;
        var title = Text("Title", stage, scene.title, new Vector2(0, 220), 54, scene.color);
        title.fontStyle = FontStyles.Bold;
        var subtitle = Text("Subtitle", stage, scene.subtitle, new Vector2(0, -120), 36, Color.white);
        var detail = Text("Detail", stage, scene.detail, new Vector2(0, -168), 22, new Color(1f, .85f, .45f));
        foreach (var t in new[] { title, subtitle, detail }) t.alpha = 0;
        var sparks = new List<KeyValuePair<Image, Vector2>>();
        for (int i = 0; i < 28; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            var spark = Img("Spark" + i, stage, new Vector2(0, 40), Vector2.one * Random.Range(7f, 15f), Color.Lerp(scene.color, Color.white, Random.value * .6f));
            spark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            sparks.Add(new KeyValuePair<Image, Vector2>(spark, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(280f, 620f)));
        }
        AudioManager.Instance?.PlaySFX("SFX_Celebrate"); // ปิด NewSounds = เสียงโล่แตกแบบเดิม

        // เด้งเข้า (1 วิ) — แตะเพื่อข้ามได้
        for (float t = 0; t < 1f && !tapped && root != null; t += Time.unscaledDeltaTime)
        {
            shade.color = new Color(0, 0, 0, .85f * Mathf.Clamp01(t / .25f));
            picture.rectTransform.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(t / .45f));
            rays.localRotation = Quaternion.Euler(0, 0, t * 30f);
            Color glow = scene.color; glow.a = .35f * Mathf.Clamp01(t / .4f);
            foreach (var ray in rayImages) ray.color = glow;
            title.rectTransform.localScale = Vector3.one * (1f + .4f * (1f - Mathf.Clamp01((t - .15f) / .3f)));
            title.alpha = Mathf.Clamp01((t - .15f) / .3f);
            subtitle.alpha = detail.alpha = Mathf.Clamp01((t - .45f) / .35f);
            foreach (var s in sparks)
            {
                s.Key.rectTransform.anchoredPosition = new Vector2(0, 40) + s.Value * t * (1f - t * .45f);
                var c = s.Key.color; c.a = Mathf.Clamp01(1f - t); s.Key.color = c;
            }
            yield return null;
        }
        if (root == null) yield break;
        shade.color = new Color(0, 0, 0, .85f);
        picture.rectTransform.localScale = Vector3.one;
        title.rectTransform.localScale = Vector3.one;
        foreach (var t in new[] { title, subtitle, detail }) t.alpha = 1;
        foreach (var s in sparks) Destroy(s.Key.gameObject);
        tapped = false;

        // ค้างไว้จนแตะ (อย่างน้อย 0.4 วิ กันแตะทะลุ)
        var tap = Text("Tap", root, "TAP TO CONTINUE", new Vector2(0, -300), 20, new Color(.8f, .9f, 1f));
        for (float t = 0; root != null && (!tapped || t < .4f); t += Time.unscaledDeltaTime)
        {
            if (t < .4f) tapped = false;
            rays.localRotation = Quaternion.Euler(0, 0, 30f + t * 16f);
            picture.rectTransform.anchoredPosition = new Vector2(0, 40 + Mathf.Sin(t * 2.4f) * 8f);
            tap.alpha = .45f + .55f * Mathf.Abs(Mathf.Sin(t * 2.5f));
            yield return null;
        }
        if (root != null) Destroy(root.gameObject);
        current = null;
        yield return null;
    }

    // ===== ตัวช่วยสร้าง UI =====
    private static RectTransform Rect(string name, Transform parent, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = pos;
        return rect;
    }

    // สร้าง Image สี่เหลี่ยมตามตำแหน่ง/ขนาด/สี (ไม่รับการกด)
    private static Image Img(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var rect = Rect(name, parent, pos);
        rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return image;
    }

    // สร้างข้อความกึ่งกลางบรรทัดเดียวด้วยฟอนต์ของเกม
    private TMP_Text Text(string name, Transform parent, string value, Vector2 pos, float size, Color color)
    {
        var rect = Rect(name, parent, pos);
        rect.sizeDelta = new Vector2(1000, size + 18);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    // สูตรเด้งเกินแล้วกลับ (ease out back) k 0-1 คืนสเกล
    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f; // ค่าคงที่ของสูตร ease out back (ระดับการเด้งเกิน)
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
