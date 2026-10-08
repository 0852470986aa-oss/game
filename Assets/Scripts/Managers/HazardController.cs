// ไฟล์ HazardController.cs — ตัวควบคุม "อันตรายในฉาก" (Hazard) ของแต่ละแม็พ อยู่ใน Scene เกมเพลย์
// Hazard ถูกสร้างผ่านเครือข่ายโดย Master Client (MapHazardManager.cs ใช้ PhotonNetwork.InstantiateRoomObject)
// จึงมีอยู่ทุกเครื่อง แต่เฉพาะเครื่องเจ้าของ (photonView.IsMine = Master) เป็นคนตัดสินว่าชนยานแล้วส่ง RPC ไปที่ยาน
// ส่วนโซนบึง/แกนพลังงาน ให้เครื่องเจ้าของยานแต่ละลำจัดการกับยานตัวเอง (ดู PlayerController.Skills.cs)
// มีคลาส MoltenContactSurface (หินลาวาที่ชนแล้วโดนดาเมจ) อยู่ในไฟล์นี้ด้วย
using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// Static arena rocks exist on each client; only the owning ship applies contact damage.
// คอมโพเนนต์ติดหินอุกกาบาตที่เป็นสิ่งกีดขวาง (ใส่โดย GameplayManager.Maps.cs) ยานชนแล้วโดนดาเมจลาวา
public class MoltenContactSurface : MonoBehaviour
{
    // เวลา (Time.time) ที่ยานแต่ละลำจะโดนดาเมจครั้งถัดไปได้ (key = InstanceID ของยาน)
    private readonly Dictionary<int, float> nextDamageTimes = new Dictionary<int, float>();
    // Unity เรียกตอนเริ่มชน: ให้ทำงานเหมือนตอนชนค้าง (Stay) เพื่อให้โดนดาเมจทันที
    private void OnCollisionEnter2D(Collision2D collision) => OnCollisionStay2D(collision);
    // Unity เรียกทุกเฟรมฟิสิกส์ที่ยังชนอยู่: รันบนเครื่องเจ้าของยานเท่านั้น (IsMine)
    // หักเลือดยานตัวเองด้วย BattleBalance.LavaDamage ไม่เกิน 1 ครั้งต่อ 1 วินาที (attacker = -1 คือไม่มีผู้ยิง)
    private void OnCollisionStay2D(Collision2D collision)
    {
        var player = collision.gameObject.GetComponentInParent<PlayerController>();
        if (player == null || !player.photonView.IsMine || player.isDead) return;
        if (nextDamageTimes.TryGetValue(player.GetInstanceID(), out float next) && Time.time < next) return;
        nextDamageTimes[player.GetInstanceID()] = Time.time + 1f;
        player.TakeDamage(BattleBalance.LavaDamage, -1);
    }
}

