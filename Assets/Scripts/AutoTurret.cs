using UnityEngine;
using Photon.Pun;

public class AutoTurret : MonoBehaviour
{
    public float detectionRadius = 15f;
    public float fireRate = 1.5f;
    public float damage = 10f;
    public string bulletPrefabName = "BulletPrefab";
    public Transform firePoint;

    private float nextFireTime = 0f;
    private Transform target;

    void Update()
    {
        // ??????????????????????????? ?????????????????????????????
        FindClosestTarget();
        
        if (target != null)
        {
            // ???????????????????????
            Vector2 direction = target.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
        else
        {
            // ???????????????? ????????????????
            transform.Rotate(0, 0, Time.deltaTime * -20f);
        }

        // ????? MasterClient ????????????????? ??????????????????????
        if (PhotonNetwork.IsMasterClient)
        {
            if (target != null && Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + fireRate;
            }
        }
    }

    void FindClosestTarget()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float closestDist = Mathf.Infinity;
        Transform closestPlayer = null;

        foreach (GameObject player in players)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);
            if (dist < detectionRadius)
            {
                // ???????????????????????
                Vector2 dir = player.transform.position - transform.position;
                RaycastHit2D hit = Physics2D.Raycast(transform.position, dir.normalized, dist, LayerMask.GetMask("Obstacle"));
                
                if (hit.collider == null) // ????????????
                {
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestPlayer = player.transform;
                    }
                }
            }
        }
        target = closestPlayer;
    }

    void Shoot()
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        object[] customInitData = new object[1];
        customInitData[0] = damage;
        
        PhotonNetwork.Instantiate(bulletPrefabName, spawnPos, transform.rotation, 0, customInitData);
    }
}

