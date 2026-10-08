// SkillUnlock.cs — สกิลปลดล็อกตามเลเวลผู้เล่น (Progression.Level)
// 3 สกิลแรก (STUN / SHIELD / NOVA) ใช้ฟรีตั้งแต่เริ่ม ที่เหลือปลดล็อกเมื่อเลเวลถึง (ตั้งไว้ไม่สูง เล่นไม่กี่แมตช์ก็ได้)
// สกิลที่ล็อก: ในหน้า Hangar การ์ดขึ้น LOCKED + เลเวลที่ต้องการ ปุ่ม INSTALL กดไม่ได้
// ถ้าสกิลที่ติดตั้งไว้ (เช่นบันทึกไว้ก่อนมีระบบนี้) ยังล็อกอยู่ ตอนเข้าห้องจะใช้สกิล STUN แทน จนกว่าเลเวลจะถึง
// แก้เลเวลที่ต้องการได้ที่ RequiredLevels / ปิด FeatureFlags.SkillLevelLock = ใช้ได้ทุกสกิลเหมือนเดิม
public static class SkillUnlock
{
    // เลเวลที่ต้องการของแต่ละสกิล (ลำดับเดียวกับ BattleLoadoutCatalog.Skills)
    // STUN, SHIELD, NOVA, SEEKER, BLINK, HEAL, CLOAK
    public static readonly int[] RequiredLevels = { 1, 1, 1, 3, 5, 7, 9 };

    // เลเวลที่ต้องใช้ปลดล็อกสกิล index (ปิดระบบ = 1)
    public static int RequiredLevel(int index)
        => FeatureFlags.SkillLevelLock && FeatureFlags.Progression && index >= 0 && index < RequiredLevels.Length ? RequiredLevels[index] : 1;

    // ปลดล็อกแล้วหรือยัง
    public static bool Unlocked(int index) => Progression.Level >= RequiredLevel(index);

    // สกิลที่ใช้ได้จริง: ยังล็อก = สกิลแรก (STUN)
    public static int Usable(int index) => Unlocked(index) ? index : 0;
}
