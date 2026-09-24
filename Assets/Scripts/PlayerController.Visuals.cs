using UnityEngine;
using Photon.Pun;

// ส่วน Visuals ของ PlayerController; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class PlayerController
{
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
            Sprite source = ShipEffectSprite(4);
            if (source == null) return;
            // Flame-only regions in the original 1536 x 1024 sheet; omit the metal nozzle.
            if (exhaustSprites[style] == null)
            {
                Rect region = style == 0 ? new Rect(486, 880, 43, 85)
                    : style == 1 ? new Rect(557, 781, 34, 62) : new Rect(264, 870, 28, 78);
                // Keep the UV crop correct if the texture importer downsizes the sheet.
                Vector2 textureScale = new Vector2(source.texture.width / 1536f, source.texture.height / 1024f);
                region = new Rect(region.x * textureScale.x, region.y * textureScale.y,
                    region.width * textureScale.x, region.height * textureScale.y);
                exhaustSprites[style] = Sprite.Create(source.texture, region, new Vector2(0.5f, 1f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                exhaustSprites[style].name = "FlameOnly_" + style;
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
                ? new Color(0.2f, 0.85f, 1f) : new Color(1f, 0.5f, 0.08f);
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
            nozzle.color = new Color(1, 1, 1, Mathf.Lerp(0.85f, 1f, displayedThrust));
            var glow = sheetThrusterGlows[i];
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
        Sprite sprite = ShipEffectSprite(death ? 117 : 129);
        if (sprite == null || spriteRenderer == null) return false;
        if (!death && Time.time - lastSheetImpact < 0.07f) return true;
        lastSheetImpact = Time.time;
        var obj = new GameObject(death ? "ShipSheetExplosion" : "ShipSheetImpact");
        obj.transform.position = transform.position;
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + 3;
        float size = Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) * (death ? 1.8f : 0.45f);
        obj.AddComponent<ShipSheetBurst>().Initialize(renderer, size, death ? 0.65f : 0.2f);
        return true;
    }
}
