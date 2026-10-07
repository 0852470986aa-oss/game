// FeatureFlags.cs — สวิตช์เปิด/ปิดฟีเจอร์ทั้งเกมรวมไว้ที่เดียว
// ถ้าฟีเจอร์ไหนมีปัญหา ตั้งเป็น false แล้วเกมกลับไปทำงานแบบเดิม (ปุ่มของฟีเจอร์นั้นจะซ่อน)
// ค่าเริ่มต้นอยู่ในไฟล์นี้ และแอดมินเขียนทับได้จาก Firebase: feature_flags/{ชื่อ} = true/false
// FirebaseManager โหลดค่าจาก Firebase ครั้งเดียวหลังล็อกอิน (LoadFeatureFlags)
using System.Collections.Generic;

public static class FeatureFlags
{
    // ระบบยิงแบบเบา: ส่งแค่คำสั่งยิงผ่าน RPC แล้วแต่ละเครื่องสร้างกระสุนเอง (false = กลับไปใช้ PhotonNetwork.Instantiate แบบเดิม)
    public static bool LightBullets = true;
    // ให้ Host ตั้งค่าห้องได้ (จำนวน Kill / เวลา / อันตรายในแม็พ) ในห้องรอ
    public static bool RoomSettings = true;
    // บอท / เล่นคนเดียว (เฟส 1)
    public static bool Bots = true;
    // ตีบวกยาน / ไอเท็ม ร้านค้า กล่องสุ่ม (เฟส 5)
    public static bool Upgrades = true;
    public static bool Items = true;
    // เลเวล + XP + หน้าโปรไฟล์ + ประวัติแมตช์ (เฟส 4)
    public static bool Progression = true;
    // แมตช์แรงค์ + MMR (เฟส 6)
    public static bool Ranked = true;
    // เฟส 2: ห้องหลายคน 2-10 คน (Free-for-all) / ตารางคะแนน / ข้อความใครฆ่าใคร
    public static bool MultiPlayer = true;
    public static bool Scoreboard = true;
    public static bool KillFeed = true;
    // เฟส 3: โหมดทีม 2v2 - 5v5 (ยิงเพื่อนไม่โดน)
    public static bool Teams = true;
    // เฟส 4: ภารกิจ / รางวัลล็อกอินรายวัน / Achievement (เลเวล+XP+โปรไฟล์ใช้สวิตช์ Progression)
    public static bool Missions = true;
    public static bool DailyLogin = true;
    public static bool Achievements = true;
    // เฟส 6: ตารางอันดับ / ฤดูกาลรายเดือน (แมตช์แรงค์ใช้สวิตช์ Ranked)
    public static bool Leaderboard = true;
    public static bool Seasons = true;
    // เฟส 7: ยานใหม่ / สกิลใหม่ / ไอเท็มเกิดในแม็พ / ประกาศ Kill Streak
    public static bool NewShips = true;
    public static bool NewSkills = true;
    public static bool PowerUps = true;
    public static bool KillStreaks = true;
    // เฟส 7B: โหมดเกมใหม่ (ยึดจุด/เก็บดาว/Survival/Battle Royale/Campaign) / หินลอยรอบเสาแม็พปริซึม
    public static bool GameModes = true;
    public static bool PillarRocks = true;
    // เฟส 8: เพื่อน+สถานะออนไลน์+ชวนเข้าห้อง / แชท / กิลด์
    public static bool Social = true;
    public static bool Chat = true;
    public static bool Guilds = true;
    // เฟส 9: หน้าตั้งค่าเพิ่มเติม (MORE OPTIONS) / ภาษาไทย-อังกฤษ / อ่านค่ายาน-สกิลจาก Firebase
    // กลับเข้าแมตช์เดิมเมื่อเน็ตหลุด (รอ 25 วิ) / Host ตรวจและตัดสินดาเมจแทนคนยิง (กันโกงครึ่งทาง)
    public static bool PlayerOptions = true;
    public static bool Language = true;
    // RemoteCatalog ค่าเริ่มต้นปิด: ข้อมูล spacecraft/skills ใน Firebase ถูกสร้างไว้นานแล้วอาจเป็นค่าเก่า
    // แอดมินตรวจ/แก้ค่าใน Console ให้ถูกก่อน แล้วตั้ง feature_flags/RemoteCatalog = true เพื่อเปิดใช้
    public static bool RemoteCatalog = false;
    public static bool BattleReconnect = true;
    public static bool HostDamage = true;
    // แม็พใหม่จากรูปชุดใหม่: แม็พ 4 สถานีอวกาศ / แม็พ 5 เนบิวลาลาวา (GameplayManager.NewMaps.cs)
    public static bool NewMaps = true;

    // เขียนทับค่าจาก Firebase (key ต้องตรงชื่อ field เช่น "LightBullets") ค่าที่ไม่ใช่ bool จะถูกข้าม
    public static void Apply(IDictionary<string, object> values)
    {
        if (values == null) return;
        foreach (var pair in values)
        {
            if (!(pair.Value is bool on)) continue;
            switch (pair.Key)
            {
                case nameof(LightBullets): LightBullets = on; break;
                case nameof(RoomSettings): RoomSettings = on; break;
                case nameof(Bots): Bots = on; break;
                case nameof(Upgrades): Upgrades = on; break;
                case nameof(Items): Items = on; break;
                case nameof(Progression): Progression = on; break;
                case nameof(Ranked): Ranked = on; break;
                case nameof(MultiPlayer): MultiPlayer = on; break;
                case nameof(Scoreboard): Scoreboard = on; break;
                case nameof(KillFeed): KillFeed = on; break;
                case nameof(Teams): Teams = on; break;
                case nameof(Missions): Missions = on; break;
                case nameof(DailyLogin): DailyLogin = on; break;
                case nameof(Achievements): Achievements = on; break;
                case nameof(Leaderboard): Leaderboard = on; break;
                case nameof(Seasons): Seasons = on; break;
                case nameof(NewShips): NewShips = on; break;
                case nameof(NewSkills): NewSkills = on; break;
                case nameof(PowerUps): PowerUps = on; break;
                case nameof(KillStreaks): KillStreaks = on; break;
                case nameof(GameModes): GameModes = on; break;
                case nameof(PillarRocks): PillarRocks = on; break;
                case nameof(Social): Social = on; break;
                case nameof(Chat): Chat = on; break;
                case nameof(Guilds): Guilds = on; break;
                case nameof(PlayerOptions): PlayerOptions = on; break;
                case nameof(Language): Language = on; break;
                case nameof(RemoteCatalog): RemoteCatalog = on; break;
                case nameof(BattleReconnect): BattleReconnect = on; break;
                case nameof(HostDamage): HostDamage = on; break;
                case nameof(NewMaps): NewMaps = on; break;
            }
        }
    }
}
