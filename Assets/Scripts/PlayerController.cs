using UnityEngine;
using Photon.Pun;

public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Ship Stats")]
    public float maxHp = 100f;
    public float currentHp = 100f;
    public float speed = 5f;
    public float attack = 10f;
    public string skillName = "NONE";
    private float baseSpeed;
    private float acceleration = 18f;
    private float rotationSpeed = 10f;
    private bool isSpawnProtected = false;
    
    [Header("Network Sync")]
    private Vector2 networkPosition;
    private float networkRotation;
    private Vector2 movementInput;
    private Rigidbody2D playerRigidbody;
    
    [Header("UI Controls")]
    private UIJoystick joystick;
    private UIButton fireButton;
    private UIButton skillButton;
    
    [Header("Shooting")]
    public Transform firePoint;
    public float fireCooldown = 0.5f;
    private float baseFireCooldown;
    private float nextFireTime = 0f;

    [Header("Arena Bounds")]
    public Vector2 arenaMin = new Vector2(-38f, -35.5f);
    public Vector2 arenaMax = new Vector2(38f, 35.5f);

    [Header("Skill Mechanics")]
    public int skillType = 0; // 0=STUN, 1=SHIELD, 2=NOVA, 3=SEEKER
    public float maxCooldown = 10f;
    public float currentCooldown = 0f;
    private bool isStunned = false;
    private bool isShielded = false;
    public bool isEnergyOverloaded = false;
    private bool matchEnded;
    public bool isDead = false;
    
    // Skill Visuals
    public GameObject shieldVisual;

    [Header("Visual Effects")]
    public ParticleSystem thrusterEffect;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        // บังคับขอบเขตแผนที่ให้เป็นค่าใหม่เสมอ (กันการโดนทับด้วยค่าเก่าใน Prefab)
        int mapIndex = GameplayManager.GetCurrentMapIndex();
        arenaMin = GameplayManager.GetArenaMin(mapIndex);
        arenaMax = GameplayManager.GetArenaMax(mapIndex);

        playerRigidbody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (photonView.IsMine)
        {
            // หา UIJoystick และ UIButton ในฉาก
            joystick = FindObjectOfType<UIJoystick>();
            
            UIButton[] buttons = FindObjectsOfType<UIButton>();
            foreach (var btn in buttons)
            {
                if (btn.buttonName == "Fire") fireButton = btn;
            }

            // แจ้ง GameplayManager ว่าเราคือผู้เล่นหลัก
            // SetLocalPlayer is called after InitializeStats so the HUD receives the
            // selected skill instead of the default value.
        }
        else
        {
            // ปิดฟิสิกส์สำหรับผู้เล่นอื่น เพราะเราจะอัปเดตตำแหน่งผ่านเน็ตเวิร์ก
            if (playerRigidbody != null)
                playerRigidbody.isKinematic = true;
        }

        // Initialize Stats based on Ship and Skill
        if (photonView.IsMine)
        {
            InitializeStats();
            if (GameplayManager.Instance != null)
            {
                GameplayManager.Instance.SetLocalPlayer(this);
            }
            StartCoroutine(SpawnProtectionRoutine());
        }
    }

    private System.Collections.IEnumerator SpawnProtectionRoutine()
    {
        isSpawnProtected = true;
        float duration = 2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // กระพริบยานเพื่อแสดงว่ากำลังอยู่ในช่วงกันตัว
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.PingPong(elapsed * 5f, 1f) * 0.5f + 0.5f);
            yield return null;
        }
        isSpawnProtected = false;
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
    }

    private void InitializeStats()
    {
        // 1. ดึงข้อมูล Ship Type จาก Network (ยิงมาจาก LobbyManager)
        if (photonView.Owner.CustomProperties.TryGetValue("ShipType", out object shipProp))
        {
            int shipIndex = (int)shipProp;
            // ตั้งค่าพื้นฐานตามยาน (Rebalanced PHASE 1)
            if (shipIndex == 0) // Nebula Ghost - เล็ก พริ้ว ยิงเร็ว
            { 
                maxHp = 80f; attack = 1.0f; speed = 6.0f; fireCooldown = 0.12f; // PHASE 6: ปรับสมดุลความเร็ว
                acceleration = 25f; rotationSpeed = 14f;
            }
            else if (shipIndex == 1) // Comet Crusher - ใหญ่ ถึก ยิงช้าแต่แรง
            { 
                maxHp = 180f; attack = 2.5f; speed = 3.5f; fireCooldown = 0.45f;
                acceleration = 12f; rotationSpeed = 6f;
            }
            else if (shipIndex == 2) // Stellar Striker - สมดุล
            { 
                maxHp = 120f; attack = 1.5f; speed = 4.5f; fireCooldown = 0.2f; // PHASE 6: ปรับสมดุลความเร็ว
                acceleration = 18f; rotationSpeed = 10f;
            }
        }
        else
        {
            maxHp = 120f; attack = 1.5f; speed = 4.5f; fireCooldown = 0.2f; // ค่าเผื่อฉุกเฉิน (Striker defaults)
            acceleration = 18f; rotationSpeed = 10f;
        }

        // 2. ดึงข้อมูล Skill Type
        if (photonView.Owner.CustomProperties.TryGetValue("SkillType", out object skillProp))
        {
            skillType = (int)skillProp;
            if (skillType == 0) { skillName = "STUN"; maxCooldown = 12f; }
            else if (skillType == 1) { skillName = "SHIELD"; maxCooldown = 20f; }
            else if (skillType == 2) { skillName = "NOVA"; maxCooldown = 15f; }
            else if (skillType == 3) { skillName = "SEEKER"; maxCooldown = 10f; }
        }

        baseSpeed = speed;
        baseFireCooldown = fireCooldown;
        currentHp = maxHp;

        // อัปเดตข้อมูลไปให้เครื่องอื่นรู้ค่า MaxHP (เผื่อต้องใช้)
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("MaxHP", maxHp);
        photonView.Owner.SetCustomProperties(props);
    }

    void Update()
    {
        if (matchEnded || isDead) return;

        if (isShielded && shieldVisual != null)
        {
            shieldVisual.transform.Rotate(0, 0, 360f * Time.deltaTime);
        }

        // หมุน Stun Indicator (ทำทั้งสองฝั่งเพื่อให้ทุกคนเห็น)
        if (isStunned && stunVisual != null && stunVisual.activeSelf)
        {
            stunVisual.transform.Rotate(0, 0, 200f * Time.deltaTime);
            // กระพริบ
            var sr = stunVisual.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1f, 1f, 0f, Mathf.PingPong(Time.time * 4f, 0.5f) + 0.4f);
        }

        if (photonView.IsMine)
        {
            if (isEnergyOverloaded)
            {
                // ลดเลือดอย่างต่อเนื่อง 5 หน่วยต่อวินาที เมื่ออยู่ใน Energy Core
                if (!isShielded) 
                {
                    currentHp -= 5f * Time.deltaTime;
                    if (currentHp <= 0)
                    {
                        currentHp = 0;
                        Die(-1);
                    }
                }
            }

            if (currentCooldown > 0)
                currentCooldown -= Time.deltaTime;

            if (!isStunned)
            {
                HandleMovement();
                HandleShooting();
                HandleSkill();
            }
        }
        else
        {
            // Sync Position Smoothly
            transform.position = Vector2.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
            float rotation = Mathf.LerpAngle(transform.eulerAngles.z, networkRotation, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        }
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine || matchEnded || isStunned || playerRigidbody == null || isDead)
        {
            return;
        }

        Vector2 targetPosition = playerRigidbody.position + movementInput * speed * Time.fixedDeltaTime;
        playerRigidbody.MovePosition(ClampToArena(targetPosition));
    }

    private float currentBankAngle = 0f;

    private void HandleMovement()
    {
        if (isStunned) return; // ไม่สามารถขยับได้ตอนติด Stun
        if (joystick != null)
        {
            Vector2 input = new Vector2(joystick.GetHorizontal(), joystick.GetVertical());
            if (input.magnitude > 0.1f)
            {
                // Smooth Acceleration แทนการเปลี่ยน velocity ทันที
                movementInput = Vector2.Lerp(movementInput, input, Time.deltaTime * acceleration);
                
                if (playerRigidbody == null)
                {
                    Vector2 targetPosition = (Vector2)transform.position + movementInput * speed * Time.deltaTime;
                    transform.position = ClampToArena(targetPosition);
                }
                
                // คำนวณองศาการเลี้ยวเพื่อเอียงยาน (Tilt)
                float angleDifference = Vector2.SignedAngle(transform.up, input);
                float targetBank = Mathf.Clamp(angleDifference, -30f, 30f) * -0.6f; // เอียงสูงสุด ~18 องศา
                currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, Time.deltaTime * 5f);

                // หมุนยานไปในทิศทางที่เดิน (ใช้ rotationSpeed ต่างกันตามยาน)
                float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.Euler(new Vector3(0, currentBankAngle, angle - 90f));
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                
                // เร่งไฟไอพ่น
                if (thrusterEffect != null)
                {
                    var emission = thrusterEffect.emission;
                    emission.rateOverTime = 50f;
                    var main = thrusterEffect.main;
                    main.startSize = 1.2f;
                    main.startSpeed = 4f;
                }
            }
            else
            {
                // Smooth Deceleration
                movementInput = Vector2.Lerp(movementInput, Vector2.zero, Time.deltaTime * acceleration * 0.5f);
                if (movementInput.magnitude < 0.01f) movementInput = Vector2.zero;
                
                // ค่อยๆ คืนยานกลับมาตรงๆ
                currentBankAngle = Mathf.Lerp(currentBankAngle, 0f, Time.deltaTime * 5f);
                transform.rotation = Quaternion.Euler(0, currentBankAngle, transform.eulerAngles.z);

                // เบาไฟไอพ่นลงเมื่อจอดนิ่ง
                if (thrusterEffect != null)
                {
                    var emission = thrusterEffect.emission;
                    emission.rateOverTime = 10f;
                    var main = thrusterEffect.main;
                    main.startSize = 0.6f;
                    main.startSpeed = 1.5f;
                }
            }
        }
    }

    private Vector2 ClampToArena(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, arenaMin.x, arenaMax.x),
            Mathf.Clamp(position.y, arenaMin.y, arenaMax.y));
    }

    private void HandleShooting()
    {
        if (isStunned) return; // ไม่สามารถยิงได้ตอนติด Stun
        if (UIButton.IsPressed("Fire") && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireCooldown;
            Shoot();
        }
    }

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
            object[] data = new object[] { attack * 0.5f }; // ดาเมจน้อยลง
            PhotonNetwork.Instantiate("Skill_StunWave", firePoint != null ? firePoint.position : spawnPos, transform.rotation, 0, data);
        }
        else if (skillType == 1) // SHIELD
        {
            photonView.RPC("ActivateShieldRPC", RpcTarget.All);
        }
        else if (skillType == 2) // NOVA
        {
            object[] data = new object[] { attack * 2.0f };
            PhotonNetwork.Instantiate("Skill_NovaBlast", spawnPos, Quaternion.identity, 0, data);
        }
        else if (skillType == 3) // SEEKER
        {
            object[] data = new object[] { attack * 1.5f };
            PhotonNetwork.Instantiate("Skill_SeekerMissile", firePoint != null ? firePoint.position : spawnPos, transform.rotation, 0, data);
        }
    }

    [PunRPC]
    public void ActivateShieldRPC()
    {
        isShielded = true;
        if (shieldVisual != null) shieldVisual.SetActive(true);
        UpdateEffectiveSpeed();
        Invoke("DeactivateShield", 2f);
    }

    private void DeactivateShield()
    {
        isShielded = false;
        UpdateEffectiveSpeed();
        if (shieldVisual != null) shieldVisual.SetActive(false);

        // Shield Break Effect (เอฟเฟกต์โล่แตก สีฟ้า ขนาดใหญ่)
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
        if (isShielded) return; // ติดโล่ป้องกันสถานะได้
        isStunned = true;
        CancelInvoke("RemoveStun");
        Invoke("RemoveStun", 2.5f);

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
        speed = isShielded ? baseSpeed * 1.5f : (isSlowed ? baseSpeed * 0.4f : baseSpeed);
    }

    [PunRPC]
    public void SetEnergyOverloadRPC(bool active)
    {
        isEnergyOverloaded = active;
        if (active)
        {
            fireCooldown = baseFireCooldown * 0.3f; // ยิงเร็วขึ้นมาก
        }
        else
        {
            fireCooldown = baseFireCooldown;
        }
    }

    private void Shoot()
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.up * 0.5f;
        
        // Muzzle Flash
        GameObject muzzleFlashPrefab = GameplayManager.GetPrefab("MuzzleFlash");
        if (muzzleFlashPrefab != null)
        {
            Instantiate(muzzleFlashPrefab, spawnPos, transform.rotation, transform);
        }
        
        // Play SFX
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Laser");

        // Micro Camera Shake (รู้สึก Recoil เวลายิง)
        if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(0.03f, 0.05f);

        // ส่งข้อมูลดาเมจไปด้วย
        object[] customInitData = new object[1];
        customInitData[0] = attack;

        // กระสุนต้องเก็บใน Resources Folder
        PhotonNetwork.Instantiate("BulletPrefab", spawnPos, transform.rotation, 0, customInitData);
    }

    [PunRPC]
    public void TakeDamage(float damage, int killerId)
    {
        if (!photonView.IsMine) return;
        if (matchEnded || isDead) return;
        if (isSpawnProtected) return; // กันตัวตอน Spawn

        if (isShielded)
        {
            // Shield Hit Feedback (โล่รับดาเมจแทน แสดงเอฟเฟกต์โดนโล่)
            photonView.RPC("PlayShieldHitRPC", RpcTarget.All);
            return;
        }

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
        if (impactPrefab != null)
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
        isDead = true;
        Debug.Log("Player Died!");

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Explosion");

        // Death Explosion
        GameObject deathPrefab = GameplayManager.GetPrefab("DeathExplosion");
        if (deathPrefab != null)
        {
            Instantiate(deathPrefab, transform.position, Quaternion.identity);
            Instantiate(deathPrefab, transform.position + new Vector3(1, 1, 0), Quaternion.identity);
            Instantiate(deathPrefab, transform.position + new Vector3(-1, -1, 0), Quaternion.identity);
        }
        
        // สั่นกล้องเฉพาะเครื่องคนที่ตาย
        if (CameraShake.Instance != null && photonView.IsMine) CameraShake.Instance.TriggerShake(1.0f, 1.0f);
        
        // PHASE 4: หน่วงเวลา Slow motion เล็กน้อยเพื่ออารมณ์ที่สะใจขึ้น (รันทุกคน)
        StartCoroutine(SlowMotionRoutine());

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
        yield return new WaitForSeconds(3.0f);
        
        if (matchEnded) yield break;
        
        currentHp = maxHp;
        
        // สุ่มตำแหน่งเกิดใหม่ที่ไม่ชนกำแพง (ลองสูงสุด 10 ครั้ง ถ้าไม่เจอที่ว่างให้เกิดตรงกลางไปเลย)
        Vector2 spawnPos = Vector2.zero;
        bool foundSafe = false;
        for (int attempt = 0; attempt < 15; attempt++)
        {
            float spawnX = Random.Range(arenaMin.x + 4f, arenaMax.x - 4f);
            float spawnY = Random.Range(arenaMin.y + 4f, arenaMax.y - 4f);
            Vector2 testPos = new Vector2(spawnX, spawnY);
            
            // เช็คว่าตำแหน่งนี้ปลอดภัยไหม (เช็คเป็นวงกลมรัศมี 2.5) — สนใจเฉพาะ Collider ที่แข็ง (ไม่ใช่ Trigger)
            Collider2D[] hits = Physics2D.OverlapCircleAll(testPos, 2.5f);
            bool isSafe = true;
            foreach (var h in hits)
            {
                if (!h.isTrigger) 
                {
                    isSafe = false;
                    break;
                }
            }
            if (isSafe)
            {
                spawnPos = testPos;
                foundSafe = true;
                break;
            }
        }
        if (!foundSafe) spawnPos = new Vector2(0f, 0f); // fallback ตรงกลาง
        
        transform.position = spawnPos;
        
        // แจ้งทุกคนให้แสดงยานนี้กลับมา
        photonView.RPC("OnPlayerRespawnedRPC", RpcTarget.All, spawnPos);
    }

    [PunRPC]
    public void OnPlayerRespawnedRPC(Vector2 spawnPos)
    {
        isDead = false;
        transform.position = spawnPos;
        if (playerRigidbody != null) playerRigidbody.position = spawnPos;
        
        // Reset Visuals
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = true;
        if (thrusterEffect != null) thrusterEffect.Play();
        
        if (photonView.IsMine)
        {
            StartCoroutine(SpawnProtectionRoutine());
        }
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

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // ส่งตำแหน่งและเลือดไปให้คนอื่น
            stream.SendNext((Vector2)transform.position);
            stream.SendNext(transform.eulerAngles.z);
            stream.SendNext(maxHp);
            stream.SendNext(currentHp);
        }
        else
        {
            // รับตำแหน่งและเลือดจากคนอื่น
            networkPosition = (Vector2)stream.ReceiveNext();
            networkRotation = (float)stream.ReceiveNext();
            maxHp = (float)stream.ReceiveNext();
            currentHp = (float)stream.ReceiveNext();
        }
    }
}