// คลาสหลักของ Hazard แต่ละชิ้น (สายฟ้า, บึงชะลอ, อุกกาบาต, อุกกาบาตลาวา, แกนพลังงาน)
// ทำงานเป็นเฟส: เตือน (กระพริบ) -> เกิดผล -> ทำลายตัวเอง; Photon เรียก OnPhotonInstantiate ตอนถูกสร้าง
public class HazardController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    // ชนิดของ Hazard: Lightning = สายฟ้า (แม็พ 0, ทำให้สตัน), SlowZone = บึงชะลอ (แม็พ 1),
    // Meteor = อุกกาบาตตกพร้อมเขย่ากล้อง, MoltenAsteroid = อุกกาบาตลาวาร่วงจากบน (แม็พ 2), EnergyCore = แกนพลังงานกลางแม็พ 0
    public enum HazardType { Lightning, SlowZone, Meteor, MoltenAsteroid, EnergyCore }
    // ชนิดของ Hazard ตัวนี้ (ตั้งไว้ใน Prefab)
    public HazardType type;

    // อายุของ Hazard (วินาที) นับตั้งแต่ถูกสร้าง ค่าจริงถูกตั้งใหม่ตามชนิดใน OnPhotonInstantiate
    private float lifetime = 5f;
    private float warningTime = 1.5f; // Time before effect happens
    // true = ผ่านช่วงเตือนแล้ว เริ่มมีผลกับยาน
    private bool isEffectActive = false;
    // true = อยู่ถาวร ไม่ทำลายตัวเอง (EnergyCore หรือ SlowZone ที่ส่งข้อมูล persistent = true มา)
    private bool permanent;
    // ยานของเครื่องนี้ที่อยู่ในโซนตอนนี้ เก็บไว้เพื่อแจ้งออกจากโซนตอน Hazard หายไป
    private readonly HashSet<PlayerController> zonePlayers = new HashSet<PlayerController>();

    // จัดการการเข้า/ออกโซนของ SlowZone และ EnergyCore: แจ้งยานของเครื่องนี้ (IsMine) ผ่าน SetBattlefieldZone
    // คืน true ถ้าเป็นชนิดโซน (ผู้เรียกจะไม่ทำต่อ) คืน false ถ้าเป็นชนิดอื่น
    private bool UpdateZone(Collider2D other, bool inside)
    {
        if (type != HazardType.SlowZone && type != HazardType.EnergyCore) return false;
        var player = other.GetComponentInParent<PlayerController>();
        if (player != null && player.photonView.IsMine)
        {
            if (inside) zonePlayers.Add(player); else zonePlayers.Remove(player);
            player.SetBattlefieldZone(GetInstanceID(), type == HazardType.EnergyCore, inside);
        }
        return true;
    }

    // Unity เรียกเมื่อ Hazard ถูกปิด/ทำลาย: แจ้งยานทุกลำที่ยังอยู่ในโซนว่าออกแล้ว กันยานติดสถานะค้าง
    public override void OnDisable()
    {
        foreach (var player in zonePlayers)
            if (player != null) player.SetBattlefieldZone(GetInstanceID(), type == HazardType.EnergyCore, false);
        zonePlayers.Clear();
        base.OnDisable();
    }
    // เวลาที่จะส่ง RPC ชะลอให้ยานแต่ละลำได้อีกครั้ง (key = ViewID ของยาน)
    private readonly Dictionary<int, float> nextSlowRefreshTimes = new Dictionary<int, float>();

    // Visuals (to be set in Editor script)
    // วงเตือนที่กระพริบก่อน Hazard ทำงาน
    public SpriteRenderer warningArea;
    // ภาพของผลจริง (สายฟ้า/บึง/หิน)
    public SpriteRenderer effectVisual;
    // Collider ที่ใช้ตรวจการชน (เปิดเฉพาะช่วงเกิดผล)
    public Collider2D hitCollider;
    // Material ของหางไฟอุกกาบาต ใช้ร่วมกันทุกลูก (static) สร้างครั้งเดียว
    private static Material meteorTrailMaterial;
    // true = อุกกาบาตลาวาชนยานไปแล้ว กันไม่ให้ทำดาเมจซ้ำ
    private bool meteorHit;

    // ตั้งภาพอุกกาบาตลาวา: ใช้ Sprite "Obs_Asteroids_5" ปรับขนาดให้กว้างราว 1.1 หน่วย
    // แล้วเพิ่ม TrailRenderer 2 เส้น (หางไฟสีส้ม + แกนร้อนสีเหลือง) ต่อท้ายลูกอุกกาบาต
    private void SetupMeteorVisual()
    {
        var sprites = SkillSheetVisual.Load("Obs_Asteroids");
        var rock = System.Array.Find(sprites, sprite => sprite.name == "Obs_Asteroids_5");
        if (effectVisual != null && rock != null)
        {
            effectVisual.sprite = rock;
            effectVisual.color = Color.white;
            effectVisual.sortingOrder = 8;
            float size = Mathf.Max(rock.bounds.size.x, rock.bounds.size.y);
            effectVisual.transform.localScale = Vector3.one * (1.1f / Mathf.Max(.01f, size));
        }
        if (meteorTrailMaterial == null)
            meteorTrailMaterial = new Material(Shader.Find("Sprites/Default"));
        // สร้างหาง 2 ชั้น: i = 0 หางไฟยาว/กว้าง, i = 1 แกนร้อนสั้น/แคบ อยู่ชั้นบนกว่า
        for (int i = 0; i < 2; i++)
        {
            var tail = new GameObject(i == 0 ? "Meteor_FireTail" : "Meteor_HotCore");
            tail.transform.SetParent(transform, false);
            var trail = tail.AddComponent<TrailRenderer>();
            trail.sharedMaterial = meteorTrailMaterial;
            trail.time = i == 0 ? .65f : .3f;
            trail.startWidth = i == 0 ? 1.7f : .7f;
            trail.endWidth = 0;
            trail.minVertexDistance = .12f;
            trail.numCapVertices = 4;
            trail.sortingOrder = 6 + i;
            // ไล่สีหางจากส้ม/เหลือง ไปแดงเข้ม และค่อย ๆ จางหายที่ปลาย
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(i == 0 ? new Color(1,.35f,.03f) : new Color(1,.95f,.55f), 0),
                new GradientColorKey(new Color(.7f,.08f,.015f), 1)
            }, new[] { new GradientAlphaKey(.85f, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient;
        }
    }

    // RPC ที่ทุกเครื่องได้รับ เมื่ออุกกาบาตลาวาชนยาน (Master เป็นคนส่ง จากใน OnTriggerEnter2D)
    // เล่นแอนิเมชันระเบิด และถ้าจุดระเบิดอยู่ในจอของเครื่องนี้ ให้เล่นเสียงระเบิดและเขย่ากล้อง
    [PunRPC]
    private void MeteorImpactRPC(Vector3 position, PhotonMessageInfo info)
    {
        // กันคนโกง: รับเฉพาะ RPC ที่ส่งมาจาก Master Client
        if (info.Sender != PhotonNetwork.MasterClient) return;
        var sheet = SkillSheetVisual.Load("Obs_Asteroids");
        var frames = new List<Sprite>();
        foreach (int index in new[] { 9, 10, 11 })
        {
            var frame = System.Array.Find(sheet, sprite => sprite.name == "Obs_Asteroids_" + index);
            if (frame != null) frames.Add(frame);
        }
        SkillSheetVisual.Create(frames.ToArray(), null, position, 4.5f, .55f);
        // เช็กว่าจุดระเบิดอยู่ในมุมกล้อง (viewport 0..1) ก่อนเล่นเสียง/เขย่าจอ
        var camera = Camera.main;
        if (camera != null)
        {
            Vector3 viewport = camera.WorldToViewportPoint(position);
            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Meteor"); // ปิด NewSounds = เสียงระเบิดแบบเดิม
                if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(.15f, .12f);
            }
        }
    }

    // Photon เรียกบนทุกเครื่องทันทีที่ Hazard ถูกสร้างผ่านเครือข่าย
    // ตั้งค่าเวลาเตือน/อายุ/ภาพตามชนิด แล้วเริ่ม HazardRoutine
    // InstantiationData (ถ้ามี): [0] = bool อยู่ถาวรไหม (SlowZone), [1] = int variant ของภาพบึง
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // 0) เช็กว่าเป็น Hazard ถาวรหรือไม่
        permanent = type == HazardType.EnergyCore || (type == HazardType.SlowZone
            && photonView.InstantiationData != null && photonView.InstantiationData.Length > 0
            && photonView.InstantiationData[0] is bool persistent && persistent);
        // Setup based on type
        // 1) สายฟ้า: เตือน 0.8 วิ แล้วมีผล 1 วิ (อายุรวม 1.0 วิ) ใช้ภาพ VFX_JellyLightning สูงราว 12 หน่วย
        if (type == HazardType.Lightning)
        {
            var bolts = Resources.LoadAll<Sprite>("Images/VFX_JellyLightning");
            if (bolts.Length > 0 && effectVisual != null)
            {
                effectVisual.sprite = bolts[0];
                effectVisual.color = Color.white;
                effectVisual.sortingOrder = 15;
                float scale = 12f / Mathf.Max(.01f, bolts[0].bounds.size.y);
                effectVisual.transform.localScale = Vector3.one * scale;
                // The lower-right end strikes the warned location; the rest extends upward-left.
                effectVisual.transform.localPosition = new Vector3(-bolts[0].bounds.max.x*scale,
                    -bolts[0].bounds.min.y*scale, 0);
            }
            warningTime = 0.8f; // PHASE 7: เพิ่มเวลาเตือนจาก 0.5 เป็น 0.8 เพื่อให้หลบได้ง่ายขึ้น
            lifetime = 1.0f;
        }
        // 2) บึงชะลอ: ค่าเริ่ม เตือน 0.5 วิ อยู่ 8 วิ; ถ้ามี variant (บึงจากแม็พปริซึม) จะเตือน 1 วิ อยู่ 24 วิ
        else if (type == HazardType.SlowZone)
        {
            warningTime = 0.5f; // Quickly appears
            lifetime = 8.0f; // Stays for a long time
            var data = photonView.InstantiationData;
            if (data != null && data.Length > 1 && data[1] is int variant)
            {
                warningTime = 1f;
                // เลือกภาพบึงตาม variant (85..87 -> index 0..2) แล้วปรับให้กว้าง 3.4 หน่วยในโลกเสมอ
                var swamp = PolishSprites.Swamp(variant-85);
                if (swamp != null && effectVisual != null)
                {
                    effectVisual.sprite = swamp;
                    effectVisual.color = Color.white;
                    effectVisual.sortingOrder = -2;
                    const float targetWorldWidth = 3.4f;
                    // Hazard prefabs may already be scaled. Compensate for their
                    // parent so the pool stays the same small size in every map.
                    float parentScale = effectVisual.transform.parent != null
                        ? Mathf.Abs(effectVisual.transform.parent.lossyScale.x) : 1f;
                    float scale = targetWorldWidth /
                        Mathf.Max(.01f, swamp.bounds.size.x * Mathf.Max(.01f, parentScale));
                    effectVisual.transform.localScale = Vector3.one * scale;
                    effectVisual.transform.localPosition = Vector3.zero;
                    // Trigger follows the visible pool rather than the original placeholder size.
                    if (hitCollider != null) hitCollider.enabled = false;
                    var area = effectVisual.gameObject.AddComponent<PolygonCollider2D>();
                    // Pool surface only: airborne droplets must not apply slow or poison.
                    // สร้างขอบ Collider เป็นวงรี 24 จุด ครอบเฉพาะผิวบึงด้านล่างของภาพ
                    var points = new Vector2[24];
                    var bounds = swamp.bounds;
                    for (int i=0;i<points.Length;i++)
                    {
                        float angle = i*Mathf.PI*2/points.Length;
                        points[i] = new Vector2(bounds.center.x+Mathf.Cos(angle)*bounds.size.x*.43f,
                            bounds.min.y+bounds.size.y*.29f+Mathf.Sin(angle)*bounds.size.y*.20f);
                    }
                    area.SetPath(0,points);
                    area.isTrigger = true;
                    hitCollider = area;
                    // ใช้ Rigidbody2D แบบ Kinematic ให้ trigger ทำงานได้โดยไม่โดนฟิสิกส์ผลัก
                    var body = GetComponent<Rigidbody2D>();
                    if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.gravityScale = 0;
                }
                lifetime = 24f;
            }
        }
        // 3) อุกกาบาตธรรมดา: เตือนนาน 2 วิ อายุรวม 2.5 วิ
        else if (type == HazardType.Meteor)
        {
            warningTime = 2.0f; // Long warning
            lifetime = 2.5f;
        }
        // 4) อุกกาบาตลาวา: ไม่มีช่วงเตือน ร่วงลงมาข้ามจอ อายุ 10 วิ
        else if (type == HazardType.MoltenAsteroid)
        {
            warningTime = 0f;
            SetupMeteorVisual();
            lifetime = 10.0f; // Crosses the screen
        }
        // 5) แกนพลังงาน: ไม่มีช่วงเตือน อยู่ตลอดแมตช์
        else if (type == HazardType.EnergyCore)
        {
            warningTime = 0f;
            lifetime = 9999f; // Stays forever
        }

        StartCoroutine(HazardRoutine());
    }

    // Coroutine วงจรชีวิตของ Hazard (รันทุกเครื่อง): เตือน -> เปิดผล -> รอจนหมดอายุ -> ทำลาย
    // การทำลายผ่านเครือข่าย (PhotonNetwork.Destroy) ทำเฉพาะเครื่องเจ้าของ (Master)
    private IEnumerator HazardRoutine()
    {
        // 1. Warning Phase
        if (warningTime > 0)
        {
            if (warningArea != null) warningArea.enabled = true;
            if (effectVisual != null) effectVisual.enabled = false;
            if (hitCollider != null) hitCollider.enabled = false;

            // Animate warning (blink)
            // กระพริบวงเตือน (บึง = สีเขียว, อื่น ๆ = สีแดง) จนครบ warningTime
            float elapsed = 0;
            while (elapsed < warningTime)
            {
                if (warningArea != null)
                    warningArea.color = type == HazardType.SlowZone
                        ? new Color(.45f,1,.08f,Mathf.PingPong(Time.time*3,.5f)+.1f)
                        : new Color(1, 0, 0, Mathf.PingPong(Time.time * 3f, 0.5f) + 0.1f);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // 2. Effect Phase
        isEffectActive = true;
        if (warningArea != null) warningArea.enabled = false;
        if (effectVisual != null) effectVisual.enabled = true;
        if (hitCollider != null) hitCollider.enabled = true;

        // Visual FX & Screen Shake
        if (CameraShake.Instance != null)
        {
            if (type == HazardType.Meteor) CameraShake.Instance.TriggerShake(0.5f, 0.4f);
            else if (type == HazardType.Lightning) CameraShake.Instance.TriggerShake(0.2f, 0.2f);
        }

        if (type == HazardType.Meteor)
        {
            GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
            if (impactPrefab != null)
            {
                // Instantiate multiple impact effects around the center for a bigger explosion
                Instantiate(impactPrefab, transform.position, Quaternion.identity);
                Instantiate(impactPrefab, transform.position + new Vector3(0.5f, 0.5f, 0), Quaternion.identity);
                Instantiate(impactPrefab, transform.position + new Vector3(-0.5f, -0.5f, 0), Quaternion.identity);
            }
            // รูปชุดใหม่: ระเบิดใหญ่ตอนอุกกาบาตกระแทก (ไม่มีรูป = ข้าม)
            var blast = SkillSheetVisual.LoadGrid("VFX/VFX_BigExplosion");
            if (blast != null && blast.Length > 0) SkillSheetVisual.Create(blast, null, transform.position, 6f, .8f);
            // เปิดตัวชนค้างไว้สั้นๆ ให้ยานที่อยู่ในวงโดนจริง (เดิมทำลายทันทีในเฟรมเดียวกัน)
            yield return new WaitForSeconds(.3f);
        }

        // Hazard ถาวรจบ coroutine ตรงนี้ ไม่ทำลายตัวเอง
        if (permanent) yield break;
        // บึง: รอจนเหลือ 1.5 วิสุดท้าย แล้วค่อย ๆ จางหายใน 1.5 วิ; ชนิดอื่นรอจนหมดอายุเลย
        if (type == HazardType.SlowZone)
        {
            yield return new WaitForSeconds(Mathf.Max(0,lifetime-warningTime-1.5f));
            float fade = 0;
            while (fade < 1.5f)
            {
                fade += Time.deltaTime;
                if (effectVisual != null) effectVisual.color = new Color(1,1,1,Mathf.Clamp01(1-fade/1.5f));
                yield return null;
            }
        }
        else yield return new WaitForSeconds(lifetime - warningTime);

        // 3. Cleanup
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    // ทุกเฟรม (ทุกเครื่อง): สายฟ้ากระพริบแสง, อุกกาบาตลาวาหมุน 360 องศา/วินาที
    // ส่วนการเคลื่อนที่ลง (8 หน่วย/วินาที) ทำเฉพาะเครื่องเจ้าของ แล้ว PhotonTransformView ใน Prefab ซิงก์ตำแหน่งไปเครื่องอื่น
    void Update()
    {
        if (type == HazardType.Lightning && isEffectActive && effectVisual != null)
            effectVisual.color = new Color(1,1,1,.55f+.45f*Mathf.Abs(Mathf.Sin(Time.time*55)));
        if (type == HazardType.MoltenAsteroid && isEffectActive)
        {
            // PHASE 5: หมุนติ้วตลอดเวลา (ทำงานทั้งสองฝั่งเพื่อ Visual)
            transform.Rotate(0, 0, 360f * Time.deltaTime);

            if (photonView.IsMine)
            {
                // PHASE 5: เพิ่มสปีดร่วงจาก 4f เป็น 8f
                transform.Translate(Vector3.down * 8f * Time.deltaTime, Space.World); 
            }
        }
    }

    // Unity เรียกเมื่อมีวัตถุเข้า trigger: ถ้าเป็นโซน (บึง/แกน) ให้ UpdateZone จัดการแล้วจบ
    // ที่เหลือทำเฉพาะเครื่องเจ้าของ Hazard (Master): ส่ง RPC ให้ยานที่โดน (สายฟ้า = สตัน, อุกกาบาต = ดาเมจ BattleBalance.MeteorDamage)
    // หมายเหตุ: SlowZone/EnergyCore ถูก return ไปตั้งแต่ UpdateZone แล้ว จึงไม่มาถึงกิ่งของชนิดนั้นด้านล่าง
    void OnTriggerEnter2D(Collider2D hitInfo)
    {
        if (isEffectActive && UpdateZone(hitInfo, true)) return;
        if (!isEffectActive || !photonView.IsMine || meteorHit) return;

        PlayerController hitPlayer = hitInfo.GetComponent<PlayerController>();
        if (hitPlayer != null)
        {
            if (type == HazardType.Lightning)
            {
                hitPlayer.photonView.RPC("ApplyStunRPC", RpcTarget.All);
            }
            else if (type == HazardType.Meteor || type == HazardType.MoltenAsteroid)
            {
                if (type == HazardType.MoltenAsteroid) meteorHit = true;
                hitPlayer.photonView.RPC("TakeDamage", RpcTarget.All, BattleBalance.MeteorDamage, -1); // PHASE 7: ลดดาเมจจาก 50 เป็น 35 ให้แฟร์ขึ้น
                // อุกกาบาตลาวาชนแล้ว: สั่งทุกเครื่องเล่นเอฟเฟกต์ระเบิด แล้วทำลายตัวเองผ่านเครือข่าย
                if (type == HazardType.MoltenAsteroid && photonView.IsMine)
                {
                    photonView.RPC(nameof(MeteorImpactRPC), RpcTarget.All, transform.position);
                    PhotonNetwork.Destroy(gameObject);
                }
            }
            else if (type == HazardType.EnergyCore)
            {
                hitPlayer.photonView.RPC("SetEnergyOverloadRPC", RpcTarget.All, true);
            }
        }
    }

    // Unity เรียกทุกเฟรมฟิสิกส์ที่วัตถุยังอยู่ใน trigger: ถ้าเป็นโซนให้ UpdateZone จัดการแล้วจบ
    // โค้ดส่วน SlowZone ด้านล่าง (ส่ง ApplySlowRPC ทุก 0.25 วิ) จึงไม่ถูกถึงในทางปฏิบัติ เพราะ return ไปก่อน
    void OnTriggerStay2D(Collider2D hitInfo)
    {
        if (isEffectActive && UpdateZone(hitInfo, true)) return;
        if (!isEffectActive || !photonView.IsMine) return;

        PlayerController hitPlayer = hitInfo.GetComponent<PlayerController>();
        if (hitPlayer != null)
        {
            if (type == HazardType.SlowZone)
            {
                int playerViewId = hitPlayer.photonView.ViewID;
                if (nextSlowRefreshTimes.TryGetValue(playerViewId, out float nextRefreshTime) && Time.time < nextRefreshTime)
                {
                    return;
                }

                // Refresh the one-second slow periodically instead of sending an RPC every physics frame.
                nextSlowRefreshTimes[playerViewId] = Time.time + 0.25f;
                hitPlayer.photonView.RPC("ApplySlowRPC", RpcTarget.All);
            }
        }
    }

    // Unity เรียกเมื่อวัตถุออกจาก trigger: ถ้าเป็นโซนให้ UpdateZone แจ้งยานว่าออกแล้วจบ
    // UpdateZone คืน true เสมอสำหรับบึง/แกน จึงไม่ถึงส่วน RemoveSlowRPC / ปิด Overload ด้านล่างในทางปฏิบัติ
    void OnTriggerExit2D(Collider2D hitInfo)
    {
        if (UpdateZone(hitInfo, false)) return;
        if (!isEffectActive || !photonView.IsMine) return;

        PlayerController hitPlayer = hitInfo.GetComponent<PlayerController>();
        if (hitPlayer != null)
        {
            if (type == HazardType.SlowZone)
            {
                nextSlowRefreshTimes.Remove(hitPlayer.photonView.ViewID);
                hitPlayer.photonView.RPC("RemoveSlowRPC", RpcTarget.All);
            }
            else if (type == HazardType.EnergyCore)
            {
                hitPlayer.photonView.RPC("SetEnergyOverloadRPC", RpcTarget.All, false);
            }
        }
    }

}
