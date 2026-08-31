using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

public class HazardController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    public enum HazardType { Lightning, SlowZone, Meteor, MoltenAsteroid, EnergyCore }
    public HazardType type;

    private float lifetime = 5f;
    private float warningTime = 1.5f; // Time before effect happens
    private bool isEffectActive = false;
    private readonly Dictionary<int, float> nextSlowRefreshTimes = new Dictionary<int, float>();

    // Visuals (to be set in Editor script)
    public SpriteRenderer warningArea;
    public SpriteRenderer effectVisual;
    public Collider2D hitCollider;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // Setup based on type
        if (type == HazardType.Lightning)
        {
            warningTime = 0.8f; // PHASE 7: เพิ่มเวลาเตือนจาก 0.5 เป็น 0.8 เพื่อให้หลบได้ง่ายขึ้น
            lifetime = 1.0f;
        }
        else if (type == HazardType.SlowZone)
        {
            warningTime = 0.5f; // Quickly appears
            lifetime = 8.0f; // Stays for a long time
        }
        else if (type == HazardType.Meteor)
        {
            warningTime = 2.0f; // Long warning
            lifetime = 2.5f;
        }
        else if (type == HazardType.MoltenAsteroid)
        {
            warningTime = 0f;
            lifetime = 10.0f; // Crosses the screen
        }
        else if (type == HazardType.EnergyCore)
        {
            warningTime = 0f;
            lifetime = 9999f; // Stays forever
        }

        StartCoroutine(HazardRoutine());
    }

    private IEnumerator HazardRoutine()
    {
        // 1. Warning Phase
        if (warningTime > 0)
        {
            if (warningArea != null) warningArea.enabled = true;
            if (effectVisual != null) effectVisual.enabled = false;
            if (hitCollider != null) hitCollider.enabled = false;

            // Animate warning (blink)
            float elapsed = 0;
            while (elapsed < warningTime)
            {
                if (warningArea != null)
                    warningArea.color = new Color(1, 0, 0, Mathf.PingPong(Time.time * 3f, 0.5f) + 0.1f);
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
        }

        yield return new WaitForSeconds(lifetime - warningTime);

        // 3. Cleanup
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    void Update()
    {
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

    void OnTriggerEnter2D(Collider2D hitInfo)
    {
        if (!isEffectActive || !photonView.IsMine) return;

        PlayerController hitPlayer = hitInfo.GetComponent<PlayerController>();
        if (hitPlayer != null)
        {
            if (type == HazardType.Lightning)
            {
                hitPlayer.photonView.RPC("ApplyStunRPC", RpcTarget.All);
            }
            else if (type == HazardType.Meteor || type == HazardType.MoltenAsteroid)
            {
                hitPlayer.photonView.RPC("TakeDamage", RpcTarget.All, 35f, -1); // PHASE 7: ลดดาเมจจาก 50 เป็น 35 ให้แฟร์ขึ้น
                if (type == HazardType.MoltenAsteroid && photonView.IsMine)
                {
                    GameObject impactPrefab = GameplayManager.GetPrefab("ImpactEffect");
                    if (impactPrefab != null) Instantiate(impactPrefab, transform.position, Quaternion.identity);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Explosion");
                    PhotonNetwork.Destroy(gameObject);
                }
            }
            else if (type == HazardType.EnergyCore)
            {
                hitPlayer.photonView.RPC("SetEnergyOverloadRPC", RpcTarget.All, true);
            }
        }
    }

    void OnTriggerStay2D(Collider2D hitInfo)
    {
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

    void OnTriggerExit2D(Collider2D hitInfo)
    {
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
