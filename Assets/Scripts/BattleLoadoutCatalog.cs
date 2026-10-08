// ไม่ได้อยู่ใน Scene (เป็นข้อมูล static) ใช้โดย PlayerController.InitializeStats, LobbyManager (หน้าคลัง/ห้อง), GameplayManager.HUD และ FirebaseManager
// มี 3 ส่วน: ShipData/SkillData (โครงข้อมูล), BattleLoadoutCatalog (รายการยาน/สกิล), ShipPaint (สีย้อมยาน)
// จุดรวมค่าของยานและสกิล: เกมและหน้าคลังอ่านจากข้อมูลชุดเดียวกัน
// atk = ดาเมจต่อกระสุนหนึ่งนัด, spd = หน่วยโลกต่อวินาที
// shotInterval = วินาทีระหว่างนัด (ยิ่งน้อยยิงยิ่งเร็ว), cooldown = วินาทีรอสกิล
// อย่าสลับลำดับในอาร์เรย์: index ถูกบันทึกใน Firebase และส่งผ่าน Photon
// ข้อมูลยาน 1 ลำ: ชื่อ, hp = HP สูงสุด, atk/spd ตามที่อธิบายด้านบน, skill = ชื่อสกิล, price = ราคา, spritePath = path รูปใน Resources
// shotInterval = วินาทีระหว่างนัด, acceleration = อัตราเร่งการขยับ, turnSpeed = ความไวการหมุนตอนเล็ง
[System.Serializable]
public class ShipData
{
    public string name; // ชื่อยานที่แสดงในหน้าคลังและสนามรบ
    public int hp; // HP สูงสุดของยานตอนเริ่มเกิด
    public float atk; // ดาเมจต่อกระสุนหนึ่งนัด
    public float spd; // ความเร็วเคลื่อนที่ (หน่วยโลกต่อวินาที)
    public string skill; // ชื่อสกิลประจำยาน ใช้จับคู่กับ SkillData
    public int price; // ราคาซื้อยานในร้านค้า
    public string spritePath; // path รูปยานใน Resources
    public float shotInterval, acceleration, turnSpeed; // วินาทีระหว่างนัด, อัตราเร่งการขยับ, ความไวการหมุนตอนเล็ง
    // เฟส 7: ชนิดอาวุธหลัก (BattleLoadoutCatalog.Weapon*), Prefab ที่ใช้ (ยานใหม่ใช้ Prefab ของยานเดิม), ภาพสำรองเมื่อยังไม่มีรูปใหม่, สีย้อมประจำยาน
    public int weapon;
    public int prefab;
    public string fallbackSprite;
    public UnityEngine.Color tint = UnityEngine.Color.white;

    // constructor: ค่า 3 ตัวท้ายมีค่าเริ่มต้นถ้าไม่ได้ระบุ
    public ShipData(string name, int hp, float atk, float spd, string skill, int price, string spritePath,
        float shotInterval = .2f, float acceleration = 18f, float turnSpeed = 10f)
    {
        this.name = name;
        this.hp = hp;
        this.atk = atk;
        this.spd = spd;
        this.skill = skill;
        this.price = price;
        this.spritePath = spritePath;
        this.shotInterval = shotInterval;
        this.acceleration = acceleration;
        this.turnSpeed = turnSpeed;
        this.prefab = -1;
    }

    // ตั้งค่าเพิ่มของยานใหม่ (เฟส 7)
    public ShipData With(int weapon, int prefab, string fallbackSprite, UnityEngine.Color tint)
    {
        this.weapon = weapon;
        this.prefab = prefab;
        this.fallbackSprite = fallbackSprite;
        this.tint = tint;
        return this;
    }
}

// ข้อมูลสกิล 1 แบบ: ชื่อ, คำอธิบาย (ต่อท้ายด้วยเวลาคูลดาวน์อัตโนมัติ), path ไอคอน, คูลดาวน์ (วินาที)
[System.Serializable]
public class SkillData
{
    public string name; // ชื่อสกิลที่แสดงบนปุ่มและหน้าคลัง
    public string description; // คำอธิบายสกิล (ต่อท้ายเวลาคูลดาวน์แล้ว)
    public string iconPath; // path ไอคอนสกิลใน Resources
    public float cooldown; // เวลารอใช้สกิลซ้ำ (วินาที)

