using UnityEngine;

// Shared inventory artwork. Sprite slicing only; the original transparent atlas stays intact.
public static class PremiumItemArt
{
    public const string Path = "Images/Premium/crystal-ticket-v1";
    private static readonly Sprite[] cache = new Sprite[2];
    public static Sprite Crystal => Get(0);
    public static Sprite Ticket => Get(1);
    private static Sprite Get(int index)
    {
        if (cache[index] != null) return cache[index];
        var texture = Resources.Load<Texture2D>(Path);
        if (texture == null) return null;
        int left = index * texture.width / 2;
        var rect = new Rect(left, 0, (index + 1) * texture.width / 2 - left, texture.height);
        if (texture.isReadable)
        {
            var pixels = texture.GetPixels32();
            int x0 = (int)rect.xMax, y0 = texture.height, x1 = -1, y1 = -1;
            for (int y = 0; y < texture.height; y++)
                for (int x = left; x < (int)rect.xMax; x++)
                    if (pixels[y * texture.width + x].a > 32)
                    { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
            if (x1 >= x0) rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }
        cache[index] = Sprite.Create(texture, rect, new Vector2(.5f, .5f), rect.height);
        cache[index].name = index == 0 ? "PremiumCrystal" : "StarForgeTicket";
        return cache[index];
    }
}
