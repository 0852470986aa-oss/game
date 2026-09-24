using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// Static arena rocks exist on each client; only the owning ship applies contact damage.
public class MoltenContactSurface : MonoBehaviour
{
    private readonly Dictionary<int, float> nextDamageTimes = new Dictionary<int, float>();
    private void OnCollisionEnter2D(Collision2D collision) => OnCollisionStay2D(collision);
    private void OnCollisionStay2D(Collision2D collision)
    {
        var player = collision.gameObject.GetComponentInParent<PlayerController>();
        if (player == null || !player.photonView.IsMine || player.isDead) return;
        if (nextDamageTimes.TryGetValue(player.GetInstanceID(), out float next) && Time.time < next) return;
        nextDamageTimes[player.GetInstanceID()] = Time.time + 1f;
        player.TakeDamage(BattleBalance.LavaDamage, -1);
    }
}

public class HazardController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    public enum HazardType { Lightning, SlowZone, Meteor, MoltenAsteroid, EnergyCore }
    public HazardType type;

    private float lifetime = 5f;
    private float warningTime = 1.5f; // Time before effect happens
    private bool isEffectActive = false;
    private bool permanent;
    private readonly HashSet<PlayerController> zonePlayers = new HashSet<PlayerController>();

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

    public override void OnDisable()
    {
        foreach (var player in zonePlayers)
            if (player != null) player.SetBattlefieldZone(GetInstanceID(), type == HazardType.EnergyCore, false);
        zonePlayers.Clear();
        base.OnDisable();
    }
    private readonly Dictionary<int, float> nextSlowRefreshTimes = new Dictionary<int, float>();

    // Visuals (to be set in Editor script)
    public SpriteRenderer warningArea;
    public SpriteRenderer effectVisual;
    public Collider2D hitCollider;
    private static Material meteorTrailMaterial;
    private bool meteorHit;

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
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(i == 0 ? new Color(1,.35f,.03f) : new Color(1,.95f,.55f), 0),
                new GradientColorKey(new Color(.7f,.08f,.015f), 1)
            }, new[] { new GradientAlphaKey(.85f, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient;
        }
    }

    [PunRPC]
    private void MeteorImpactRPC(Vector3 position, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient) return;
        var sheet = SkillSheetVisual.Load("Obs_Asteroids");
        var frames = new List<Sprite>();
        foreach (int index in new[] { 9, 10, 11 })
        {
            var frame = System.Array.Find(sheet, sprite => sprite.name == "Obs_Asteroids_" + index);
            if (frame != null) frames.Add(frame);
        }
        SkillSheetVisual.Create(frames.ToArray(), null, position, 4.5f, .55f);
        var camera = Camera.main;
        if (camera != null)
        {
            Vector3 viewport = camera.WorldToViewportPoint(position);
            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Explosion");
                if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(.15f, .12f);
            }
        }
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        permanent = type == HazardType.EnergyCore || (type == HazardType.SlowZone
            && photonView.InstantiationData != null && photonView.InstantiationData.Length > 0
            && photonView.InstantiationData[0] is bool persistent && persistent);
        // Setup based on type
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
        else if (type == HazardType.SlowZone)
        {
            warningTime = 0.5f; // Quickly appears
            lifetime = 8.0f; // Stays for a long time
            var data = photonView.InstantiationData;
            if (data != null && data.Length > 1 && data[1] is int variant)
            {
                warningTime = 1f;
                var swamp = PolishSprites.Swamp(variant-85);
                if (swamp != null && effectVisual != null)
                {
                    effectVisual.sprite = swamp;
                    effectVisual.color = Color.white;
                    effectVisual.sortingOrder = -2;
                    float scale = 4.5f / Mathf.Max(.01f, swamp.bounds.size.x);
                    effectVisual.transform.localScale = Vector3.one * scale;
                    effectVisual.transform.localPosition = -swamp.bounds.center * scale;
                    // Trigger follows the visible pool rather than the original placeholder size.
                    if (hitCollider != null) hitCollider.enabled = false;
                    var area = effectVisual.gameObject.AddComponent<PolygonCollider2D>();
                    // Pool surface only: airborne droplets must not apply slow or poison.
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
                    var body = GetComponent<Rigidbody2D>();
                    if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.gravityScale = 0;
                }
                lifetime = 24f;
            }
        }
        else if (type == HazardType.Meteor)
        {
            warningTime = 2.0f; // Long warning
            lifetime = 2.5f;
        }
        else if (type == HazardType.MoltenAsteroid)
        {
            warningTime = 0f;
            SetupMeteorVisual();
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
        }

        if (permanent) yield break;
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
