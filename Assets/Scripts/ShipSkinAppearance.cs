using UnityEngine;

// เปลี่ยนภาพเท่านั้น ขนาดโลกอิง sprite เดิม ไม่แตะตำแหน่ง/ตัวชน
public sealed class ShipSkinAppearance : MonoBehaviour
{
    private Sprite generated;
    public void Apply(SpriteRenderer target, Sprite art, float height)
    {
        Release();
        generated = Sprite.Create(art.texture, art.rect, new Vector2(.5f, .5f),
            art.rect.height / Mathf.Max(.01f, height), 0, SpriteMeshType.FullRect);
        generated.name = art.name;
        target.sprite = generated;
        var rig = GetComponent<SkinPartMesh>();
        if (SkinPartMotion.Index(generated) >= 0)
        {
            if (rig == null) rig = gameObject.AddComponent<SkinPartMesh>();
            rig.Configure(target);
        }
        else if (rig != null) rig.Deactivate();
    }
    private void Release()
    {
        if (generated == null) return;
        if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
        generated = null;
    }
    private void OnDestroy() { Release(); }
}
