using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// Pure visual; keeps frame sizes proportional and ignores the gameplay object's nonuniform scale.
public sealed class SkillSheetVisual : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
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

    private Sprite[] frames;
    private Transform follow;
    private bool hadFollow, upright, bottom;
    private Vector3 anchor;
    private SpriteRenderer renderer2D;
    private SpriteRenderer previousRenderer;
    private float frameChangedAt;
    private float scale, duration, rotationOffset;
    private double started;
    private int frame = -1;
    public float breakTime = -1;
    public void Break() { if (breakTime >= 0) started = PhotonNetwork.Time - breakTime; }
    public void SetFrame(int index)
    {
        index = Mathf.Clamp(index, 0, frames.Length - 1);
        if (index == frame) return;
        previousRenderer.sprite = renderer2D.sprite;
        frameChangedAt = Time.time;
        frame = index;
        renderer2D.sprite = frames[frame];
    }
    private void LateUpdate()
    {
        if (hadFollow && follow == null) { Destroy(gameObject); return; }
        if (hadFollow && !follow.gameObject.activeInHierarchy)
        {
            renderer2D.enabled = previousRenderer.enabled = false;
            return;
        }
        renderer2D.enabled = true;
        float age = (float)(PhotonNetwork.Time - started);
        if (duration > 0)
        {
            if (age >= duration) { Destroy(gameObject); return; }
            int index = breakTime > 0
                ? (age >= breakTime ? frames.Length - 1 : Mathf.FloorToInt(age / breakTime * (frames.Length - 1)))
                : Mathf.FloorToInt(age / duration * frames.Length);
            SetFrame(index);
        }
        Quaternion rotation = !upright && follow != null ? follow.rotation * Quaternion.Euler(0, 0, rotationOffset) : Quaternion.identity;
        Bounds bounds = renderer2D.sprite.bounds;
        Vector3 pivot = bottom ? new Vector3(bounds.center.x, bounds.min.y, 0) : bounds.center;
        // Short crossfades bridge the sparse hand-cut frames without changing the gameplay clock.
        float blendTime = duration > 0 && breakTime < 0 ? Mathf.Min(.09f, duration / frames.Length * .5f) : .1f;
        float blend = previousRenderer.sprite == null ? 1 : Mathf.SmoothStep(0, 1, (Time.time - frameChangedAt) / Mathf.Max(.01f, blendTime));
        float opacity = Mathf.SmoothStep(0, 1, age / .065f);
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
        transform.position = (follow != null ? follow.position : anchor) - rotation * (pivot * animatedScale);
    }
}

