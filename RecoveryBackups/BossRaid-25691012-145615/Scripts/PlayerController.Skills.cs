// ส่วนสกิลและสถานะผิดปกติของ PlayerController: กดใช้สกิล (STUN/SHIELD/NOVA/SEEKER), โล่, สตัน, ความช้า, โซนบึงพิษ/Energy Core
// สกิลแบบยิง (STUN/NOVA/SEEKER) สร้างวัตถุ SkillController.cs ผ่าน PhotonNetwork.Instantiate ส่วนโล่ใช้ RPC
// ค่าดาเมจ/เวลาของสกิลอยู่ใน BattleBalance.cs คูลดาวน์อยู่ใน BattleLoadoutCatalog.cs
using UnityEngine;
using Photon.Pun;

// ส่วน Skills ของ PlayerController; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class PlayerController
{
    // เรียกจาก Update เฉพาะเจ้าของยาน: ตรวจว่ากดปุ่มสกิล (ปุ่มบนจอ หรือ Space/E) และคูลดาวน์หมดแล้ว จึงเริ่มคูลดาวน์ใหม่และใช้สกิล
    private void HandleSkill()
    {
        if (IsBot)
        {
            // บอทกดสกิลผ่าน BotController (ใช้ครั้งเดียวแล้วรีเซ็ต)
            if (botSkill && currentCooldown <= 0) { currentCooldown = maxCooldown; UseSkill(); }
            botSkill = false;
            return;
        }
        bool isSkillPressed = UIButton.IsPressed("Skill") || UIButton.IsPressed("skill") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);

        // เช็คจาก GameplayManager โดยตรงเผื่อปุ่มไม่ได้ตั้งชื่อว่า Skill
        if (GameplayManager.Instance != null && GameplayManager.Instance.skillButton != null)
        {
            if (GameplayManager.Instance.skillButton.isPressed)
            {
                isSkillPressed = true;
            }
        }

        if (isSkillPressed && currentCooldown <= 0)
        {
            currentCooldown = maxCooldown;
            UseSkill();
            // นับการใช้สกิล (ภารกิจรายวัน)
            Progression.SkillsThisMatch++;
        }
    }

    // ใช้สกิลตาม skillType (รันเฉพาะเจ้าของ): STUN/NOVA/SEEKER = PhotonNetwork.Instantiate วัตถุสกิลพร้อมดาเมจให้ทุกเครื่องเห็น
    // SHIELD = ส่ง RPC ActivateShieldRPC ไป RpcTarget.All (ทุกเครื่องรวมตัวเอง) เพื่อเปิดโล่ให้ทุกคนเห็น
    private void UseSkill()
    {
        Debug.Log("Used Skill: " + skillName);
        // เฟส 7: BLINK / HEAL / CLOAK (PlayerController.Content.cs)
        if (UseNewSkill()) return;

        if (skillType == 0) // STUN
        {
            object[] data = new object[] { RemoteCatalog.SkillDamage(0, BattleBalance.StunDamage), CombatantId }; // [1] = ผู้ใช้สกิล (รองรับบอท)
            PhotonNetwork.Instantiate("Skill_StunWave", GetFirePosition(), transform.rotation, 0, data);
        }
        else if (skillType == 1) // SHIELD
        {
            photonView.RPC("ActivateShieldRPC", RpcTarget.All);
        }
        else if (skillType == 2) // NOVA
        {
            object[] data = new object[] { RemoteCatalog.SkillDamage(2, BattleBalance.NovaDamage), CombatantId };
            PhotonNetwork.Instantiate("Skill_NovaBlast", GetFirePosition(), Quaternion.identity, 0, data);
        }
        else if (skillType == 3) // SEEKER
        {
            object[] data = new object[] { RemoteCatalog.SkillDamage(3, BattleBalance.SeekerDamage), CombatantId };
            PhotonNetwork.Instantiate("Skill_SeekerMissile", GetFirePosition(), transform.rotation, 0, data);
        }
    }

    // RPC เปิดโล่: เจ้าของยานส่งด้วย RpcTarget.All จึงรันทุกเครื่อง ทุกเครื่องตั้ง isShielded และสร้างภาพโล่ของตัวเอง
    // โล่อยู่ BattleBalance.ShieldSeconds วินาที (ระหว่างนั้นวิ่งเร็วขึ้นตาม ShieldSpeedMultiplier) แล้ว Invoke DeactivateShield
    [PunRPC]
    public void ActivateShieldRPC()
    {
        if (isDead || matchEnded) return;
        isShielded = true;
        if (authoredShield != null) Destroy(authoredShield.gameObject);
        var frames = SkillSheetVisual.Load("VFX_Shield");
        float diameter = spriteRenderer != null ? Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) * 1.7f : 4f;
        authoredShield = SkillSheetVisual.Create(frames, transform, transform.position, diameter, BattleBalance.ShieldSeconds + .3f);
        if (authoredShield != null) authoredShield.breakTime = BattleBalance.ShieldSeconds;
        if (shieldVisual != null) shieldVisual.SetActive(authoredShield == null);
        UpdateEffectiveSpeed();
        CancelInvoke("DeactivateShield");
        Invoke("DeactivateShield", BattleBalance.ShieldSeconds);
    }

    // ปิดโล่ (ถูก Invoke จาก ActivateShieldRPC จึงรันทุกเครื่อง): คืนความเร็ว และเล่นเอฟเฟกต์/เสียงโล่แตก
    private void DeactivateShield()
    {
        isShielded = false;
        UpdateEffectiveSpeed();
        if (shieldVisual != null) shieldVisual.SetActive(false);

        // Shield Break Effect (เอฟเฟกต์โล่แตก สีฟ้า ขนาดใหญ่)
        if (authoredShield != null)
        {
            authoredShield.Break();
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ShieldBreak");
            return;
        }
        GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
        if (impactPrefab != null)
        {
            GameObject fx = Instantiate(impactPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = new Vector3(2f, 2f, 2f);
            var sr = fx.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.2f, 0.8f, 1f, 1f); // สีฟ้าสว่าง
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ShieldBreak");
    }

    // Stun Visual
    private GameObject stunVisual;

    // RPC ทำให้ยานติดสตัน: ส่งด้วย RpcTarget.All จาก SkillController (คนยิงคลื่นสตัน) หรือ HazardController (สายฟ้า) จึงรันทุกเครื่อง
    // ไม่มีผลถ้าตาย/กันตัว/มีโล่; สตัน BattleBalance.StunSeconds วินาที ระหว่างนั้น Update ของเจ้าของจะไม่รับ input (ขยับ/ยิงไม่ได้)
    [PunRPC]
    public void ApplyStunRPC()
    {
        if (!BattleInputAllowed || isDead || isSpawnProtected) return;
        if (isShielded) return; // ติดโล่ป้องกันสถานะได้
        isStunned = true;
        CancelInvoke("RemoveStun");
        Invoke("RemoveStun", BattleBalance.StunSeconds);

        // Stun Visual Feedback — ทั้งสองฝั่ง (ตัวเองและศัตรู) เห็นว่ายานนี้โดนสตัน
        if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 0.3f, 1f); // เหลืองจัด

        // สร้างไอคอน Stun หมุนเหนือหัว
        if (stunVisual == null)
        {
            stunVisual = new GameObject("StunIndicator");
            stunVisual.transform.SetParent(transform);
            stunVisual.transform.localPosition = new Vector3(0, 1.2f, 0);
            stunVisual.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            var sr = stunVisual.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 1f, 0f, 0.9f);
            sr.sortingOrder = 10;
            // ใช้ sprite จากยานตัวเอง (วงกลมเหลือง)
            SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
            if (mainSr != null) sr.sprite = mainSr.sprite;
        }
        stunVisual.SetActive(true);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Stun");
    }

    // ถูก Invoke หลังหมดเวลาสตัน (ทุกเครื่อง): คืนสียานและซ่อนไอคอนสตัน
    private void RemoveStun()
    {
        isStunned = false;
        if (spriteRenderer != null) spriteRenderer.color = paintTint;
        if (stunVisual != null) stunVisual.SetActive(false);
    }

    // RPC ทำให้ช้า 0.35 วินาที (โดนซ้ำจะต่อเวลา) ไม่มีผลถ้ามีโล่; รันทุกเครื่องที่ได้รับ (HazardController ส่งด้วย RpcTarget.All)
    // หมายเหตุ: ปัจจุบันบึงชะลอถูกจัดการผ่าน SetBattlefieldZone ในเครื่องเจ้าของยานเป็นหลัก
    [PunRPC]
    public void ApplySlowRPC()
    {
        if (isDead || !BattleInputAllowed) return;
        if (isShielded) return;
        
        // ถ้าเพิ่งโดน slow ไป ให้รีเฟรชเวลา
        CancelInvoke("RemoveSlow");
        isSlowed = true;
        UpdateEffectiveSpeed();
        Invoke("RemoveSlow", 0.35f);
    }

    // RPC ยกเลิกสถานะช้าทันที (คู่กับ ApplySlowRPC) รันทุกเครื่องที่ได้รับ
    [PunRPC]
    public void RemoveSlowRPC()
    {
        CancelInvoke("RemoveSlow");
        isSlowed = false;
        UpdateEffectiveSpeed();
    }

    // ถูก Invoke เมื่อครบเวลาช้า: คืนความเร็ว
    private void RemoveSlow()
    {
        isSlowed = false;
        UpdateEffectiveSpeed();
    }

    // true = ติดสถานะช้าจาก ApplySlowRPC (แยกจากการอยู่ในบึง swampSources)
    private bool isSlowed;

    // คำนวณ speed ใหม่จาก baseSpeed: คูณ ShieldSpeedMultiplier ถ้ามีโล่ และคูณ SlowSpeedMultiplier ถ้าช้า/อยู่ในบึง
    private void UpdateEffectiveSpeed()
    {
        if (baseSpeed <= 0f) return;
        speed = baseSpeed * (isShielded ? BattleBalance.ShieldSpeedMultiplier : 1f) * ((isSlowed || swampSources.Count > 0) ? BattleBalance.SlowSpeedMultiplier : 1f)
            * PowerSpeedFactor; // ไอเท็ม BOOST ในแม็พ (เฟส 7)
    }

    // swampSources = id ของโซนบึงที่ยานอยู่ข้างใน (ซ้อนกันได้หลายโซน), swampExposure = เวลาที่อยู่ในบึงต่อเนื่อง (วินาที)
    // IsSwampPoisoned ให้ HUD ใช้แสดงไอคอนพิษ; coreSources = id ของ Energy Core ที่ยานอยู่ข้างใน
    private readonly System.Collections.Generic.HashSet<int> swampSources = new System.Collections.Generic.HashSet<int>();
    private float swampExposure;
    public bool IsSwampPoisoned => swampSources.Count > 0 && swampExposure > BattleBalance.PoisonDelay && !isDead;
    private readonly System.Collections.Generic.HashSet<int> coreSources = new System.Collections.Generic.HashSet<int>();

    // เรียกตรง (ไม่ใช่ RPC) จาก HazardController เมื่อยานของเครื่องนี้ (IsMine) เข้า/ออกโซนบึงหรือ Energy Core
    // core = true คือ Energy Core (เปิด/ปิดโหมด Overload), false คือบึง (อัปเดตความเร็ว) ; ตายแล้วไม่นับการเข้าโซน
    public void SetBattlefieldZone(int source, bool core, bool inside)
    {
        if (isDead && inside) return;
        var sources = core ? coreSources : swampSources;
        if (inside) sources.Add(source); else sources.Remove(source);
        if (core) SetEnergyOverloadRPC(coreSources.Count > 0);
        else UpdateEffectiveSpeed();
    }

    // เปิด/ปิดโหมด Energy Overload: ยิงเร็วขึ้น (fireCooldown หารด้วย CoreFireRateMultiplier) แต่ Update จะหักเลือดต่อเนื่อง
    // มี [PunRPC] ให้ส่งผ่านเครือข่ายได้ แต่ในไฟล์นี้ถูกเรียกแบบเมธอดปกติจาก SetBattlefieldZone และ ResetLifeState
    [PunRPC]
    public void SetEnergyOverloadRPC(bool active)
    {
        isEnergyOverloaded = active;
        if (active)
        {
            fireCooldown = baseFireCooldown / BattleBalance.CoreFireRateMultiplier; // ยิงเร็วขึ้นมาก
        }
        else
        {
            fireCooldown = baseFireCooldown;
        }
    }
}
