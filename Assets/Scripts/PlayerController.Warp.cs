// ไฟล์นี้เป็นส่วนหนึ่งของ partial class PlayerController (ดูไฟล์หลัก PlayerController.cs) ทำงานคู่กับ WarpPad.cs
// ลำดับ: WarpPad ตรวจการชน -> TryWarp (เฉพาะเจ้าของยาน) -> RPC WarpRPC ไปทุกเครื่องให้ย้ายยานพร้อมกัน
using UnityEngine;
using Photon.Pun;

// ส่วนวาร์ปของ PlayerController (แท่นวาร์ปในแม็พปริซึม)
public partial class PlayerController
{
    // คูลดาวน์ระหว่างการวาร์ป 3 วินาที (กันเด้งไปมาระหว่างแท่น) และเวลาที่วาร์ปได้ครั้งถัดไป
    public const float WarpCooldown = 3f;
    private float warpReadyAt;

    // เรียกจาก WarpPad เมื่อยานแตะแท่น: เฉพาะเจ้าของยานเป็นคนสั่ง
    public void TryWarp(WarpPad from, WarpPad to)
    {
        if (!photonView.IsMine || from == null || to == null) return;
        if (isDead || matchEnded || isSpawnProtected || !BattleInputAllowed) return;
        if (Time.time < warpReadyAt) return;
        warpReadyAt = Time.time + WarpCooldown;
        photonView.RPC("WarpRPC", RpcTarget.All, (Vector2)from.transform.position, to.ExitPoint());
    }

    // RPC ย้ายยาน: รันทุกเครื่อง (ส่งด้วย RpcTarget.All) ตรวจว่าผู้ส่งคือเจ้าของยาน แล้วย้ายตำแหน่งทันที
    // ตั้ง networkPosition ด้วยเพื่อไม่ให้เครื่องอื่น Lerp ไหลข้ามแม็พ, กระพริบแท่นต้นทาง/ปลายทาง และเครื่องเจ้าของให้กล้องกระโดดตาม
    [PunRPC]
    public void WarpRPC(Vector2 from, Vector2 to, PhotonMessageInfo info)
    {
        if (!FromController(info)) return;
        PlayWarpFx(from, to); // รูปชุดใหม่: ประตูวาร์ปทั้งจุดออกและจุดถึง
        transform.position = to;
        networkPosition = to; // ไม่ให้ยานอีกฝั่งค่อยๆ ไหลข้ามแม็พ
        if (playerRigidbody != null) playerRigidbody.position = to;
        previousVfxPosition = transform.position;
        Physics2D.SyncTransforms();
        foreach (var pad in FindObjectsByType<WarpPad>(FindObjectsSortMode.None))
        {
            Vector2 padPosition = pad.transform.position;
            if ((padPosition - from).sqrMagnitude < 1f || (pad.ExitPoint() - to).sqrMagnitude < 1f) pad.Flash();
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ShieldHit");
        if (IsLocalHuman)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.SnapToTarget();
        }
    }
}
