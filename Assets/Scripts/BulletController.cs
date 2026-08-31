using UnityEngine;
using Photon.Pun;

public class BulletController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    public float speed = 18f; // PHASE 6: ยิงเร็วขึ้นจาก 15 เป็น 18
    public float lifeTime = 3f;
    private float damage = 10f; // จะถูกตั้งค่าตอน instantiate
    private bool isDestroyed = false;

    void Awake()
    {
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

    void Start()
    {
        // ให้ทุกเครื่องกำหนดความเร็วเท่ากันเพื่อให้ภาพกระสุนไม่ค้างเฉพาะฝั่งที่ไม่ได้ยิง
        if (GetComponent<Rigidbody2D>() != null)
        {
            GetComponent<Rigidbody2D>().linearVelocity = transform.up * speed;
        }

        // ลบตัวเองถ้าอยู่นานเกินไป (เพื่อไม่ให้กินสเปคเครื่อง)
        if (photonView.IsMine)
        {
            Invoke("DestroyBullet", lifeTime);
        }
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // รับค่า damage ที่ส่งมาจากคนยิง
        object[] instantiationData = info.photonView.InstantiationData;
        if (instantiationData != null && instantiationData.Length > 0)
        {
            damage = (float)instantiationData[0];
        }
    }

    void OnTriggerEnter2D(Collider2D hitInfo)
    {
        if (!photonView.IsMine || isDestroyed) return; // เฉพาะคนยิงเท่านั้นที่จะเป็นคนคำนวณดาเมจ และถ้ากระสุนถูกทำลายไปแล้วจะไม่คิดซ้ำ

        PlayerController enemy = hitInfo.GetComponent<PlayerController>();
        if (enemy != null)
        {
            // ถ้าชนโดนผู้เล่นอื่น (ไม่ใช่ตัวเอง)
            if (enemy.photonView.OwnerActorNr != photonView.CreatorActorNr)
            {
                enemy.photonView.RPC("TakeDamage", RpcTarget.All, damage, photonView.CreatorActorNr);
                DestroyBullet();
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

    private void DestroyBullet()
    {
        if (photonView.IsMine && !isDestroyed)
        {
            isDestroyed = true;
            // ซ่อนภาพและปิด Collider ทันทีเพื่อให้ดูเหมือนถูกทำลายแล้ว
            if (GetComponent<SpriteRenderer>()) GetComponent<SpriteRenderer>().enabled = false;
            if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = false;
            
            // หน่วงเวลาทำลายจริง 0.1 วิ เพื่อให้ระบบ Network ส่งข้อมูลการสร้าง(Instantiate)ให้เสร็จก่อน
            StartCoroutine(NetworkDestroyRoutine());
        }
    }

    private System.Collections.IEnumerator NetworkDestroyRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        if (photonView != null && gameObject != null)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
