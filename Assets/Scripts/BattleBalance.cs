// ใช้โดย PlayerController(.Skills).cs, SkillController ผ่านข้อมูลที่ส่งตอนสร้าง, HazardController, AutoTurret และ FirebaseManager
// จุดปรับสมดุลสกิลและอันตรายแม็พ (หน่วยเวลาเป็นวินาที)
// ค่ายานและคูลดาวน์อยู่ใน BattleLoadoutCatalog.cs
// คงค่าจากก่อนจัดโค้ดทั้งหมด ไม่ใช่การปรับบาลานซ์รอบใหม่
// คลาส static เก็บค่าคงที่ (const) อย่างเดียว ไม่ต้องสร้าง object และไม่อยู่ใน Scene
public static class BattleBalance
{
    // ดาเมจของสกิล (หน่วย HP ต่อครั้งที่โดน): คลื่นสตัน, ระเบิดโนวา, มิสไซล์ติดตาม
    public const float StunDamage = 8f;
    public const float NovaDamage = 30f;
    public const float SeekerDamage = 22f;
    // สตันนาน 1 วินาที, โล่อยู่ 2 วินาที
    public const float StunSeconds = 1f;
    public const float ShieldSeconds = 2f;
    // ตัวคูณความเร็ว: มีโล่วิ่งเร็วขึ้น 1.2 เท่า, ช้า/อยู่ในบึงเหลือ 0.7 เท่า
    public const float ShieldSpeedMultiplier = 1.2f;
    public const float SlowSpeedMultiplier = .7f;
    // บึงพิษ: อยู่ครบ 1.5 วินาทีแล้วเริ่มเสีย 2 HP/วินาที
    public const float PoisonDelay = 1.5f;
    public const float PoisonDamagePerSecond = 2f;
    // Energy Core: เสีย 3 HP/วินาที แต่ยิงเร็วขึ้น 1.35 เท่า (fireCooldown หารด้วยค่านี้)
    public const float CoreDamagePerSecond = 3f;
    public const float CoreFireRateMultiplier = 1.35f;
    // ดาเมจอันตรายในแม็พ: ลาวา (ต่อครั้ง) และอุกกาบาต (ต่อครั้งที่โดน)
    public const float LavaDamage = 6f;
    public const float MeteorDamage = 18f;
    // ป้อมปืน: ดาเมจ 6 ต่อนัด ยิงทุก 2 วินาที
    public const float TurretDamage = 6f;
    public const float TurretShotInterval = 2f;
}
