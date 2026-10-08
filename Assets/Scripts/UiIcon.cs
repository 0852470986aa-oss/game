// UiIcon.cs — ไอคอนหน้าข้อความบนปุ่ม/ป้าย (ใช้ได้ทั้งล็อบบี้และสนามรบ)
// รูปอยู่ที่ Resources/Images/UI/icon_<ชื่อ>.png (ชุดไอคอนเส้นสีขาว) — ไม่มีรูป = ไม่ใส่ไอคอน ปุ่มเป็นตัวหนังสือเหมือนเดิม
// ไอคอนเกาะอยู่ชิดซ้ายของตัวหนังสือเสมอ (ขยับตามเองเมื่อข้อความเปลี่ยน เช่น READY ↔ CANCEL READY หรือเปลี่ยนภาษา)
// กล่องข้อความถูกหดจากด้านซ้ายหนึ่งช่องไอคอน เพื่อไม่ให้ไอคอนทับตัวหนังสือที่ยาวเต็มกล่อง
// สีไอคอน = สีตัวหนังสือ / ปิด FeatureFlags.UiIcons = ไม่ใส่ไอคอนเลย
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// คอมโพเนนต์ไอคอนที่เกาะหน้าข้อความ (วางตำแหน่งตามตัวหนังสือทุกเฟรม)
public class UiIcon : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private TMP_Text label;
    private Image image;
    private float size, gap;

    // โหลดรูปไอคอน (จำไว้ ไม่โหลดซ้ำ) ไม่มีรูปคืน null
    public static Sprite Load(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!cache.TryGetValue(name, out var sprite)) cache[name] = sprite = Resources.Load<Sprite>("Images/UI/icon_" + name);
        return sprite;
    }

    // ใส่ไอคอนให้ปุ่ม (หา Label ในปุ่ม) — เรียกซ้ำได้ ไม่ซ้อน (เปลี่ยนรูปตามชื่อใหม่)
    public static UiIcon OnButton(Component button, string icon, float sizeScale = .52f)
    {
        if (button == null) return null;
        var label = button.transform.Find("Label")?.GetComponent<TMP_Text>() ?? button.GetComponentInChildren<TMP_Text>(true);
        return Attach(label, icon, sizeScale);
    }

    // ใส่ไอคอนหน้าข้อความ label (ขนาด = ความสูงกล่องข้อความ x sizeScale)
    public static UiIcon Attach(TMP_Text label, string icon, float sizeScale = .52f)
    {
        if (!FeatureFlags.UiIcons || label == null || !Application.isPlaying) return null;
        var sprite = Load(icon);
        if (sprite == null) return null;
        var parent = label.transform.parent;
        string key = "UiIcon_" + label.name;
        var existing = parent.Find(key);
        var follower = existing != null ? existing.GetComponent<UiIcon>() : null;
        if (follower != null && follower.label != label) { Destroy(follower.gameObject); follower = null; } // ป้ายถูกสร้างใหม่: ทำไอคอนใหม่
        if (follower == null)
        {
            var go = new GameObject(key, typeof(RectTransform), typeof(Image));
            go.layer = label.gameObject.layer;
            go.transform.SetParent(parent, false);
            follower = go.AddComponent<UiIcon>();
            follower.label = label;
            follower.image = go.GetComponent<Image>();
            follower.image.raycastTarget = false;
            follower.image.preserveAspect = true;
            var labelRect = label.rectTransform;
            float h = Mathf.Max(20f, labelRect.rect.height > 1 ? labelRect.rect.height : labelRect.sizeDelta.y);
            follower.size = Mathf.Clamp(h * sizeScale + 6f, 16f, 40f);
            follower.gap = follower.size * .3f;
            // เว้นที่ให้ไอคอนทางซ้าย: ขอบซ้ายของกล่องข้อความขยับขวา d ขอบขวาอยู่ที่เดิม (ใช้ได้ทุก pivot)
            float d = follower.size + follower.gap;
            labelRect.anchoredPosition += new Vector2(d * (1f - labelRect.pivot.x), 0);
            labelRect.sizeDelta -= new Vector2(d, 0);
        }
        follower.image.sprite = sprite;
        follower.Place();
        return follower;
    }

    // ตามตำแหน่งตัวหนังสือทุกเฟรม (ข้อความเปลี่ยนความยาว/ภาษา ไอคอนก็ขยับตาม)
    void LateUpdate() => Place();

    // วางไอคอนชิดซ้ายของตัวหนังสือจริง ขนาด/สีตามป้าย ซ่อนเมื่อป้ายว่าง/ปิด
    private void Place()
    {
        if (label == null) { Destroy(gameObject); return; }
        var rect = (RectTransform)transform;
        var labelRect = label.rectTransform;
        bool show = label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text);
        if (image.enabled != show) image.enabled = show;
        if (!show) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(size, size);
        image.color = label.color;
        // ตำแหน่งตัวหนังสือจริงในกล่อง (พิกัดของกล่องข้อความเอง) ตามการจัดชิดซ้าย/กลาง/ขวา
        Rect box = labelRect.rect;
        float width = Mathf.Min(label.renderedWidth > 0 ? label.renderedWidth : label.preferredWidth, box.width);
        var align = label.horizontalAlignment;
        float textLeft = align == HorizontalAlignmentOptions.Left || align == HorizontalAlignmentOptions.Justified || align == HorizontalAlignmentOptions.Flush
            ? box.xMin : align == HorizontalAlignmentOptions.Right ? box.xMax - width : box.center.x - width * .5f;
        Vector3 local = labelRect.localPosition;
        rect.localPosition = new Vector3(local.x + textLeft - gap - size * .5f, local.y + box.center.y, local.z);
    }
}
