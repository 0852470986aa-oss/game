using UnityEngine;
using Photon.Pun;

// Enemy-only sprites: the player's chosen ship, old save IDs and combat stats stay unchanged.
public static class StoryFleetArt
{
    public const string Path = "Images/Bosses/story-fleet-v1";
    public static readonly string[] Names = { "CRIMSON RAIDER", "EMERALD DRONE", "VOID ESCORT", "SIEGE DREADNOUGHT", "INFERNO CRUISER", "VOID MOTHERSHIP" };
    private static readonly Sprite[] sprites = new Sprite[6];
    public static Sprite Get(int index)
    {
        index = Mathf.Clamp(index, 0, 5);
        if (sprites[index] != null) return sprites[index];
        var texture = Resources.Load<Texture2D>(Path);
        if (texture == null) return null;
        int col = index % 3, row = 1 - index / 3;
        // Measured transparent gutters in the generated 1536x1024 atlas (top row is shorter).
        int[] cuts = { 0, texture.width * 512 / 1536, texture.width * 1006 / 1536, texture.width };
        int split = texture.height * 589 / 1024;
        int x0 = cuts[col], x1 = cuts[col + 1];
        int y0 = row == 0 ? 0 : split, y1 = row == 0 ? split : texture.height;
        Rect rect = new Rect(x0, y0, x1 - x0, y1 - y0);
        if (texture.isReadable)
        {
            var pixels = texture.GetPixels32();
            int l = x1, r = -1, b = y1, t = -1;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (pixels[y * texture.width + x].a > 32)
                    { l = Mathf.Min(l, x); r = Mathf.Max(r, x); b = Mathf.Min(b, y); t = Mathf.Max(t, y); }
            if (r >= l) rect = new Rect(l, b, r - l + 1, t - b + 1);
        }
        sprites[index] = Sprite.Create(texture, rect, new Vector2(.5f, .5f), rect.height, 0, SpriteMeshType.FullRect);
        sprites[index].name = "StoryFleet_" + index;
        return sprites[index];
    }

    public static int Variant(bool boss, int stage, int id)
        => boss ? (stage >= 10 ? 5 : stage >= 8 ? 4 : 3) : Mathf.Abs(id % 3);

    public static bool Apply(PlayerController ship, SpriteRenderer renderer)
    {
        if (ship == null || !ship.IsBot || renderer == null || renderer.sprite == null
            || MatchRules.GameMode(PhotonNetwork.CurrentRoom) != MatchRules.ModeCampaign) return false;
        var art = Get(Variant(ship.IsBoss, MatchRules.CampaignStage(PhotonNetwork.CurrentRoom), ship.CombatantId));
        if (art == null) return false;
        var appearance = ship.GetComponent<ShipSkinAppearance>();
        if (appearance == null) appearance = ship.gameObject.AddComponent<ShipSkinAppearance>();
        appearance.Apply(renderer, art, renderer.sprite.bounds.size.y);
        SkinAura.Apply(ship.transform, renderer, null);
        renderer.color = Color.white;
        return true;
    }
}
