// จุดปรับสมดุลสกิลและอันตรายแม็พ (หน่วยเวลาเป็นวินาที)
// ค่ายานและคูลดาวน์อยู่ใน BattleLoadoutCatalog.cs
// คงค่าจากก่อนจัดโค้ดทั้งหมด ไม่ใช่การปรับบาลานซ์รอบใหม่
public static class BattleBalance
{
    public const float StunDamage = 8f;
    public const float NovaDamage = 30f;
    public const float SeekerDamage = 22f;
    public const float StunSeconds = 1f;
    public const float ShieldSeconds = 2f;
    public const float ShieldSpeedMultiplier = 1.2f;
    public const float SlowSpeedMultiplier = .7f;
    public const float PoisonDelay = 1.5f;
    public const float PoisonDamagePerSecond = 2f;
    public const float CoreDamagePerSecond = 3f;
    public const float CoreFireRateMultiplier = 1.35f;
    public const float LavaDamage = 6f;
    public const float MeteorDamage = 18f;
    public const float TurretDamage = 6f;
    public const float TurretShotInterval = 2f;
}
