// PlayerController.Fx.cs — เอฟเฟกต์ภาพจากรูปชุดใหม่ (เฟสรูป) เล่นในเครื่องตัวเอง (ทุกเครื่องเรียกจาก RPC เดิมอยู่แล้ว)
// รูปอยู่ที่ Resources/Images/VFX: VFX_Heal (ซ่อมเลือด), VFX_Warp (วาร์ป/BLINK), VFX_CloakLoop/VFX_CloakReveal (ล่องหน; VFX_Cloak = ชุดเดิมเมื่อปิด CloakFx), VFX_BigExplosion (บอสระเบิด)
// ไม่มีรูป = ไม่เล่น (เกมทำงานเหมือนเดิม)
using UnityEngine;

// ส่วนเอฟเฟกต์ภาพชุดใหม่ของ PlayerController (partial): ซ่อมเลือด ล่องหน วาร์ป และระเบิดใหญ่ตอนบอสตาย
public partial class PlayerController
{
    // เล่นชีตเอฟเฟกต์ที่ตำแหน่ง/ตามยาน: sizeMul = เท่าของขนาดยาน, duration = วินาที
    private void PlayShipFx(string sheet, int columns, int rows, Vector3 position, float sizeMul, float duration, bool followShip)
    {
        Sprite[] frames = SkillSheetVisual.LoadGrid(sheet, columns, rows);
        if (frames == null || frames.Length == 0) return;
        float shipSize = spriteRenderer != null ? Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) : 2f;
        SkillSheetVisual.Create(frames, followShip ? transform : null, position, Mathf.Max(1f, shipSize) * sizeMul, duration);
    }

    // เอฟเฟกต์ประกายเขียวตอนซ่อมเลือด (HEAL) ชีต 4x4 ขนาด 1.7 เท่าของยาน เล่น 0.7 วินาที และเคลื่อนตามยาน
    private void PlayHealFx() => PlayShipFx("VFX/VFX_Heal", 4, 4, transform.position, 1.7f, .7f, true);
    // เอฟเฟกต์วังวนม่วงตอนเริ่มล่องหน (CLOAK) ชีต 8x1 ขนาด 1.6 เท่าของยาน เล่น 0.5 วินาที ค้างที่ตำแหน่งเดิม
    // FeatureFlags.CloakFx: ใช้ภาพชุดใหม่ (แสงวาบ VFX_CloakReveal) แทนวังวนม่วงเดิม ให้ทั้งสกิลเป็นภาพชุดเดียวกัน
    private void PlayCloakFx()
    {
        if (FeatureFlags.CloakFx) PlayShipFx("VFX/VFX_CloakReveal", 8, 1, transform.position, 1.8f, .5f, false);
        else PlayShipFx("VFX/VFX_Cloak", 8, 1, transform.position, 1.6f, .5f, false);
    }

    // ===== ภาพล่องหนชุดใหม่ (FeatureFlags.CloakFx) =====
    // VFX_CloakLoop: ออร่าม่วงระยิบ 8 เฟรม วนรอบละ 0.5 วิ ตามยานตลอดเวลาล่องหน (ศัตรูไม่เห็น)
    // VFX_CloakReveal: แสงวาบ + วงแหวน + เศษแสง 8 เฟรม เล่นครั้งเดียวตอนหลุดล่องหน (ทุกคนเห็นว่ายานโผล่ตรงไหน)
    private SkillSheetVisual cloakLoopFx;
    // เริ่มออร่าระหว่างล่องหน (seconds = ระยะเวลาล่องหน)
    private void StartCloakLoopFx(float seconds)
    {
        StopCloakLoopFx();
        if (!FeatureFlags.CloakFx || HiddenFromLocalViewer) return; // ศัตรู: ยานหายไปเฉย ๆ เหมือนเดิม
        Sprite[] frames = SkillSheetVisual.LoadGrid("VFX/VFX_CloakLoop", 8, 1);
        if (frames == null || frames.Length == 0) return;
        int loops = Mathf.Max(1, Mathf.RoundToInt(seconds / .5f)); // 8 เฟรมต่อ 0.5 วิ
        var sequence = new Sprite[frames.Length * loops];
        for (int i = 0; i < sequence.Length; i++) sequence[i] = frames[i % frames.Length];
        float shipSize = spriteRenderer != null ? Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) : 2f;
        cloakLoopFx = SkillSheetVisual.Create(sequence, transform, transform.position, Mathf.Max(1f, shipSize) * 1.5f, seconds);
    }
    // หยุดออร่า (หลุดล่องหน / ตาย)
    private void StopCloakLoopFx()
    {
        if (cloakLoopFx != null) Destroy(cloakLoopFx.gameObject);
        cloakLoopFx = null;
    }
    // แสงแตกกระจายตอนหลุดล่องหน (อยู่กับที่ ขนาด 1.8 เท่าของยาน 0.5 วิ)
    private void PlayCloakRevealFx()
    {
        if (FeatureFlags.CloakFx) PlayShipFx("VFX/VFX_CloakReveal", 8, 1, transform.position, 1.8f, .5f, false);
    }
    // เอฟเฟกต์วาร์ป (BLINK/แท่นวาร์ป) เล่นพร้อมกัน 2 จุด: ตำแหน่งต้นทาง from และปลายทาง to
    private void PlayWarpFx(Vector2 from, Vector2 to)
    {
        PlayShipFx("VFX/VFX_Warp", 4, 4, from, 1.5f, .55f, false);
        PlayShipFx("VFX/VFX_Warp", 4, 4, to, 1.5f, .55f, false);
    }
    // บอสถูกทำลาย: ระเบิดใหญ่เพิ่มทับระเบิดปกติ
    private void PlayBossDeathFx()
    {
        if (!IsBoss) return;
        PlayShipFx("VFX/VFX_BigExplosion", 4, 4, transform.position, 3.2f, 1f, false);
    }
}
