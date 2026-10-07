// ไฟล์ ThaiFont.cs — เครื่องมือ Unity Editor (ไม่ได้รันในเกม / ไม่ถูก build ลงมือถือ)
// สร้าง TextMeshPro Font Asset ภาษาไทยจากฟอนต์ที่ติดตั้งใน Windows แล้วบันทึกไว้ที่
// Assets/Resources/Fonts/ThaiFont.asset เพื่อให้ข้อความ UI แสดงภาษาไทยได้
// มี GetOrCreateFont() เป็น public static ให้สคริปต์ Editor อื่นเรียกใช้ได้ (MenuItem ของไฟล์นี้ถูกปิดไว้)
using UnityEditor;
using UnityEngine;
using TMPro;

// คลาส static-helper สำหรับหา/สร้างฟอนต์ไทย และแคชไว้ไม่ให้สร้างซ้ำ
public class ThaiFont
{
    // แคชฟอนต์ที่โหลด/สร้างแล้ว เรียกครั้งถัดไปจะคืนตัวนี้ทันที
    private static TMP_FontAsset _cachedFont;

    // [MenuItem("Battlefield/สร้างฟอนต์ไทย (Generate Thai Font)")]
    // สั่งสร้างฟอนต์ไทยแล้วแจ้งผลใน Console (เดิมผูกกับเมนู แต่ตอนนี้ MenuItem ถูกคอมเมนต์ปิดไว้)
    public static void GenerateThaiFont()
    {
        TMP_FontAsset font = CreateThaiFont();
        if (font != null)
            Debug.Log("สร้างฟอนต์ไทยสำเร็จ! บันทึกไว้ที่ Assets/Resources/Fonts/ThaiFont.asset");
        else
            Debug.LogError("ไม่สามารถสร้างฟอนต์ไทยได้");
    }

    // คืนฟอนต์ไทย: ใช้จากแคชก่อน -> ถ้าไม่มีลองโหลดจาก Resources/Fonts/ThaiFont -> ถ้ายังไม่มีค่อยสร้างใหม่
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

    // สร้าง TMP_FontAsset จากฟอนต์ไทยในระบบ แล้วบันทึกเป็นไฟล์ .asset ใน Resources/Fonts
    // คืน null ถ้าหาฟอนต์ไทยในเครื่องไม่เจอหรือสร้างไม่สำเร็จ
    private static TMP_FontAsset CreateThaiFont()
    {
        // ลองใช้ฟอนต์ไทยในระบบ Windows
        string[] thaifonts = { "Leelawadee UI", "Tahoma", "Cordia New", "Angsana New", "Microsoft Sans Serif" };
        Font osFont = null;

        // วนลองชื่อฟอนต์ตามลำดับ ตัวแรกที่สร้างได้ (ขนาด 32) จะถูกใช้
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

        // ตำแหน่งไฟล์ที่บันทึก (อยู่ใต้ Resources จึงโหลดด้วย Resources.Load ได้ตอนรันเกม)
        string path = "Assets/Resources/Fonts/ThaiFont.asset";
        AssetDatabase.CreateAsset(fontAsset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }
}