    // constructor: สร้างคำอธิบายเป็น "<desc>\nCooldown X sec" โดยใช้ InvariantCulture ให้ทศนิยมเป็นจุดเสมอ
    public SkillData(string name, string desc, string iconPath, float cooldown = 10f)
    {
        this.name = name;
        this.description = desc + "\nCooldown " + cooldown.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " sec";
        this.iconPath = iconPath;
        this.cooldown = cooldown;
    }
}



// Gameplay values are intentionally preserved here; balance changes now have one source.
// รายการยาน/สกิลทั้งหมดของเกม (static) index ของยานคือ "ShipType" และ index ของสกิลคือ "SkillType" ใน Custom Properties
public static class BattleLoadoutCatalog
{
    // ยาน 0 Nebula Ghost (เร็ว เลือดน้อย), 1 Comet Crusher (ถึก ยิงช้า), 2 Stellar Striker (สมดุล)
    // ลำดับค่า: ชื่อ, hp, atk, spd, skill, price, sprite, shotInterval, acceleration, turnSpeed
    public static readonly ShipData[] Ships = {
        new ShipData("Nebula Ghost", 90, 6f, 8.4f, "STUN", 0, "Images/ship1", .2f, 25f, 14f),
        new ShipData("Comet Crusher", 150, 12f, 5.6f, "SHIELD", 2800, "Images/ship2", .44f, 14f, 8f),
        new ShipData("Stellar Striker", 115, 8f, 7f, "NOVA", 3089, "Images/ship3", .28f, 20f, 11f),
        // ===== เฟส 7: ยานใหม่ (ยังไม่มีรูป/Prefab ของตัวเอง: ใช้ Prefab ยานเดิม + ย้อมสีประจำยาน, ใส่รูป Images/ship4-6 แล้วจะเปลี่ยนเอง) =====
        // 3 Viper Lance = สไนเปอร์ กระสุนเร็วไกล แรงมาก ยิงช้า
        new ShipData("Viper Lance", 85, 20f, 7.4f, "BLINK", 3600, "Images/ship4", .85f, 22f, 13f)
            .With(WeaponSniper, 0, "Images/ship1", new UnityEngine.Color(.75f, 1f, .7f)),
        // 4 Aurora Warden = ปืนกล ยิงรัวดาเมจต่อนัดต่ำ ถึก
        new ShipData("Aurora Warden", 135, 4.2f, 6.4f, "HEAL", 3300, "Images/ship5", .11f, 17f, 10f)
            .With(WeaponGatling, 2, "Images/ship3", new UnityEngine.Color(.7f, .85f, 1f)),
        // 5 Hydra Scatter = ยิงกระจาย 3 นัด ระยะใกล้แรง
        new ShipData("Hydra Scatter", 125, 6.5f, 6.8f, "CLOAK", 4200, "Images/ship6", .55f, 18f, 10f)
            .With(WeaponSpread, 1, "Images/ship2", new UnityEngine.Color(1f, .75f, .95f))
    };

    // ===== อาวุธหลัก (เฟส 7) =====
    public const int WeaponStandard = 0, WeaponSpread = 1, WeaponSniper = 2, WeaponGatling = 3;
    public static readonly string[] WeaponNames = { "BLASTER", "SCATTER x3", "RAILGUN", "GATLING" };

    // ชื่อ Prefab ใน Resources (ยานใหม่ใช้ Prefab ของยานเดิม)
    public static string PrefabName(int ship)
    {
        ship = ValidShip(ship);
        int prefab = Ships[ship].prefab >= 0 ? Ships[ship].prefab : ship;
        return "ShipPrefabs/Ship" + (prefab + 1);
    }

    // รูปยาน: ใช้รูปของยานเอง ถ้ายังไม่มีใช้รูปสำรอง
    public static UnityEngine.Sprite ShipSprite(int ship)
    {
        ship = ValidShip(ship);
        var sprite = UnityEngine.Resources.Load<UnityEngine.Sprite>(Ships[ship].spritePath);
        if (sprite == null && !string.IsNullOrEmpty(Ships[ship].fallbackSprite)) sprite = UnityEngine.Resources.Load<UnityEngine.Sprite>(Ships[ship].fallbackSprite);
        return sprite;
    }
    // มีรูปของตัวเองหรือยัง (ยังไม่มี = ย้อมสีประจำยานให้แยกออก)
    public static bool HasOwnArt(int ship) => UnityEngine.Resources.Load<UnityEngine.Sprite>(Ships[ValidShip(ship)].spritePath) != null;
    // สีย้อมยาน: มีรูปของตัวเองแล้ว = ขาว (ไม่ย้อม) ยังไม่มี = ใช้สี tint ประจำยาน
    public static UnityEngine.Color ShipTint(int ship) => HasOwnArt(ship) ? UnityEngine.Color.white : Ships[ValidShip(ship)].tint;

