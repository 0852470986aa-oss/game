// ไฟล์สกิลแบบวัตถุ: SkillController อยู่บน Prefab Skill_StunWave / Skill_NovaBlast / Skill_SeekerMissile ใน Resources
// สร้างด้วย PhotonNetwork.Instantiate จาก PlayerController.UseSkill (เครื่องเจ้าของยาน) พร้อมดาเมจใน InstantiationData
// เครื่องเจ้าของสกิล (IsMine = คนใช้สกิล) เป็นคนเคลื่อนที่/ตรวจชน/ส่ง RPC; เครื่องอื่นแสดงภาพตามเวลา PhotonNetwork.Time
// มีคลาส SkillSheetVisual (ตัวเล่นภาพ spritesheet) ที่ PlayerController และไฟล์อื่นเรียกใช้ด้วย
using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// ตัวช่วยโหลดและเล่น spritesheet ของเอฟเฟกต์สกิล แยกจากโค้ดที่คำนวณความเสียหายจริง
// Pure visual; keeps frame sizes proportional and ignores the gameplay object's nonuniform scale.
public sealed class SkillSheetVisual : MonoBehaviour
{
    // แคช sprite ที่โหลดแล้ว (key = ชื่อชีต) กันโหลดซ้ำ
    private static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
    private static readonly Dictionary<string, Sprite[]> gridSheets = new Dictionary<string, Sprite[]>();
    // โหลด sprite ทั้งหมดจาก Resources/Images/<name> แล้วเรียงตามตำแหน่ง x ในชีต (ซ้ายไปขวา = ลำดับเฟรม)
    public static Sprite[] Load(string name)
    {
        if (!sheets.TryGetValue(name, out var sprites))
        {
            sprites = Resources.LoadAll<Sprite>("Images/" + name);
            System.Array.Sort(sprites, (a, b) => a.rect.x.CompareTo(b.rect.x));
            sheets[name] = sprites;
        }
        return sprites;
    }