public class SkillController : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    public enum SkillBehavior { StunWave, NovaBlast, SeekerMissile }
    public SkillBehavior behavior;
    public float damage = 10f;
    public float speed = 10f;
    public float lifeTime = 3f;
    public float skillParam2 = 0f;
    private bool isDestroyed, novaArmed, impactPlayed;
    private Transform target;
    private float seekerSpeedMultiplier = .5f;
    private double started;
    private int missileColor;
    private SkillSheetVisual visual;
    private Sprite[] frames;
    private readonly HashSet<int> damagedPlayerViewIds = new HashSet<int>();
    private const float NovaDelay = 1.5f;
    private const float NovaRadius = 3f;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        var data = photonView.InstantiationData;
        if (data != null && data.Length > 0 && data[0] is float value) damage = value;
        started = info.SentServerTime;
        if (photonView.Owner != null && photonView.Owner.CustomProperties.TryGetValue("ShipType", out object shipValue) && shipValue is int ship)
            missileColor = ship == 1 ? 1 : ship == 2 ? 0 : 2; // Ship1 purple, Ship2 blue, Ship3 orange.
        else missileColor = 2;

        if (behavior == SkillBehavior.NovaBlast) lifeTime = 2f;
        string sheet = behavior == SkillBehavior.NovaBlast ? "VFX_NovaBomb"
            : behavior == SkillBehavior.SeekerMissile ? "VFX_SeekerMissile" : "VFX_StunWave";
        frames = SkillSheetVisual.Load(sheet);
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

    void Update()
    {
        if (isDestroyed) return;
        float age = Mathf.Max(0, (float)(PhotonNetwork.Time - started));
        UpdateVisual(age);
        if (behavior == SkillBehavior.NovaBlast)
        {
            if (!novaArmed && age >= NovaDelay) DetonateNova();
        }
        if (!photonView.IsMine) return;
        if (age >= lifeTime) { DestroySkill(); return; }
        if (behavior == SkillBehavior.NovaBlast) return;
        Vector2 movementStart = transform.position;
        if (target != null && (target.GetComponent<PlayerController>() == null || target.GetComponent<PlayerController>().isDead))
            target = null;
        if (target != null)
        {
            Vector2 direction = target.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, angle), Time.deltaTime * 5f);
        }
        if (behavior == SkillBehavior.SeekerMissile)
            seekerSpeedMultiplier = Mathf.Min(seekerSpeedMultiplier + Time.deltaTime * 1.5f, 2.5f);
        float multiplier = behavior == SkillBehavior.SeekerMissile ? seekerSpeedMultiplier : 1f;
        Vector2 end = movementStart + (Vector2)transform.up * speed * multiplier * Time.deltaTime;
        if (ProjectileSweep.FirstHit(transform, photonView.CreatorActorNr, movementStart, end, out var hit))
        {
            transform.position = hit.centroid;
            OnTriggerEnter2D(hit.collider);
        }
        else transform.position = end;
    }

    private void DetonateNova()
    {
        novaArmed = true;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Explosion");
        if (visual == null)
        {
            var prefab = GameplayManager.GetPrefab("DeathExplosion");
            if (prefab != null) Instantiate(prefab, transform.position, Quaternion.identity);
        }
        if (!photonView.IsMine) return;
        foreach (var collider in Physics2D.OverlapCircleAll(transform.position, NovaRadius))
        {
            var player = collider.GetComponentInParent<PlayerController>();
            if (player != null && player.photonView.OwnerActorNr != photonView.CreatorActorNr) DealDamage(player);
        }
    }

    private void FindNearestTarget()
    {
        // Preserve Seeker's existing lock behavior; Stun only acquires within its original travel budget.
        float nearest = behavior == SkillBehavior.StunWave ? speed * lifeTime : float.PositiveInfinity;
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.isDead || player.photonView.OwnerActorNr == photonView.CreatorActorNr) continue;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < nearest) { nearest = distance; target = player.transform; }
        }
    }

    private void DealDamage(PlayerController player)
    {
        if (!player.isDead && damagedPlayerViewIds.Add(player.photonView.ViewID))
            player.photonView.RPC("TakeDamage", RpcTarget.All, damage, photonView.CreatorActorNr);
    }

    void OnCollisionEnter2D(Collision2D collision) => OnTriggerEnter2D(collision.collider);
    void OnTriggerEnter2D(Collider2D hit)
    {
        if (!photonView.IsMine || isDestroyed || behavior == SkillBehavior.NovaBlast) return;
        var player = hit.GetComponentInParent<PlayerController>();
        if (player != null && (player.isDead || player.photonView.OwnerActorNr == photonView.CreatorActorNr)) return;
        if (player == null && hit.isTrigger) return;
        // Lock before RPCs so trigger and swept collision cannot apply the same hit twice.
        DestroySkill();
        photonView.RPC(nameof(PlayImpactRPC), RpcTarget.All, transform.position);
        if (player == null) return;
        if (behavior == SkillBehavior.StunWave) player.photonView.RPC("ApplyStunRPC", RpcTarget.All);
        DealDamage(player);
    }

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
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Hit");
    }

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
    private IEnumerator NetworkDestroyRoutine()
    {
        yield return new WaitForSeconds(.1f);
        if (PhotonNetwork.InRoom && photonView.IsMine) PhotonNetwork.Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if (visual != null) Destroy(visual.gameObject);
    }
}
