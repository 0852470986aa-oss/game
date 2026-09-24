using UnityEngine;
using Photon.Pun;

// ส่วน Health ของ PlayerController; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class PlayerController
{
    [PunRPC]
    public void TakeDamage(float damage, int killerId)
    {
        if (!BattleInputAllowed) return;
        if (!photonView.IsMine) return;
        if (matchEnded || isDead) return;
        if (isSpawnProtected) return; // กันตัวตอน Spawn
        if (GameplayManager.Instance != null) GameplayManager.Instance.ShowIncomingDamage(killerId);

        if (isShielded)
        {
            // Shield Hit Feedback (โล่รับดาเมจแทน แสดงเอฟเฟกต์โดนโล่)
            ConfirmHitToShooter(killerId, true);
            photonView.RPC("PlayShieldHitRPC", RpcTarget.All);
            return;
        }

        ConfirmHitToShooter(killerId, false);
        currentHp -= damage;
        
        photonView.RPC("PlayHitEffectsRPC", RpcTarget.All, damage);

        // Camera Shake ตามดาเมจที่โดน (ยิ่งดาเมจสูง = สั่นแรง)
        if (CameraShake.Instance != null)
        {
            float shakeIntensity = Mathf.Clamp(damage / 50f, 0.1f, 0.5f);
            CameraShake.Instance.TriggerShake(0.15f, shakeIntensity);
        }

        if (currentHp <= 0)
        {
            currentHp = 0;
            Die(killerId);
        }
    }

    private void ConfirmHitToShooter(int shooterId, bool shield)
    {
        var shooter = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(shooterId) : null;
        if (shooter != null && shooterId != photonView.OwnerActorNr)
            photonView.RPC("ConfirmProjectileHitRPC", shooter, shield);
    }

    [PunRPC]
    public void ConfirmProjectileHitRPC(bool shield, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Owner) return;
        if (GameplayManager.Instance != null) GameplayManager.Instance.ShowConfirmedHit(shield);
    }

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
            if (ft != null) ft.Setup(damage);
        }
    }

    [PunRPC]
    public void PlayShieldHitRPC()
    {
        // Shield กระพริบตอนโดนโจมตี
        if (shieldVisual != null)
        {
            StartCoroutine(ShieldHitFlashRoutine());
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ShieldHit");
        
        // Impact Effect ของโล่ (สีฟ้าอ่อน)
        GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
        if (impactPrefab != null)
        {
            GameObject fx = Instantiate(impactPrefab, transform.position, Quaternion.identity);
            var sr = fx.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.4f, 0.8f, 1f, 1f); // สีฟ้า
        }
    }

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
        spriteRenderer.color = Color.white;
    }

    private void Die(int killerId)
    {
        if (matchEnded || isDead) return;
        
        // แจ้งทุกคนว่ายานนี้ตาย (ทุกคนจะได้เล่นเอฟเฟกต์ระเบิดและซ่อนยาน)
        photonView.RPC("OnPlayerDiedRPC", RpcTarget.All, killerId);
        
        // เริ่มกระบวนการเกิดใหม่ (รันเฉพาะฝั่งเจ้าของยาน)
        StartCoroutine(RespawnRoutine());
    }

    [PunRPC]
    public void OnPlayerDiedRPC(int killerId)
    {
        if (isDead || matchEnded) return;
        isDead = true;
        ResetLifeState();
        RespawnReadyAt = Time.unscaledTime + 3f;
        var killer = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(killerId) : null;
        DeathReason = killerId == photonView.OwnerActorNr ? "SELF DESTRUCTION"
            : killer != null ? "DESTROYED BY " + killer.NickName : "DESTROYED BY BATTLEFIELD HAZARD";
        Debug.Log("Player Died!");

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Explosion");

        // Death Explosion
        GameObject deathPrefab = GameplayManager.GetPrefab("DeathExplosion");
        if (!PlaySheetBurst(true) && deathPrefab != null)
        {
            Instantiate(deathPrefab, transform.position, Quaternion.identity);
            Instantiate(deathPrefab, transform.position + new Vector3(1, 1, 0), Quaternion.identity);
            Instantiate(deathPrefab, transform.position + new Vector3(-1, -1, 0), Quaternion.identity);
        }
        
        // สั่นกล้องเฉพาะเครื่องคนที่ตาย
        if (CameraShake.Instance != null && photonView.IsMine) CameraShake.Instance.TriggerShake(1.0f, 1.0f);
        
        // PHASE 4: หน่วงเวลา Slow motion เล็กน้อยเพื่ออารมณ์ที่สะใจขึ้น (รันทุกคน)
        // Keep online simulation and the round clock at normal speed after a kill.

        // ซ่อนยาน
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = false;
        if (shieldVisual != null) shieldVisual.SetActive(false);
        if (thrusterEffect != null) thrusterEffect.Stop();
        // ถ้า "ตัวฉันเอง" (เครื่องนี้) คือคนที่ฆ่า (ActorNumber ตรงกับ killerId)
        if (PhotonNetwork.LocalPlayer.ActorNumber == killerId)
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
        }
    }

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

    private System.Collections.IEnumerator RespawnRoutine()
    {
        RespawnReadyAt = Time.unscaledTime + 3f;
        while (Time.unscaledTime < RespawnReadyAt)
        {
            if (matchEnded || !PhotonNetwork.InRoom) yield break;
            yield return null;
        }
        Vector2 spawnPos;
        while (!GameplayManager.TryFindSafeSpawn(gameObject, GameplayManager.GetCurrentMapIndex(),
            PhotonNetwork.IsMasterClient, out spawnPos))
        {
            if (matchEnded || !PhotonNetwork.InRoom) yield break;
            yield return new WaitForSecondsRealtime(.5f);
        }
        if (matchEnded || !PhotonNetwork.InRoom) yield break;
        photonView.RPC("OnPlayerRespawnedRPC", RpcTarget.All, spawnPos);
    }

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

    private System.Collections.IEnumerator ShowResultWithDelay(bool isWinner)
    {
        // หน่วงเวลา 2 วินาทีให้ดูระเบิดก่อน
        yield return new WaitForSeconds(2.0f);

        if (GameplayManager.Instance != null)
        {
            if (isWinner)
            {
                string enemyShip = gameObject.name.Replace("(Clone)", "");
                string enemyName = photonView.Owner.NickName;
                string myShip = GameplayManager.Instance.localPlayer != null ? GameplayManager.Instance.localPlayer.gameObject.name.Replace("(Clone)", "") : "MyShip";
                GameplayManager.Instance.ShowResultScreen(true, myShip, enemyShip, enemyName);
            }
            else
            {
                string myShip = gameObject.name.Replace("(Clone)", "");
                string enemyName = GameplayManager.Instance.remotePlayer != null ? GameplayManager.Instance.remotePlayer.photonView.Owner.NickName : "Enemy";
                string enemyShip = GameplayManager.Instance.remotePlayer != null ? GameplayManager.Instance.remotePlayer.gameObject.name.Replace("(Clone)", "") : "Unknown";
                GameplayManager.Instance.ShowResultScreen(false, myShip, enemyShip, enemyName);
            }
        }
    }

    [PunRPC]
    public void GameOverRPC()
    {
        if (matchEnded) return;
        matchEnded = true;

        // ฝั่งคนชนะ ก็ดูระเบิดหน่วงเวลา 2 วินาทีเหมือนกัน
        StartCoroutine(ShowResultWithDelay(true));
    }

    [PunRPC]
    public void SetMatchEndedRPC()
    {
        matchEnded = true;
        movementInput = Vector2.zero;
        if (playerRigidbody != null) playerRigidbody.linearVelocity = Vector2.zero;
    }
}
