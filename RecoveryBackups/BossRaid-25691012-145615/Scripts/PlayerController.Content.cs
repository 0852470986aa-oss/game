// PlayerController.Content.cs — เนื้อหาเพิ่มเฟส 7 (partial class ของ PlayerController)
// 1) อาวุธหลัก 4 แบบตามยาน: BLASTER (เดิม), SCATTER (กระจาย 3 นัด), RAILGUN (กระสุนเร็วไกล), GATLING (รัว ส่ายเล็กน้อย)
// 2) สกิลใหม่: BLINK (วาร์ปไปข้างหน้า), HEAL (ซ่อมเลือด), CLOAK (ล่องหนจากศัตรู 3 วิ ยิงแล้วหลุด)
// 3) ผลของไอเท็มเกิดในแม็พ (Power-up): ซ่อม / ยิงเร็ว / วิ่งเร็ว / โล่ / แรงขึ้น (PowerUpManager.cs เป็นคนเรียก)
// 4) รูปยานใหม่ (Images/ship4-6) ถ้ามีจะเปลี่ยนให้เอง ไม่มีใช้ Prefab ยานเดิมย้อมสีประจำยาน
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// ส่วนเนื้อหาเพิ่มเฟส 7 ของ PlayerController (partial): อาวุธหลักแบบต่าง ๆ, สกิลใหม่, ผลของ Power-up และรูปยานใหม่
public partial class PlayerController
{
    public int WeaponType => BattleLoadoutCatalog.Ships[ShipIndex].weapon; // ชนิดอาวุธหลักของยานนี้ (BLASTER / SCATTER / RAILGUN / GATLING)

    // ===== 1) อาวุธหลัก =====
    // มุมของแต่ละนัดตามชนิดอาวุธ (ทุกเครื่องคำนวณเหมือนกันจากมุมที่ส่งมา)
    private static float[] VolleyAngles(int weapon)
        => weapon == BattleLoadoutCatalog.WeaponSpread ? new[] { -12f, 0f, 12f } : new[] { 0f };
    // ตัวคูณความเร็วกระสุนตามชนิดอาวุธ: RAILGUN (WeaponSniper) เร็วขึ้น 1.8 เท่า อาวุธอื่นเท่าเดิม
    private static float VolleySpeed(int weapon) => weapon == BattleLoadoutCatalog.WeaponSniper ? 1.8f : 1f;

    // ยิงตามชนิดอาวุธ (เรียกจาก FireLightShot) คืน false = อาวุธปกติ ให้ใช้วิธีเดิม
    private bool FireWeaponVolley(Vector3 spawnPos, float damage)
    {
        int weapon = WeaponType;
        if (weapon == BattleLoadoutCatalog.WeaponStandard) return false;
        float angle = transform.eulerAngles.z;
        if (weapon == BattleLoadoutCatalog.WeaponGatling) angle += Random.Range(-3.5f, 3.5f);
        SpawnVolley(spawnPos, angle, damage, weapon, true, 0f);
        photonView.RPC("FireVolleyRPC", RpcTarget.Others, (Vector2)spawnPos, angle, damage, weapon);
        return true;
    }

    // สร้างกระสุนในเครื่องนี้ตามมุมของอาวุธ (SCATTER = 3 นัด) และปรับความเร็วตามชนิดอาวุธ
    // authority = เครื่องที่ยิงจริง, lag = เวลาหน่วงเครือข่ายใช้ชดเชยตำแหน่งกระสุน
    private void SpawnVolley(Vector2 position, float angle, float damage, int weapon, bool authority, float lag)
    {
        foreach (float offset in VolleyAngles(weapon))
        {
            var bullet = BulletController.SpawnLocal(position, angle + offset, damage, CombatantId, authority, lag);
            if (bullet != null) bullet.speed *= VolleySpeed(weapon);
        }
    }

