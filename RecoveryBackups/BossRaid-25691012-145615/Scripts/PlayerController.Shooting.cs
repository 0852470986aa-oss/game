// PlayerController.Shooting.cs — ระบบยิงแบบเบา (partial class ของ PlayerController)
// แทนการสร้างกระสุนทุกนัดผ่าน PhotonNetwork.Instantiate (หนักมากเมื่อผู้เล่นเยอะ)
// วิธีใหม่: เครื่องคนยิงสร้างกระสุนของตัวเองทันที (เป็นคนตัดสินว่าโดน) แล้วส่ง RPC "ยิงจากตรงนี้ ทิศนี้" ให้เครื่องอื่น
// เครื่องอื่นสร้างกระสุนสำเนาไว้แสดงผลเท่านั้น และเลื่อนไปข้างหน้าตามเวลาที่ข้อความใช้เดินทาง
// ปิดได้ด้วย FeatureFlags.LightBullets = false (กลับไปใช้แบบเดิม)
using UnityEngine;
using Photon.Pun;

// ส่วนระบบยิงแบบเบาของ PlayerController (partial): สร้างกระสุนในเครื่องตัวเองแล้วส่ง RPC ให้เครื่องอื่นสร้างสำเนา
public partial class PlayerController
{
    // เรียกจาก Shoot() บนเครื่องเจ้าของยาน
    private void FireLightShot(Vector3 spawnPos)
    {
        float damage = attack * PowerDamageFactor; // ไอเท็ม DAMAGE ในแม็พ (เฟส 7)
        // อาวุธพิเศษของยานใหม่ (กระจาย/สไนเปอร์/ปืนกล) — PlayerController.Content.cs
        if (FireWeaponVolley(spawnPos, damage)) return;
        float angle = transform.eulerAngles.z;
        int shooter = CombatantId;
        BulletController.SpawnLocal(spawnPos, angle, damage, shooter, true, 0f);
        photonView.RPC("FireShotRPC", RpcTarget.Others, (Vector2)spawnPos, angle, damage);
    }

    // RPC บนเครื่องอื่น: สร้างกระสุนสำเนา (ไม่คิดดาเมจ) ชดเชยเวลาที่ข้อความเดินทาง
    [PunRPC]
    public void FireShotRPC(Vector2 position, float angle, float damage, PhotonMessageInfo info)
    {
        if (!FromController(info)) return;
        float lag = Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime));
        BulletController.SpawnLocal(position, angle, damage, CombatantId, false, lag);
    }
}
