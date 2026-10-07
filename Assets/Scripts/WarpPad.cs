// ไฟล์ WarpPad.cs: แท่นวาร์ปในแม็พปริซึม สร้างโดย GameplayManager.CreateWarpPad (GameplayManager.Maps.cs)
// ทำงานคู่กับ PlayerController.Warp.cs (TryWarp / WarpRPC)
using UnityEngine;

// แท่นวาร์ปในแม็พปริซึม: บินเข้าแท่นแล้วไปโผล่ที่แท่นคู่กัน (สีเดียวกัน)
// เจ้าของยานเป็นคนตัดสินใจวาร์ป แล้วแจ้งทุกเครื่องผ่าน PlayerController.WarpRPC
// มีคูลดาวน์ต่อยาน (PlayerController.WarpCooldown) กันวาร์ปไปกลับรัวๆ
[RequireComponent(typeof(Collider2D))]
// คอมโพเนนต์แท่นวาร์ป 1 อัน: ตรวจยานที่เข้า Trigger แล้วส่งให้ยานวาร์ปไปแท่นคู่ (partner)
public class WarpPad : MonoBehaviour
{
    // สีของคู่ A (ฟ้า) และคู่ B (ชมพู)
    public static readonly Color ColorA = new Color(.45f, .95f, 1f);
    public static readonly Color ColorB = new Color(1f, .55f, 1f);

    // แท่นปลายทางที่จับคู่กัน และสีของแท่นนี้
    public WarpPad partner;
    public Color tint = Color.white;
    private SpriteRenderer glow;
    private float flash;
    // ขนาดแสงเรืองรอบแท่น (หน่วยโลก)
    private const float GlowSize = 7f;

    // ตอนเริ่ม: หาแท่นคู่ถ้ายังไม่ได้ผูก แล้วสร้างแสงเรืองรอบแท่น
    private void Start()
    {
        // แท่นที่บันทึกใน Scene แล้วลืมผูกคู่: หาคู่จากชื่อ (WarpPad_A1 ↔ WarpPad_A2)
        if (partner == null && transform.parent != null && name.Length > 1)
        {
            string partnerName = name.Substring(0, name.Length - 1) + (name.EndsWith("1") ? "2" : "1");
            var other = transform.parent.Find(partnerName);
            if (other != null) partner = other.GetComponent<WarpPad>();
        }
        glow = PrismFx.CreateGlow(transform, transform.position, GlowSize, tint, -1);
    }

    // ทุกเฟรม: แสงเรืองกะพริบเบาๆ และสว่าง/ขยายขึ้นชั่วครู่หลังมีการวาร์ป (flash ลดลงจนเป็น 0)
    private void Update()
    {
        if (glow == null) return;
        flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 2.5f);
        float t = Time.time * 2.6f + transform.position.x * .1f;
        float alpha = .28f + .14f * Mathf.Sin(t) + flash * .6f;
        glow.color = new Color(tint.r, tint.g, tint.b, alpha);
        glow.transform.localScale = Vector3.one * (GlowSize / PrismFx.DotWorldSize) * (1f + .07f * Mathf.Sin(t * .7f) + flash * .5f)
            / Mathf.Max(.001f, transform.lossyScale.x);
    }

    // เมื่อมี Collider เข้ามาหรืออยู่ในแท่น ให้ลองวาร์ป (ทำงานทุกเครื่อง แต่ PlayerController.TryWarp จะทำต่อเฉพาะเครื่องเจ้าของยาน)
    private void OnTriggerEnter2D(Collider2D other) => TryWarp(other);
    private void OnTriggerStay2D(Collider2D other) => TryWarp(other);

    // หา PlayerController จาก Collider ที่ชน แล้วสั่ง TryWarp จากแท่นนี้ไปแท่นคู่
    private void TryWarp(Collider2D other)
    {
        if (partner == null) return;
        var ship = other.GetComponentInParent<PlayerController>();
        if (ship != null) ship.TryWarp(this, partner);
    }

    // จุดโผล่: ห่างจากแท่นไปทางกลางสนาม ไม่ทับวงแท่น (กันวาร์ปเด้งกลับทันที)
    public Vector2 ExitPoint()
    {
        Vector2 position = transform.position;
        Vector2 toCenter = position.sqrMagnitude > .01f ? -position.normalized : Vector2.up;
        return position + toCenter * 3.8f;
    }

    // แสงวาบที่แท่น (เรียกจาก PlayerController.WarpRPC บนทุกเครื่อง ทั้งแท่นต้นทางและปลายทาง)
    public void Flash()
    {
        flash = 1f;
        PrismFx.Burst(transform.position, tint, 5f);
    }
}
