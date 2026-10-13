// GameSettings.cs — ค่าตั้งของผู้เล่นแต่ละเครื่อง (เฟส 9) เก็บใน PlayerPrefs ไม่ส่งผ่านเครือข่าย
// ปรับได้ที่หน้า SETTINGS > MORE OPTIONS (BattleSettingsPanel) ทั้งในล็อบบี้และในสนามรบ
// - Kill Feed / ตัวเลขดาเมจ / อีโมต / กล้องสั่น / ตัวนับ FPS
// - FPS เป้าหมาย (30/60/120) และคุณภาพกราฟิก (LOW/MEDIUM/HIGH)
// - ปุ่มบนจอ: ขนาด / ความทึบ / สลับซ้าย-ขวา (มือซ้าย)
// - ภาษา (ดู Lang.cs)
// ปิด FeatureFlags.PlayerOptions = ซ่อนปุ่ม MORE OPTIONS และทุกค่ากลับเป็นค่าเริ่มต้น (เหมือนก่อนเฟส 9)
using UnityEngine;

// คลาส static รวมค่าตั้งของผู้เล่นในเครื่อง อ่านจาก PlayerPrefs (ถ้าปิด FeatureFlags.PlayerOptions คืนค่าเริ่มต้นทั้งหมด)
public static class GameSettings
{
    // แจ้งเมื่อค่าใดๆ เปลี่ยน (GameplayManager ใช้จัดปุ่มบนจอใหม่ทันที)
    public static event System.Action Changed;

    private static bool On => FeatureFlags.PlayerOptions; // true = เปิดระบบค่าตั้งผู้เล่น (ปิดแล้วใช้ค่าเริ่มต้นทั้งหมด)
    // อ่านค่าเปิด/ปิดจาก PlayerPrefs (เก็บเป็น 1/0) ถ้ายังไม่เคยตั้งใช้ค่า fallback
    private static bool Flag(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;

    // แสดงข้อความใครฆ่าใครมุมจอ
    public static bool KillFeed => !On || Flag("Opt_KillFeed", true);
    // ตัวเลขดาเมจลอยเหนือยาน
    public static bool DamageNumbers => !On || Flag("Opt_DamageNumbers", true);
    // แสดงอีโมตของคนอื่น + ปุ่ม EMOTE
    public static bool Emotes => !On || Flag("Opt_Emotes", true);
    // แสดงตัวนับ FPS มุมจอ
    public static bool ShowFps => On && Flag("Opt_ShowFps", false);
    // ปุ่มบนจอสลับข้าง (จอยขวา ปุ่มยิงซ้าย)
    public static bool LeftHanded => On && Flag("Opt_LeftHanded", false);

    // ระดับกล้องสั่น 0 ปิด, 1 เบา, 2 เต็ม
    public static readonly string[] ShakeNames = { "OFF", "LOW", "FULL" };
    public static int ShakeLevel => On ? Mathf.Clamp(PlayerPrefs.GetInt("Opt_Shake", 2), 0, 2) : 2;
    public static float ShakeScale => ShakeLevel == 0 ? 0f : ShakeLevel == 1 ? .45f : 1f;

    // FPS เป้าหมาย
    public static readonly int[] FpsValues = { 30, 60, 120 };
    public static int FpsIndex => On ? Mathf.Clamp(PlayerPrefs.GetInt("Opt_Fps", 1), 0, FpsValues.Length - 1) : 1;

    // คุณภาพกราฟิก 0 LOW, 1 MEDIUM, 2 HIGH (ใช้ระดับ Quality ของโปรเจคตามสัดส่วน, LOW ปิดเอฟเฟกต์ยิบย่อยบางอย่าง)
    public static readonly string[] GraphicsNames = { "LOW", "MEDIUM", "HIGH" };
    public static int Graphics => On ? Mathf.Clamp(PlayerPrefs.GetInt("Opt_Graphics", 2), 0, 2) : 2;
    public static bool LowGraphics => Graphics == 0;

    // ขนาดปุ่มบนจอ
    public static readonly float[] ButtonSizes = { .8f, 1f, 1.2f, 1.4f };
    public static int ButtonSizeIndex => On ? Mathf.Clamp(PlayerPrefs.GetInt("Opt_ButtonSize", 1), 0, ButtonSizes.Length - 1) : 1;
    public static float ButtonScale => ButtonSizes[ButtonSizeIndex];

    // ความทึบปุ่มบนจอ
    public static readonly float[] ButtonOpacities = { .4f, .7f, 1f };
    public static int ButtonOpacityIndex => On ? Mathf.Clamp(PlayerPrefs.GetInt("Opt_ButtonOpacity", 2), 0, ButtonOpacities.Length - 1) : 2;
    public static float ButtonOpacity => ButtonOpacities[ButtonOpacityIndex];

    // บันทึกค่าเปิด/ปิดลง PlayerPrefs แล้วเรียก Save (ใช้การแสดงผลใหม่และแจ้ง Changed)
    public static void SetFlag(string key, bool value) { PlayerPrefs.SetInt(key, value ? 1 : 0); Save(); }
    // บันทึกค่าตัวเลข (ดัชนีตัวเลือก) ลง PlayerPrefs แล้วเรียก Save
    public static void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); Save(); }

    // บันทึก PlayerPrefs ลงดิสก์ ใช้ค่า FPS/กราฟิกใหม่ทันที และแจ้งผู้ที่สมัคร event Changed
    private static void Save()
    {
        PlayerPrefs.Save();
        ApplyDisplay();
        Changed?.Invoke();
    }

    // ตั้ง FPS/กราฟิกตอนเปิดเกม (ก่อนโหลด Scene แรก)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        ApplyDisplay();
    }

    // ใช้ค่า FPS และระดับคุณภาพกราฟิก
    public static void ApplyDisplay()
    {
        if (!On) return;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FpsValues[FpsIndex];
        // คุณภาพกราฟิก: เปลี่ยนเฉพาะเมื่อผู้เล่นเคยเลือกเอง (ไม่งั้นใช้ระดับเดิมของโปรเจค)
        if (!PlayerPrefs.HasKey("Opt_Graphics")) return;
        int levels = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        if (levels > 1)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(Graphics / 2f * (levels - 1)), 0, levels - 1);
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
        }
        // FeatureFlags.FpsUnlock: การสลับระดับ Quality จะใช้ VSync ของระดับนั้น (Medium/High/Ultra ตั้ง VSync ไว้)
        // ทำให้ FPS ติดเพดานรีเฟรชจอ (เช่น 60) แม้เลือก 120 — จึงปิด VSync ซ้ำหลังสลับระดับ ให้ targetFrameRate มีผลจริง
        if (FeatureFlags.FpsUnlock) QualitySettings.vSyncCount = 0;
    }
}
