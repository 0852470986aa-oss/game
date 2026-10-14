using UnityEngine;

// Visual-only: never changes bullet movement, collider, damage, or network messages.
public sealed class SkinProjectileFx : MonoBehaviour
{
    public static readonly string[] Names = { "ION FORK", "EMBER CORE", "TOXIC ORBIT", "ROSE SHARD", "FROST NEEDLE", "NEBULA HELIX", "SOLAR HALO", "ABYSS RIFT", "JADE FANGS", "PRISM SPECTRUM", "VOID ECLIPSE", "GOLDEN COMET", "ACE PULSE", "SEASON CROSS", "SOVEREIGN CROWN" };
    private SpriteRenderer body;
    private readonly SpriteRenderer[] motes = new SpriteRenderer[3];
    private Color primary, secondary;
    private int style;
    private float born;
    private bool impactPlayed;
    private static Material trailMaterial;
    public static Material TrailMaterial
    {
        get
        {
            if (trailMaterial == null) trailMaterial = new Material(Shader.Find("Sprites/Default"));
            return trailMaterial;
        }
    }

    public static void Apply(GameObject bullet, SkinDef skin)
    {
        if (skin == null || bullet.GetComponent<SkinProjectileFx>() != null) return;
        int index = SkinCatalog.IndexOf(skin.id);
        if (index < 0 || index >= Names.Length) return;
        bullet.AddComponent<SkinProjectileFx>().Setup(index, skin.aura);
    }

    public static void ApplyCombatant(GameObject bullet, PlayerController shooter)
    {
        var hull = shooter.GetComponent<SpriteRenderer>();
        string name = hull != null && hull.sprite != null ? hull.sprite.name : "";
        if (name == "DreadnoughtRaidVisual" || name.StartsWith("StoryFleet_"))
        {
            string id = name.EndsWith("2") || name.EndsWith("5") ? "NEBULA" : name.EndsWith("1") ? "TOXIC" : "MAGMA";
            Apply(bullet, SkinCatalog.Find(id));
        }
        else Apply(bullet, shooter.ProjectileSkin);
    }

    // Called only on collision, never when a projectile expires or the scene is unloaded.
    public void Impact()
    {
        if (impactPlayed) return;
        impactPlayed = true;
        PrismFx.Burst(transform.position, primary, .55f + (style % 3) * .12f);
    }

    private void Setup(int index, Color tint)
    {
        style = index;
        born = Time.time;
        primary = tint;
        secondary = Color.Lerp(tint, Color.white, .65f);
        body = GetComponent<SpriteRenderer>();
        if (body != null) body.color = secondary;
        var trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.sharedMaterial = TrailMaterial;
            trail.startColor = secondary;
            trail.endColor = new Color(primary.r, primary.g, primary.b, 0);
            trail.time = .09f + (index % 5) * .025f;
            trail.startWidth = .18f + (index % 3) * .06f;
            trail.endWidth = 0;
        }
        for (int i = 0; i < motes.Length; i++)
        {
            var node = new GameObject(Names[index] + "_" + i);
            node.transform.SetParent(transform, false);
            var sr = node.AddComponent<SpriteRenderer>();
            sr.sprite = (index == 2 || index == 6 || index == 10 || index == 12) && i == 0 ? UiFx.Ring : UiFx.Glow;
            sr.sortingLayerID = body != null ? body.sortingLayerID : 0;
            sr.sortingOrder = body != null ? body.sortingOrder + 1 : 5;
            motes[i] = sr;
        }
    }

    private void LateUpdate()
    {
        float t = Time.time - born;
        for (int i = 0; i < motes.Length; i++)
        {
            var mote = motes[i];
            if (mote == null) continue;
            mote.enabled = body != null && body.enabled;
            float a = t * (6 + style) + i * 2.094f;
            Vector2 p = Vector2.zero, size = new Vector2(.18f, .18f);
            switch (style)
            {
                case 0: p = new Vector2((i - 1) * .15f, -.12f); size = new Vector2(.09f, .4f); break;
                case 1: p = new Vector2(Mathf.Sin(a) * .09f, -i * .13f); size = Vector2.one * (.28f - i * .05f); break;
                case 2: p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .17f; break;
                case 3: p = new Vector2((i - 1) * .11f, -Mathf.Abs(i - 1) * .15f); size = new Vector2(.11f, .35f); break;
                case 4: p = new Vector2((i - 1) * .09f, -i * .12f); size = new Vector2(.06f, .55f); break;
                case 5: p = new Vector2(Mathf.Sin(a) * .2f, -i * .17f); break;
                case 6: p = Vector2.zero; size = Vector2.one * (.24f + i * .1f); break;
                case 7: p = new Vector2((i - 1) * .16f, 0); size = new Vector2(.08f, .44f); break;
                case 8: p = new Vector2((i - 1) * .18f, Mathf.Abs(i - 1) * .1f); size = new Vector2(.1f, .35f); break;
                case 9: p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .2f; size = Vector2.one * .16f; break;
                case 10: p = Vector2.zero; size = Vector2.one * (.3f + .05f * Mathf.Sin(a)); break;
                case 11: p = new Vector2(Mathf.Sin(a) * .06f, -i * .22f); size = new Vector2(.15f - i * .035f, .4f); break;
                case 12: p = new Vector2(0, -i * .17f); size = Vector2.one * (.23f + .04f * Mathf.Sin(a)); break;
                case 13: p = Vector2.zero; size = i == 0 ? new Vector2(.42f, .08f) : new Vector2(.08f, .42f); break;
                case 14: p = new Vector2((i - 1) * .2f, i == 1 ? .16f : 0); size = new Vector2(.1f, .32f); break;
            }
            mote.transform.localPosition = p;
            mote.transform.localScale = new Vector3(size.x / mote.sprite.bounds.size.x, size.y / mote.sprite.bounds.size.y, 1);
            Color color = style == 9 ? Color.HSVToRGB(Mathf.Repeat(t * .6f + i / 3f, 1), .7f, 1) : i == 0 ? secondary : primary;
            color.a = .55f + .15f * Mathf.Sin(a);
            mote.color = color;
        }
    }
}
