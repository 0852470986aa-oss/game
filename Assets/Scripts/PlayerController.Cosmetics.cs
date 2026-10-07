// ไฟล์นี้เป็นส่วนหนึ่งของ partial class PlayerController (ดูไฟล์หลัก PlayerController.cs)
// สีย้อมยานอ่านจาก Custom Property "ShipSkin" ของเจ้าของ (ShipPaint ใน BattleLoadoutCatalog.cs) อีโมตแสดงผ่าน GameplayManager.ShowEmote
using UnityEngine;
using Photon.Pun;

// ส่วนตกแต่งของ PlayerController: สียาน (ShipPaint) และอีโมตระหว่างแข่ง ไม่มีผลต่อการเล่น
public partial class PlayerController
{
    // สีที่ใช้ย้อมภาพยาน ใช้คืนสีหลังกระพริบ/หลังหายสตัน
    private Color paintTint = Color.white;
    public Color PaintTint => paintTint;

    // ข้อความอีโมต: index ส่งผ่าน Photon อย่าสลับลำดับ
    public static readonly string[] Emotes = { "GG", "NICE SHOT!", "OOPS!", "HELLO!" };
    // คูลดาวน์อีโมต 2.5 วินาที (กันกดรัว) และเวลาที่ส่งอีโมตครั้งถัดไปได้
    private const float EmoteCooldown = 2.5f;
    private float nextEmoteAt;

    // ดึงสีของเจ้าของยานลำนี้จาก Photon แล้วย้อมภาพยาน (เรียกใน Start ทุกเครื่อง ทุกคนจึงเห็นสีเดียวกัน)
    private void ApplyPaint()
    {
        paintTint = IsBot ? ShipPaint.Colors[botPaint] : ShipPaint.For(photonView.Owner);
        // ยานใหม่ที่ยังไม่มีรูปของตัวเอง: ย้อมสีประจำยานเพิ่ม ให้แยกจากยานเดิมได้ (เฟส 7)
        paintTint *= BattleLoadoutCatalog.ShipTint(ShipIndex);
        if (spriteRenderer != null && !isStunned && !isDead) spriteRenderer.color = paintTint;
    }

    // เรียกจากปุ่มอีโมตบน HUD (เฉพาะยานของเรา) มีคูลดาวน์กันสแปม
    public bool TrySendEmote(int index)
    {
        // เจ้าของส่ง RPC ShowEmoteRPC ไป RpcTarget.All ให้ทุกเครื่อง (รวมตัวเอง) แสดงอีโมตเหนือยาน
        if (!photonView.IsMine || index < 0 || index >= Emotes.Length) return false;
        if (Time.unscaledTime < nextEmoteAt) return false;
        nextEmoteAt = Time.unscaledTime + EmoteCooldown;
        photonView.RPC("ShowEmoteRPC", RpcTarget.All, index);
        return true;
    }

    // RPC แสดงอีโมต: รันทุกเครื่อง ตรวจว่าผู้ส่งคือเจ้าของยานจริงและ index ถูกต้อง แล้วให้ GameplayManager แสดงข้อความ
    [PunRPC]
    public void ShowEmoteRPC(int index, PhotonMessageInfo info)
    {
        if (!FromController(info) || index < 0 || index >= Emotes.Length) return;
        if (GameplayManager.Instance != null) GameplayManager.Instance.ShowEmote(this, Emotes[index]);
    }
}
