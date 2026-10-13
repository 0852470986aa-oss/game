// ไฟล์ ControlRingGraphic.cs: กราฟิก UI รูปวงแหวน (เต็มวงหรือบางส่วน) สำหรับปุ่มควบคุมบน HUD
// สร้าง/ตั้งค่าโดย GameplayManager.HUD.cs (เรียก SetRing เช่นแสดงคูลดาวน์สกิล)
using UnityEngine;

// วาดวงแหวนปุ่มควบคุมด้วย mesh เท่านั้น ไม่รับอินพุตและไม่คำนวณสกิล
// Code-drawn HUD artwork: no baked labels, texture rectangles or extra input handlers.
// สืบทอด MaskableGraphic ของ Unity UI แล้ววาดรูปทรงเองใน OnPopulateMesh
public sealed class ControlRingGraphic : UnityEngine.UI.MaskableGraphic
{
    // SerializeField: ให้รูปวงแหวนคงอยู่เมื่อบันทึก HUD ลง Scene (ค่าตอนรันเหมือนเดิม)
    // innerRatio = รัศมีวงในเทียบวงนอก (0 = วงกลมทึบ), fraction = สัดส่วนวงที่วาด (1 = เต็มวง)
    [SerializeField] private float innerRatio;
    [SerializeField] private float fraction = 1f;

    // ตั้งขนาดวงใน/สัดส่วนวง (บีบให้อยู่ 0-1) ถ้าค่าเปลี่ยนจึงสั่งวาด mesh ใหม่
    public void SetRing(float inner, float amount = 1f)
    {
        inner = Mathf.Clamp01(inner);
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(innerRatio, inner) && Mathf.Approximately(fraction, amount)) return;
        innerRatio = inner;
        fraction = amount;
        SetVerticesDirty();
    }

    // Unity UI เรียกเมื่อต้องสร้าง mesh: วาดวงแหวนเริ่มจากด้านบนวนตามเข็มนาฬิกา ไม่เกิน 64 ช่วง
    // แต่ละมุมมี 4 จุด (ขอบในจาง, วงใน, วงนอก, ขอบนอกจาง) ต่อเป็นสามเหลี่ยม 3 แถบ ให้ขอบนุ่ม
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        if (fraction <= 0) return;
        Rect rect = GetPixelAdjustedRect();
        float outer = Mathf.Min(rect.width, rect.height) * .5f - .6f;
        if (outer <= 0) return;
        float inner = outer * innerRatio;
        int steps = Mathf.Max(1, Mathf.CeilToInt(64 * fraction));
        Color transparent = color; transparent.a = 0;
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.PI * .5f - (float)i / steps * fraction * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(rect.center + direction * Mathf.Max(0, inner - .6f), inner > 0 ? transparent : color, Vector2.zero);
            mesh.AddVert(rect.center + direction * inner, color, Vector2.zero);
            mesh.AddVert(rect.center + direction * outer, color, Vector2.zero);
            mesh.AddVert(rect.center + direction * (outer + .6f), transparent, Vector2.zero);
            if (i == steps) continue;
            int v = i * 4;
            for (int band = 0; band < 3; band++)
            {
                mesh.AddTriangle(v + band, v + band + 4, v + band + 1);
                mesh.AddTriangle(v + band + 1, v + band + 4, v + band + 5);
            }
        }
    }
}
