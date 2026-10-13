// SkillInfo.cs — คำอธิบายสกิลแบบละเอียด ภาษาไทย/อังกฤษ (ใช้ในหน้าโรงเก็บยาน แท็บ SKILLS)
// ตัวเลขดึงจากค่าจริงในเกม (BattleBalance / RemoteCatalog / คูลดาวน์ใน BattleLoadoutCatalog) จึงตรงเสมอแม้ปรับบาลานซ์
// index สกิล: 0 STUN, 1 SHIELD, 2 NOVA, 3 SEEKER, 4 BLINK, 5 HEAL, 6 CLOAK
using System.Globalization;

// คลาส static สร้างข้อความอธิบายสกิล (ดาเมจ/คูลดาวน์/ระยะเวลา) ตามภาษาที่ผู้เล่นเลือก
public static class SkillInfo
{
    // แปลงตัวเลขเป็นข้อความทศนิยมไม่เกิน 1 ตำแหน่ง (เช่น 2.5, 3) โดยใช้ InvariantCulture ให้จุดทศนิยมเหมือนกันทุกเครื่อง
    private static string N(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    // ดาเมจของสกิล (0 = ไม่มีดาเมจ)
    public static float Damage(int index)
    {
        switch (index)
        {
            case 0: return RemoteCatalog.SkillDamage(0, BattleBalance.StunDamage);
            case 2: return RemoteCatalog.SkillDamage(2, BattleBalance.NovaDamage);
            case 3: return RemoteCatalog.SkillDamage(3, BattleBalance.SeekerDamage);
        }
        return 0f;
    }

    // คำอธิบายเต็ม (หลายประโยค) ตามภาษาที่เลือก
    public static string Describe(int index)
    {
        bool th = Lang.Thai;
        string dmg = N(Damage(index));
        switch (index)
        {
            case 0:
                return th ? "ยิงคลื่นพลังงานพุ่งเข้าหาศัตรูที่ใกล้ที่สุดในระยะ โดนแล้วเสีย " + dmg + " ดาเมจ และติดสตัน " + N(BattleBalance.StunSeconds)
                            + " วินาที (ขยับและยิงไม่ได้) ศัตรูที่เปิดโล่อยู่จะไม่ติดสตัน"
                          : "Fires an energy wave that locks on to the nearest enemy in range. Hit deals " + dmg + " damage and stuns for "
                            + N(BattleBalance.StunSeconds) + " sec (cannot move or shoot). Enemies with a shield are immune.";
            case 1:
                return th ? "เปิดโล่พลังงานรอบยาน " + N(BattleBalance.ShieldSeconds) + " วินาที ระหว่างนั้นไม่เสียเลือด ไม่ติดสตัน และบินเร็วขึ้น "
                            + N((BattleBalance.ShieldSpeedMultiplier - 1f) * 100f) + "% ใช้หลบตอนโดนรุมหรือพุ่งเข้าชิงจุด"
                          : "Raises an energy shield for " + N(BattleBalance.ShieldSeconds) + " sec: no damage taken, immune to stun and "
                            + N((BattleBalance.ShieldSpeedMultiplier - 1f) * 100f) + "% faster. Use it to escape or rush an objective.";
            case 2:
                return th ? "ระเบิดพลังงานรอบตัวยาน รัศมี 3 หน่วย ศัตรูทุกลำในวงเสีย " + dmg + " ดาเมจพร้อมกัน เหมาะกับตอนศัตรูเข้ามาใกล้หลายลำ"
                          : "Detonates a blast around your ship (radius 3 units). Every enemy inside takes " + dmg + " damage. Best when several enemies are close.";
            case 3:
                return th ? "ยิงมิสไซล์ติดตามที่วิ่งตามศัตรูที่ใกล้ที่สุดได้ทั้งแผนที่ ยิ่งบินนานยิ่งเร็วขึ้น โดนแล้วเสีย " + dmg + " ดาเมจ"
                          : "Launches a homing missile that chases the nearest enemy anywhere on the map and speeds up over time. Hit deals " + dmg + " damage.";
            case 4:
                return th ? "วาร์ปไปข้างหน้าทันทีสูงสุด 9 หน่วย หยุดก่อนชนสิ่งกีดขวางและขอบสนาม ใช้หลบกระสุนหรือไล่ตามศัตรู"
                          : "Instantly teleports up to 9 units forward, stopping before obstacles and the arena edge. Dodge shots or chase enemies.";
            case 5:
                return th ? "ซ่อมยานทันที ฟื้นเลือด 35% ของเลือดสูงสุด (เกินเลือดเต็มไม่ได้) ใช้ตอนเลือดเหลือน้อยเพื่อสู้ต่อ"
                          : "Instantly repairs 35% of your max HP (cannot exceed full HP). Use it when low to stay in the fight.";
            case 6:
                return th ? "ล่องหน 3 วินาที ศัตรูมองไม่เห็นยานคุณ ยิงเมื่อไหร่จะปรากฏตัวทันที ใช้ย่องเข้าใกล้หรือหนีออกจากวงล้อม"
                          : "Become invisible to enemies for 3 sec. Firing reveals you immediately. Sneak up on targets or slip away.";
        }
        return "";
    }

    // บรรทัดสรุปตัวเลข: คูลดาวน์ / ดาเมจ / ระยะเวลา
    public static string Stats(int index, float cooldown)
    {
        bool th = Lang.Thai;
        string text = (th ? "คูลดาวน์ " : "Cooldown ") + N(cooldown) + (th ? " วิ" : " sec");
        float damage = Damage(index);
        if (damage > 0) text += (th ? "   •   ดาเมจ " : "   •   Damage ") + N(damage);
        float duration = index == 0 ? BattleBalance.StunSeconds : index == 1 ? BattleBalance.ShieldSeconds : index == 6 ? 3f : 0f;
        if (duration > 0) text += (th ? "   •   นาน " : "   •   Lasts ") + N(duration) + (th ? " วิ" : " sec");
        return text;
    }
}
