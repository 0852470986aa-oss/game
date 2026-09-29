using UnityEngine;
using Photon.Pun;

// ส่วน Visuals ของ PlayerController; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class PlayerController
{
    private static Sprite[] redThrusterFrames;
    private static bool redThrusterLoaded;
    private static Sprite[] greenThrusterFrames;

    private static Sprite GreenThrusterFrame(float time, int engine)
    {
        if (greenThrusterFrames == null)
            greenThrusterFrames = SkillSheetVisual.LoadGrid("VFX/VFX_GreenThruster", 4, 4, true);
        return greenThrusterFrames == null || greenThrusterFrames.Length == 0 ? null
            : greenThrusterFrames[(Mathf.FloorToInt(time * 12f) + engine * 3) % greenThrusterFrames.Length];
    }

    private static Sprite RedThrusterFrame(float time, int engine)
    {
        if (!redThrusterLoaded)
        {
            redThrusterLoaded = true;
            var texture = Resources.Load<Texture2D>("Images/VFX_RedThruster_Higgsfield");
            if (texture != null)
            {
                redThrusterFrames = new Sprite[16];
                float w = texture.width / 4f, h = texture.height / 4f;
                for (int i = 0; i < 16; i++)
                    redThrusterFrames[i] = Sprite.Create(texture,
                        new Rect(i % 4 * w, (3 - i / 4) * h, w, h),
                        new Vector2(.5f, 1f), 100, 0, SpriteMeshType.FullRect);
            }
        }
        return redThrusterFrames == null ? null
            : redThrusterFrames[(Mathf.FloorToInt(time * 12f) + engine * 3) % 16];
    }

    private static Sprite ShipEffectSprite(int id)
    {
        if (shipEffectSprites == null) shipEffectSprites = Resources.LoadAll<Sprite>("Images/VFX_ShipEffects");
        foreach (var sprite in shipEffectSprites)
            if (sprite.name.EndsWith("_" + id)) return sprite;
        return null;
    }

    private void LateUpdate()
    {
        UpdateAimGuide();
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        float traveled = Vector3.Distance(transform.position, previousVfxPosition);
        previousVfxPosition = transform.position;
        if (sheetThrusters == null)
        {
            int style = spriteRenderer.sprite.name.ToLowerInvariant().Contains("ship2") ? 1
                : spriteRenderer.sprite.name.ToLowerInvariant().Contains("ship3") ? 2 : 0;
            exhaustStyle = style;
            Sprite source = style == 2 ? GreenThrusterFrame(0, 0) : ShipEffectSprite(4);
            if (source == null) return;
            if (exhaustSprites[style] == null)
            {
                if (style == 2)
                    exhaustSprites[style] = source;
                else
                {
                    // Flame-only regions in the original 1536 x 1024 sheet; omit the metal nozzle.
                    Rect region = style == 0 ? new Rect(486, 880, 43, 85) : new Rect(557, 781, 34, 62);
                    Vector2 textureScale = new Vector2(source.texture.width / 1536f, source.texture.height / 1024f);
                    region = new Rect(region.x * textureScale.x, region.y * textureScale.y,
                        region.width * textureScale.x, region.height * textureScale.y);
                    exhaustSprites[style] = Sprite.Create(source.texture, region, new Vector2(0.5f, 1f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    exhaustSprites[style].name = "FlameOnly_" + style;
                }
            }
            // Anchors match the replacement PNGs supplied by the user (including their transparent padding).
            // Red: four rear nozzles. White: two rear and two forward-facing nozzles. Green: two heavy rear engines.
            exhaustAnchors = style == 0 ? new[] {
                new Vector2(.235f, .055f), new Vector2(.423f, .055f),
                new Vector2(.577f, .055f), new Vector2(.754f, .055f) }
                : style == 1 ? new[] {
                    new Vector2(.404f, .025f), new Vector2(.594f, .025f),
                    new Vector2(.404f, .64f), new Vector2(.594f, .64f) }
                : new[] { new Vector2(.333f, .215f), new Vector2(.662f, .215f) };
            exhaustAngles = style == 1 ? new[] { 0f, 0f, 180f, 180f } : new float[exhaustAnchors.Length];
            sheetThrusters = new SpriteRenderer[exhaustAnchors.Length];
            sheetThrusterGlows = new SpriteRenderer[exhaustAnchors.Length];
            exhaustGlowColor = style == 0 ? new Color(1f, 0.35f, 1f) : style == 1
                ? new Color(0.2f, 0.85f, 1f) : new Color(0.35f, 1f, 0.25f);
            for (int i = 0; i < sheetThrusters.Length; i++)
            {
                var obj = new GameObject("ShipSheetThruster" + i);
                obj.transform.SetParent(transform, false);
                var nozzle = obj.AddComponent<SpriteRenderer>();
                nozzle.sprite = exhaustSprites[style];
                nozzle.sortingLayerID = spriteRenderer.sortingLayerID;
                // Above the baked-in exhaust art, anchored at the nozzle, not behind the opaque ship sprite.
                nozzle.sortingOrder = spriteRenderer.sortingOrder + 2;
                sheetThrusters[i] = nozzle;
                var glowObject = new GameObject("ExhaustGlow" + i);
                glowObject.transform.SetParent(transform, false);
                var glow = glowObject.AddComponent<SpriteRenderer>();
                glow.sprite = nozzle.sprite;
                glow.sortingLayerID = nozzle.sortingLayerID;
                glow.sortingOrder = spriteRenderer.sortingOrder + 1;
                sheetThrusterGlows[i] = glow;
            }
            if (thrusterEffect != null) thrusterEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        Bounds hull = spriteRenderer.sprite.bounds;
        float actualThrust = Mathf.Clamp01(traveled / Mathf.Max(0.001f, Time.deltaTime * speed));
        // Smooth fixed-step motion, while keeping the engine visibly burning when pushing against cover.
        float requestedThrust = photonView.IsMine && !isStunned ? movementInput.magnitude : actualThrust;
        displayedThrust = Mathf.MoveTowards(displayedThrust, Mathf.Clamp01(requestedThrust), Time.deltaTime * 5f);
        float pulse = 1f + 0.025f * Mathf.Sin(Time.time * 19f);
        float length = hull.size.y * Mathf.Lerp(0.12f, 0.21f, displayedThrust) * pulse;
        for (int i = 0; i < sheetThrusters.Length; i++)
        {
            var nozzle = sheetThrusters[i];
            if (exhaustStyle == 0)
            {
                var animated = RedThrusterFrame(Time.time, i);
                if (animated != null) nozzle.sprite = animated;
            }
            else if (exhaustStyle == 2)
            {
                var animated = GreenThrusterFrame(Time.time, i);
                if (animated != null) nozzle.sprite = animated;
            }
            nozzle.enabled = !isDead && !matchEnded && spriteRenderer.enabled;
            float engineSize = exhaustStyle == 2 ? 1.3f : exhaustStyle == 1 && i >= 2 ? .75f : 1f;
            float scale = length * engineSize / Mathf.Max(0.01f, nozzle.sprite.bounds.size.y);
            float width = hull.size.x * (exhaustStyle == 2 ? Mathf.Lerp(.10f, .14f, displayedThrust)
                : Mathf.Lerp(.052f, .072f, displayedThrust));
            if (exhaustStyle == 1 && i >= 2) width *= .85f;
            nozzle.transform.localScale = new Vector3(width / Mathf.Max(0.01f, nozzle.sprite.bounds.size.x), scale, 1f);
            Vector2 anchor = exhaustAnchors[i];
            if (spriteRenderer.flipX) anchor.x = 1f - anchor.x;
            if (spriteRenderer.flipY) anchor.y = 1f - anchor.y;
            nozzle.transform.localRotation = Quaternion.Euler(0, 0, exhaustAngles[i] + (spriteRenderer.flipY ? 180 : 0));
            nozzle.transform.localPosition = new Vector3(hull.min.x + hull.size.x * anchor.x, hull.min.y + hull.size.y * anchor.y, 0);
            Color flameTint = exhaustStyle == 2 ? new Color(0.68f, 1f, 0.42f) : Color.white;
            flameTint.a = Mathf.Lerp(0.85f, 1f, displayedThrust);
            nozzle.color = flameTint;
            var glow = sheetThrusterGlows[i];
            glow.sprite = nozzle.sprite;
            glow.enabled = nozzle.enabled;
            glow.transform.localPosition = nozzle.transform.localPosition;
            glow.transform.localRotation = nozzle.transform.localRotation;
            glow.transform.localScale = Vector3.Scale(nozzle.transform.localScale, new Vector3(1.8f, 1.03f, 1));
            glow.color = new Color(exhaustGlowColor.r, exhaustGlowColor.g, exhaustGlowColor.b,
                Mathf.Lerp(0.18f, 0.32f, displayedThrust));
        }
        if (thrusterEffect != null && thrusterEffect.isPlaying) thrusterEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private bool PlaySheetBurst(bool death)
    {
        Sprite[] frames = SkillSheetVisual.LoadGrid(death ? "VFX/VFX_ShipExplosion" : "VFX/VFX_ShipImpact");
        if (frames == null || frames.Length == 0 || spriteRenderer == null) return false;
        if (!death && Time.time - lastSheetImpact < 0.07f) return true;
        lastSheetImpact = Time.time;
        float size = Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) * (death ? 1.8f : 0.65f);
        SkillSheetVisual.Create(frames, null, transform.position, size, death ? 0.65f : 0.24f);
        return true;
    }
}
