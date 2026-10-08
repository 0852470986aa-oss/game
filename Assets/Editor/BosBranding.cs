// BosBranding.cs (Editor เท่านั้น) — ตั้งชื่อแอปเป็น "BOS" และไอคอนแอป (แทนโลโก้ Unity เดิม) ให้อัตโนมัติ
// ไอคอนอยู่ที่ Assets/Branding/: BOS_Icon.png (ไอคอนเต็ม), BOS_IconBackground.png + BOS_IconForeground.png (ไอคอนแบบ Adaptive ของ Android)
// - เปิดโปรเจกต์/คอมไพล์เสร็จ: ถ้าชื่อยังไม่ใช่ BOS หรือยังไม่ได้ตั้งไอคอน จะตั้งให้เอง 1 ครั้ง (ปิดได้ด้วย FeatureFlags.AppBranding = false)
// - ตั้งซ้ำเอง: เมนูด้านบน Tools > BOS > Apply App Name and Icon
// - เปลี่ยนรูป: วางไฟล์ชื่อเดิมทับใน Assets/Branding แล้วกดเมนูข้างบนอีกครั้ง
// ไม่เปลี่ยนรหัสแอป (com.Battlefield.Game) เครื่องที่ลงเกมไว้แล้วอัปเดตทับได้ตามปกติ
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// ตัวตั้งค่าชื่อ/ไอคอนแอป (static ทำงานใน Editor)
[InitializeOnLoad]
public static class BosBranding
{
    private const string AppName = "BOS"; // ชื่อแอปที่จะตั้งใน Player Settings
    private const string Folder = "Assets/Branding/"; // โฟลเดอร์เก็บรูปไอคอนแอป
    private const string IconPath = Folder + "BOS_Icon.png"; // ไฟล์ไอคอนแอปแบบเต็ม
    private const string BackgroundPath = Folder + "BOS_IconBackground.png"; // ไฟล์พื้นหลังไอคอน Adaptive ของ Android
    private const string ForegroundPath = Folder + "BOS_IconForeground.png"; // ไฟล์ภาพหน้าไอคอน Adaptive ของ Android

    // Unity เรียกตอนเปิดโปรเจกต์/หลังคอมไพล์: รอให้ Editor พร้อมก่อนแล้วค่อยตรวจ
    static BosBranding()
    {
        EditorApplication.delayCall += ApplyIfNeeded;
    }

    // ตั้งให้เฉพาะเมื่อยังไม่ได้ตั้ง (ทำครั้งเดียวต่อการเปิด Unity)
    private static void ApplyIfNeeded()
    {
        if (!FeatureFlags.AppBranding || SessionState.GetBool("BOS_BrandingChecked", false)) return;
        SessionState.SetBool("BOS_BrandingChecked", true);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) return; // รูปยังนำเข้าไม่เสร็จ: ไว้ครั้งหน้า
        var current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
        bool iconSet = current != null && current.Length > 0 && current[0] == icon;
        if (PlayerSettings.productName != AppName || !iconSet) Apply();
    }

    // เมนู Tools > BOS > Apply App Name and Icon: ตั้งชื่อ + ไอคอน (ไอคอนหลัก, Android แบบปกติ/กลม/Adaptive)
    [MenuItem("Tools/BOS/Apply App Name and Icon")]
    public static void Apply()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) { Debug.LogWarning("BOS icon not found: " + IconPath); return; }
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);

        PlayerSettings.productName = AppName;
        // ไอคอนหลัก (Default Icon) ใช้กับทุกแพลตฟอร์มที่ไม่ได้ตั้งแยก
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        // Android: ทุกชนิดไอคอนที่รองรับ (Adaptive = พื้นหลัง + ตัวหน้า, แบบอื่น = รูปเต็ม)
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var platformIcon in icons)
            {
                if (platformIcon.maxLayerCount >= 2 && background != null && foreground != null)
                    platformIcon.SetTextures(background, foreground);
                else
                    platformIcon.SetTexture(icon);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BOS] App name set to \"" + AppName + "\" and app icon applied (Assets/Branding).");
    }
}
