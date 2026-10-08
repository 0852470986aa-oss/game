// PlayerController.Items.cs — ผลของตีบวกยานและไอเท็มในแมตช์ (เฟส 5) (partial class ของ PlayerController)
// ค่าพลังคำนวณจาก Economy.BuildBonus ตอน InitializeStats บนเครื่องเจ้าของยานเท่านั้น
// (HP สูงสุดส่งให้เครื่องอื่นผ่าน OnPhotonSerializeView อยู่แล้ว ดาเมจกระสุนคิดบนเครื่องคนยิง จึงไม่ต้องส่งค่าอื่นเพิ่ม)
using UnityEngine;
using Photon.Pun;

// ส่วนผลของตีบวกและไอเท็มของ PlayerController (partial): คูณค่าพลัง ฟื้นเลือด ฟื้นเมื่อฆ่าได้ และเวลากันตัวหลังเกิด
public partial class PlayerController
{
    private float itemRegen;          // HP/วินาที เมื่อไม่โดนยิง 3 วินาที (Nano Repair)
    private float itemKillHeal;       // ฟื้น % ของ HP สูงสุดเมื่อฆ่าได้ (Siphon Module)
    private float itemProtection;     // วินาทีกันตัวหลังเกิดเพิ่ม (Aegis Core)
    private float lastHullDamageAt = -10f;

    // คูณค่าพลังตามตีบวก + ไอเท็ม (ห้องปิด UPGRADES หรือปิดสวิตช์ = ไม่เปลี่ยนอะไร)
    private void ApplyLoadoutBonus(int shipIndex)
    {
        if (!FeatureFlags.Upgrades && !FeatureFlags.Items) return;
        if (PhotonNetwork.InRoom && !MatchRules.UpgradesAllowed(PhotonNetwork.CurrentRoom)) return;
        var bonus = Economy.BuildBonus(shipIndex);
        maxHp = Mathf.Round(maxHp * (1f + bonus.hp));
        attack *= 1f + bonus.atk;
        speed *= 1f + bonus.spd;
        fireCooldown *= 1f - bonus.fireRate;
        maxCooldown *= 1f - bonus.cooldown;
        itemRegen = bonus.regen;
        itemKillHeal = bonus.killHeal;
        itemProtection = bonus.protection;
    }

    // เรียกทุกเฟรมบนเครื่องเจ้าของ: ฟื้นเลือดช้าๆ เมื่อไม่โดนยิงมา 3 วินาที
    private void UpdateItemEffects()
    {
        if (itemRegen <= 0 || isDead || currentHp >= maxHp) return;
        if (Time.time - lastHullDamageAt < 3f) return;
        currentHp = Mathf.Min(maxHp, currentHp + itemRegen * Time.deltaTime);
    }

    // เราฆ่าศัตรูได้ (เรียกจาก OnPlayerDiedRPC บนเครื่องคนฆ่า)
    public void OnLocalKill()
    {
        if (itemKillHeal <= 0 || isDead || !photonView.IsMine) return;
        currentHp = Mathf.Min(maxHp, currentHp + maxHp * itemKillHeal);
    }
}
