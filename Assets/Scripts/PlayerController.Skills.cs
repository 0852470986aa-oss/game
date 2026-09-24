using UnityEngine;
using Photon.Pun;

// ส่วน Skills ของ PlayerController; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class PlayerController
{
    private void HandleSkill()
    {
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
        }
    }

    private void UseSkill()
    {
        Debug.Log("Used Skill: " + skillName);
        Vector3 spawnPos = transform.position;

        if (skillType == 0) // STUN
        {
            object[] data = new object[] { BattleBalance.StunDamage }; // ดาเมจน้อยลง
            PhotonNetwork.Instantiate("Skill_StunWave", firePoint != null ? firePoint.position : spawnPos, transform.rotation, 0, data);
        }
        else if (skillType == 1) // SHIELD
        {
            photonView.RPC("ActivateShieldRPC", RpcTarget.All);
        }
        else if (skillType == 2) // NOVA
        {
            object[] data = new object[] { BattleBalance.NovaDamage };
            PhotonNetwork.Instantiate("Skill_NovaBlast", spawnPos, Quaternion.identity, 0, data);
        }
        else if (skillType == 3) // SEEKER
        {
            object[] data = new object[] { BattleBalance.SeekerDamage };
            PhotonNetwork.Instantiate("Skill_SeekerMissile", firePoint != null ? firePoint.position : spawnPos, transform.rotation, 0, data);
        }
    }

    [PunRPC]
    public void ActivateShieldRPC()
    {
        if (isDead || matchEnded) return;
        isShielded = true;
        if (authoredShield != null) Destroy(authoredShield.gameObject);
        var frames = SkillSheetVisual.Load("VFX_Shield");
        float diameter = spriteRenderer != null ? Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) * 1.2f : 4f;
        authoredShield = SkillSheetVisual.Create(frames, transform, transform.position, diameter, BattleBalance.ShieldSeconds + .3f);
        if (authoredShield != null) authoredShield.breakTime = BattleBalance.ShieldSeconds;
        if (shieldVisual != null) shieldVisual.SetActive(authoredShield == null);
        UpdateEffectiveSpeed();
        CancelInvoke("DeactivateShield");
        Invoke("DeactivateShield", BattleBalance.ShieldSeconds);
    }

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

    private void RemoveStun()
    {
        isStunned = false;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (stunVisual != null) stunVisual.SetActive(false);
    }

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

    [PunRPC]
    public void RemoveSlowRPC()
    {
        CancelInvoke("RemoveSlow");
        isSlowed = false;
        UpdateEffectiveSpeed();
    }

    private void RemoveSlow()
    {
        isSlowed = false;
        UpdateEffectiveSpeed();
    }

    private bool isSlowed;

    private void UpdateEffectiveSpeed()
    {
        if (baseSpeed <= 0f) return;
        speed = baseSpeed * (isShielded ? BattleBalance.ShieldSpeedMultiplier : 1f) * ((isSlowed || swampSources.Count > 0) ? BattleBalance.SlowSpeedMultiplier : 1f);
    }

    private readonly System.Collections.Generic.HashSet<int> swampSources = new System.Collections.Generic.HashSet<int>();
    private float swampExposure;
    public bool IsSwampPoisoned => swampSources.Count > 0 && swampExposure > BattleBalance.PoisonDelay && !isDead;
    private readonly System.Collections.Generic.HashSet<int> coreSources = new System.Collections.Generic.HashSet<int>();

    public void SetBattlefieldZone(int source, bool core, bool inside)
    {
        if (isDead && inside) return;
        var sources = core ? coreSources : swampSources;
        if (inside) sources.Add(source); else sources.Remove(source);
        if (core) SetEnergyOverloadRPC(coreSources.Count > 0);
        else UpdateEffectiveSpeed();
    }

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