    // User-authored VFX boards use a regular 4x4 layout. Explicitly cropping each cell
    // avoids Unity's automatic sprite slicer treating glow fragments as separate frames.
    public static Sprite[] LoadGrid(string name, int columns = 4, int rows = 4, bool pivotAtTop = false)
    {
        string cacheKey = name + "|" + columns + "x" + rows + (pivotAtTop ? "|top" : "|center");
        if (gridSheets.TryGetValue(cacheKey, out var sprites)) return sprites;
        var texture = Resources.Load<Texture2D>("Images/" + name);
        if (texture == null || columns < 1 || rows < 1) return System.Array.Empty<Sprite>();

        sprites = new Sprite[columns * rows];
        int index = 0;
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            int left = Mathf.RoundToInt(column * texture.width / (float)columns);
            int right = Mathf.RoundToInt((column + 1) * texture.width / (float)columns);
            int bottom = Mathf.RoundToInt((rows - row - 1) * texture.height / (float)rows);
            int top = Mathf.RoundToInt((rows - row) * texture.height / (float)rows);
            sprites[index] = Sprite.Create(texture, new Rect(left, bottom, right - left, top - bottom),
                pivotAtTop ? new Vector2(.5f, 1f) : new Vector2(.5f, .5f),
                100, 0, SpriteMeshType.FullRect);
            sprites[index].name = name + "_frame_" + index;
            index++;
        }
        gridSheets[cacheKey] = sprites;
        return sprites;
    }

    // สร้าง GameObject แสดงแอนิเมชัน: follow = ตามวัตถุ (null = อยู่กับที่), size = ขนาด (หน่วยโลก), duration = อายุ (0 = ให้ผู้เรียกสั่ง SetFrame เอง)
    // ใช้ PhotonNetwork.Time เป็นนาฬิกา ภาพจึงเล่นตรงกันทุกเครื่อง คืน null ถ้าไม่มีเฟรม
    public static SkillSheetVisual Create(Sprite[] frames, Transform follow, Vector3 position, float size,
        float duration = 0, bool upright = true, float rotationOffset = 0, bool bottom = false)
    {
        if (frames == null || frames.Length == 0) return null;
        var obj = new GameObject("SkillSheetVisual");
        obj.transform.position = position;
        var visual = obj.AddComponent<SkillSheetVisual>();
        visual.frames = frames;
        visual.follow = follow;
        visual.hadFollow = follow != null;
        visual.anchor = position;
        visual.followOffset = follow != null ? position - follow.position : Vector3.zero;
        visual.upright = upright;
        visual.rotationOffset = rotationOffset;
        visual.bottom = bottom;
        visual.duration = duration;
        visual.started = PhotonNetwork.Time;
        visual.renderer2D = obj.AddComponent<SpriteRenderer>();
        visual.renderer2D.sortingOrder = 12;
        var previous = new GameObject("FrameBlend");
        previous.transform.SetParent(obj.transform, false);
        visual.previousRenderer = previous.AddComponent<SpriteRenderer>();
        visual.previousRenderer.sortingOrder = 11;
        visual.previousRenderer.enabled = false;
        float largest = .01f;
        foreach (var sprite in frames) largest = Mathf.Max(largest, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        visual.scale = size / largest;
        visual.SetFrame(0);
        return visual;
    }

    // ข้อมูลภายในของแอนิเมชัน: เฟรม, เป้าที่ตาม, การจัดตำแหน่ง, renderer หลัก + renderer เฟรมก่อนหน้า (ไว้ผสมเฟรม), เวลาเริ่ม
    private Sprite[] frames;
    private Transform follow;
    private bool hadFollow, upright, bottom;
    private Vector3 anchor;
    private Vector3 followOffset;
    private SpriteRenderer renderer2D;
    private SpriteRenderer previousRenderer;
    private float frameChangedAt;
    private float scale, duration, rotationOffset;
    private double started;
    private int frame = -1;
    // breakTime = วินาทีที่เริ่มเล่นช่วง "แตก" (ใช้กับโล่) -1 = ไม่ใช้; Break() กระโดดไปช่วงแตกทันที
    public float breakTime = -1;
    // สั่งให้แอนิเมชันกระโดดไปเล่นช่วง "แตก" ทันที (เลื่อนเวลาเริ่ม started ย้อนกลับ) ใช้ตอนโล่แตก ไม่มีผลถ้า breakTime < 0
    public void Break() { if (breakTime >= 0) started = PhotonNetwork.Time - breakTime; }
    // เปลี่ยนเฟรมที่แสดง เก็บเฟรมเดิมไว้ใน previousRenderer เพื่อทำ crossfade
    public void SetFrame(int index)
    {
        index = Mathf.Clamp(index, 0, frames.Length - 1);
        if (index == frame) return;
        previousRenderer.sprite = renderer2D.sprite;
        frameChangedAt = Time.time;
        frame = index;
        renderer2D.sprite = frames[frame];
    }
    // ทุกเฟรมหลัง Update: คำนวณเฟรมจากอายุ, ขนาด/ความทึบ (ขยาย-จาง หรือเต้นเบาๆ) และวางภาพตามวัตถุที่ตาม
    private void LateUpdate()
    {
        // 1) ถ้าวัตถุที่ตามถูกทำลายให้ลบตัวเอง ถ้าถูกปิดอยู่ให้ซ่อน
        if (hadFollow && follow == null) { Destroy(gameObject); return; }
        if (hadFollow && !follow.gameObject.activeInHierarchy)
        {
            renderer2D.enabled = previousRenderer.enabled = false;
            return;
        }
        renderer2D.enabled = true;
        // 2) เลือกเฟรมจากอายุ (หมดอายุแล้วทำลายตัวเอง)
        float age = (float)(PhotonNetwork.Time - started);
        if (duration > 0)
        {
            if (age >= duration) { Destroy(gameObject); return; }
            int index = breakTime > 0
                ? (age >= breakTime ? frames.Length - 1 : Mathf.FloorToInt(age / breakTime * (frames.Length - 1)))
                : Mathf.FloorToInt(age / duration * frames.Length);
            SetFrame(index);
        }
        // 3) หามุมและจุด pivot ของภาพ
        Quaternion rotation = !upright && follow != null ? follow.rotation * Quaternion.Euler(0, 0, rotationOffset) : Quaternion.identity;
        Bounds bounds = renderer2D.sprite.bounds;
        Vector3 pivot = bottom ? new Vector3(bounds.center.x, bounds.min.y, 0) : bounds.center;
        // Short crossfades bridge the sparse hand-cut frames without changing the gameplay clock.
        float blendTime = duration > 0 && breakTime < 0 ? Mathf.Min(.09f, duration / frames.Length * .5f) : .1f;
        float blend = previousRenderer.sprite == null ? 1 : Mathf.SmoothStep(0, 1, (Time.time - frameChangedAt) / Mathf.Max(.01f, blendTime));
        float opacity = Mathf.SmoothStep(0, 1, age / .065f);
        // 4) คำนวณขนาดและความทึบตามแบบของเอฟเฟกต์ (อยู่กับที่ / มี breakTime / ตามวัตถุ)
        float size = 1f;
        if (!hadFollow && duration > 0)
        {
            float progress = Mathf.Clamp01(age / duration);
            size = Mathf.Lerp(.8f, 1.12f, 1 - Mathf.Pow(1 - progress, 3));
            opacity *= 1 - Mathf.SmoothStep(0, 1, (progress - .6f) / .4f);
        }
        else if (breakTime > 0)
        {
            size = Mathf.Lerp(.88f, 1f, Mathf.SmoothStep(0, 1, age / .18f)) * (1 + .018f * Mathf.Sin(age * 8));
            if (age >= breakTime)
            {
                float breaking = Mathf.Clamp01((age - breakTime) / Mathf.Max(.01f, duration - breakTime));
                size *= Mathf.Lerp(1, 1.18f, breaking);
                opacity *= 1 - Mathf.SmoothStep(0, 1, breaking);
            }
        }
        else size = 1 + (bottom ? .014f : .022f) * Mathf.Sin(age * (bottom ? 12 : 20));
        // 5) ใส่สี ผสมเฟรมก่อนหน้า และวางตำแหน่ง/หมุน/สเกลจริง
        float animatedScale = scale * size;
        renderer2D.color = new Color(1, 1, 1, opacity * blend);
        previousRenderer.enabled = previousRenderer.sprite != null && blend < 1;
        if (previousRenderer.enabled)
        {
            Bounds previousBounds = previousRenderer.sprite.bounds;
            Vector3 previousPivot = bottom ? new Vector3(previousBounds.center.x, previousBounds.min.y, 0) : previousBounds.center;
            previousRenderer.transform.localPosition = pivot - previousPivot;
            previousRenderer.color = new Color(1, 1, 1, opacity * (1 - blend));
        }
        transform.rotation = rotation;
        transform.localScale = Vector3.one * animatedScale;
        transform.position = (follow != null ? follow.position + followOffset : anchor) - rotation * (pivot * animatedScale);
    }
}

