// ไฟล์ PrismBurst.cs: เอฟเฟกต์แสงวาบในแม็พปริซึม สร้างผ่าน PrismFx.Burst (อยู่ใน PrismArenaVisuals.cs)
// ถูกใช้โดย PrismReflector.Flash และ WarpPad.Flash รันเฉพาะเครื่องตัวเอง ไม่ส่งผ่านเน็ต
using UnityEngine;

// แสงวาบสั้นๆ (ขยายแล้วจางหาย) ใช้ตอนกระสุนเด้งคริสตัลและตอนวาร์ป เฉพาะภาพ ไม่มีผลต่อเกม
// คอมโพเนนต์ติดกับ Sprite แสงวาบ: ขยายจาก 40% เป็น 100% และจางหายภายใน 0.3 วิ แล้วลบตัวเอง
public class PrismBurst : MonoBehaviour
{
    private SpriteRenderer art;
    private Color color;
    private float size, age;
    // อายุของแสงวาบ (วินาที)
    private const float Life = .3f;

    // เริ่มเอฟเฟกต์: กำหนดสีและขนาด (หน่วยโลก) แล้วเรียก Update ทันทีให้เฟรมแรกถูกขนาด / เรียกจาก PrismFx.Burst
    public void Begin(Color tint, float worldSize)
    {
        art = GetComponent<SpriteRenderer>();
        color = tint;
        size = worldSize;
        Update();
    }

    // ทุกเฟรม: เพิ่มอายุ ขยายขนาดและลดความทึบ เมื่อครบอายุก็ Destroy
    private void Update()
    {
        if (art == null) return;
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Life);
        transform.localScale = Vector3.one * Mathf.Lerp(.4f, 1f, Mathf.Sqrt(t)) * size / PrismFx.DotWorldSize;
        art.color = new Color(color.r, color.g, color.b, (1 - t) * .9f);
        if (t >= 1) Destroy(gameObject);
    }
}
