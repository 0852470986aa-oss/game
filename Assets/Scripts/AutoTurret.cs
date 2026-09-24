using UnityEngine;
using Photon.Pun;

public class AutoTurret : MonoBehaviour
{
    public float detectionRadius = 24f;
    public float fireRate = BattleBalance.TurretShotInterval;
    public float damage = BattleBalance.TurretDamage;
    public string bulletPrefabName = "BulletPrefab";
    public Transform firePoint;
    private float nextFireTime;
    private float nextScan;
    private PlayerController target;

    void Update()
    {
        if (!PhotonNetwork.InRoom || GameplayManager.Instance == null || !GameplayManager.Instance.MatchInputAllowed) return;
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .2f;
            target = null;
            float nearest = detectionRadius;
            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                float distance = Vector2.Distance(transform.position, player.transform.position);
                if (!player.isDead && distance < nearest && ClearSight(player))
                { target = player; nearest = distance; }
            }
        }
        if (target == null || target.isDead) return;
        Vector2 direction = target.transform.position - transform.position;
        Quaternion aim = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, aim, 120 * Time.deltaTime);
        if (!PhotonNetwork.IsMasterClient || Time.time < nextFireTime || Quaternion.Angle(transform.rotation, aim) > 8 || !ClearSight(target)) return;
        nextFireTime = Time.time + Mathf.Max(.3f, fireRate);
        Vector3 muzzle = transform.position + transform.up * MuzzleDistance();
        if (firePoint != null) muzzle = firePoint.position;
        PhotonNetwork.InstantiateRoomObject(bulletPrefabName, muzzle, transform.rotation, 0, new object[] { damage, true });
    }

    float MuzzleDistance()
    {
        var shape = GetComponent<Collider2D>();
        return shape != null ? shape.bounds.extents.magnitude + .5f : 2.5f;
    }

    bool ClearSight(PlayerController player)
    {
        Vector2 delta = player.transform.position - transform.position;
        foreach (var hit in Physics2D.RaycastAll(transform.position, delta.normalized, delta.magnitude))
        {
            if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (hit.collider.GetComponentInParent<BulletController>() != null || hit.collider.GetComponentInParent<SkillController>() != null) continue;
            return false;
        }
        return delta.magnitude > MuzzleDistance();
    }
}

