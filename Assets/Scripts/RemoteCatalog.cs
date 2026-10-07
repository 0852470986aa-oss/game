// RemoteCatalog.cs — ให้เกมอ่านค่ายาน/สกิลจาก Firebase (เฟส 9)
// แอดมินแก้ใน Firebase Console ที่ spacecraft/{เลขยาน 1-6} และ skills/{เลขสกิล 1-7} แล้วผู้เล่นที่ล็อกอินครั้งถัดไปได้ค่าใหม่
//   spacecraft: base_hp, speed, fire_rate (วินาทีระหว่างนัด), bullet_damage, spacecraft_price, spacecraft_name
//   skills:     damage (เฉพาะ STUN/NOVA/SEEKER), cooldown (วินาที)
// ค่าที่ผิดช่วงจะถูกบีบให้อยู่ในช่วงปลอดภัย (กันพิมพ์ผิดแล้วเกมพัง) ค่าที่ไม่มี = ใช้ค่าในโค้ดเดิม
// FirebaseManager เรียก Apply หลังล็อกอิน / ปิด FeatureFlags.RemoteCatalog = ใช้ค่าในโค้ดอย่างเดียว
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class RemoteCatalog
{
    // ดาเมจสกิลที่แอดมินตั้ง (key = index สกิล 0..)
    private static readonly Dictionary<int, float> skillDamage = new Dictionary<int, float>();
    // จำนวนค่าที่โหลดจาก Firebase ครั้งล่าสุด (โชว์ใน Log)
    public static int AppliedValues { get; private set; }

    // ดาเมจของสกิล index (ไม่มีค่าจาก Firebase = ใช้ค่าเดิม)
    public static float SkillDamage(int skill, float fallback)
        => FeatureFlags.RemoteCatalog && skillDamage.TryGetValue(skill, out float value) ? value : fallback;

    // ใช้ค่าจาก snapshot.Value ของ spacecraft และ skills
    public static void Apply(object spacecraft, object skills)
    {
        if (!FeatureFlags.RemoteCatalog) return;
        int applied = 0;
        foreach (var entry in Entries(spacecraft))
        {
            int index = entry.Key - 1;
            if (index < 0 || index >= BattleLoadoutCatalog.Ships.Length) continue;
            ShipData ship = BattleLoadoutCatalog.Ships[index];
            var data = entry.Value;
            if (TryNumber(data, "base_hp", out double hp)) { ship.hp = Mathf.RoundToInt(Mathf.Clamp((float)hp, 20, 1000)); applied++; }
            if (TryNumber(data, "speed", out double speed)) { ship.spd = Mathf.Clamp((float)speed, 1f, 20f); applied++; }
            if (TryNumber(data, "fire_rate", out double rate)) { ship.shotInterval = Mathf.Clamp((float)rate, .05f, 3f); applied++; }
            if (TryNumber(data, "bullet_damage", out double atk)) { ship.atk = Mathf.Clamp((float)atk, .5f, 100f); applied++; }
            if (TryNumber(data, "spacecraft_price", out double price)) { ship.price = Mathf.RoundToInt(Mathf.Clamp((float)price, 0, 1000000)); applied++; }
            if (data.TryGetValue("spacecraft_name", out object name) && name is string text && text.Trim().Length > 0)
                ship.name = text.Trim().Length > 24 ? text.Trim().Substring(0, 24) : text.Trim();
        }
        foreach (var entry in Entries(skills))
        {
            int index = entry.Key - 1;
            if (index < 0 || index >= BattleLoadoutCatalog.Skills.Length) continue;
            SkillData skill = BattleLoadoutCatalog.Skills[index];
            var data = entry.Value;
            if (TryNumber(data, "cooldown", out double cooldown))
            {
                skill.cooldown = Mathf.Clamp((float)cooldown, 1f, 120f);
                int cut = skill.description.LastIndexOf("\nCooldown ", StringComparison.Ordinal);
                if (cut >= 0) skill.description = skill.description.Substring(0, cut)
                    + "\nCooldown " + skill.cooldown.ToString("0.#", CultureInfo.InvariantCulture) + " sec";
                applied++;
            }
            // เฉพาะสกิลที่มีดาเมจ (STUN 0, NOVA 2, SEEKER 3)
            if ((index == 0 || index == 2 || index == 3) && TryNumber(data, "damage", out double damage))
            {
                skillDamage[index] = Mathf.Clamp((float)damage, 0f, 150f);
                applied++;
            }
        }
        AppliedValues = applied;
        if (applied > 0) Debug.Log("RemoteCatalog: applied " + applied + " ship/skill values from Firebase.");
    }

    // อ่านรายการจาก Firebase: key เป็นตัวเลข 1..n (Firebase อาจส่งมาเป็น Dictionary หรือ List ที่ช่อง 0 ว่าง)
    private static IEnumerable<KeyValuePair<int, IDictionary<string, object>>> Entries(object value)
    {
        if (value is IDictionary<string, object> map)
        {
            foreach (var pair in map)
                if (int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int key) && pair.Value is IDictionary<string, object> data)
                    yield return new KeyValuePair<int, IDictionary<string, object>>(key, data);
        }
        else if (value is IList list)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] is IDictionary<string, object> data)
                    yield return new KeyValuePair<int, IDictionary<string, object>>(i, data);
        }
    }

    private static bool TryNumber(IDictionary<string, object> data, string key, out double number)
    {
        number = 0;
        if (data == null || !data.TryGetValue(key, out object value) || value == null) return false;
        if (!double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return false;
        return !double.IsNaN(number) && !double.IsInfinity(number);
    }
}
