// จุดรวมค่าของยานและสกิล: เกมและหน้าคลังอ่านจากข้อมูลชุดเดียวกัน
// atk = ดาเมจต่อกระสุนหนึ่งนัด, spd = หน่วยโลกต่อวินาที
// shotInterval = วินาทีระหว่างนัด (ยิ่งน้อยยิงยิ่งเร็ว), cooldown = วินาทีรอสกิล
// อย่าสลับลำดับในอาร์เรย์: index ถูกบันทึกใน Firebase และส่งผ่าน Photon
[System.Serializable]
public class ShipData
{
    public string name;
    public int hp;
    public float atk;
    public float spd;
    public string skill;
    public int price;
    public string spritePath;
    public float shotInterval, acceleration, turnSpeed;

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
    }
}

[System.Serializable]
public class SkillData
{
    public string name;
    public string description;
    public string iconPath;
    public float cooldown;

    public SkillData(string name, string desc, string iconPath, float cooldown = 10f)
    {
        this.name = name;
        this.description = desc + "\nCooldown " + cooldown.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " sec";
        this.iconPath = iconPath;
        this.cooldown = cooldown;
    }
}



// Gameplay values are intentionally preserved here; balance changes now have one source.
public static class BattleLoadoutCatalog
{
    public static readonly ShipData[] Ships = {
        new ShipData("Nebula Ghost", 90, 6f, 8.4f, "STUN", 0, "Images/ship1", .2f, 25f, 14f),
        new ShipData("Comet Crusher", 150, 12f, 5.6f, "SHIELD", 2800, "Images/ship2", .44f, 14f, 8f),
        new ShipData("Stellar Striker", 115, 8f, 7f, "NOVA", 3089, "Images/ship3", .28f, 20f, 11f)
    };
    public static readonly SkillData[] Skills = {
        new SkillData("STUN", "Paralyze wave", "Images/icon_stun", 12f),
        new SkillData("SHIELD", "Invincibility bubble", "Images/icon_shield", 16f),
        new SkillData("NOVA", "Area explosion", "Images/icon_nova", 12f),
        new SkillData("SEEKER", "Homing missile", "Images/icon_seeker", 11f)
    };
    public static int ValidShip(int index) => index >= 0 && index < Ships.Length ? index : 0;
    public static int ValidSkill(int index) => index >= 0 && index < Skills.Length ? index : 0;
}
