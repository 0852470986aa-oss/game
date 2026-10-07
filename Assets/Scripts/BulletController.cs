// ไฟล์กระสุนปกติ: อยู่บน Prefab "BulletPrefab" ใน Resources สร้างด้วย PhotonNetwork.Instantiate จาก PlayerController.Shoot (เจ้าของยาน)
// หรือ PhotonNetwork.InstantiateRoomObject จาก AutoTurret (Master Client) ทุกเครื่องจึงเห็นกระสุนลูกเดียวกัน
// หลักเครือข่าย: เฉพาะเครื่องเจ้าของกระสุน (photonView.IsMine = คนยิง) เป็นคนตรวจชน ส่ง RPC TakeDamage และสั่งทำลายกระสุน
using UnityEngine;
using Photon.Pun;

// ระบบกระสุน: ProjectileSweep หาเป้าชนระหว่างเฟรม ส่วนตัวควบคุมกระสุนจัดการการเคลื่อนที่และผลการชน
// Shared swept-volume check: explicit trigger filtering keeps results independent of project query settings.
public static class ProjectileSweep
{
    // ตรวจการชนแบบกวาด (CircleCast) จาก start ไป end เพื่อกันกระสุนเร็วทะลุของ คืน hit ที่ใกล้ที่สุด
    // ข้าม: ตัวเอง, ป้อมปืน (ถ้า ignoreTurrets), ยานที่ตายหรือยานของคนยิง, Trigger และกระสุน/สกิลอื่น; ใช้โดย BulletController และ SkillController
    public static bool FirstHit(Transform projectile, int shooter, Vector2 start, Vector2 end, out RaycastHit2D nearest,
        bool ignoreTurrets = false)
    {
        nearest = default;
        float radius = .08f;
        var shape = projectile.GetComponent<Collider2D>();
        if (shape != null) radius = Mathf.Max(radius, Mathf.Min(shape.bounds.extents.x, shape.bounds.extents.y));
        var filter = new ContactFilter2D { useTriggers = true };
        var hits = new System.Collections.Generic.List<RaycastHit2D>();
        Vector2 travel = end - start;
        Physics2D.CircleCast(start, radius, travel.sqrMagnitude > 0 ? travel.normalized : Vector2.up,
            filter, hits, travel.magnitude);
        float distance = float.PositiveInfinity;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(projectile)) continue;
            if (ignoreTurrets && hit.collider.GetComponentInParent<AutoTurret>() != null) continue;
            var player = hit.collider.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                // ตัวเอง/เพื่อนร่วมทีม = กระสุนทะลุผ่าน (ยิงเพื่อนไม่โดน)
                if (player.isDead || MatchRules.IsAlly(player.CombatantId, shooter)) continue;
            }
            else if (hit.collider.isTrigger || hit.collider.GetComponentInParent<BulletController>() != null
                || hit.collider.GetComponentInParent<SkillController>() != null) continue;
            if (hit.distance < distance) { distance = hit.distance; nearest = hit; }
        }
        return nearest.collider != null;
    }
}

