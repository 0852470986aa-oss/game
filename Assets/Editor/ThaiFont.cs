using UnityEditor;
using UnityEngine;
using TMPro;

public class ThaiFont
{
    private static TMP_FontAsset _cachedFont;

    [MenuItem("Battlefield/สร้างฟอนต์ไทย (Generate Thai Font)")]
    public static void GenerateThaiFont()
    {
        TMP_FontAsset font = CreateThaiFont();
        if (font != null)
            Debug.Log("สร้างฟอนต์ไทยสำเร็จ! บันทึกไว้ที่ Assets/Resources/Fonts/ThaiFont.asset");
        else
            Debug.LogError("ไม่สามารถสร้างฟอนต์ไทยได้");
    }

    public static TMP_FontAsset GetOrCreateFont()
    {
        if (_cachedFont != null) return _cachedFont;

        // ลองโหลดจากที่บันทึกไว้ก่อน
        _cachedFont = Resources.Load<TMP_FontAsset>("Fonts/ThaiFont");
        if (_cachedFont != null) return _cachedFont;

        // ถ้ายังไม่มี ให้สร้างใหม่
        _cachedFont = CreateThaiFont();
        return _cachedFont;
    }

    private static TMP_FontAsset CreateThaiFont()
    {
        // ลองใช้ฟอนต์ไทยในระบบ Windows
        string[] thaifonts = { "Leelawadee UI", "Tahoma", "Cordia New", "Angsana New", "Microsoft Sans Serif" };
        Font osFont = null;

        foreach (string name in thaifonts)
        {
            osFont = Font.CreateDynamicFontFromOSFont(name, 32);
            if (osFont != null)
            {
                Debug.Log($"ใช้ฟอนต์: {name}");
                break;
            }
        }

        if (osFont == null)
        {
            Debug.LogError("ไม่พบฟอนต์ไทยในระบบ");
            return null;
        }

        // สร้าง TMP Font Asset
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(osFont);
        if (fontAsset == null) return null;

        fontAsset.name = "ThaiFont";

        // บันทึกเป็น Asset
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Fonts"))
            AssetDatabase.CreateFolder("Assets/Resources", "Fonts");

        string path = "Assets/Resources/Fonts/ThaiFont.asset";
        AssetDatabase.CreateAsset(fontAsset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }
}
