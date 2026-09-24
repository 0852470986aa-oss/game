using UnityEngine;
using Photon.Pun;

// Local-only visual: no collider, damage, or network object is created.
public class ShipSheetBurst : MonoBehaviour
{
    private SpriteRenderer visual;
    private float lifetime, age, baseScale;
    private Vector3 origin;

    public void Initialize(SpriteRenderer renderer, float size, float duration)
    {
        visual = renderer;
        lifetime = duration;
        origin = transform.position;
        baseScale = size / Mathf.Max(0.01f, Mathf.Max(visual.sprite.bounds.size.x, visual.sprite.bounds.size.y));
        ApplyFrame(0);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (visual == null || age >= lifetime) { Destroy(gameObject); return; }
        ApplyFrame(age / lifetime);
    }

    private void ApplyFrame(float t)
    {
        float scale = baseScale * Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(t));
        transform.localScale = Vector3.one * scale;
        transform.position = origin - visual.sprite.bounds.center * scale;
        visual.color = new Color(1, 1, 1, 1 - t * t);
    }
}

public partial class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
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
    // Read-only HUD state; presentation must not apply or clear gameplay effects.
    public bool IsStunned => isStunned;
    public bool IsShielded => isShielded;
    public bool IsSpawnProtected => isSpawnProtected;
    public bool IsSlowed => isSlowed || swampSources.Count > 0;
    public bool HasMatchEnded => matchEnded;
    
    // Skill Visuals
    public GameObject shieldVisual;

    [Header("Visual Effects")]
    public ParticleSystem thrusterEffect;
    private SpriteRenderer spriteRenderer;
    private SkillSheetVisual authoredShield;
    private SpriteRenderer[] sheetThrusters;
    private SpriteRenderer[] sheetThrusterGlows;
    private Vector2[] exhaustAnchors;
    private float[] exhaustAngles;
    private int exhaustStyle;
    private float displayedThrust;
    private Color exhaustGlowColor;
    private static readonly Sprite[] exhaustSprites = new Sprite[3];
    private Vector3 previousVfxPosition;
    private float lastSheetImpact = -10f;
    private static Sprite[] shipEffectSprites;


    void Start()
    {
        // บังคับขอบเขตแผนที่ให้เป็นค่าใหม่เสมอ (กันการโดนทับด้วยค่าเก่าใน Prefab)
        int mapIndex = GameplayManager.GetCurrentMapIndex();
        arenaMin = GameplayManager.GetArenaMin(mapIndex);
        arenaMax = GameplayManager.GetArenaMax(mapIndex);

        playerRigidbody = GetComponent<Rigidbody2D>();
        ConfigureShipPhysics(photonView.IsMine);
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
            // Remote bodies stay kinematic; ConfigureShipPhysics sets the ownership policy above.
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

    public void ConfigureShipPhysics(bool locallyOwned)
    {
        playerRigidbody = GetComponent<Rigidbody2D>();
        if (playerRigidbody == null) return;
        playerRigidbody.bodyType = locallyOwned ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
        playerRigidbody.gravityScale = 0f;
        playerRigidbody.constraints |= RigidbodyConstraints2D.FreezeRotation;
        playerRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        playerRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private System.Collections.IEnumerator SpawnProtectionRoutine()
    {
        isSpawnProtected = true;
        while (!BattleInputAllowed && !matchEnded) yield return null;
        float duration = 2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (isDead || matchEnded) yield break;
            elapsed += Time.unscaledDeltaTime;
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
        int shipIndex = 0;
        int chosenSkill = 0;
        var owner = photonView.Owner;
        if (owner != null)
        {
            if (owner.CustomProperties.TryGetValue("ShipType", out object shipValue) && shipValue is int shipId)
                shipIndex = BattleLoadoutCatalog.ValidShip(shipId);
            if (owner.CustomProperties.TryGetValue("SkillType", out object skillValue) && skillValue is int skillId)
                chosenSkill = BattleLoadoutCatalog.ValidSkill(skillId);
        }
        ShipData ship = BattleLoadoutCatalog.Ships[shipIndex];
        maxHp = ship.hp;
        attack = ship.atk;
        speed = ship.spd;
        fireCooldown = ship.shotInterval;
        acceleration = ship.acceleration;
        rotationSpeed = ship.turnSpeed;
        skillType = chosenSkill;
        SkillData skill = BattleLoadoutCatalog.Skills[chosenSkill];
        skillName = skill.name;
        maxCooldown = skill.cooldown;

        baseSpeed = speed;
        baseFireCooldown = fireCooldown;
        currentHp = maxHp;

        // อัปเดตข้อมูลไปให้เครื่องอื่นรู้ค่า MaxHP (เผื่อต้องใช้)
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("MaxHP", maxHp);
        if (owner != null) owner.SetCustomProperties(props);
    }

    private bool BattleInputAllowed => GameplayManager.Instance == null || GameplayManager.Instance.MatchInputAllowed;

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
            if (!BattleInputAllowed) return;
            if (swampSources.Count == 0) swampExposure = 0;
            else
            {
                float before = swampExposure;
                swampExposure += Time.deltaTime;
                float elapsed = Mathf.Max(0, swampExposure-BattleBalance.PoisonDelay)-Mathf.Max(0,before-BattleBalance.PoisonDelay);
                if (elapsed > 0 && !isShielded && !isSpawnProtected)
                {
                    currentHp = Mathf.Max(0,currentHp-BattleBalance.PoisonDamagePerSecond*elapsed);
                    if (currentHp <= 0) { Die(-1); return; }
                }
            }
            if (isEnergyOverloaded)
            {
                // ลดเลือดอย่างต่อเนื่อง 5 หน่วยต่อวินาที เมื่ออยู่ใน Energy Core
                if (!isShielded && !isSpawnProtected)
                {
                    currentHp -= BattleBalance.CoreDamagePerSecond * Time.deltaTime;
                    if (currentHp <= 0)
                    {
                        currentHp = 0;
                        Die(-1);
                    }
                }
            }

            if (currentCooldown > 0)
                currentCooldown -= Time.deltaTime;

            if (!isStunned && !BattleSettingsPanel.IsOpen)
            {
                HandleMovement();
                HandleAiming();
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
        if (!photonView.IsMine || playerRigidbody == null) return;
        if (matchEnded || isStunned || isDead || !BattleInputAllowed || BattleSettingsPanel.IsOpen)
        {
            movementInput = Vector2.zero;
            playerRigidbody.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetPosition = playerRigidbody.position + movementInput * speed * Time.fixedDeltaTime;
        targetPosition += JellyArenaVisuals.PullAt(playerRigidbody.position) * Time.fixedDeltaTime;
        playerRigidbody.MovePosition(ClampToArena(targetPosition));
    }

    private LineRenderer aimGuide;
    private LineRenderer spawnRing;
    private Material spawnRingMaterial;
    public float RespawnReadyAt { get; private set; }
    public string DeathReason { get; private set; } = "SHIP DESTROYED";

    private void ResetLifeState()
    {
        if (authoredShield != null) { Destroy(authoredShield.gameObject); authoredShield = null; }
        CancelInvoke("RemoveStun");
        CancelInvoke("RemoveSlow");
        CancelInvoke("DeactivateShield");
        isStunned = isSlowed = isShielded = isSpawnProtected = false;
        swampSources.Clear();
        swampExposure = 0;
        coreSources.Clear();
        SetEnergyOverloadRPC(false);
        UpdateEffectiveSpeed();
        movementInput = Vector2.zero;
        if (playerRigidbody != null) playerRigidbody.linearVelocity = Vector2.zero;
        if (stunVisual != null) stunVisual.SetActive(false);
        if (shieldVisual != null) shieldVisual.SetActive(false);
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }

    private void UpdateSpawnRing()
    {
        bool visible = isSpawnProtected && !isDead && !matchEnded;
        if (visible && spawnRing == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            spawnRingMaterial = new Material(shader);
            var ring = new GameObject("SpawnProtectionRing");
            ring.transform.SetParent(transform, false);
            spawnRing = ring.AddComponent<LineRenderer>();
            spawnRing.sharedMaterial = spawnRingMaterial;
            spawnRing.useWorldSpace = true;
            spawnRing.loop = true;
            spawnRing.positionCount = 64;
            spawnRing.sortingOrder = 15;
        }
        if (spawnRing == null) return;
        spawnRing.enabled = visible;
        if (!visible) return;
        float radius = spriteRenderer != null ? Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y) + .35f : 2f;
        spawnRing.startWidth = spawnRing.endWidth = .09f + .025f * Mathf.Sin(Time.unscaledTime * 8f);
        spawnRing.startColor = spawnRing.endColor = new Color(.25f, .95f, 1f, .85f);
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64f;
            spawnRing.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
    }
    private Material aimGuideMaterial;
    private readonly RaycastHit2D[] aimGuideHits = new RaycastHit2D[64];

    private void UpdateAimGuide()
    {
        UpdateSpawnRing();
        bool visible = photonView.IsMine && BattleInputAllowed && !matchEnded && !isDead && !isStunned
            && fireButton != null && fireButton.isPressed;
        if (!visible)
        {
            if (aimGuide != null) aimGuide.enabled = false;
            return;
        }
        if (aimGuide == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            aimGuideMaterial = new Material(shader);
            var guideObject = new GameObject("LocalAimGuide");
            guideObject.transform.SetParent(transform, false);
            aimGuide = guideObject.AddComponent<LineRenderer>();
            aimGuide.sharedMaterial = aimGuideMaterial;
            aimGuide.useWorldSpace = true;
            aimGuide.positionCount = 2;
            aimGuide.startWidth = .055f;
            aimGuide.endWidth = .025f;
            aimGuide.numCapVertices = 2;
            if (spriteRenderer != null)
            {
                aimGuide.sortingLayerID = spriteRenderer.sortingLayerID;
                aimGuide.sortingOrder = spriteRenderer.sortingOrder + 4;
            }
        }
        Vector2 start = firePoint != null ? firePoint.position : transform.position + transform.up * .5f;
        Vector2 direction = transform.up;
        const float range = 8f;
        float length = range;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        filter.useTriggers = false;
        int count = Physics2D.Raycast(start, direction, filter, aimGuideHits, range);
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            var hit = aimGuideHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance < length) { length = hit.distance; blocked = true; }
        }
        aimGuide.enabled = true;
        aimGuide.startColor = new Color(.3f, .95f, 1f, .65f);
        aimGuide.endColor = blocked ? new Color(1f, .65f, .2f, .8f) : new Color(.3f, .95f, 1f, .12f);
        aimGuide.SetPosition(0, new Vector3(start.x, start.y, transform.position.z));
        Vector2 end = start + direction * length;
        aimGuide.SetPosition(1, new Vector3(end.x, end.y, transform.position.z));
    }

    private void OnDestroy()
    {
        if (authoredShield != null) Destroy(authoredShield.gameObject);
        if (spawnRingMaterial != null) Destroy(spawnRingMaterial);
        if (aimGuideMaterial != null) Destroy(aimGuideMaterial);
    }

    public override void OnDisable()
    {
        if (spawnRing != null) spawnRing.enabled = false;
        if (aimGuide != null) aimGuide.enabled = false;
        base.OnDisable();
    }


    private void HandleShooting()
    {
        if (isStunned) return; // ไม่สามารถยิงได้ตอนติด Stun
        if (fireButton != null && fireButton.isPressed && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireCooldown;
            Shoot();
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