    // สกิล index 0 STUN, 1 SHIELD, 2 NOVA, 3 SEEKER (ตรงกับ skillType ใน PlayerController) ค่าท้ายคือคูลดาวน์ (วินาที)
    public static readonly SkillData[] Skills = {
        new SkillData("STUN", "Paralyze wave", "Images/icon_stun", 12f),
        new SkillData("SHIELD", "Invincibility bubble", "Images/icon_shield", 16f),
        new SkillData("NOVA", "Area explosion", "Images/icon_nova", 12f),
        new SkillData("SEEKER", "Homing missile", "Images/icon_seeker", 11f),
        // ===== เฟส 7: สกิลใหม่ =====
        new SkillData("BLINK", "Teleport forward 9 units", "Images/icon_blink", 8f),
        new SkillData("HEAL", "Repair 35% of your hull", "Images/icon_heal", 14f),
        new SkillData("CLOAK", "Invisible to enemies for 3 sec (firing reveals you)", "Images/icon_cloak", 15f)
    };
    // ตรวจ index ที่รับมา (เช่นจาก Firebase/Photon) ถ้าเกินช่วงคืน 0 กันเกม error
    // ปิด FeatureFlags.NewShips / NewSkills = ยาน/สกิลเฟส 7 ใช้ไม่ได้ (กลับเป็นยาน/สกิลแรก)
    public static int ValidShip(int index) => index >= 0 && index < Ships.Length && (index < 3 || FeatureFlags.NewShips) ? index : 0;
    // ตรวจ index สกิล: เกินช่วง หรือเป็นสกิลเฟส 7 ตอนปิด FeatureFlags.NewSkills = คืน 0
    public static int ValidSkill(int index) => index >= 0 && index < Skills.Length && (index < 4 || FeatureFlags.NewSkills) ? index : 0;
}

// สีย้อมยาน: ใช้ภาพยานเดิมแล้วคูณสีทับ ไม่ต้องวาดภาพใหม่ และไม่เปลี่ยนค่าพลัง
// บันทึกในเครื่อง (PlayerPrefs) และส่งให้คู่แข่งเห็นผ่าน Photon property "ShipSkin"
// อย่าสลับลำดับ: index ถูกส่งผ่าน Photon
public static class ShipPaint
{
    // Property = ชื่อ key ใน Photon Custom Properties, PrefsKey = ชื่อ key ใน PlayerPrefs
    public const string Property = "ShipSkin";
    private const string PrefsKey = "ShipSkin";
    // ชื่อสี และค่าสีที่ใช้คูณกับภาพยาน (index ตรงกัน)
    public static readonly string[] Names = { "ORIGINAL", "CRIMSON", "AZURE", "EMERALD", "GOLD", "SHADOW" };
    public static readonly UnityEngine.Color[] Colors = {
        UnityEngine.Color.white,
        new UnityEngine.Color(1f, .55f, .55f),
        new UnityEngine.Color(.55f, .8f, 1f),
        new UnityEngine.Color(.6f, 1f, .62f),
        new UnityEngine.Color(1f, .85f, .4f),
        new UnityEngine.Color(.55f, .55f, .7f)
    };
    // ตรวจ index สี ถ้าเกินช่วงคืน 0 (ORIGINAL)
    public static int Valid(int index) => index >= 0 && index < Colors.Length ? index : 0;
    // สีที่ผู้เล่นเครื่องนี้เลือก: อ่าน/บันทึกใน PlayerPrefs
    public static int Local
    {
        get => Valid(UnityEngine.PlayerPrefs.GetInt(PrefsKey, 0));
        set { UnityEngine.PlayerPrefs.SetInt(PrefsKey, Valid(value)); UnityEngine.PlayerPrefs.Save(); }
    }
    // ค่าสีของผู้เล่นเครื่องนี้
    public static UnityEngine.Color LocalColor => Colors[Local];
    // สีของผู้เล่นใดๆ จาก Custom Property "ShipSkin" (ไม่มีค่า = สีขาว/สีเดิม) ใช้ใน PlayerController.ApplyPaint
    public static UnityEngine.Color For(Photon.Realtime.Player player)
        => player != null && player.CustomProperties.TryGetValue(Property, out object value) && value is int index
            ? Colors[Valid(index)] : UnityEngine.Color.white;
}
