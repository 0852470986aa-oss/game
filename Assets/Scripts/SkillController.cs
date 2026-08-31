using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

public class SkillController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    public enum SkillBehavior { StunWave, NovaBlast, SeekerMissile }
    public SkillBehavior behavior;

    public float damage = 10f;
    public float speed = 10f;
    public float lifeTime = 3f;
    public float skillParam2 = 0f;
    private PlayerController creator;
    private bool isDestroyed = false;

    private Transform target;
    private bool novaArmed;
    private GameObject warningVisual; // สำหรับ Nova
    private float seekerSpeedMultiplier = 0.5f; // สำหรับ Seeker Accel
    private readonly HashSet<int> damagedPlayerViewIds = new HashSet<int>();

    void Awake()
    {
        // PHASE 4: เพิ่มหางควัน (Trail) ให้จรวดติดตาม
        if (behavior == SkillBehavior.SeekerMissile && GetComponent<TrailRenderer>() == null)
        {
            TrailRenderer tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.5f;
            tr.startWidth = 0.4f;
            tr.endWidth = 0f;
            tr.material = new Material(Shader.Find("Sprites/Default"));
            
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.4f, 0f), 0.0f), new GradientColorKey(Color.gray, 1.0f) }, // ส้มไปเทา
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            tr.colorGradient = gradient;
        }
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // รับค่าดาเมจตอน Instantiate
        object[] initData = info.photonView.InstantiationData;
        if (initData != null && initData.Length > 0)
        {
            damage = (float)initData[0];
        }

        // หาเป้าหมายที่ใกล้ที่สุดสำหรับ Seeker
        if (behavior == SkillBehavior.SeekerMissile)
        {
            FindNearestTarget();
        }

        // สำหรับ Nova ให้ทำลายตัวเองหลังจากเอฟเฟกต์จบ
        if (behavior == SkillBehavior.NovaBlast)
        {
            lifeTime = 2f;
            StartCoroutine(ArmNovaBlast());
            
            // สร้าง Warning Visual ขึ้นมาชั่วคราว
            warningVisual = new GameObject("NovaWarning");
            warningVisual.transform.SetParent(transform);
            warningVisual.transform.localPosition = Vector3.zero;
            SpriteRenderer sr = warningVisual.AddComponent<SpriteRenderer>();
            
            // เลี่ยงการใช้ UnityEditor ใน Runtime โดยการดึง Sprite มาจากตัวหลักเอง
            SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
            if (mainSr != null) sr.sprite = mainSr.sprite;
            
            sr.color = new Color(1f, 0f, 0f, 0.3f);
            warningVisual.transform.localScale = new Vector3(6f, 6f, 1f); // รัศมี 3f (กว้างรวม 6f)
        }

        if (photonView.IsMine)
        {
            Invoke("DestroySkill", lifeTime);
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (behavior == SkillBehavior.StunWave)
        {
            // เคลื่อนที่ไปข้างหน้าตรงๆ
            transform.Translate(Vector3.up * speed * Time.deltaTime, Space.Self);
        }
        else if (behavior == SkillBehavior.SeekerMissile)
        {
            // พุ่งหาเป้าหมาย พร้อม Acceleration
            seekerSpeedMultiplier = Mathf.Min(seekerSpeedMultiplier + Time.deltaTime * 1.5f, 2.5f); // ค่อยๆ เร็วขึ้น สูงสุด 2.5 เท่า
            
            if (target != null)
            {
                Vector2 direction = (target.position - transform.position).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0, 0, angle - 90f)), Time.deltaTime * 5f); // Smooth Lock-on
                transform.Translate(Vector3.up * speed * seekerSpeedMultiplier * Time.deltaTime, Space.Self);
            }
            else
            {
                // ถ้าเป้าหมายตายแล้วหรือไม่มีเป้าหมาย ให้พุ่งตรงๆ แทน
                transform.Translate(Vector3.up * speed * seekerSpeedMultiplier * Time.deltaTime, Space.Self);
            }
        }
        // Nova Blast ไม่ต้องขยับ เพราะจะขยายตัวหรือคงที่
        if (behavior == SkillBehavior.NovaBlast && warningVisual != null && !novaArmed)
        {
            // ให้วงกลมคำเตือนกระพริบ
            warningVisual.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0f, Mathf.PingPong(Time.time * 4f, 0.4f) + 0.1f);
        }
    }

    private IEnumerator ArmNovaBlast()
    {
        yield return new WaitForSeconds(1.5f);
        novaArmed = true;

        // ซ่อน Warning
        if (warningVisual != null) warningVisual.SetActive(false);

        // ระเบิดวงกว้าง
        GameObject impactPrefab = GameplayManager.GetPrefab("DeathExplosion");
        if (impactPrefab != null)
        {
            Instantiate(impactPrefab, transform.position, Quaternion.identity);
            Instantiate(impactPrefab, transform.position + new Vector3(1,1,0), Quaternion.identity);
            Instantiate(impactPrefab, transform.position + new Vector3(-1,-1,0), Quaternion.identity);
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Explosion");
        if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(0.5f, 0.8f); // สั่นแรงมาก

        if (!photonView.IsMine) yield break;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 3f);
        foreach (Collider2D hitCollider in colliders)
        {
            PlayerController hitPlayer = hitCollider.GetComponent<PlayerController>();
            if (hitPlayer != null && !hitPlayer.photonView.IsMine)
            {
                DealDamage(hitPlayer);
            }
        }
    }

    private void DealDamage(PlayerController hitPlayer)
    {
        if (damagedPlayerViewIds.Add(hitPlayer.photonView.ViewID))
        {
            hitPlayer.photonView.RPC("TakeDamage", RpcTarget.All, damage, photonView.CreatorActorNr);
        }
    }

    private void FindNearestTarget()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        float minDistance = Mathf.Infinity;

        foreach (PlayerController p in players)
        {
            if (!p.photonView.IsMine) // หาศัตรู
            {
                float dist = Vector2.Distance(transform.position, p.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    target = p.transform;
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D hitInfo)
    {
        if (!photonView.IsMine || isDestroyed) return;

        PlayerController hitPlayer = hitInfo.GetComponent<PlayerController>();
        if (behavior == SkillBehavior.NovaBlast)
        {
            if (novaArmed && hitPlayer != null && !hitPlayer.photonView.IsMine)
            {
                DealDamage(hitPlayer);
            }
            return;
        }

        if (hitPlayer != null && !hitPlayer.photonView.IsMine)
        {
            if (behavior == SkillBehavior.StunWave)
            {
                hitPlayer.photonView.RPC("ApplyStunRPC", RpcTarget.All);
                // เพิ่มดาเมจเล็กน้อย (10) เพื่อให้ Stun ไม่รู้สึกเสียเปรียบเกินไป
                hitPlayer.photonView.RPC("TakeDamage", RpcTarget.All, damage, photonView.CreatorActorNr);
                
                // Stun Effect / Sound
                GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
                if (impactPrefab != null)
                {
                    GameObject fx = Instantiate(impactPrefab, hitPlayer.transform.position, Quaternion.identity);
                    var sr = fx.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(1f, 1f, 0f, 1f); // สีเหลือง
                }
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Hit");

                DestroySkill();
            }
            else if (behavior == SkillBehavior.SeekerMissile)
            {
                hitPlayer.photonView.RPC("TakeDamage", RpcTarget.All, damage, photonView.CreatorActorNr);
                
                // Seeker Impact
                GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
                if (impactPrefab != null) Instantiate(impactPrefab, transform.position, Quaternion.identity);

                DestroySkill();
            }
        }
        // สำหรับ NovaBlast จะตรวจจับศัตรูในระยะ (ด้วย CircleCollider2D ที่ขยายตัว)
        else if (behavior == SkillBehavior.NovaBlast)
        {
            if (hitPlayer != null && !hitPlayer.photonView.IsMine)
            {
                hitPlayer.photonView.RPC("TakeDamage", RpcTarget.All, damage, photonView.CreatorActorNr);
                // ไม่ทำลายกระสุน ปล่อยให้มันระเบิดโดนทุกคนในรัศมี
            }
        }
        else if (!hitInfo.isTrigger)
        {
            // PHASE 6: ถ้าเป็น StunWave หรือ SeekerMissile วิ่งไปชนเสาหิน ให้ระเบิดทิ้ง
            if (behavior == SkillBehavior.StunWave || behavior == SkillBehavior.SeekerMissile)
            {
                GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
                if (impactPrefab != null) Instantiate(impactPrefab, transform.position, Quaternion.identity);
                DestroySkill();
            }
        }
    }

    private void DestroySkill()
    {
        if (photonView.IsMine && !isDestroyed)
        {
            isDestroyed = true;
            // ซ่อนภาพและปิด Collider ทันที
            if (GetComponent<SpriteRenderer>()) GetComponent<SpriteRenderer>().enabled = false;
            if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = false;
            
            StartCoroutine(NetworkDestroyRoutine());
        }
    }

    private IEnumerator NetworkDestroyRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        if (photonView != null && gameObject != null)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

}
