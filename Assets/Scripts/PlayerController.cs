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
    private SpriteRenderer[] sheetThrusters;
    private SpriteRenderer[] sheetThrusterGlows;
    private Vector2[] exhaustAnchors;
    private float displayedThrust;
    private Color exhaustGlowColor;
    private static readonly Sprite[] exhaustSprites = new Sprite[3];
    private Vector3 previousVfxPosition;
    private float lastSheetImpact = -10f;
    private static Sprite[] shipEffectSprites;

    private static Sprite ShipEffectSprite(int id)
    {
        if (shipEffectSprites == null) shipEffectSprites = Resources.LoadAll<Sprite>("Images/VFX_ShipEffects");
        foreach (var sprite in shipEffectSprites)
            if (sprite.name.EndsWith("_" + id)) return sprite;
        return null;
    }

    private void LateUpdate()
    {
        UpdateAimGuide();
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        float traveled = Vector3.Distance(transform.position, previousVfxPosition);
        previousVfxPosition = transform.position;
        if (sheetThrusters == null)
        {
            int style = spriteRenderer.sprite.name.ToLowerInvariant().Contains("ship2") ? 1
                : spriteRenderer.sprite.name.ToLowerInvariant().Contains("ship3") ? 2 : 0;
            Sprite source = ShipEffectSprite(4);
            if (source == null) return;
            // Flame-only regions in the original 1536 x 1024 sheet; omit the metal nozzle.
            if (exhaustSprites[style] == null)
            {
                Rect region = style == 0 ? new Rect(486, 880, 43, 85)
                    : style == 1 ? new Rect(557, 781, 34, 62) : new Rect(264, 870, 28, 78);
                // Keep the UV crop correct if the texture importer downsizes the sheet.
                Vector2 textureScale = new Vector2(source.texture.width / 1536f, source.texture.height / 1024f);
                region = new Rect(region.x * textureScale.x, region.y * textureScale.y,
                    region.width * textureScale.x, region.height * textureScale.y);
                exhaustSprites[style] = Sprite.Create(source.texture, region, new Vector2(0.5f, 1f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                exhaustSprites[style].name = "FlameOnly_" + style;
            }
            // Normalized nozzle locations in each ship's original image, not its padded bounds.
            exhaustAnchors = style == 0 ? new[] { new Vector2(.412f, .398f), new Vector2(.527f, .398f), new Vector2(.587f, .398f) }
                : style == 1 ? new[] { new Vector2(.471f, .431f), new Vector2(.532f, .431f) }
                : new[] { new Vector2(.42f, .37f), new Vector2(.58f, .37f) };
            sheetThrusters = new SpriteRenderer[exhaustAnchors.Length];
            sheetThrusterGlows = new SpriteRenderer[exhaustAnchors.Length];
            exhaustGlowColor = style == 0 ? new Color(1f, 0.35f, 1f) : style == 1
                ? new Color(0.2f, 0.85f, 1f) : new Color(1f, 0.5f, 0.08f);
            for (int i = 0; i < sheetThrusters.Length; i++)
            {
                var obj = new GameObject("ShipSheetThruster" + i);
                obj.transform.SetParent(transform, false);
                var nozzle = obj.AddComponent<SpriteRenderer>();
                nozzle.sprite = exhaustSprites[style];
                nozzle.sortingLayerID = spriteRenderer.sortingLayerID;
                // Above the baked-in exhaust art, anchored at the nozzle, not behind the opaque ship sprite.
                nozzle.sortingOrder = spriteRenderer.sortingOrder + 2;
                sheetThrusters[i] = nozzle;
                var glowObject = new GameObject("ExhaustGlow" + i);
                glowObject.transform.SetParent(transform, false);
                var glow = glowObject.AddComponent<SpriteRenderer>();
                glow.sprite = nozzle.sprite;
                glow.sortingLayerID = nozzle.sortingLayerID;
                glow.sortingOrder = spriteRenderer.sortingOrder + 1;
                sheetThrusterGlows[i] = glow;
            }
            if (thrusterEffect != null) thrusterEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        Bounds hull = spriteRenderer.sprite.bounds;
        float actualThrust = Mathf.Clamp01(traveled / Mathf.Max(0.001f, Time.deltaTime * speed));
        // Smooth fixed-step motion, while keeping the engine visibly burning when pushing against cover.
        float requestedThrust = photonView.IsMine && !isStunned ? movementInput.magnitude : actualThrust;
        displayedThrust = Mathf.MoveTowards(displayedThrust, Mathf.Clamp01(requestedThrust), Time.deltaTime * 5f);
        float pulse = 1f + 0.025f * Mathf.Sin(Time.time * 19f);
        float length = hull.size.y * Mathf.Lerp(0.12f, 0.21f, displayedThrust) * pulse;
        for (int i = 0; i < sheetThrusters.Length; i++)
        {
            var nozzle = sheetThrusters[i];
            nozzle.enabled = !isDead && !matchEnded && spriteRenderer.enabled;
            float scale = length / Mathf.Max(0.01f, nozzle.sprite.bounds.size.y);
            float width = hull.size.x * Mathf.Lerp(0.032f, 0.045f, displayedThrust);
            nozzle.transform.localScale = new Vector3(width / Mathf.Max(0.01f, nozzle.sprite.bounds.size.x), scale, 1f);
            Vector2 anchor = exhaustAnchors[i];
            if (spriteRenderer.flipX) anchor.x = 1f - anchor.x;
            if (spriteRenderer.flipY) anchor.y = 1f - anchor.y;
            nozzle.transform.localRotation = Quaternion.Euler(0, 0, spriteRenderer.flipY ? 180 : 0);
            nozzle.transform.localPosition = new Vector3(hull.min.x + hull.size.x * anchor.x, hull.min.y + hull.size.y * anchor.y, 0);
            nozzle.color = new Color(1, 1, 1, Mathf.Lerp(0.85f, 1f, displayedThrust));
            var glow = sheetThrusterGlows[i];
            glow.enabled = nozzle.enabled;
            glow.transform.localPosition = nozzle.transform.localPosition;
            glow.transform.localRotation = nozzle.transform.localRotation;
            glow.transform.localScale = Vector3.Scale(nozzle.transform.localScale, new Vector3(1.8f, 1.03f, 1));
            glow.color = new Color(exhaustGlowColor.r, exhaustGlowColor.g, exhaustGlowColor.b,
                Mathf.Lerp(0.18f, 0.32f, displayedThrust));
        }
        if (thrusterEffect != null && thrusterEffect.isPlaying) thrusterEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private bool PlaySheetBurst(bool death)
    {
        Sprite sprite = ShipEffectSprite(death ? 117 : 129);
        if (sprite == null || spriteRenderer == null) return false;
        if (!death && Time.time - lastSheetImpact < 0.07f) return true;
        lastSheetImpact = Time.time;
        var obj = new GameObject(death ? "ShipSheetExplosion" : "ShipSheetImpact");
        obj.transform.position = transform.position;
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + 3;
        float size = Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) * (death ? 1.8f : 0.45f);
        obj.AddComponent<ShipSheetBurst>().Initialize(renderer, size, death ? 0.65f : 0.2f);
        return true;
    }

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
            if (isEnergyOverloaded)
            {
                // ลดเลือดอย่างต่อเนื่อง 5 หน่วยต่อวินาที เมื่ออยู่ใน Energy Core
                if (!isShielded && !isSpawnProtected)
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
        if (matchEnded || isStunned || isDead || !BattleInputAllowed)
        {
            movementInput = Vector2.zero;
            playerRigidbody.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetPosition = playerRigidbody.position + movementInput * speed * Time.fixedDeltaTime;
        playerRigidbody.MovePosition(ClampToArena(targetPosition));
    }

    private LineRenderer aimGuide;
    private LineRenderer spawnRing;
    private Material spawnRingMaterial;
    public float RespawnReadyAt { get; private set; }
    public string DeathReason { get; private set; } = "SHIP DESTROYED";

    private void ResetLifeState()
    {
        CancelInvoke("RemoveStun");
        CancelInvoke("RemoveSlow");
        CancelInvoke("DeactivateShield");
        isStunned = isSlowed = isShielded = isSpawnProtected = false;
        swampSources.Clear();
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
        if (spawnRingMaterial != null) Destroy(spawnRingMaterial);
        if (aimGuideMaterial != null) Destroy(aimGuideMaterial);
    }

    public override void OnDisable()
    {
        if (spawnRing != null) spawnRing.enabled = false;
        if (aimGuide != null) aimGuide.enabled = false;
        base.OnDisable();
    }

    private void HandleAiming()
    {
        if (fireButton == null || !fireButton.isPressed || !fireButton.HasAim) return;
        Vector2 direction = fireButton.AimDirection;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        // Only the right stick rotates the ship; translation remains controlled by the left stick.
        float facing = Mathf.LerpAngle(transform.eulerAngles.z, angle, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0, 0, facing);
    }

    private void HandleMovement()
    {
        if (isStunned) return; // ไม่สามารถขยับได้ตอนติด Stun
        if (joystick != null)
        {
            Vector2 input = Vector2.ClampMagnitude(new Vector2(joystick.GetHorizontal(), joystick.GetVertical()), 1f);
            if (input.magnitude > 0.1f)
            {
                // Smooth Acceleration แทนการเปลี่ยน velocity ทันที
                float response = Vector2.Dot(movementInput, input) < 0 ? .55f : .35f;
                movementInput = Vector2.MoveTowards(movementInput, input, Time.deltaTime * acceleration * response);
                
                if (playerRigidbody == null)
                {
                    Vector2 targetPosition = (Vector2)transform.position + movementInput * speed * Time.deltaTime;
                    transform.position = ClampToArena(targetPosition);
                }
                
                // คำนวณองศาการเลี้ยวเพื่อเอียงยาน (Tilt)

                // หมุนยานไปในทิศทางที่เดิน (ใช้ rotationSpeed ต่างกันตามยาน)
                
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
                movementInput = Vector2.MoveTowards(movementInput, Vector2.zero, Time.deltaTime * acceleration * .75f);
                if (movementInput.magnitude < 0.01f) movementInput = Vector2.zero;
                
                // ค่อยๆ คืนยานกลับมาตรงๆ

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
        if (fireButton != null && fireButton.isPressed && Time.time >= nextFireTime)
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
        if (!BattleInputAllowed || isDead || isSpawnProtected) return;
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
        speed = baseSpeed * (isShielded ? 1.5f : 1f) * ((isSlowed || swampSources.Count > 0) ? 0.7f : 1f);
    }

    private readonly System.Collections.Generic.HashSet<int> swampSources = new System.Collections.Generic.HashSet<int>();
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