// คลาสกระสุน 1 นัด: เคลื่อนที่ตรงไปทางหัว (transform.up) ชนยานศัตรูแล้วส่งดาเมจ ชนของแข็งแล้วระเบิด ชนคริสตัลปริซึมแล้วเด้ง
public class BulletController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    // speed = ความเร็วกระสุน (หน่วย/วินาที), lifeTime = อายุก่อนหายเอง (วินาที), damage = ดาเมจต่อนัด (รับจาก InstantiationData)
    public float speed = 18f; // PHASE 6: ยิงเร็วขึ้นจาก 15 เป็น 18
    public float lifeTime = 3f;
    private float damage = 10f; // จะถูกตั้งค่าตอน instantiate
    private bool isDestroyed = false;
    // true = กระสุนจากป้อมปืน (ไม่ชนป้อมปืนด้วยกัน)
    private bool isTurretProjectile;
    // ActorNumber ของคนยิง; ถ้า InstantiationData[1] เป็น true (กระสุนของฉาก/ป้อมปืน) จะได้ -1 คือไม่มีเจ้าของที่ได้แต้ม
    private int Shooter => photonView.InstantiationData != null && photonView.InstantiationData.Length > 1
        && photonView.InstantiationData[1] is bool environmental && environmental ? -1 : photonView.CreatorActorNr;
    private Rigidbody2D body;
    // จำนวนครั้งที่เด้งจากคริสตัลได้อีก (เริ่มที่ PrismReflector.MaxBounces)
    private int bouncesLeft = PrismReflector.MaxBounces;

    // ===== โหมดกระสุนแบบเบา (FeatureFlags.LightBullets) =====
    // localMode = กระสุนที่แต่ละเครื่องสร้างเองจาก RPC (ไม่มี PhotonView) แทนการสร้างผ่าน PhotonNetwork.Instantiate
    // authority = เครื่องนี้เป็นคนยิง (คิดชน/ส่งดาเมจ) ส่วนเครื่องอื่นเป็นแค่ภาพที่บินตามและหายเมื่อชน
    private bool localMode;
    private bool authority;
    private int localShooter = -1;
    private bool IsAuthority => localMode ? authority : photonView.IsMine;
    private int ShooterId => localMode ? localShooter : Shooter;

    // สร้างกระสุนแบบเบาบนเครื่องนี้ (เรียกจาก PlayerController.SpawnShot)
    // lag = เวลาที่ข้อความเดินทางมา (วินาที) ใช้เลื่อนกระสุนไปข้างหน้าให้ตรงกับเครื่องคนยิง
    public static BulletController SpawnLocal(UnityEngine.Vector2 position, float angle, float damage,
        int shooter, bool isAuthority, float lag)
    {
        var prefab = Resources.Load<GameObject>("BulletPrefab");
        if (prefab == null) return null;
        Quaternion rotation = Quaternion.Euler(0, 0, angle);
        Vector2 forward = rotation * Vector3.up;
        position += forward * 18f * Mathf.Clamp(lag, 0f, .3f);
        GameObject go = Instantiate(prefab, position, rotation);
        // กระสุนนี้ไม่ใช่วัตถุออนไลน์: เอาตัวซิงก์ของ Photon (เช่น PhotonTransformView) และ PhotonView ออก
        // ไม่งั้นตัวซิงก์จะพยายามดึงกระสุนไปตำแหน่งจากเครือข่ายที่ไม่มีอยู่จริง
        foreach (var component in go.GetComponents<MonoBehaviour>())
            if (component is IPunObservable && !(component is BulletController)) DestroyImmediate(component);
        var view = go.GetComponent<PhotonView>();
        if (view != null) DestroyImmediate(view);
        var bullet = go.GetComponent<BulletController>();
        if (bullet == null) { Destroy(go); return null; }
        bullet.localMode = true;
        bullet.authority = isAuthority;
        bullet.localShooter = shooter;
        bullet.damage = damage;
        return bullet;
    }

    // ทุกช่วงฟิสิกส์ เฉพาะเครื่องคนยิง: กวาดหาสิ่งที่จะชนระหว่างตำแหน่งนี้ถึงตำแหน่งถัดไป ถ้าเจอให้เด้งหรือคิดผลชน
    // ถ้าไม่มี Rigidbody2D จะขยับ transform เอง
    void FixedUpdate()
    {
        if (isDestroyed) return;
        // เครื่องอื่น (ไม่ใช่คนยิง) ในโหมดเบา: กระสุนเป็นแค่ภาพ ถ้าชนอะไรก็หายไป (ไม่คิดดาเมจ)
        if (localMode && !authority)
        {
            Vector2 from = body != null ? body.position : (Vector2)transform.position;
            Vector2 to = from + (Vector2)transform.up * speed * Time.fixedDeltaTime;
            if (ProjectileSweep.FirstHit(transform, ShooterId, from, to, out var visualHit, isTurretProjectile))
            {
                if (TryReflect(visualHit.collider, visualHit.normal, visualHit.centroid)) return;
                transform.position = visualHit.centroid;
                DestroyBullet();
            }
            else if (body == null) transform.position = to;
            return;
        }
        if (!IsAuthority) return;
        Vector2 start = body != null ? body.position : (Vector2)transform.position;
        Vector2 end = start + (Vector2)transform.up * speed * Time.fixedDeltaTime;
        if (ProjectileSweep.FirstHit(transform, ShooterId, start, end, out var hit, isTurretProjectile))
        {
            if (TryReflect(hit.collider, hit.normal, hit.centroid)) return;
            transform.position = hit.centroid;
            OnTriggerEnter2D(hit.collider);
        }
        else if (body == null) transform.position = end;
    }

    // Unity เรียกเมื่อชนแบบไม่ใช่ Trigger (ทุกเครื่อง): ลองเด้งก่อน ไม่งั้นส่งต่อให้ OnTriggerEnter2D (ซึ่งเช็ค IsMine อีกที)
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.contactCount > 0)
        {
            var contact = collision.GetContact(0);
            if (TryReflect(collision.collider, contact.normal, contact.point + contact.normal * .2f)) return;
        }
        OnTriggerEnter2D(collision.collider);
    }

    // แม็พปริซึม: ชนคริสตัล (PrismReflector) แล้วเด้งออกแทนการระเบิด คนยิงเป็นคนคำนวณ แล้วแจ้งเครื่องอื่น
    private bool TryReflect(Collider2D surface, Vector2 normal, Vector2 contactCenter)
    {
        if (isDestroyed || bouncesLeft <= 0 || surface == null) return false;
        // โหมดเบา: ทุกเครื่องคำนวณการเด้งเองจากตำแหน่งคริสตัลที่เหมือนกัน (ไม่ต้องส่ง RPC) / โหมดเดิม: เฉพาะคนยิง
        if (!localMode && !photonView.IsMine) return false;
        var reflector = surface.GetComponentInParent<PrismReflector>();
        if (reflector == null || normal.sqrMagnitude < .0001f) return false;
        bouncesLeft--;
        Vector2 direction = Vector2.Reflect(transform.up, normal.normalized).normalized;
        Vector2 position = contactCenter + normal.normalized * .1f;
        ApplyBounce(position, direction);
        reflector.Flash(position);
        if (!localMode) photonView.RPC("BounceRPC", RpcTarget.Others, position, direction);
        return true;
    }

    // RPC เด้ง: คนยิงส่งด้วย RpcTarget.Others (เครื่องอื่นเท่านั้น เพราะเครื่องตัวเองเด้งไปแล้วใน TryReflect)
    // ตรวจว่าผู้ส่งเป็นเจ้าของกระสุนจริง แล้วตั้งตำแหน่ง/ทิศใหม่และเล่นแสงวาบ
    [PunRPC]
    private void BounceRPC(Vector2 position, Vector2 direction, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Owner) return;
        ApplyBounce(position, direction);
        PrismFx.Burst(position, PrismReflector.FlashColor, 1.8f);
    }

    // ตั้งตำแหน่งและหมุนกระสุนไปทิศใหม่ ปรับความเร็ว Rigidbody และเล่นเสียง (ใช้ทั้งเครื่องคนยิงและเครื่องอื่น)
    private void ApplyBounce(Vector2 position, Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
        if (body != null)
        {
            body.position = position;
            body.rotation = angle;
            body.linearVelocity = direction * speed;
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_HitMiss");
    }

    // Unity เรียกตอนสร้าง (ทุกเครื่อง): ตั้ง Rigidbody2D ไม่มีแรงโน้มถ่วง ตรวจชนแบบ Continuous และเพิ่มหางแสง (TrailRenderer) เหลือง-ส้ม
    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body != null) body.gravityScale = 0;
        if (body != null) body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // PHASE 4: เพิ่มหางแสง (Trail) ให้กระสุนดูพุ่งเร็วและแรงขึ้น
        if (GetComponent<TrailRenderer>() == null)
        {
            TrailRenderer tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.12f;
            tr.startWidth = 0.3f;
            tr.endWidth = 0f;
            tr.material = new Material(Shader.Find("Sprites/Default"));
            
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 1f, 0.5f), 0.0f), new GradientColorKey(new Color(1f, 0.5f, 0f), 1.0f) }, // เหลืองไปส้ม
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            tr.colorGradient = gradient;
        }
    }

    // Unity เรียกหลัง Awake (ทุกเครื่อง): ตั้งความเร็วพุ่งไปข้างหน้า; เครื่องคนยิงตั้งเวลาทำลายเมื่อครบ lifeTime
    void Start()
    {
        // ให้ทุกเครื่องกำหนดความเร็วเท่ากันเพื่อให้ภาพกระสุนไม่ค้างเฉพาะฝั่งที่ไม่ได้ยิง
        if (GetComponent<Rigidbody2D>() != null)
        {
            GetComponent<Rigidbody2D>().linearVelocity = transform.up * speed;
        }

        // ลบตัวเองถ้าอยู่นานเกินไป (เพื่อไม่ให้กินสเปคเครื่อง) — โหมดเบาทุกเครื่องลบสำเนาของตัวเอง
        if (localMode || photonView.IsMine)
        {
            Invoke("DestroyBullet", lifeTime);
        }
    }

    // Photon เรียกตอนกระสุนถูกสร้างบนแต่ละเครื่อง: อ่าน InstantiationData [0] = ดาเมจ, [2] = เป็นกระสุนป้อมปืนหรือไม่
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // รับค่า damage ที่ส่งมาจากคนยิง
        object[] instantiationData = info.photonView.InstantiationData;
        if (instantiationData != null && instantiationData.Length > 0)
        {
            damage = (float)instantiationData[0];
            isTurretProjectile = instantiationData.Length > 2 && instantiationData[2] is bool turretShot && turretShot;
        }
    }

    // ผลการชน (คิดเฉพาะเครื่องคนยิง): ชนยานศัตรู = ทำลายกระสุนแล้วส่ง RPC "TakeDamage" ไป RpcTarget.All (เครื่องเจ้าของยานที่โดนเป็นคนหักเลือด)
    // ชนของแข็งที่ไม่ใช่ Trigger = เล่นเอฟเฟกต์ยิงพลาดในเครื่องคนยิงแล้วทำลายกระสุน
    void OnTriggerEnter2D(Collider2D hitInfo)
    {
        if (localMode && !authority)
        {
            var touched = hitInfo.GetComponentInParent<PlayerController>();
            if (!isDestroyed && ((touched != null && !MatchRules.IsAlly(touched.CombatantId, ShooterId)) || (touched == null && !hitInfo.isTrigger))) DestroyBullet();
            return;
        }
        if (!IsAuthority || isDestroyed) return; // เฉพาะคนยิงเท่านั้นที่จะเป็นคนคำนวณดาเมจ และถ้ากระสุนถูกทำลายไปแล้วจะไม่คิดซ้ำ
        if (isTurretProjectile && hitInfo.GetComponentInParent<AutoTurret>() != null) return;

        PlayerController enemy = hitInfo.GetComponentInParent<PlayerController>();
        if (enemy != null)
        {
            // ถ้าชนโดนผู้เล่นอื่น (ไม่ใช่ตัวเอง)
            if (!enemy.isDead && !MatchRules.IsAlly(enemy.CombatantId, ShooterId))
            {
                DestroyBullet();
                PlayerController.SendDamage(enemy, damage, ShooterId, false); // เฟส 9: ผ่าน Host ถ้าเปิด HostDamage
            }
        }
        else if (!hitInfo.isTrigger)
        {
            // PHASE 6: ถ้าชนกับวัตถุแข็ง (เช่น เสาหิน) ที่ไม่ใช่ Trigger ให้กระสุนระเบิดทิ้ง
            GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
            if (impactPrefab != null)
            {
                GameObject fx = Instantiate(impactPrefab, transform.position, Quaternion.identity);
                // เปลี่ยนสี Impact เป็นสีเทา/ฟ้าอ่อน เมื่อยิงโดนกำแพง (ยิงพลาด)
                var sr = fx.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.6f, 0.7f, 0.8f, 1f);
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_HitMiss");
            DestroyBullet();
        }
    }

    // ทำลายกระสุน (เฉพาะเครื่องคนยิง ทำครั้งเดียว): หยุด ซ่อนภาพ ปิด Collider ทันที แล้วค่อยลบผ่านเครือข่าย
    private void DestroyBullet()
    {
        // โหมดเบา: ลบเฉพาะสำเนาบนเครื่องนี้ (ไม่ต้องผ่านเครือข่าย)
        if (localMode)
        {
            if (isDestroyed) return;
            isDestroyed = true;
            if (body != null) body.linearVelocity = Vector2.zero;
            if (GetComponent<SpriteRenderer>()) GetComponent<SpriteRenderer>().enabled = false;
            if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = false;
            Destroy(gameObject, .1f);
            return;
        }
        if (photonView.IsMine && !isDestroyed)
        {
            isDestroyed = true;
            if (body != null) body.linearVelocity = Vector2.zero;
            // ซ่อนภาพและปิด Collider ทันทีเพื่อให้ดูเหมือนถูกทำลายแล้ว
            if (GetComponent<SpriteRenderer>()) GetComponent<SpriteRenderer>().enabled = false;
            if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = false;
            
            // หน่วงเวลาทำลายจริง 0.1 วิ เพื่อให้ระบบ Network ส่งข้อมูลการสร้าง(Instantiate)ให้เสร็จก่อน
            StartCoroutine(NetworkDestroyRoutine());
        }
    }

    // รอ 0.1 วินาทีแล้ว PhotonNetwork.Destroy เพื่อลบกระสุนออกจากทุกเครื่อง
    private System.Collections.IEnumerator NetworkDestroyRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        if (photonView != null && gameObject != null)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
