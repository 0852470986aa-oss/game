using System;
using UnityEngine;

// ตำแหน่งเป็นสัดส่วนของพื้นที่ HUD จึงใช้ได้กับจอหลายขนาด เก็บเฉพาะเครื่องนี้
public static class ControlLayout
{
    [Serializable] public class Slot { public Vector2 position; public float scale = 1; }
    [Serializable] public class Profile { public Slot[] slots; }
    public const string Key = "Opt_ControlLayoutV1";
    public static int Revision { get; private set; }
    public static readonly string[] Names = { "MOVE", "AIM / FIRE", "SKILL" };
    public static readonly float[] Sizes = { 196, 164, 112 };
    public static Profile Load()
    {
        if (!FeatureFlags.PlayerOptions) return null;
        try
        {
            var value = JsonUtility.FromJson<Profile>(PlayerPrefs.GetString(Key, ""));
            if (!Valid(value)) return null;
            return value;
        }
        catch { return null; }
    }
    public static bool Valid(Profile value)
    {
        if (value?.slots == null || value.slots.Length != 3) return false;
        foreach (var slot in value.slots)
            if (slot == null || !Finite(slot.position.x) || !Finite(slot.position.y) || !Finite(slot.scale)
                || slot.position.x < 0 || slot.position.x > 1 || slot.position.y < 0 || slot.position.y > 1
                || slot.scale < .65f || slot.scale > 1.6f) return false;
        return true;
    }
    private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    public static Profile Defaults()
    {
        bool left = GameSettings.LeftHanded;
        Vector2[] positions = { new Vector2(.12f,.2f), new Vector2(.87f,.19f), new Vector2(.93f,.43f) };
        var profile = new Profile { slots = new Slot[3] };
        for (int i=0;i<3;i++)
        {
            if (left) positions[i].x = 1-positions[i].x;
            profile.slots[i] = new Slot { position = positions[i], scale = GameSettings.ButtonScale };
        }
        return profile;
    }
    public static void Save(Profile profile)
    {
        if (!Valid(profile)) throw new ArgumentException("Invalid control layout");
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(profile)); PlayerPrefs.Save(); Revision++;
    }
    public static void Reset() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); Revision++; }
    public static Vector2 ClampPosition(Vector2 position, Vector2 area, Vector2 halfSize)
    {
        Vector2 margin = new Vector2(Mathf.Min(.49f, (halfSize.x+8)/Mathf.Max(1,area.x)),
            Mathf.Min(.49f, (halfSize.y+30)/Mathf.Max(1,area.y)));
        return new Vector2(Mathf.Clamp(position.x,margin.x,1-margin.x), Mathf.Clamp(position.y,margin.y,1-margin.y));
    }
    public static void Apply(RectTransform control, Slot slot)
    {
        if (control == null || !(control.parent is RectTransform parent)) return;
        control.localScale = Vector3.one * slot.scale;
        Vector2 half = control.rect.size * slot.scale * .5f;
        Vector2 position = ClampPosition(slot.position,parent.rect.size,half);
        // ยึด anchor ที่ผู้ใช้เลือก ไม่ใช้ pixel offset ข้ามขนาดจอ
        control.anchorMin = control.anchorMax = position;
        control.pivot = new Vector2(.5f,.5f); control.anchoredPosition = Vector2.zero;
    }
}
