// SkinAura.cs — แสงออร่ารอบยานของสกินพรีเมียม (SkinGacha.cs) ในสนามรบ (FeatureFlags.SkinGacha)
// เป็นลูกของยาน ใช้ภาพแสงเรืองกลม (UiFx.Glow) วาดใต้ตัวยาน — ไม่มีผลต่อการชน/ค่าพลัง
// ความทึบตามตัวยาน: ยานตาย (ปิดภาพ) = ออร่าหาย, ล่องหนโปร่งใส = ออร่าจางตาม
// ไม่แตะ renderer.enabled เลย เพื่อให้ระบบล่องหน (CLOAK) ซ่อน/คืนภาพได้ตามเดิม
using UnityEngine;

public class SkinAura : MonoBehaviour
{
    private SpriteRenderer ship;   // ภาพตัวยาน (ใช้อ่านความทึบ/ลำดับการวาด)
    private SpriteRenderer glow;   // ภาพแสง
    private Color color;           // สีพื้นฐาน
    private int style;             // 0 นิ่ง / 1 เต้น / 2 สีรุ้ง
    private float phase;           // เฟสสุ่ม ไม่ให้ทุกลำเต้นพร้อมกัน

    // ใส่/อัปเดตออร่าให้ยาน (skin = null หรือปิดระบบ = เอาออก)
    public static void Apply(Transform shipRoot, SpriteRenderer shipRenderer, SkinDef skin)
    {
        if (shipRoot == null) return;
        var existing = shipRoot.Find("SkinAura");
        if (skin == null || !FeatureFlags.SkinGacha || skin.aura.a <= 0f || shipRenderer == null)
        {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }
        GameObject go = existing != null ? existing.gameObject : new GameObject("SkinAura");
        go.transform.SetParent(shipRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        var aura = go.GetComponent<SkinAura>();
        if (aura == null) aura = go.AddComponent<SkinAura>();
        aura.Setup(shipRenderer, skin);
    }

    private void Setup(SpriteRenderer shipRenderer, SkinDef skin)
    {
        ship = shipRenderer;
        color = skin.aura;
        style = skin.auraStyle;
        phase = Random.value * 10f;
        glow = GetComponent<SpriteRenderer>();
        if (glow == null) glow = gameObject.AddComponent<SpriteRenderer>();
        glow.sprite = UiFx.Glow;
        glow.sortingLayerID = ship.sortingLayerID;
        glow.sortingOrder = ship.sortingOrder - 1;
        // ขนาด: ราว 2.1 เท่าของด้านยาวของยาน (คิดในพิกัดของยาน)
        float size = 1f;
        if (ship.sprite != null)
        {
            var bounds = ship.sprite.bounds.size;
            var shipScale = ship.transform.lossyScale;
            var rootScale = transform.parent.lossyScale;
            float sx = rootScale.x != 0 ? shipScale.x / rootScale.x : 1f, sy = rootScale.y != 0 ? shipScale.y / rootScale.y : 1f;
            size = Mathf.Max(bounds.x * Mathf.Abs(sx), bounds.y * Mathf.Abs(sy));
        }
        float glowSize = glow.sprite != null ? glow.sprite.bounds.size.x : 1.28f;
        float scale = size * 2.1f / Mathf.Max(.01f, glowSize);
        transform.localScale = new Vector3(scale, scale, 1f);
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (ship == null || glow == null) { Destroy(gameObject); return; }
        float visible = ship.enabled && ship.gameObject.activeInHierarchy ? ship.color.a : 0f;
        Color c = color;
        float t = Time.time + phase;
        if (style == 1) c.a *= .7f + .3f * Mathf.Sin(t * 3.2f);
        else if (style == 2)
        {
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(t * .18f, 1f), .65f, 1f);
            c = new Color(rainbow.r, rainbow.g, rainbow.b, color.a * (.8f + .2f * Mathf.Sin(t * 2.4f)));
        }
        c.a *= visible;
        glow.color = c;
    }
}
