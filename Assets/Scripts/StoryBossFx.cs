using UnityEngine;

// Cosmetic reactor/charging rings. No new damage areas or changes to boss AI.
public sealed class StoryBossFx : MonoBehaviour
{
    private PlayerController owner;
    private SpriteRenderer body, core, ring;
    private float height, previousHp;
    private float hitFlash;
    private int phase;
    private bool deathPlayed;
    private Color tint;
    private void Start()
    {
        owner = GetComponent<PlayerController>();
        body = GetComponent<SpriteRenderer>();
        if (owner == null || body == null || body.sprite == null) { enabled = false; return; }
        height = body.sprite.bounds.size.y;
        tint = body.sprite.name.EndsWith("5") ? new Color(.8f, .2f, 1) : new Color(1, .35f, .08f);
        core = Create("StoryReactor", UiFx.Glow, .24f);
        ring = Create("StoryChargeRing", UiFx.Ring, .64f);
        // Reactor center measured in atlas pixels; respects the alpha-trimmed sprite rect.
        Vector2 pixel = body.sprite.name.EndsWith("5") ? new Vector2(1271,684)
            : body.sprite.name.EndsWith("4") ? new Vector2(768,639) : new Vector2(264,655);
        Rect rect = body.sprite.rect;
        Vector2 anchor = new Vector2((pixel.x * body.sprite.texture.width / 1536f - rect.x) / rect.width,
            ((1024 - pixel.y) * body.sprite.texture.height / 1024f - rect.y) / rect.height);
        Vector3 center = body.sprite.bounds.min + Vector3.Scale(body.sprite.bounds.size, new Vector3(anchor.x,anchor.y,0));
        core.transform.localPosition = ring.transform.localPosition = center;
        previousHp = owner.currentHp;
    }
    private SpriteRenderer Create(string name, Sprite sprite, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * height * scale / sprite.bounds.size.y;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = body.sortingLayerID;
        sr.sortingOrder = body.sortingOrder + 1;
        return sr;
    }
    private void LateUpdate()
    {
        if (owner == null || core == null) return;
        float alpha = body.enabled && !owner.isDead && !owner.HiddenFromLocalViewer ? body.color.a : 0;
        int next = BossRaidRules.Phase(owner.currentHp, owner.maxHp);
        if (phase > 0 && next > phase && !owner.isDead) Burst(.85f);
        phase = next;
        if (owner.isDead && !deathPlayed) { deathPlayed = true; Burst(1.3f); }
        if (owner.currentHp < previousHp) hitFlash = .2f;
        hitFlash = Mathf.Max(0,hitFlash-Time.deltaTime);
        float hit = .35f + hitFlash * 2.5f;
        previousHp = owner.currentHp;
        core.color = new Color(tint.r, tint.g, tint.b, alpha * (hit + .15f * Mathf.Sin(Time.time * (3 + phase))));
        ring.color = new Color(tint.r, tint.g, tint.b, alpha * (.07f + phase * .045f));
        ring.transform.localRotation = Quaternion.Euler(0, 0, Time.time * 18f * phase);
        ring.transform.localScale = Vector3.one * height * (.6f + .04f * Mathf.Sin(Time.time * 2)) / ring.sprite.bounds.size.y;
    }
    private void Burst(float scale)
    {
        var go = new GameObject("StoryBossPulse");
        go.transform.position = core != null ? core.transform.position : transform.position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = UiFx.Ring;
        sr.sortingOrder = body.sortingOrder + 1;
        sr.sortingLayerID = body.sortingLayerID;
        go.AddComponent<StoryBossPulse>().Initialize(sr, tint, height * transform.lossyScale.y * scale);
    }
}

public sealed class StoryBossPulse : MonoBehaviour
{
    private SpriteRenderer view;
    private Color tint;
    private float size, age;
    public void Initialize(SpriteRenderer renderer, Color color, float diameter)
    { view = renderer; tint = color; size = diameter; Destroy(gameObject, .65f); }
    private void Update()
    {
        if (view == null) return;
        age += Time.deltaTime;
        transform.localScale = Vector3.one * size * Mathf.Lerp(.25f, 1.25f, age / .65f) / view.sprite.bounds.size.y;
        tint.a = Mathf.Clamp01(1 - age / .65f) * .6f;
        view.color = tint;
    }
}
