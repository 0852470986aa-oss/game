// ส่วน HP/ตาย/เกิดใหม่ของ PlayerController (partial class เดียวกับ PlayerController.cs)
// หลัก: คนยิง (BulletController/SkillController) หรือ Hazard ส่ง RPC TakeDamage แต่ "เครื่องเจ้าของยานที่โดน" เป็นคนหักเลือดจริง
// เมื่อเลือดหมด เจ้าของส่ง OnPlayerDiedRPC ไปทุกเครื่อง แล้วนับ 3 วินาทีก่อนส่ง OnPlayerRespawnedRPC ไปทุกเครื่อง
// RPC เอฟเฟกต์ (PlayHitEffectsRPC/PlayShieldHitRPC) ส่ง RpcTarget.All เพื่อให้ทั้งสองเครื่องเห็นภาพ/เสียงเหมือนกัน
using UnityEngine;
using Photon.Pun;

// ส่วน Health ของ PlayerController; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class PlayerController
{
    // RPC รับดาเมจ: ผู้ส่งใช้ RpcTarget.All แต่บรรทัด !photonView.IsMine ทำให้คิดจริงเฉพาะเครื่องเจ้าของยานที่โดน (เครื่องอื่น return ทันที)
    // killerId = ActorNumber ของคนยิง (-1 = Hazard/ฉาก); ถูกเรียกจาก BulletController, SkillController และ HazardController
    // เฟส 9: เมธอดนี้ไม่ใช่ RPC แล้ว (RPC อยู่ใน PlayerController.Authority.cs ซึ่งตรวจว่ามาจาก Host ก่อนเรียกเมธอดนี้)
    public void TakeDamage(float damage, int killerId)
    {
        // 1) ไม่รับดาเมจถ้า: ยังไม่เริ่มแมตช์, ไม่ใช่ยานของเรา, แมตช์จบ/ตายอยู่, หรือกำลังกันตัวหลังเกิด
        if (!BattleInputAllowed) return;
        if (!photonView.IsMine) return;
        if (matchEnded || isDead) return;
        if (isSpawnProtected) return; // กันตัวตอน Spawn
        if (GameplayManager.Instance != null && IsLocalHuman) GameplayManager.Instance.ShowIncomingDamage(killerId);
        if (IsBot) { var brain = GetComponent<BotController>(); if (brain != null) brain.NotifyDamaged(killerId); }

        // 2) มีโล่: ไม่เสียเลือด แจ้งคนยิงว่าโดนโล่ และให้ทุกเครื่องเล่นเอฟเฟกต์โล่
        if (isShielded)
        {
            // Shield Hit Feedback (โล่รับดาเมจแทน แสดงเอฟเฟกต์โดนโล่)
            ConfirmHitToShooter(killerId, true);
            photonView.RPC("PlayShieldHitRPC", RpcTarget.All);
            return;
        }

        // 3) หักเลือด แจ้งคนยิงว่ายิงโดน และส่ง RPC เอฟเฟกต์โดนยิงไปทุกเครื่อง (ค่า currentHp ใหม่ไปถึงอีกเครื่องผ่าน OnPhotonSerializeView)
        ConfirmHitToShooter(killerId, false);
        MatchStats.Took(CombatantId, Mathf.Min(damage, Mathf.Max(0f, currentHp))); // ดาเมจที่โดน (สถิติหลังแมตช์)
        currentHp -= damage;
        lastHullDamageAt = Time.time;
        
        photonView.RPC("PlayHitEffectsRPC", RpcTarget.All, damage);

        // Camera Shake ตามดาเมจที่โดน (ยิ่งดาเมจสูง = สั่นแรง)
        if (CameraShake.Instance != null && IsLocalHuman)
        {
            float shakeIntensity = Mathf.Clamp(damage / 50f, 0.1f, 0.5f);
            CameraShake.Instance.TriggerShake(0.15f, shakeIntensity);
        }

        // 4) เลือดหมด = ตาย (ส่ง killerId ต่อไปเพื่อให้คนฆ่าได้แต้ม)
        if (currentHp <= 0)
        {
            currentHp = 0;
            Die(killerId);
        }
    }

    // รันบนเครื่องเจ้าของยานที่โดน: ส่ง ConfirmProjectileHitRPC ไปเฉพาะเครื่องของคนยิง (target เป็น Player คนเดียว ไม่ใช่ทุกเครื่อง)
    // ไม่ส่งถ้าคนยิงเป็น Hazard (หา Player ไม่เจอ) หรือยิงโดนตัวเอง
    private void ConfirmHitToShooter(int shooterId, bool shield)
    {
        var shooter = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(shooterId) : null;
        if (shooter != null && shooterId != CombatantId)
            photonView.RPC("ConfirmProjectileHitRPC", shooter, shield);
    }

    // RPC ยืนยันว่ายิงโดน: รันเฉพาะเครื่องคนยิง ตรวจว่าผู้ส่งคือเจ้าของยานลำนี้จริง แล้วให้ HUD แสดง hit marker (shield = โดนโล่)
    [PunRPC]
    public void ConfirmProjectileHitRPC(bool shield, PhotonMessageInfo info)
    {
        if (!FromController(info)) return;
        if (GameplayManager.Instance != null) GameplayManager.Instance.ShowConfirmedHit(shield);
    }

    // RPC เอฟเฟกต์โดนยิง: เจ้าของยานส่ง RpcTarget.All จึงรันทุกเครื่อง เล่นเสียง กระพริบ ดีดถอยหลัง ระเบิดเล็ก และตัวเลขดาเมจลอย
    // แรงดีด AddForce มีผลจริงเฉพาะเครื่องเจ้าของ เพราะยานของคนอื่นเป็น Kinematic
    [PunRPC]
    public void PlayHitEffectsRPC(float damage)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Hit");

        // 1. Hit Flash (ขาวก่อนแดง ดูดีกว่าแดงอย่างเดียว)
        if (spriteRenderer != null)
        {
            StartCoroutine(HitFlashRoutine());
        }

        // 2. Knockback (กระเด้งหลังเล็กน้อยตอนโดนยิง)
        if (playerRigidbody != null)
        {
            Vector2 knockDir = (Vector2)transform.position - (Vector2)transform.up;
            playerRigidbody.AddForce(knockDir.normalized * damage * 0.5f, ForceMode2D.Impulse);
        }

        // 3. Impact Explosion (เนื้อหนัง เลือดสาด/ประกายไฟสีแดง)
        GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
        if (!PlaySheetBurst(false) && impactPrefab != null)
        {
            GameObject fx = Instantiate(impactPrefab, transform.position, Quaternion.identity);
            var sr = fx.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1f, 0.2f, 0.2f, 1f); // สีแดงสด
        }

        // 4. Floating Text
        GameObject floatingTextPrefab = Resources.Load<GameObject>("FloatingText");
        if (floatingTextPrefab != null)
        {
            GameObject txtObj = Instantiate(floatingTextPrefab, transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
            FloatingText ft = txtObj.GetComponent<FloatingText>();
            if (ft != null)
            {
                ft.Setup(damage);
                // ดาเมจหนัก (สกิล/อันตราย) ตัวเลขใหญ่และเป็นสีส้ม ให้รู้ว่าโดนแรง
                if (damage >= 20f) ft.Emphasize(new Color(1f, .5f, .15f), 1.35f);
            }
        }
    }

    // RPC เอฟเฟกต์กระสุนโดนโล่: รันทุกเครื่อง กระพริบโล่ เล่นเสียง ขึ้นข้อความ "BLOCKED" และภาพแรงกระแทกสีฟ้า
    [PunRPC]
    public void PlayShieldHitRPC()
    {
        // Shield กระพริบตอนโดนโจมตี
        if (shieldVisual != null)
        {
            StartCoroutine(ShieldHitFlashRoutine());
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ShieldHit");
        // บอกให้ชัดว่ากระสุนโดนโล่ ไม่เสียเลือด
        GameObject blockedTextPrefab = Resources.Load<GameObject>("FloatingText");
        if (blockedTextPrefab != null)
        {
            var blocked = Instantiate(blockedTextPrefab, transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity)
                .GetComponent<FloatingText>();
            if (blocked != null) blocked.Setup("BLOCKED", new Color(.35f, .85f, 1f));
        }
        
        // Blue animated impact from the supplied shield-hit sheet.
        Sprite[] frames = SkillSheetVisual.LoadGrid("VFX/VFX_ShieldImpact");
        if (frames != null && frames.Length > 0)
        {
            float size = spriteRenderer != null
                ? Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) * 1.35f
                : 1.6f;
            SkillSheetVisual.Create(frames, null, transform.position, size, 0.3f);
        }
        else
        {
            GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
            if (impactPrefab != null)
            {
                GameObject fx = Instantiate(impactPrefab, transform.position, Quaternion.identity);
                var sr = fx.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.4f, 0.8f, 1f, 1f);
            }
        }
    }

    // Coroutine ทำโล่ (แบบ shieldVisual) กระพริบขาว 0.08 วินาทีแล้วคืนสีเดิม
    private System.Collections.IEnumerator ShieldHitFlashRoutine()
    {
        if (shieldVisual == null) yield break;
        SpriteRenderer shieldSR = shieldVisual.GetComponent<SpriteRenderer>();
        if (shieldSR == null) yield break;
        Color orig = shieldSR.color;
        shieldSR.color = new Color(1f, 1f, 1f, 0.9f);
        yield return new WaitForSeconds(0.08f);
        shieldSR.color = orig;
    }

    // Coroutine กระพริบยาน ขาว 0.05 วิ -> แดง 0.1 วิ -> คืนสีย้อม (หรือสีเหลืองถ้ายังติดสตัน)
    private System.Collections.IEnumerator HitFlashRoutine()
    {
        // White flash ก่อน แล้ว Red flash (ดูดีกว่าแดงอย่างเดียว)
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.05f);
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = isStunned ? new Color(1f, 1f, 0.3f, 1f) : paintTint;
    }

    // เรียกเฉพาะเครื่องเจ้าของ (จาก TakeDamage หรือดาเมจพิษ/Energy Core ใน Update): ส่ง OnPlayerDiedRPC ไป RpcTarget.All
    // แล้วเริ่ม RespawnRoutine บนเครื่องเจ้าของเท่านั้น
    private void Die(int killerId)
    {
        if (matchEnded || isDead) return;
        
        // แจ้งทุกคนว่ายานนี้ตาย (ทุกคนจะได้เล่นเอฟเฟกต์ระเบิดและซ่อนยาน)
        photonView.RPC("OnPlayerDiedRPC", RpcTarget.All, killerId);
        
        // เริ่มกระบวนการเกิดใหม่ (รันเฉพาะฝั่งเจ้าของยาน)
        StartCoroutine(RespawnRoutine());
    }

    // RPC ยานตาย: รันทุกเครื่อง ตั้ง isDead ล้างสถานะ ตั้งเวลาเกิดใหม่ (+3 วินาที) และข้อความสาเหตุการตายให้ HUD
    // เล่นระเบิด ซ่อนยาน/ปิด Collider และถ้าเครื่องนี้คือคนฆ่า (LocalPlayer.ActorNumber == killerId) จะบวก Kills ของตัวเอง
    [PunRPC]
    public void OnPlayerDiedRPC(int killerId)
    {
        // 1) ตั้งสถานะตาย และหาชื่อสาเหตุการตาย (ฆ่าตัวเอง / ชื่อคนฆ่า / อันตรายในฉาก)
        if (isDead || matchEnded) return;
        isDead = true;
        ResetLifeState();
        RespawnReadyAt = Time.unscaledTime + 3f;
        var killerShip = killerId > 0 ? FindCombatant(killerId) : null;
        DeathReason = killerId == CombatantId ? "SELF DESTRUCTION"
            : killerShip != null ? "DESTROYED BY " + killerShip.PilotName : "DESTROYED BY BATTLEFIELD HAZARD";
        Debug.Log("Player Died!");

        // 2) เสียงและภาพระเบิด (ใช้ spritesheet ถ้ามี ไม่งั้นใช้ Prefab DeathExplosion 3 ลูก)
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Explosion");

        // Death Explosion
        GameObject deathPrefab = GameplayManager.GetPrefab("DeathExplosion");
        PlayBossDeathFx(); // รูปชุดใหม่: บอสระเบิดใหญ่
        if (!PlaySheetBurst(true) && deathPrefab != null)
        {
            Instantiate(deathPrefab, transform.position, Quaternion.identity);
            Instantiate(deathPrefab, transform.position + new Vector3(1, 1, 0), Quaternion.identity);
            Instantiate(deathPrefab, transform.position + new Vector3(-1, -1, 0), Quaternion.identity);
        }
        
        // สั่นกล้องเฉพาะเครื่องคนที่ตาย
        if (CameraShake.Instance != null && IsLocalHuman) CameraShake.Instance.TriggerShake(1.0f, 1.0f);
        // บอทเป็นคนฆ่า: Master บวก Kill ของบอทใน Room Property (ดู MatchRules.AddBotKill)
        if (killerId >= BotIdBase && killerId != CombatantId && PhotonNetwork.IsMasterClient) MatchRules.AddBotKill(killerId);
        // นับจำนวนครั้งที่ตาย (ใช้ในตารางคะแนน): คน = เครื่องเจ้าของบวกของตัวเอง, บอท = Master บวกใน Room Property
        if (IsLocalHuman)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["Deaths"] = MatchRules.Deaths(PhotonNetwork.LocalPlayer) + 1 });
        else if (IsBot && PhotonNetwork.IsMasterClient) MatchRules.AddBotDeath(CombatantId);
        // ข้อความ "ใครฆ่าใคร" มุมจอ (ทุกเครื่องเห็น)
        if (GameplayManager.Instance != null) GameplayManager.Instance.OnCombatantDied(killerId, this);
        
        // PHASE 4: หน่วงเวลา Slow motion เล็กน้อยเพื่ออารมณ์ที่สะใจขึ้น (รันทุกคน)
        // Keep online simulation and the round clock at normal speed after a kill.

        // 3) ซ่อนยานและปิดการชน
        // ซ่อนยาน
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = false;
        if (shieldVisual != null) shieldVisual.SetActive(false);
        if (thrusterEffect != null) thrusterEffect.Stop();
        // 4) นับแต้ม: เฉพาะเครื่องของคนฆ่าเท่านั้นที่บวก "Kills" ใน Custom Property ของตัวเอง (Photon ซิงก์ให้ทุกเครื่อง; GameplayManager ใช้นับสกอร์ 3 Kill)
        // ถ้า "ตัวฉันเอง" (เครื่องนี้) คือคนที่ฆ่า (ActorNumber ตรงกับ killerId)
        // (ไม่นับกรณีทำลายตัวเอง)
        if (PhotonNetwork.LocalPlayer.ActorNumber == killerId && killerId != CombatantId)
        {
            int currentKills = 0;
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Kills", out object kills))
            {
                currentKills = (int)kills;
            }
            currentKills++;
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props.Add("Kills", currentKills);
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            
            // ลอยข้อความ "Kill +1" ที่กลางจอหรือบนยานศัตรูก็ได้
            if (GameplayManager.Instance != null) GameplayManager.Instance.ShowKillMessage();
            // ไอเท็ม Siphon: ฆ่าได้แล้วฟื้นเลือด
            if (GameplayManager.Instance != null && GameplayManager.Instance.localPlayer != null) GameplayManager.Instance.localPlayer.OnLocalKill();
            // คนที่ฆ่าได้ก็รู้สึกถึงแรงระเบิดด้วย (สั่นเบากว่าคนที่ตาย)
            if (CameraShake.Instance != null && !photonView.IsMine) CameraShake.Instance.TriggerShake(0.3f, 0.35f);
        }
    }

    // Coroutine ทำ slow motion (timeScale 0.3 แล้วคืนค่า) ปัจจุบันไม่พบที่เรียกใช้ในโปรเจกต์ (ปิดไว้เพื่อไม่ให้เวลาเกมออนไลน์เพี้ยน)
    private System.Collections.IEnumerator SlowMotionRoutine()
    {
        // หน่วงเวลาเกมให้ช้าลง 3 เท่า
        Time.timeScale = 0.3f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        
        // รอ 1.5 วินาทีของเวลาจริง (เท่ากับ 0.5 วินาทีในเกมที่ช้าลง)
        yield return new WaitForSecondsRealtime(1.5f);
        
        // คืนค่าปกติ
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    // Coroutine เกิดใหม่ (รันเฉพาะเครื่องเจ้าของ เริ่มจาก Die): รอ 3 วินาที (เวลาจริง) หาจุดเกิดที่ปลอดภัย ถ้ายังไม่เจอลองใหม่ทุก 0.5 วินาที
    // เจอแล้วส่ง OnPlayerRespawnedRPC ไป RpcTarget.All; ถ้าแมตช์จบหรือออกจากห้องระหว่างรอจะยกเลิก
    private System.Collections.IEnumerator RespawnRoutine()
    {
        RespawnReadyAt = Time.unscaledTime + 3f;
        while (Time.unscaledTime < RespawnReadyAt)
        {
            if (matchEnded || !PhotonNetwork.InRoom) yield break;
            yield return null;
        }
        // เฟส 7B: โหมดที่มีจำนวนชีวิต (Survival/Campaign/Battle Royale) — หมดชีวิตไม่เกิดใหม่ / บอทที่เกิดใหม่ไม่ได้ถูกลบออก
        if (GameplayManager.Instance != null && !GameplayManager.Instance.CanRespawn(this))
        {
            // Survival: ลบบอทที่ตายออก (ระลอกถัดไปจะเริ่มเมื่อบอทหมด) / โหมดอื่นปล่อยซากไว้ (ยังอยู่ในตารางคะแนน)
            if (IsBot && photonView.IsMine && MatchRules.GameMode(PhotonNetwork.CurrentRoom) == MatchRules.ModeSurvival) PhotonNetwork.Destroy(gameObject);
            yield break;
        }
        Vector2 spawnPos;
        // บอทเกิดฝั่งตรงข้ามกับ Master (ฝั่งบน) / โหมดทีม: BLUE ฝั่งล่าง RED ฝั่งบน
        int team = MatchRules.TeamOf(CombatantId);
        bool lowerSide = team >= 0 ? team == 0 : !IsBot && PhotonNetwork.IsMasterClient;
        while (!GameplayManager.TryFindSafeSpawn(gameObject, GameplayManager.GetCurrentMapIndex(),
            lowerSide, out spawnPos))
        {
            if (matchEnded || !PhotonNetwork.InRoom) yield break;
            yield return new WaitForSecondsRealtime(.5f);
        }
        if (matchEnded || !PhotonNetwork.InRoom) yield break;
        photonView.RPC("OnPlayerRespawnedRPC", RpcTarget.All, spawnPos);
    }

    // RPC เกิดใหม่: รันทุกเครื่อง เติมเลือดเต็ม ย้ายยานไปจุดเกิด เปิดภาพ/Collider กลับ และเริ่มช่วงกันตัว (กระพริบให้ทุกคนเห็น)
    [PunRPC]
    public void OnPlayerRespawnedRPC(Vector2 spawnPos)
    {
        if (matchEnded || !isDead) return;
        ResetLifeState();
        currentHp = maxHp;
        isDead = false;
        transform.position = spawnPos;
        if (playerRigidbody != null) playerRigidbody.position = spawnPos;
        
        // Reset Visuals
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = true;
        if (thrusterEffect != null) thrusterEffect.Play();
        
        StartCoroutine(SpawnProtectionRoutine());
    }

    // Coroutine รอ 2 วินาทีแล้วเรียก GameplayManager.ShowResultScreen (ชนะ/แพ้) ถูกเรียกจาก GameOverRPC เท่านั้น
    private System.Collections.IEnumerator ShowResultWithDelay(bool isWinner)
    {
        // หน่วงเวลา 2 วินาทีให้ดูระเบิดก่อน
        yield return new WaitForSeconds(2.0f);

        if (GameplayManager.Instance != null)
        {
            if (isWinner)
            {
                string enemyShip = gameObject.name.Replace("(Clone)", "");
                string enemyName = PilotName;
                string myShip = GameplayManager.Instance.localPlayer != null ? GameplayManager.Instance.localPlayer.gameObject.name.Replace("(Clone)", "") : "MyShip";
                GameplayManager.Instance.ShowResultScreen(true, myShip, enemyShip, enemyName);
            }
            else
            {
                string myShip = gameObject.name.Replace("(Clone)", "");
                string enemyName = GameplayManager.Instance.remotePlayer != null ? GameplayManager.Instance.remotePlayer.PilotName : "Enemy";
                string enemyShip = GameplayManager.Instance.remotePlayer != null ? GameplayManager.Instance.remotePlayer.gameObject.name.Replace("(Clone)", "") : "Unknown";
                GameplayManager.Instance.ShowResultScreen(false, myShip, enemyShip, enemyName);
            }
        }
    }

    // RPC จบเกมแบบเก่า: ตั้ง matchEnded แล้วแสดงหน้าผลแบบชนะ; ปัจจุบันไม่พบที่ส่ง RPC นี้ในโปรเจกต์ (GameplayManager ใช้ SetMatchEndedRPC แทน)
    [PunRPC]
    public void GameOverRPC()
    {
        if (matchEnded) return;
        matchEnded = true;

        // ฝั่งคนชนะ ก็ดูระเบิดหน่วงเวลา 2 วินาทีเหมือนกัน
        StartCoroutine(ShowResultWithDelay(true));
    }

    // RPC หยุดยานเมื่อแมตช์จบ: GameplayManager.EndMatch ส่งด้วย RpcTarget.All (ทุกเครื่อง) และ StopInterruptedBattle เรียกตรงในเครื่องตัวเอง
    // ตั้ง matchEnded ทำให้ Update/FixedUpdate หยุดรับ input และหยุดความเร็วยาน
    [PunRPC]
    public void SetMatchEndedRPC()
    {
        matchEnded = true;
        movementInput = Vector2.zero;
        if (playerRigidbody != null) playerRigidbody.linearVelocity = Vector2.zero;
    }
}