// คลาสวัตถุสกิลในเครือข่าย 1 ชิ้น: คลื่นสตัน (พุ่งหาเป้า), ระเบิดโนวา (รอ 1.5 วิแล้วระเบิดเป็นวง), มิสไซล์ติดตาม (เร่งความเร็วขึ้นเรื่อยๆ)
public class SkillController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    // ชนิดสกิล ตั้งไว้ใน Prefab แต่ละตัว
    public enum SkillBehavior { StunWave, NovaBlast, SeekerMissile }
    public SkillBehavior behavior; // ชนิดสกิลของ Prefab นี้ กำหนดวิธีเคลื่อนที่และเอฟเฟกต์
    // damage = ดาเมจต่อครั้ง (ถูกแทนด้วยค่าที่ส่งมาตอนสร้าง), speed = ความเร็ว (หน่วย/วินาที), lifeTime = อายุ (วินาที)
    public float damage = 10f;
    public float speed = 10f;
    public float lifeTime = 3f;
    public float skillParam2 = 0f;
    // isDestroyed = กันคิดชนซ้ำหลังสั่งทำลาย, novaArmed = โนวาระเบิดไปแล้ว, impactPlayed = เล่นเอฟเฟกต์ชนไปแล้ว
    private bool isDestroyed, novaArmed, impactPlayed;
    private Transform target;
    // ตัวคูณความเร็วมิสไซล์: เริ่ม 0.5 เท่า เพิ่ม 1.5 ต่อวินาที สูงสุด 2.5 เท่า (ดู Update)
    private float seekerSpeedMultiplier = .5f;
    private double started;
    private int missileColor;
    private SkillSheetVisual visual;
    private Sprite[] frames;
    // ViewID ของยานที่โดนดาเมจไปแล้ว กันสกิลเดียวทำดาเมจยานเดิมซ้ำ
    private readonly HashSet<int> damagedPlayerViewIds = new HashSet<int>();
    // ผู้ใช้สกิล (คน = ActorNumber, บอท = 1000+) ใช้กันสกิลโดนตัวเอง และส่งเป็นคนฆ่า
    private int casterId = -1;
    // โนวาระเบิดหลังสร้าง 1.5 วินาที รัศมี 3 หน่วยโลก
    private const float NovaDelay = 1.5f;
    private const float NovaRadius = 3f;

    // Photon เรียกตอนวัตถุสกิลถูกสร้างบนแต่ละเครื่อง (ทุกเครื่อง): อ่านดาเมจ/เวลาเริ่ม เลือกสีมิสไซล์ตามยานของเจ้าของ และสร้างภาพ
    // โนวาจะหยุดนิ่งและปิด Collider (ใช้ OverlapCircle แทน) ส่วนสกิลอื่นเครื่องเจ้าของจะหาเป้าหมายที่ใกล้สุด
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // 1) อ่านข้อมูลตอนสร้าง: ดาเมจ, เวลาเซิร์ฟเวอร์ที่สร้าง, สีมิสไซล์
        var data = photonView.InstantiationData;
        if (data != null && data.Length > 0 && data[0] is float value) damage = value;
        // [1] = เลขผู้ใช้สกิล (บอทใช้เลข 1000+) ถ้าไม่มีใช้ ActorNumber ของคนสร้างแบบเดิม
        casterId = data != null && data.Length > 1 && data[1] is int caster ? caster : photonView.CreatorActorNr;
        started = info.SentServerTime;
        if (photonView.Owner != null && photonView.Owner.CustomProperties.TryGetValue("ShipType", out object shipValue) && shipValue is int ship)
            missileColor = ship == 1 ? 1 : ship == 2 ? 0 : 2; // Ship1 purple, Ship2 blue, Ship3 orange.
        else missileColor = 2;

        // 2) โหลด spritesheet ของสกิล แล้วซ่อนภาพ/อนุภาค/หางเดิมของ Prefab ใช้ SkillSheetVisual แทน
        if (behavior == SkillBehavior.NovaBlast) lifeTime = 2f;
        string sheet = behavior == SkillBehavior.NovaBlast ? "VFX/VFX_NovaBlast"
            : behavior == SkillBehavior.SeekerMissile ? "VFX_SeekerMissile" : "VFX_StunWave";
        frames = behavior == SkillBehavior.NovaBlast
            ? SkillSheetVisual.LoadGrid(sheet)
            : SkillSheetVisual.Load(sheet);
        if (frames.Length > 0)
        {
            var main = GetComponent<SpriteRenderer>();
            if (main != null) main.enabled = false;
            foreach (var particle in GetComponentsInChildren<ParticleSystem>())
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var trail in GetComponentsInChildren<TrailRenderer>()) { trail.emitting = false; trail.Clear(); trail.enabled = false; }
            visual = SkillSheetVisual.Create(frames, transform, transform.position,
                behavior == SkillBehavior.NovaBlast ? 6f : behavior == SkillBehavior.SeekerMissile ? 2.1f : 3f,
                0, behavior == SkillBehavior.NovaBlast, behavior == SkillBehavior.SeekerMissile ? 55f : -90f,
                behavior == SkillBehavior.NovaBlast);
            if (behavior == SkillBehavior.SeekerMissile) visual.SetFrame(missileColor);
        }

        // 3) ตั้งค่าตามชนิด: โนวาไม่เคลื่อนที่ / สตันและมิสไซล์ให้เจ้าของล็อกเป้า
        if (behavior == SkillBehavior.NovaBlast)
        {
            var body = GetComponent<Rigidbody2D>();
            if (body != null) { body.linearVelocity = Vector2.zero; body.bodyType = RigidbodyType2D.Kinematic; }
            // The blast is resolved by one radius query, never by the prefab's oversized trigger.
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        }
        else if (photonView.IsMine) FindNearestTarget();
        UpdateVisual(0);
    }

    // เลือกเฟรมภาพตามอายุ: โนวาแสดงช่วงชาร์จจนครบ NovaDelay แล้วเป็นช่วงระเบิด, คลื่นสตันไล่เฟรมตาม lifeTime
    private void UpdateVisual(float age)
    {
        if (visual == null || frames == null || frames.Length == 0) return;
        if (behavior == SkillBehavior.NovaBlast)
        {
            int chargeCount = Mathf.Max(1, frames.Length - 2);
            // Frame 3 contains a baked '3'; omit it rather than advertise a false countdown.
            int[] charge = frames.Length == 8 ? new[] { 0, 1, 2, 4, 5 } : null;
            int index = age < NovaDelay
                ? (charge != null ? charge[Mathf.Clamp(Mathf.FloorToInt(age / NovaDelay * charge.Length), 0, charge.Length - 1)]
                    : Mathf.Clamp(Mathf.FloorToInt(age / NovaDelay * chargeCount), 0, chargeCount - 1))
                : chargeCount + Mathf.Clamp(Mathf.FloorToInt((age - NovaDelay) / .5f * 2), 0, frames.Length - chargeCount - 1);
            visual.SetFrame(index);
        }
        else if (behavior == SkillBehavior.StunWave)
            visual.SetFrame(Mathf.FloorToInt(Mathf.Clamp01(age / Mathf.Max(.01f, lifeTime)) * (frames.Length - 1)));
    }

    // ทุกเฟรม: ทุกเครื่องอัปเดตภาพและจุดระเบิดโนวา; เฉพาะเครื่องเจ้าของสกิลจะนับอายุ หันหาเป้า เคลื่อนที่ และตรวจชน
    void Update()
    {
        if (isDestroyed) return;
        // 1) ทุกเครื่อง: ภาพตามอายุ และโนวาระเบิดเมื่อครบเวลา
        float age = Mathf.Max(0, (float)(PhotonNetwork.Time - started));
        UpdateVisual(age);
        if (behavior == SkillBehavior.NovaBlast)
        {
            if (!novaArmed && age >= NovaDelay) DetonateNova();
        }
        // 2) ต่อจากนี้เฉพาะเจ้าของ: หมดอายุแล้วทำลาย (โนวาไม่เคลื่อนที่)
        if (!photonView.IsMine) return;
        if (age >= lifeTime) { DestroySkill(); return; }
        if (behavior == SkillBehavior.NovaBlast) return;
        // 3) หันหาเป้า (ถ้าเป้าตายแล้วเลิกตาม)
        Vector2 movementStart = transform.position;
        if (target != null && (target.GetComponent<PlayerController>() == null || target.GetComponent<PlayerController>().isDead))
            target = null;
        if (target != null)
        {
            Vector2 direction = target.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, angle), Time.deltaTime * 5f);
        }
        // 4) มิสไซล์เร่งความเร็ว แล้วกวาดตรวจชนระหว่างจุดเดิมกับจุดใหม่ก่อนขยับ
        if (behavior == SkillBehavior.SeekerMissile)
            seekerSpeedMultiplier = Mathf.Min(seekerSpeedMultiplier + Time.deltaTime * 1.5f, 2.5f);
        float multiplier = behavior == SkillBehavior.SeekerMissile ? seekerSpeedMultiplier : 1f;
        Vector2 end = movementStart + (Vector2)transform.up * speed * multiplier * Time.deltaTime;
        if (ProjectileSweep.FirstHit(transform, casterId, movementStart, end, out var hit))
        {
            transform.position = hit.centroid;
            OnTriggerEnter2D(hit.collider);
        }
        else transform.position = end;
    }

    // ระเบิดโนวา: ทุกเครื่องเล่นเสียง/ภาพ แต่เฉพาะเครื่องเจ้าของหายานศัตรูในรัศมี NovaRadius แล้วส่งดาเมจ
    private void DetonateNova()
    {
        novaArmed = true;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Nova"); // ปิด NewSounds = เสียงระเบิดแบบเดิม
        if (visual == null)
        {
            var prefab = GameplayManager.GetPrefab("DeathExplosion");
            if (prefab != null) Instantiate(prefab, transform.position, Quaternion.identity);
        }
        if (!photonView.IsMine) return;
        foreach (var collider in Physics2D.OverlapCircleAll(transform.position, NovaRadius))
        {
            var player = collider.GetComponentInParent<PlayerController>();
            if (player != null && !MatchRules.IsAlly(player.CombatantId, casterId)) DealDamage(player);
        }
    }

    // หาเป้า (เครื่องเจ้าของ): ยานศัตรูที่ยังไม่ตายและใกล้ที่สุด; คลื่นสตันล็อกได้ไม่เกินระยะ speed * lifeTime, มิสไซล์ไม่จำกัดระยะ
    private void FindNearestTarget()
    {
        // Preserve Seeker's existing lock behavior; Stun only acquires within its original travel budget.
        float nearest = behavior == SkillBehavior.StunWave ? speed * lifeTime : float.PositiveInfinity;
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.isDead || MatchRules.IsAlly(player.CombatantId, casterId)) continue;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < nearest) { nearest = distance; target = player.transform; }
        }
    }

    // ส่ง RPC "TakeDamage" ไป RpcTarget.All (เครื่องเจ้าของยานที่โดนเป็นคนหักเลือด) โดยยานแต่ละลำโดนได้ครั้งเดียวต่อสกิล
    private void DealDamage(PlayerController player)
    {
        if (!player.isDead && damagedPlayerViewIds.Add(player.photonView.ViewID))
            PlayerController.SendDamage(player, damage, casterId, true); // เฟส 9: ผ่าน Host ถ้าเปิด HostDamage
    }

    // Unity เรียกเมื่อชนแบบไม่ใช่ Trigger: ส่งต่อให้ OnTriggerEnter2D
    void OnCollisionEnter2D(Collision2D collision) => OnTriggerEnter2D(collision.collider);
    // ผลการชน (เฉพาะเครื่องเจ้าของ ไม่ใช้กับโนวา): ข้ามยานตัวเอง/ยานที่ตาย/Trigger ที่ไม่ใช่ยาน
    // ทำลายสกิล ส่ง PlayImpactRPC ไปทุกเครื่อง ถ้าเป็นคลื่นสตันส่ง ApplyStunRPC (RpcTarget.All) แล้วส่งดาเมจ
    void OnTriggerEnter2D(Collider2D hit)
    {
        if (!photonView.IsMine || isDestroyed || behavior == SkillBehavior.NovaBlast) return;
        var player = hit.GetComponentInParent<PlayerController>();
        if (player != null && (player.isDead || MatchRules.IsAlly(player.CombatantId, casterId))) return;
        if (player == null && hit.isTrigger) return;
        // Lock before RPCs so trigger and swept collision cannot apply the same hit twice.
        DestroySkill();
        photonView.RPC(nameof(PlayImpactRPC), RpcTarget.All, transform.position);
        if (player == null) return;
        if (behavior == SkillBehavior.StunWave) player.photonView.RPC("ApplyStunRPC", RpcTarget.All);
        DealDamage(player);
    }

    // RPC เอฟเฟกต์ระเบิดตอนสกิลชน: รันทุกเครื่อง ตรวจผู้ส่งว่าเป็นเจ้าของสกิล ซ่อนภาพสกิลแล้วเล่นภาพแรงกระแทก + เสียง (เล่นครั้งเดียว)
    [PunRPC]
    public void PlayImpactRPC(Vector3 position, PhotonMessageInfo info)
    {
        if (impactPlayed || info.Sender != photonView.Owner) return;
        impactPlayed = true;
        if (visual != null) visual.gameObject.SetActive(false);
        var root = GetComponent<SpriteRenderer>();
        if (root != null) root.enabled = false;
        if (behavior == SkillBehavior.SeekerMissile)
        {
            int[] ids = missileColor == 2 ? new[] { 194, 166 } : missileColor == 1
                ? new[] { 170, 164, 171 } : new[] { 124, 125, 117 };
            var impacts = new List<Sprite>();
            foreach (int id in ids)
                foreach (var sprite in SkillSheetVisual.Load("VFX_ShipEffects"))
                    if (sprite.name.EndsWith("_" + id)) { impacts.Add(sprite); break; }
            SkillSheetVisual.Create(impacts.ToArray(), null, position, 3.5f, .35f);
        }
        else if (frames != null && frames.Length > 0)
        {
            int start = Mathf.Max(0, frames.Length - 3);
            var impacts = new Sprite[frames.Length - start];
            System.Array.Copy(frames, start, impacts, 0, impacts.Length);
            SkillSheetVisual.Create(impacts, null, position, 3f, .35f);
        }
        // มิสไซล์ = ระเบิดเล็ก (ปิด NewSounds = เสียงโดนยิงแบบเดิม) / คลื่นสตัน = เสียงโดนยิง
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(behavior == SkillBehavior.SeekerMissile ? "SFX_MissileHit" : "SFX_Hit");
    }

    // ทำลายสกิล (เฉพาะเจ้าของ ครั้งเดียว): ซ่อนภาพ ปิด Collider หยุดความเร็ว แล้วค่อยลบผ่านเครือข่าย
    private void DestroySkill()
    {
        if (!photonView.IsMine || isDestroyed) return;
        isDestroyed = true;
        if (visual != null) visual.gameObject.SetActive(false);
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        var body = GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        StartCoroutine(NetworkDestroyRoutine());
    }
    // รอ 0.1 วินาทีแล้ว PhotonNetwork.Destroy เพื่อลบออกจากทุกเครื่อง
    private IEnumerator NetworkDestroyRoutine()
    {
        yield return new WaitForSeconds(.1f);
        if (PhotonNetwork.InRoom && photonView.IsMine) PhotonNetwork.Destroy(gameObject);
    }
    // Unity เรียกตอนถูกทำลาย: ลบ GameObject ภาพที่สร้างแยกไว้ด้วย
    private void OnDestroy()
    {
        if (visual != null) Destroy(visual.gameObject);
    }
}