    // RPC ที่เครื่องอื่นได้รับเมื่อมีการยิงอาวุธพิเศษ: ตรวจว่าผู้ส่งเป็นเจ้าของยาน แล้วสร้างกระสุนตามพร้อมชดเชย lag จาก SentServerTime
    [PunRPC]
    public void FireVolleyRPC(Vector2 position, float angle, float damage, int weapon, PhotonMessageInfo info)
    {
        if (!FromController(info)) return;
        float lag = Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime));
        SpawnVolley(position, angle, damage, weapon, false, lag);
    }

    // ===== 2) สกิลใหม่ (skillType 4-6) =====
    private bool UseNewSkill()
    {
        switch (skillType)
        {
            case 4: SkillBlink(); return true;
            case 5: photonView.RPC("HealRPC", RpcTarget.All); return true;
            case 6: photonView.RPC("CloakRPC", RpcTarget.All, true); return true;
        }
        return false;
    }

    // BLINK: วาร์ปไปข้างหน้าสูงสุด 9 หน่วย หยุดก่อนชนสิ่งกีดขวาง/ขอบสนาม แล้วบอกทุกเครื่อง (ใช้ WarpRPC เดิม)
    private void SkillBlink()
    {
        Vector2 from = transform.position;
        Vector2 dir = transform.up;
        float distance = 9f;
        foreach (var hit in Physics2D.CircleCastAll(from, 1.2f, dir, distance))
        {
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<BulletController>() != null || hit.collider.GetComponentInParent<SkillController>() != null) continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .3f));
        }
        Vector2 to = from + dir * distance;
        to.x = Mathf.Clamp(to.x, arenaMin.x + 1.5f, arenaMax.x - 1.5f);
        to.y = Mathf.Clamp(to.y, arenaMin.y + 1.5f, arenaMax.y - 1.5f);
        photonView.RPC("WarpRPC", RpcTarget.All, from, to);
    }

    // HEAL: เจ้าของเพิ่มเลือด (ค่าใหม่ส่งต่อผ่าน OnPhotonSerializeView) ทุกเครื่องเห็นยานกระพริบเขียว
    [PunRPC]
    public void HealRPC(PhotonMessageInfo info)
    {
        if (!FromController(info) || isDead) return;
        if (photonView.IsMine) currentHp = Mathf.Min(maxHp, currentHp + maxHp * .35f);
        StartCoroutine(TintFlash(new Color(.4f, 1f, .5f)));
        PlayHealFx(); // รูปชุดใหม่: ประกายเขียว
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Heal"); // ปิด NewSounds = เสียงโดนโล่แบบเดิม
    }

    // Coroutine เปลี่ยนสียานเป็นสีที่กำหนด 0.35 วินาที แล้วคืนเป็นสีประจำยาน (ถ้าไม่ได้ติดสตัน)
    private System.Collections.IEnumerator TintFlash(Color color)
    {
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = color;
        yield return new WaitForSeconds(.35f);
        if (spriteRenderer != null && !isStunned) spriteRenderer.color = paintTint;
    }

    // ===== CLOAK =====
    private bool cloaked;
    private float cloakUntil;
    private readonly List<Renderer> cloakHidden = new List<Renderer>();
    public bool IsCloaked => cloaked && Time.time < cloakUntil;

    // RPC เปิด/ปิดล่องหน (CLOAK) บนทุกเครื่อง: เล่นเอฟเฟกต์ตอนเริ่ม และตั้งเวลาหมดล่องหน 3 วินาที
    [PunRPC]
    public void CloakRPC(bool on, PhotonMessageInfo info)
    {
        if (!FromController(info)) return;
        bool wasCloaked = cloaked;
        if (on && !cloaked) PlayCloakFx(); // รูปชุดใหม่: วังวนม่วงตอนเริ่มล่องหน
        cloaked = on && !isDead;
        cloakUntil = Time.time + 3f;
        // ภาพชุดใหม่ (FeatureFlags.CloakFx): เริ่ม = แสงวาบ, ระหว่างล่องหน = ออร่าวนรอบยาน (เฉพาะตัวเอง/เพื่อนเห็น) / หลุดล่องหน = แสงแตกกระจาย (ทุกคนเห็น)
        if (cloaked && !wasCloaked) StartCloakLoopFx(3f);
        else if (!cloaked && wasCloaked)
        {
            StopCloakLoopFx();
            if (!isDead) PlayCloakRevealFx();
        }
    }

    // ศัตรูมองไม่เห็นยานที่ล่องหน (ตัวเองและเพื่อนร่วมทีมยังเห็น)
    public bool HiddenFromLocalViewer
    {
        get
        {
            if (!IsCloaked || IsLocalHuman) return false;
            var me = GameplayManager.Instance != null ? GameplayManager.Instance.localPlayer : null;
            return me == null || !MatchRules.IsAlly(me.CombatantId, CombatantId);
        }
    }

    // เรียกจาก LateUpdate ทุกเฟรม: ซ่อน/แสดงทุกภาพของยาน (ตัวยาน ไฟไอพ่น ฯลฯ)
    private void ApplyCloakVisibility()
    {
        // เจ้าของ: หมดเวลา = เลิกล่องหน
        if (cloaked && Time.time >= cloakUntil && photonView.IsMine) photonView.RPC("CloakRPC", RpcTarget.All, false);
        bool hide = HiddenFromLocalViewer && !isDead;
        if (hide)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; cloakHidden.Add(r); }
        }
        else if (cloakHidden.Count > 0)
        {
            foreach (var r in cloakHidden) if (r != null) r.enabled = r != spriteRenderer || !isDead;
            cloakHidden.Clear();
        }
        // ตัวเอง/เพื่อน: เห็นยานโปร่งใสครึ่งหนึ่งระหว่างล่องหน
        if (!hide && spriteRenderer != null && IsCloaked && !isDead)
        {
            var c = spriteRenderer.color;
            c.a = .45f;
            spriteRenderer.color = c;
            cloakFaded = true;
        }
        else if (cloakFaded && spriteRenderer != null && !IsCloaked)
        {
            // แก้บัค: หมดล่องหนแล้วยานค้างโปร่งใส (เดิมกลับมาทึบเฉพาะตอนโดนยิง/ติดสตัน) -> คืนความทึบทันที
            cloakFaded = false;
            var c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
        }
    }
    // ยานถูกทำให้โปร่งใสเพราะล่องหนอยู่ (ต้องคืนความทึบเมื่อหมดล่องหน)
    private bool cloakFaded;

    // ยิงแล้วหลุดล่องหน (เรียกจาก Shoot)
    private void BreakCloak()
    {
        if (cloaked && photonView.IsMine) photonView.RPC("CloakRPC", RpcTarget.All, false);
    }

    // ===== 3) Power-up =====
    public const int PowerRepair = 0, PowerOverdrive = 1, PowerBoost = 2, PowerShield = 3, PowerDamage = 4;
    private float overdriveUntil, boostUntil, damageUntil;
    public float PowerFireFactor => Time.time < overdriveUntil ? .5f : 1f;
    public float PowerSpeedFactor => Time.time < boostUntil ? 1.35f : 1f;
    public float PowerDamageFactor => Time.time < damageUntil ? 1.5f : 1f;
    public bool HasOverdrive => Time.time < overdriveUntil;
    public bool HasBoost => Time.time < boostUntil;
    public bool HasDamageBoost => Time.time < damageUntil;

    // เรียกบนเครื่องเจ้าของยาน (คน = เครื่องตัวเอง, บอท = Master) เมื่อเก็บไอเท็มได้
    public void ApplyPowerUp(int type)
    {
        if (!photonView.IsMine || isDead) return;
        switch (type)
        {
            case PowerRepair: currentHp = Mathf.Min(maxHp, currentHp + maxHp * .4f); break;
            case PowerOverdrive: overdriveUntil = Time.time + 8f; break;
            case PowerBoost: boostUntil = Time.time + 8f; StartCoroutine(RefreshSpeedAfter(8.05f)); break;
            case PowerShield: photonView.RPC("ActivateShieldRPC", RpcTarget.All); break;
            case PowerDamage: damageUntil = Time.time + 8f; break;
        }
        UpdateEffectiveSpeed();
    }

    // Coroutine รอให้บูสต์ความเร็วหมดเวลา แล้วคำนวณความเร็วยานใหม่
    private System.Collections.IEnumerator RefreshSpeedAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        UpdateEffectiveSpeed();
    }

    // ===== 4) รูปยานใหม่ =====
    private void ApplyShipArt()
    {
        if (spriteRenderer == null || ShipIndex < 3) return;
        var own = Resources.Load<Sprite>(BattleLoadoutCatalog.Ships[ShipIndex].spritePath);
        if (own == null) return;
        // ปรับให้ยานใหม่สูงเท่ารูปเดิมของ Prefab (ตัวชนเท่าเดิม) ไม่ว่ารูปจะกี่พิกเซล
        float worldHeight = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.y : 0f;
        if (worldHeight > .01f && own.rect.height > 0f)
            own = Sprite.Create(own.texture, own.rect, new Vector2(.5f, .5f), own.rect.height / worldHeight, 0, SpriteMeshType.FullRect);
        spriteRenderer.sprite = own;
    }

    // ===== 5) เสียเลือดจากวงนอกเขต Battle Royale (เฟส 7B) — เรียกบนเครื่องเจ้าของยาน =====
    public void ApplyZoneDamage(float amount)
    {
        if (!photonView.IsMine || isDead || matchEnded || !BattleInputAllowed) return;
        currentHp -= amount;
        lastHullDamageAt = Time.time;
        if (currentHp <= 0) { currentHp = 0; Die(-1); }
    }
}
