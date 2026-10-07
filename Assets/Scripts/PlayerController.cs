// ไฟล์หลักของ PlayerController (ยานผู้เล่น) อยู่บน Prefab ยานที่ GameplayManager สร้างด้วย PhotonNetwork.Instantiate ในฉากต่อสู้
// ไฟล์นี้: ค่าสถานะยาน, Start/Update/FixedUpdate, การยิงกระสุน, เส้นเล็ง, วงกันตัวตอนเกิด และซิงก์ตำแหน่ง/HP ผ่าน OnPhotonSerializeView
// ส่วนอื่นของคลาสเดียวกันแยกไว้ที่ PlayerController.Movement/Skills/Health/Visuals/Cosmetics/Warp.cs (partial class)
// หลักเครือข่าย: ยานแต่ละลำมีเจ้าของ 1 เครื่อง (photonView.IsMine = true เฉพาะเครื่องเจ้าของ) เจ้าของเป็นคนรับ input/คิดเลือด/สั่ง RPC
// เครื่องอื่นเห็นยานลำนี้เป็นแค่ "ร่างสำเนา" ที่ขยับตามค่าที่รับมาจากเครือข่าย
using UnityEngine;
using Photon.Pun;

// เอฟเฟกต์ภาพตอนยานระเบิดที่แสดงเฉพาะเครื่องนี้ ไม่มี Collider หรือผลต่อเกมออนไลน์
// Local-only visual: no collider, damage, or network object is created.
public class ShipSheetBurst : MonoBehaviour
{
    private SpriteRenderer visual;
    private float lifetime, age, baseScale;
    private Vector3 origin;

    // ตั้งค่าเอฟเฟกต์: renderer ที่ใช้, ขนาดสุดท้าย (หน่วยโลก) และระยะเวลาแสดง (วินาที)
    public void Initialize(SpriteRenderer renderer, float size, float duration)
    {
        visual = renderer;
        lifetime = duration;
        origin = transform.position;
        baseScale = size / Mathf.Max(0.01f, Mathf.Max(visual.sprite.bounds.size.x, visual.sprite.bounds.size.y));
        ApplyFrame(0);
    }

    // ทุกเฟรม: นับอายุ ครบเวลาแล้วทำลายตัวเอง ไม่งั้นอัปเดตภาพตามสัดส่วนเวลา (0..1)
    private void Update()
    {
        age += Time.deltaTime;
        if (visual == null || age >= lifetime) { Destroy(gameObject); return; }
        ApplyFrame(age / lifetime);
    }

    // t = ความคืบหน้า 0..1: ขยายจาก 45% เป็น 100% และค่อยๆ จางหาย จัดตำแหน่งให้ศูนย์กลางภาพอยู่ที่จุดเดิม
    private void ApplyFrame(float t)
    {
        float scale = baseScale * Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(t));
        transform.localScale = Vector3.one * scale;
        transform.position = origin - visual.sprite.bounds.center * scale;
        visual.color = new Color(1, 1, 1, 1 - t * t);
    }
}

