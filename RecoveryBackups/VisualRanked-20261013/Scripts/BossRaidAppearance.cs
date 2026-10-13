// ภาพเฉพาะบอส Co-op: เปลี่ยนเฉพาะ Sprite ไม่แก้ Collider / Transform / ค่าพลัง
using UnityEngine;

public sealed class BossRaidAppearance : MonoBehaviour
{
    public const string ResourcePath = "Images/Bosses/dreadnought-v1";
    private Sprite generatedSprite;

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
        return true;
    }

    private void OnDestroy()
    {
        if (generatedSprite != null) Destroy(generatedSprite);
    }
}
