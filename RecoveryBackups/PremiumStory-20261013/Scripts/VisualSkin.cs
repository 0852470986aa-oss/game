using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ภาพสกินใช้ index เดิมทั้งหมด จึงไม่เปลี่ยนของที่ผู้เล่นมีหรือข้อมูล Photon.
public static class VisualSkin
{
    public const string AtlasPath = "Images/Skins/ship-skins-v1";
    private static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
    public static Sprite SpriteFor(SkinDef skin)
    {
        if (skin == null) return null;
        int index = SkinCatalog.IndexOf(skin.id);
        if (index < 0) return null;
        if (sprites.TryGetValue(index, out var cached) && cached != null) return cached;
        var texture = Resources.Load<Texture2D>(AtlasPath);
        if (texture == null) return null;
        int col = index % 5, row = 2 - index / 5;
        // ขอบเขตใช้พิกเซลจริงเพื่อไม่ให้เซลล์ซ้อนเมื่อขนาดภาพหารไม่ลงตัว
        int x = col * texture.width / 5, y = row * texture.height / 3;
        var rect = new Rect(x, y, (col + 1) * texture.width / 5 - x, (row + 1) * texture.height / 3 - y);
        rect = Trim(texture, rect);
        var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), rect.height, 0, SpriteMeshType.FullRect);
        sprite.name = "Skin_" + skin.id;
        sprites[index] = sprite;
        return sprite;
    }

    // ตัดเฉพาะพื้นที่ว่างในการอ้างอิง sprite ไม่แก้ไฟล์ภาพต้นฉบับ
    private static Rect Trim(Texture2D texture, Rect cell)
    {
        if (!texture.isReadable) return cell;
        var pixels = texture.GetPixels32();
        int left = (int)cell.xMax, bottom = (int)cell.yMax, right = -1, top = -1;
        for (int y = (int)cell.y; y < (int)cell.yMax; y++)
            for (int x = (int)cell.x; x < (int)cell.xMax; x++)
                if (pixels[y * texture.width + x].a > 32)
                { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
        return right < left ? cell : new Rect(left, bottom, right - left + 1, top - bottom + 1);
    }

    public static void Preview(Image image, int shipIndex, int paint, bool owned = true)
        => Preview(image, shipIndex, FeatureFlags.SkinGacha ? SkinCatalog.FromPaint(paint) : null, owned);

    public static void Preview(Image image, int shipIndex, SkinDef skin, bool owned = true)
    {
        if (image == null) return;
        var art = SpriteFor(skin);
        image.sprite = art != null ? art : BattleLoadoutCatalog.ShipSprite(shipIndex);
        image.color = (art != null ? Color.white : BattleLoadoutCatalog.ShipTint(shipIndex)) * (owned ? 1f : .45f);
        image.preserveAspect = true;
        image.raycastTarget = false;
        if (Application.isPlaying && image.GetComponent<ShipPreviewFloat>() == null) image.gameObject.AddComponent<ShipPreviewFloat>();
    }
}