// คลาสยานผู้เล่น (partial) สืบทอด MonoBehaviourPunCallbacks เพื่อใช้ photonView และ IPunObservable เพื่อส่งข้อมูลต่อเนื่อง
// ทุกเครื่องมี PlayerController ของทั้งสองยาน แต่เฉพาะเครื่องเจ้าของ (IsMine) เท่านั้นที่ควบคุมยานลำนั้นจริง
public partial class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    // HP สูงสุด/ปัจจุบัน: ค่าจริงถูกตั้งจาก BattleLoadoutCatalog ใน InitializeStats (เจ้าของ) แล้วซิงก์ให้อีกเครื่องผ่าน OnPhotonSerializeView
    [Header("Ship Stats")]
    public float maxHp = 100f;
    public float currentHp = 100f;
    // speed = ความเร็ว (หน่วยโลก/วินาที) ปัจจุบัน อาจถูกคูณจากโล่/สถานะช้า; attack = ดาเมจต่อกระสุน 1 นัด
    public float speed = 5f;
    public float attack = 10f;
    public string skillName = "NONE";
    // baseSpeed = ความเร็วตั้งต้นของยาน ใช้คำนวณ speed ใหม่ใน UpdateEffectiveSpeed
    private float baseSpeed;
    // acceleration = อัตราเร่งของ input (ยิ่งมากยิ่งออกตัว/หยุดไว), rotationSpeed = ความไวในการหมุนหัวยานตอนเล็ง
    private float acceleration = 18f;
    private float rotationSpeed = 10f;
    // true ช่วงกันตัวหลังเกิด (ไม่รับดาเมจ/สตัน/วาร์ป)
    private bool isSpawnProtected = false;
    
    // ตำแหน่ง/มุมล่าสุดที่รับจากเครือข่าย (ใช้เฉพาะยานของอีกฝ่ายเพื่อ Lerp ให้ลื่น), movementInput = ทิศจากจอยที่ผ่านการเร่ง/หน่วงแล้ว
    [Header("Network Sync")]
    private Vector2 networkPosition;
    private float networkRotation;
    // ความเร็วล่าสุดที่ได้รับ + เวลาที่ได้รับ (ใช้เดาตำแหน่งล่วงหน้าเมื่อห้องคนเยอะส่งข้อมูลถี่น้อยลง)
    private Vector2 networkVelocity;
    private float networkReceivedAt;
    private Vector2 movementInput;
    private Rigidbody2D playerRigidbody;
    
    // อ้างอิงปุ่ม/จอยบนจอ หาให้เฉพาะยานของเราใน Start
    [Header("UI Controls")]
    private UIJoystick joystick;
    private UIButton fireButton;
    private UIButton skillButton;
    
    [Header("Shooting")]
    public Transform firePoint;
    // fireCooldown = เวลาระหว่างนัด (วินาที) ยิ่งน้อยยิ่งยิงเร็ว; baseFireCooldown = ค่าเดิมไว้คืนหลังหมดบัฟ Energy Core
    public float fireCooldown = 0.5f;
    private float baseFireCooldown;
    private float nextFireTime = 0f;

    // ขอบเขตแม็พที่ยานเดินได้ (หน่วยโลก) ค่านี้ถูกแทนที่ด้วยค่าของแม็พจริงใน Start
    [Header("Arena Bounds")]
    public Vector2 arenaMin = new Vector2(-38f, -35.5f);
    public Vector2 arenaMax = new Vector2(38f, 35.5f);

    // ข้อมูลสกิล: ชนิดสกิล, คูลดาวน์เต็ม (วินาที) และคูลดาวน์ที่เหลือ (นับลงใน Update ของเจ้าของ)
    [Header("Skill Mechanics")]
    public int skillType = 0; // 0=STUN, 1=SHIELD, 2=NOVA, 3=SEEKER
    public float maxCooldown = 10f;
    public float currentCooldown = 0f;
    // ธงสถานะของยาน: สตัน, โล่, อยู่ใน Energy Core (ยิงเร็วขึ้นแต่เสียเลือด), แมตช์จบแล้ว, ตายอยู่
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

    // ตัวแปรภาพ: ไฟไอพ่น, ภาพยาน, โล่แบบ spritesheet และชิ้นส่วนไอพ่นที่สร้างตอนรัน (ใช้ใน PlayerController.Visuals.cs)
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


    // Unity เรียกครั้งแรกเมื่อยานถูกสร้าง (ทุกเครื่อง): ตั้งขอบแม็พ ฟิสิกส์ และสียาน
    // ถ้าเป็นยานของเรา (IsMine) จะหา UI ควบคุม โหลดค่าสถานะ แจ้ง GameplayManager และเริ่มช่วงกันตัว
    void Start()
    {
        // 1) ตั้งขอบแม็พตามแม็พปัจจุบัน
        // บังคับขอบเขตแผนที่ให้เป็นค่าใหม่เสมอ (กันการโดนทับด้วยค่าเก่าใน Prefab)
        int mapIndex = GameplayManager.GetCurrentMapIndex();
        arenaMin = GameplayManager.GetArenaMin(mapIndex);
        arenaMax = GameplayManager.GetArenaMax(mapIndex);

        // 2) ตั้งฟิสิกส์: เจ้าของใช้ Dynamic, เครื่องอื่นใช้ Kinematic (ดู ConfigureShipPhysics) แล้วลงสียาน
        playerRigidbody = GetComponent<Rigidbody2D>();
        ConfigureShipPhysics(photonView.IsMine);
        // เริ่มจากตำแหน่งเกิดจริง (กันยานของคนอื่นไหลเข้ากลางแม็พก่อนได้ข้อมูลชุดแรก)
        networkPosition = transform.position;
        networkRotation = transform.eulerAngles.z;
        networkReceivedAt = Time.time;
        spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyShipArt(); // ยานใหม่: ใช้รูปของตัวเองถ้ามี (เฟส 7)
        ApplyPaint();

        // 3) เฉพาะเครื่องเจ้าของ (คนจริง ไม่ใช่บอท): ผูกปุ่มยิงและจอยบนจอ
        if (IsLocalHuman)
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
        // 4) เฉพาะเจ้าของ: โหลดค่ายาน/สกิล แล้วลงทะเบียนเป็น localPlayer และเริ่มกันตัว 2 วินาที
        // บอทโหลดค่าและใส่สมองบนทุกเครื่อง (สมองทำงานเฉพาะเครื่อง Master) เพื่อให้ Master คนใหม่รับช่วงต่อได้ทันทีถ้า Master เดิมหลุด
        if (photonView.IsMine || IsBot)
        {
            InitializeStats();
            // บอท: ใส่สมอง (BotController) แทนการผูก HUD / คน: ลงทะเบียนเป็นยานหลักของเครื่องนี้
            if (IsBot)
            {
                if (GetComponent<BotController>() == null) gameObject.AddComponent<BotController>();
            }
            else if (GameplayManager.Instance != null)
            {
                GameplayManager.Instance.SetLocalPlayer(this);
            }
            if (photonView.IsMine) StartCoroutine(SpawnProtectionRoutine());
        }
    }

    // ตั้ง Rigidbody2D: ยานของเรา = Dynamic (ฟิสิกส์จริง ชนกำแพงได้), ยานคนอื่น = Kinematic (ขยับตามค่าจากเน็ต)
    // เรียกจาก Start และจาก Editor/SceneSetupTool.cs ตอนตั้งค่า Prefab
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

    // Coroutine กันตัวหลังเกิด: รอจนเริ่มแมตช์ได้ แล้วอมตะ 2 วินาที (เวลาจริง) พร้อมทำยานกระพริบ
    // เรียกจาก Start (เจ้าของ) และจาก OnPlayerRespawnedRPC (ทุกเครื่อง เพื่อให้ทุกคนเห็นกระพริบ)
    private System.Collections.IEnumerator SpawnProtectionRoutine()
    {
        isSpawnProtected = true;
        while (!BattleInputAllowed && !matchEnded) yield return null;
        float duration = 2f + itemProtection;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (isDead || matchEnded) yield break;
            elapsed += Time.unscaledDeltaTime;
            // กระพริบยานเพื่อแสดงว่ากำลังอยู่ในช่วงกันตัว
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(paintTint.r, paintTint.g, paintTint.b, Mathf.PingPong(elapsed * 5f, 1f) * 0.5f + 0.5f);
            yield return null;
        }
        isSpawnProtected = false;
        if (spriteRenderer != null)
            spriteRenderer.color = paintTint;
    }

    // อ่านยาน/สกิลที่ผู้เล่นเลือกจาก Custom Properties ("ShipType", "SkillType") แล้วดึงค่าจาก BattleLoadoutCatalog
    // เรียกใน Start เฉพาะเจ้าของ แล้วประกาศ "MaxHP" เป็น Custom Property ให้เครื่องอื่นรู้
    private void InitializeStats()
    {
        int shipIndex = 0;
        int chosenSkill = 0;
        var owner = IsBot ? null : photonView.Owner;
        if (IsBot) LoadBotLoadout(out shipIndex, out chosenSkill);
        else if (owner != null)
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

        // เฟส 5: ค่าตีบวก + ไอเท็มของเรา (เฉพาะยานคนจริงบนเครื่องตัวเอง และห้องเปิด UPGRADES) — PlayerController.Items.cs
        if (!IsBot && photonView.IsMine) ApplyLoadoutBonus(shipIndex);
        ApplyBossStats(); // เฟส 7B: บอส

        baseSpeed = speed;
        baseFireCooldown = fireCooldown;
        currentHp = maxHp;

        // อัปเดตข้อมูลไปให้เครื่องอื่นรู้ค่า MaxHP (เผื่อต้องใช้)
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Add("MaxHP", maxHp);
        if (owner != null) owner.SetCustomProperties(props);
    }

    // true เมื่อ GameplayManager อนุญาตให้ควบคุม (นับถอยหลังจบแล้วและแมตช์ยังไม่จบ) ถ้าไม่มี GameplayManager ถือว่าเล่นได้
    private bool BattleInputAllowed => GameplayManager.Instance == null || GameplayManager.Instance.MatchInputAllowed;

    // Unity เรียกทุกเฟรม (ทุกเครื่อง): หมุนภาพโล่/สตันให้ทุกคนเห็น
    // ถ้าเป็นยานของเรา: คิดดาเมจหนองพิษ/Energy Core, ลดคูลดาวน์ และรับ input; ถ้าเป็นยานคนอื่น: Lerp ไปตำแหน่งที่รับจากเน็ต
    void Update()
    {
        if (matchEnded || isDead) return;

        // 1) ภาพสถานะ (ทุกเครื่อง): หมุนโล่
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

        // 2) เฉพาะเครื่องเจ้าของยาน
        if (photonView.IsMine)
        {
            if (!BattleInputAllowed) return;
            // 2.1) หนองพิษ: อยู่ในโซนนาน PoisonDelay วินาทีแล้วเริ่มเสียเลือด PoisonDamagePerSecond ต่อวินาที (ไม่โดนตอนมีโล่/กันตัว)
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
            // 2.2) อยู่ใน Energy Core: เสียเลือด CoreDamagePerSecond ต่อวินาที (เลือดหมด = ตายโดยไม่มีคนฆ่า killerId = -1)
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

            // 2.25) ผลของไอเท็ม (ฟื้นเลือดเมื่อไม่โดนยิง) — PlayerController.Items.cs
            UpdateItemEffects();

            // 2.3) นับคูลดาวน์สกิลลง แล้วรับ input เคลื่อนที่/เล็ง/ยิง/สกิล (ยกเว้นตอนติดสตันหรือเปิดหน้าตั้งค่า)
            if (currentCooldown > 0)
                currentCooldown -= Time.deltaTime;

            if (!isStunned && (IsBot || !BattleSettingsPanel.IsOpen))
            {
                HandleMovement();
                HandleAiming();
                HandleShooting();
                HandleSkill();
            }
        }
        else
        {
            // 3) ยานของผู้เล่นอื่น: ค่อยๆ เลื่อนไปตำแหน่ง/มุมที่รับมาจาก OnPhotonSerializeView
            // Sync Position Smoothly
            // เดาตำแหน่งล่วงหน้าตามความเร็ว (ไม่เกิน 0.25 วินาที) แล้วค่อยๆ ไหลไปหา
            Vector2 predicted = networkPosition + networkVelocity * Mathf.Min(Time.time - networkReceivedAt, .25f);
            transform.position = Vector2.Lerp(transform.position, predicted, Time.deltaTime * 10f);
            float rotation = Mathf.LerpAngle(transform.eulerAngles.z, networkRotation, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        }
    }

    // Unity เรียกทุกช่วงฟิสิกส์ เฉพาะยานของเรา: ขยับ Rigidbody ตาม movementInput * speed
    // บวกแรงดูดของแม็พแมงกะพรุน (JellyArenaVisuals.PullAt) และบีบให้อยู่ในขอบแม็พ; ถ้าเล่นไม่ได้ให้หยุดนิ่ง
    private void FixedUpdate()
    {
        if (!photonView.IsMine || playerRigidbody == null) return;
        if (matchEnded || isStunned || isDead || !BattleInputAllowed || (!IsBot && BattleSettingsPanel.IsOpen))
        {
            movementInput = Vector2.zero;
            playerRigidbody.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetPosition = playerRigidbody.position + movementInput * speed * Time.fixedDeltaTime;
        targetPosition += JellyArenaVisuals.PullAt(playerRigidbody.position) * Time.fixedDeltaTime;
        playerRigidbody.MovePosition(ClampToArena(targetPosition));
    }

    // เส้นเล็ง/วงกันตัว (สร้างตอนรัน) และข้อมูลการเกิดใหม่ที่ HUD ของ GameplayManager อ่านไปแสดง
    private LineRenderer aimGuide;
    private LineRenderer spawnRing;
    private Material spawnRingMaterial;
    public float RespawnReadyAt { get; private set; }
    public string DeathReason { get; private set; } = "SHIP DESTROYED";

    // ล้างสถานะทั้งหมดของชีวิตก่อนหน้า (สตัน ช้า โล่ โซนอันตราย ความเร็ว ภาพ) เรียกตอนตายและตอนเกิดใหม่ (ทุกเครื่อง)
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
        if (spriteRenderer != null) spriteRenderer.color = paintTint;
    }

    // วาดวงแหวนฟ้ารอบยานระหว่างกันตัว (เห็นทุกเครื่อง) สร้าง LineRenderer ครั้งแรกที่ต้องใช้ เรียกจาก UpdateAimGuide
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
    // วัสดุและ buffer สำหรับ Raycast ของเส้นเล็ง (ใช้ซ้ำทุกเฟรม ไม่สร้างใหม่)
    private Material aimGuideMaterial;
    private readonly RaycastHit2D[] aimGuideHits = new RaycastHit2D[64];

    // เรียกจาก LateUpdate ทุกเฟรม: อัปเดตวงกันตัว และวาดเส้นเล็งยาว 8 หน่วย ตอนกดปุ่มยิงค้าง
    // เส้นเล็งแสดงเฉพาะเครื่องเจ้าของ (ผู้เล่นอีกฝั่งไม่เห็น) เปลี่ยนเป็นสีส้มถ้ามีสิ่งกีดขวาง
    private void UpdateAimGuide()
    {
        UpdateSpawnRing();
        // 1) เช็คว่าควรแสดงเส้นเล็งไหม
        bool visible = IsLocalHuman && BattleInputAllowed && !matchEnded && !isDead && !isStunned
            && fireButton != null && fireButton.isPressed;
        if (!visible)
        {
            if (aimGuide != null) aimGuide.enabled = false;
            return;
        }
        // 2) สร้าง LineRenderer ครั้งแรก
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
        // 3) Raycast จากหัวยานไปข้างหน้า หาจุดที่ชนสิ่งกีดขวางที่ใกล้สุด (ข้ามตัวเอง)
        Vector2 start = GetFirePosition();
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
        // 4) วาดเส้นจากหัวยานถึงจุดชนหรือสุดระยะ
        aimGuide.enabled = true;
        aimGuide.startColor = new Color(.3f, .95f, 1f, .65f);
        aimGuide.endColor = blocked ? new Color(1f, .65f, .2f, .8f) : new Color(.3f, .95f, 1f, .12f);
        aimGuide.SetPosition(0, new Vector3(start.x, start.y, transform.position.z));
        Vector2 end = start + direction * length;
        aimGuide.SetPosition(1, new Vector3(end.x, end.y, transform.position.z));
    }

    // Unity เรียกตอนยานถูกทำลาย: ลบโล่และ Material ที่สร้างเองเพื่อไม่ให้หน่วยความจำรั่ว
    private void OnDestroy()
    {
        if (authoredShield != null) Destroy(authoredShield.gameObject);
        if (spawnRingMaterial != null) Destroy(spawnRingMaterial);
        if (aimGuideMaterial != null) Destroy(aimGuideMaterial);
    }

    // Unity/Photon เรียกตอน GameObject ถูกปิด: ซ่อนวงกันตัวและเส้นเล็ง
    public override void OnDisable()
    {
        if (spawnRing != null) spawnRing.enabled = false;
        if (aimGuide != null) aimGuide.enabled = false;
        base.OnDisable();
    }


    // เรียกจาก Update (เฉพาะเจ้าของ): กดปุ่มยิงค้างและพ้นคูลดาวน์ fireCooldown แล้วจึงยิง
    private void HandleShooting()
    {
        if (isStunned) return; // ไม่สามารถยิงได้ตอนติด Stun
        bool firePressed = IsBot ? botFire : fireButton != null && fireButton.isPressed;
        if (firePressed && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireCooldown * PowerFireFactor; // ไอเท็ม OVERDRIVE ยิงเร็ว 2 เท่า
            Shoot();
        }
    }

    // Derive the firing origin from the ship sprite's actual nose so a stale FirePoint
    // position in a prefab cannot make projectiles appear from inside the hull.
    // คำนวณจุดปล่อยกระสุนที่หัวยาน (จากขอบภาพยาน) เพื่อไม่ให้กระสุนออกจากกลางลำ
    private Vector3 GetFirePosition()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds hull = spriteRenderer.sprite.bounds;
            float noseY = spriteRenderer.flipY ? hull.min.y : hull.max.y;
            Vector3 localNose = new Vector3(hull.center.x, noseY, 0f);
            return transform.TransformPoint(localNose) + transform.up * 0.12f;
        }

        return firePoint != null ? firePoint.position : transform.position + transform.up * 0.5f;
    }


    // ยิง 1 นัด (รันเฉพาะเครื่องเจ้าของ): เล่นแสงปากกระบอก/เสียง/สั่นกล้องในเครื่องเรา
    // แล้ว PhotonNetwork.Instantiate กระสุน (BulletController) ให้ทุกเครื่องเห็น ส่งดาเมจ attack ไปใน InstantiationData
    private void Shoot()
    {
        Vector3 spawnPos = GetFirePosition();
        BreakCloak(); // ยิงแล้วหลุดล่องหน (เฟส 7)
        
        // Local muzzle flash from the supplied sprite sheet; retain the prefab as fallback.
        var muzzleFrames = SkillSheetVisual.LoadGrid("VFX/VFX_MuzzleFlash");
        if (muzzleFrames != null && muzzleFrames.Length > 0)
            // 16 เฟรม: 0.2 วินาทีให้จอแสดงเฟรมได้ครบขึ้น (เดิม 0.14 เร็วจนข้ามเฟรมครึ่งหนึ่ง)
            SkillSheetVisual.Create(muzzleFrames, transform, spawnPos, 1.2f, 0.2f, false);
        else
        {
            GameObject muzzleFlashPrefab = GameplayManager.GetPrefab("MuzzleFlash");
            if (muzzleFlashPrefab != null) Instantiate(muzzleFlashPrefab, spawnPos, transform.rotation, transform);
        }
        
        // Play SFX
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Laser");

        // Micro Camera Shake (รู้สึก Recoil เวลายิง)
        if (CameraShake.Instance != null && IsLocalHuman) CameraShake.Instance.TriggerShake(0.03f, 0.05f);

        // ระบบยิงแบบเบา: สร้างกระสุนในเครื่องเราทันที แล้วส่งคำสั่งยิงให้เครื่องอื่นสร้างตาม (ดู PlayerController.Shooting.cs)
        if (FeatureFlags.LightBullets) { FireLightShot(spawnPos); return; }

        // ส่งข้อมูลดาเมจไปด้วย
        object[] customInitData = new object[1];
        customInitData[0] = attack;

        // กระสุนต้องเก็บใน Resources Folder
        PhotonNetwork.Instantiate("BulletPrefab", spawnPos, transform.rotation, 0, customInitData);
    }


    // Photon เรียกเป็นระยะ: เครื่องเจ้าของ (IsWriting) ส่งตำแหน่ง มุม maxHp currentHp
    // เครื่องอื่น (อ่าน) เก็บค่าไว้ แล้ว Update จะ Lerp ยานไปหา; ลำดับ SendNext/ReceiveNext ต้องตรงกัน
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // ส่งตำแหน่งและเลือดไปให้คนอื่น
            stream.SendNext((Vector2)transform.position);
            stream.SendNext(transform.eulerAngles.z);
            stream.SendNext(maxHp);
            stream.SendNext(currentHp);
            stream.SendNext(playerRigidbody != null && !isDead ? playerRigidbody.linearVelocity : Vector2.zero);
        }
        else
        {
            // รับตำแหน่งและเลือดจากคนอื่น
            networkPosition = (Vector2)stream.ReceiveNext();
            networkRotation = (float)stream.ReceiveNext();
            maxHp = (float)stream.ReceiveNext();
            currentHp = (float)stream.ReceiveNext();
            networkVelocity = (Vector2)stream.ReceiveNext();
            // นับเวลาที่ข้อมูลเดินทางมา (lag) เพื่อเดาตำแหน่งปัจจุบันได้ตรงขึ้น
            float lag = Mathf.Clamp((float)(PhotonNetwork.Time - info.SentServerTime), 0f, .5f);
            networkReceivedAt = Time.time - lag;
        }
    }
}
