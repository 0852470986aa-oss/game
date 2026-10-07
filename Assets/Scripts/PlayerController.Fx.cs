// PlayerController.Fx.cs — เอฟเฟกต์ภาพจากรูปชุดใหม่ (เฟสรูป) เล่นในเครื่องตัวเอง (ทุกเครื่องเรียกจาก RPC เดิมอยู่แล้ว)
// รูปอยู่ที่ Resources/Images/VFX: VFX_Heal (ซ่อมเลือด), VFX_Warp (วาร์ป/BLINK), VFX_Cloak (ล่องหน), VFX_BigExplosion (บอสระเบิด)
// ไม่มีรูป = ไม่เล่น (เกมทำงานเหมือนเดิม)
using UnityEngine;

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

    private void PlayHealFx() => PlayShipFx("VFX/VFX_Heal", 4, 4, transform.position, 1.7f, .7f, true);
    private void PlayCloakFx() => PlayShipFx("VFX/VFX_Cloak", 8, 1, transform.position, 1.6f, .5f, false);
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
