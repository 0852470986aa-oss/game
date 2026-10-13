// ภาพเฉพาะบอส Co-op: เปลี่ยนเฉพาะ Sprite ไม่แก้ Collider / Transform / ค่าพลัง
using UnityEngine;

public sealed class BossRaidAppearance : MonoBehaviour
{
    public const string ResourcePath = "Images/Bosses/dreadnought-v2";
    private Sprite generatedSprite;
    private SpriteRenderer body, reactor;
    private readonly SpriteRenderer[] engines = new SpriteRenderer[3];
    private PlayerController owner;
    private int previousPhase;
    private float previousHp, hitFlash;
    private bool deathPlayed;

    public bool Apply(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return false;
        var texture = Resources.Load<Texture2D>(ResourcePath);
        if (texture == null) return false; // ยังไม่มีภาพ/นำเข้าไม่สำเร็จ = ใช้ภาพเดิมต่อ
        // รักษาความสูงเดิมในพิกัดโลก ไม่ให้ภาพความละเอียดสูงทำให้ตัวยานโตผิดขนาด
        float height = Mathf.Max(.01f, renderer.sprite.bounds.size.y);
        generatedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(.5f, .5f), texture.height / height, 0, SpriteMeshType.FullRect);
        generatedSprite.name = "DreadnoughtRaidVisual";
        renderer.sprite = generatedSprite;
        body = renderer;
        owner = GetComponent<PlayerController>();
        // เอฟเฟกต์เป็นลูกของภาพ ไม่ขยับตัวชนหรือจุดยิง
        reactor = Glow("RaidReactor", renderer, new Vector2(0, -.004f), height * .12f);
        for (int i = 0; i < engines.Length; i++)
            engines[i] = Glow("RaidEngine" + i, renderer, new Vector2((i - 1) * .10925f, i == 1 ? -.4362f : -.3987f), height * .065f);
        previousHp = owner != null ? owner.currentHp : 0;
        return true;
    }

    private SpriteRenderer Glow(string name, SpriteRenderer target, Vector2 offset, float size)
    {
        var child = new GameObject(name);
        child.transform.SetParent(target.transform, false);
        child.transform.localPosition = offset * target.sprite.bounds.size.y;
        var glow = child.AddComponent<SpriteRenderer>();
        glow.sprite = UiFx.Glow;
        glow.sortingLayerID = target.sortingLayerID;
        glow.sortingOrder = target.sortingOrder + 1;
        child.transform.localScale = Vector3.one * size / Mathf.Max(.01f, glow.sprite.bounds.size.y);
        return glow;
    }

    private void LateUpdate()
    {
        if (body == null || reactor == null) return;
        int phase = owner != null ? BossRaidRules.Phase(owner.currentHp, owner.maxHp) : 1;
        if (owner != null)
        {
            if (owner.currentHp < previousHp) hitFlash = .22f;
            previousHp = owner.currentHp;
            hitFlash = Mathf.Max(0, hitFlash - Time.deltaTime);
            if (previousPhase > 0 && phase > previousPhase && !owner.isDead) Pulse(.9f);
            if (owner.isDead && !deathPlayed) { deathPlayed = true; Pulse(1.6f); }
        }
        previousPhase = phase;
        float pulse = .35f + .2f * Mathf.Sin(Time.time * (2 + phase));
        float alpha = body.enabled && (owner == null || (!owner.isDead && !owner.HiddenFromLocalViewer)) ? body.color.a : 0;
        reactor.color = new Color(1f, phase == 3 ? .06f : .35f, .03f, alpha * (pulse + hitFlash * 2));
        for (int i = 0; i < engines.Length; i++)
            if (engines[i] != null) engines[i].color = new Color(.1f, .85f, 1f, alpha * (.3f + .12f * Mathf.Sin(Time.time * 9 + i)));
    }

    private void OnDestroy()
    {
        if (generatedSprite != null) Destroy(generatedSprite);
    }

    private void Pulse(float scale)
    {
        var go = new GameObject("RaidPhasePulse");
        go.transform.position = reactor.transform.position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = UiFx.Ring;
        sr.sortingLayerID = body.sortingLayerID;
        sr.sortingOrder = body.sortingOrder + 2;
        go.AddComponent<StoryBossPulse>().Initialize(sr, new Color(1,.3f,.05f), body.bounds.size.y * scale);
    }
}
