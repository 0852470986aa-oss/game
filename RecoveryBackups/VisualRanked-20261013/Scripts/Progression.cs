// Progression.cs — ระบบความก้าวหน้าของผู้เล่น (เฟส 4)
// เลเวล + XP / ภารกิจรายวัน 3 ข้อ + รายสัปดาห์ 3 ข้อ / รางวัลล็อกอิน 7 วัน / Achievement + ฉายา / สถิติ / ประวัติ 15 แมตช์ล่าสุด
// ข้อมูลทั้งหมดอยู่ในคลาส PlayerProgress แปลงเป็น JSON ด้วย JsonUtility แล้วเก็บ 2 ที่:
//   1) Firebase: users/{uid}/progress_json (FirebaseManager.LoadProgressJson / SaveProgressJson)
//   2) ในเครื่อง: PlayerPrefs "progress_{uid}" (ใช้ตอนไม่มีเน็ต/ทดสอบใน Editor) — โหลดแล้วเลือกก้อนที่บันทึกล่าสุด (savedAt)
// เรียกใช้: LobbyManager.Progress.cs (หน้าภารกิจ/โปรไฟล์) และ GameplayManager.Progress.cs (บันทึกผลจบแมตช์)
// ปิดได้ด้วย FeatureFlags.Progression / Missions / DailyLogin / Achievements
using System;
using System.Collections.Generic;
using UnityEngine;

// ความคืบหน้าของภารกิจหนึ่งข้อ (id ตรงกับ MissionDef.id) และรับรางวัลแล้วหรือยัง
[Serializable]
public class MissionState
{
    public string id; // รหัสภารกิจ ตรงกับ MissionDef.id
    public int progress; // ความคืบหน้าที่ทำได้แล้ว
    public bool claimed; // true = รับรางวัลแล้ว
}

// ประวัติแมตช์หนึ่งรายการ (เก็บ 15 แมตช์ล่าสุดใน PlayerProgress.history แสดงในหน้าโปรไฟล์)
[Serializable]
public class MatchRecord
{
    public string date;     // วันเวลา (เวลาเครื่อง)
    public string mode;     // 1V1 / FFA / TEAM
    public string result;   // WIN / LOSE / DRAW / #3 of 8
    public int kills; // จำนวน Kill ในแมตช์
    public int deaths; // จำนวนครั้งที่ตายในแมตช์
    public string map; // ชื่อแม็พที่เล่น
    public int coins; // เหรียญที่ได้จากแมตช์
    public int xp; // XP ที่ได้จากแมตช์
    public bool online; // true = แมตช์ออนไลน์
}

// ข้อมูลความก้าวหน้าทั้งหมดของผู้เล่น (เลเวล/สถิติ/ภารกิจ/ล็อกอิน/Achievement/ประวัติ) แปลงเป็น JSON ทั้งก้อน
[Serializable]
public class PlayerProgress
{
    public int level = 1; // เลเวลปัจจุบันของผู้เล่น
    public int xp;              // XP สะสมในเลเวลปัจจุบัน
    public int totalXp; // XP สะสมทั้งหมดตั้งแต่เริ่มเล่น
    public int matches, wins, losses, draws, kills, deaths; // สถิติรวม: แมตช์ ชนะ แพ้ เสมอ Kill และตาย
    public int onlineMatches, ffaWins, teamWins, hardBotWins, flawlessWins, skillsUsed; // สถิติใช้ปลด Achievement: ออนไลน์ ชนะแต่ละแบบ และการใช้สกิล
    public int winStreak, bestStreak; // ชนะติดต่อกันตอนนี้ และสถิติสูงสุด
    public string dailyId = ""; // รหัสวันของภารกิจรายวันชุดปัจจุบัน
    public List<MissionState> daily = new List<MissionState>(); // ภารกิจรายวัน 3 ข้อของวันนี้
    public string weeklyId = ""; // รหัสสัปดาห์ของภารกิจรายสัปดาห์ชุดปัจจุบัน
    public List<MissionState> weekly = new List<MissionState>(); // ภารกิจรายสัปดาห์ของสัปดาห์นี้
    public string lastLoginClaim = ""; // วันที่รับรางวัลล็อกอินล่าสุด
    public int loginStreak; // จำนวนวันที่ล็อกอินต่อเนื่อง (วนรอบ 7 วัน)
    public List<string> achievements = new List<string>(); // รหัส Achievement ที่ปลดแล้ว
    public string title = "RECRUIT"; // ฉายาที่ผู้เล่นเลือกโชว์
    public List<MatchRecord> history = new List<MatchRecord>(); // ประวัติแมตช์ล่าสุด (สูงสุด 15 แมตช์)
    public int campaignStage;   // ด่าน Campaign ที่ผ่านแล้วสูงสุด (เฟส 7B)
    public long savedAt; // เวลาบันทึกล่าสุด ใช้เลือกข้อมูลที่ใหม่กว่า
}

// ข้อมูลผลแมตช์ที่ส่งมาจาก GameplayManager ตอนจบเกม
public class MatchReport
{
    public string mode = "1V1"; // โหมดที่เล่น: 1V1 / FFA / TEAM
    public bool won, draw, online, vsBots, hardBot; // ผลแมตช์และประเภทแมตช์ (ออนไลน์ / เล่นกับบอท / บอทยาก)
    public int kills, deaths, place = 1, count = 2, skills, coins; // Kill, ตาย, อันดับ, จำนวนผู้เล่น, สกิลที่ใช้, เหรียญที่ได้
    public string map = ""; // ชื่อแม็พที่เล่น
}

// ผลที่ได้จากแมตช์ (แสดงในหน้าผล)
public class ProgressGain
{
    public int xp; // XP ที่ได้จากแมตช์นี้
    public int levelsGained; // จำนวนเลเวลที่เพิ่มขึ้น
    public int levelCoins; // เหรียญรางวัลจากการเลเวลอัป
    public int newLevel; // เลเวลใหม่หลังจบแมตช์
    public List<string> newAchievements = new List<string>(); // ชื่อ Achievement ที่เพิ่งปลด
    public int missionsReady; // จำนวนภารกิจที่รับรางวัลได้
}

// ตัวจัดการระบบความก้าวหน้า แบบ static: โหลด/บันทึก PlayerProgress และคิด XP ภารกิจ รางวัล
public static class Progression
{
    // ===== ข้อมูลตั้งต้น (แก้ตัวเลขได้ที่นี่) =====
    public const int MaxLevel = 50;
    // XP ที่ต้องใช้จากเลเวล n ไป n+1
    public static int XpToNext(int level) => 100 + 50 * (Mathf.Max(1, level) - 1);
    // เหรียญเมื่อเลเวลอัป (ไปถึงเลเวล level)
    public static int LevelUpCoins(int level) => 50 + level * 10;
    // ฉายาที่ปลดล็อกตามเลเวล
    public static readonly (int level, string title)[] LevelTitles =
    {
        (1, "RECRUIT"), (5, "CADET"), (10, "STAR CAPTAIN"), (20, "COMMANDER"), (30, "ADMIRAL"), (50, "LEGEND OF THE STARS")
    };
    // รางวัลล็อกอินวันที่ 1-7 (วันที่ 7 ได้มากสุด แล้ววนใหม่)
    public static readonly int[] LoginCoins = { 30, 40, 50, 60, 80, 100, 200 };

    // นิยามภารกิจ: ข้อความ เป้าหมาย รางวัลเหรียญ/XP และฟังก์ชัน count ที่บอกว่าแมตช์หนึ่งนับได้กี่หน่วย
    public class MissionDef
    {
        public string id, text; // รหัสภารกิจ และข้อความที่แสดง
        public int goal, coins, xp; // เป้าหมาย, เหรียญรางวัล, XP รางวัล
        public Func<MatchReport, int> count; // ฟังก์ชันนับว่าแมตช์หนึ่งได้ความคืบหน้ากี่หน่วย
        // สร้างนิยามภารกิจ (ใช้ใน DailyPool / WeeklyPool)
        public MissionDef(string id, string text, int goal, int coins, int xp, Func<MatchReport, int> count)
        { this.id = id; this.text = text; this.goal = goal; this.coins = coins; this.xp = xp; this.count = count; }
    }

    // ภารกิจรายวัน (สุ่มวันละ 3 ข้อจากรายการนี้ ทุกเครื่องได้ชุดเดียวกันในวันเดียวกัน)
    public static readonly MissionDef[] DailyPool =
    {
        new MissionDef("PLAY3", "Play 3 matches", 3, 40, 40, r => 1),
        new MissionDef("WIN1", "Win 1 match", 1, 60, 50, r => r.won ? 1 : 0),
        new MissionDef("KILL10", "Destroy 10 enemy ships", 10, 50, 50, r => r.kills),
        new MissionDef("SKILL8", "Use your skill 8 times", 8, 40, 40, r => r.skills),
        new MissionDef("ONLINE2", "Play 2 online matches", 2, 60, 50, r => r.online ? 1 : 0),
        new MissionDef("TOP3", "Finish top 3 in Free-for-all", 1, 60, 60, r => r.mode == "FFA" && r.place <= 3 ? 1 : 0),
        new MissionDef("TEAMWIN", "Win a team battle", 1, 70, 60, r => r.mode == "TEAM" && r.won ? 1 : 0),
        new MissionDef("CLEAN", "Win with 2 deaths or fewer", 1, 60, 60, r => r.won && r.deaths <= 2 ? 1 : 0),
    };

    // ภารกิจรายสัปดาห์ (ชุดเดิมทุกสัปดาห์)
    public static readonly MissionDef[] WeeklyPool =
    {
        new MissionDef("W_PLAY20", "Play 20 matches this week", 20, 300, 300, r => 1),
        new MissionDef("W_KILL60", "Destroy 60 ships this week", 60, 300, 300, r => r.kills),
        new MissionDef("W_WIN10", "Win 10 matches this week", 10, 400, 350, r => r.won ? 1 : 0),
    };

    // นิยาม Achievement: ชื่อ คำอธิบาย ฉายาที่ได้ และเงื่อนไข unlocked (เช็กจาก PlayerProgress + ผลแมตช์)
    public class AchievementDef
    {
        public string id, name, text, title; // รหัส, ชื่อ, คำอธิบาย และฉายาที่ได้
        public Func<PlayerProgress, MatchReport, bool> unlocked; // เงื่อนไขปลด Achievement
        // สร้างนิยาม Achievement (ใช้ในรายการ Achievements)
        public AchievementDef(string id, string name, string text, string title, Func<PlayerProgress, MatchReport, bool> unlocked)
        { this.id = id; this.name = name; this.text = text; this.title = title; this.unlocked = unlocked; }
    }

    // Achievement 12 รายการ (ปลดแล้วได้ฉายาไว้ตั้งโชว์)
    public static readonly AchievementDef[] Achievements =
    {
        new AchievementDef("FIRST_BLOOD", "First Blood", "Destroy your first ship", "ROOKIE", (p, r) => p.kills >= 1),
        new AchievementDef("FIRST_WIN", "First Victory", "Win your first match", "VICTOR", (p, r) => p.wins >= 1),
        new AchievementDef("KILLS_100", "Ace Pilot", "Destroy 100 ships", "ACE", (p, r) => p.kills >= 100),
        new AchievementDef("KILLS_500", "Star Destroyer", "Destroy 500 ships", "STAR DESTROYER", (p, r) => p.kills >= 500),
        new AchievementDef("WINS_25", "Veteran", "Win 25 matches", "VETERAN", (p, r) => p.wins >= 25),
        new AchievementDef("MATCHES_50", "Frequent Flyer", "Play 50 matches", "FREQUENT FLYER", (p, r) => p.matches >= 50),
        new AchievementDef("STREAK_5", "Unstoppable", "Win 5 matches in a row", "UNSTOPPABLE", (p, r) => p.bestStreak >= 5),
        new AchievementDef("FLAWLESS", "Untouchable", "Win a match without dying", "UNTOUCHABLE", (p, r) => p.flawlessWins >= 1),
        new AchievementDef("FFA_CHAMP", "Last Pilot Standing", "Win a Free-for-all", "LAST PILOT STANDING", (p, r) => p.ffaWins >= 1),
        new AchievementDef("TEAM_10", "Squad Leader", "Win 10 team battles", "SQUAD LEADER", (p, r) => p.teamWins >= 10),
        new AchievementDef("HARD_BOT", "Machine Breaker", "Beat a HARD bot", "MACHINE BREAKER", (p, r) => p.hardBotWins >= 1),
        new AchievementDef("LEVEL_10", "Rising Star", "Reach level 10", "RISING STAR", (p, r) => p.level >= 10),
    };

    // ===== สถานะ =====
    public static PlayerProgress Data { get; private set; }
    // true = โหลดข้อมูลความก้าวหน้าเสร็จแล้ว
    public static bool Loaded { get; private set; }
    public static event Action Changed; // แจ้งเมื่อข้อมูลเปลี่ยน ให้ UI อัปเดต
    // นับการใช้สกิลของแมตช์ปัจจุบัน (PlayerController.Skills เพิ่ม, GameplayManager รีเซ็ต)
    public static int SkillsThisMatch;
    private static string loadedFor;

    public static int Level => Data != null ? Data.level : 1; // เลเวลปัจจุบัน (ยังไม่โหลด = 1)
    public static string Title => Data != null && !string.IsNullOrEmpty(Data.title) ? Data.title : "RECRUIT"; // ฉายาที่โชว์อยู่ (ไม่มี = RECRUIT)
    public static bool Enabled => FeatureFlags.Progression; // true = เปิดระบบความก้าวหน้า

    private static string Uid => FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn() ? FirebaseManager.Instance.GetUserId() : "local"; // uid ผู้เล่น Firebase (ไม่ได้ล็อกอิน = local)
    private static string PrefsKey => "progress_" + Uid; // key PlayerPrefs สำหรับเก็บข้อมูลในเครื่อง

    // โหลดจากเครื่องทันที แล้วลองโหลดจาก Firebase (ถ้าใหม่กว่าใช้ของ Firebase)
    public static void Load(Action done = null)
    {
        LoadLocal();
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsLoggedIn()) { Loaded = true; Changed?.Invoke(); done?.Invoke(); return; }
        string uid = Uid;
        FirebaseManager.Instance.LoadProgressJson(json =>
        {
            if (uid != Uid) return;
            var remote = Parse(json);
            if (remote != null && remote.savedAt >= Data.savedAt) Data = remote;
            Normalize();
            Loaded = true;
            Changed?.Invoke();
            done?.Invoke();
        });
    }

    // โหลดเฉพาะในเครื่อง (ใช้เมื่อเข้าฉากต่อสู้ตรงๆ ใน Editor โดยไม่ผ่านล็อบบี้)
    public static void EnsureLoaded()
    {
        if (Data == null || loadedFor != Uid) LoadLocal();
    }

    // อ่านข้อมูลจาก PlayerPrefs ของบัญชีปัจจุบัน (ไม่มี = เริ่มใหม่) แล้ว Normalize
    private static void LoadLocal()
    {
        loadedFor = Uid;
        Data = Parse(PlayerPrefs.GetString(PrefsKey, "")) ?? new PlayerProgress();
        Normalize();
    }

    // แปลง JSON เป็น PlayerProgress (ว่าง/เสีย = null)
    private static PlayerProgress Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<PlayerProgress>(json); }
        catch (Exception) { return null; }
    }

    // เติมค่าที่ขาด และหมุนภารกิจเมื่อขึ้นวัน/สัปดาห์ใหม่
    private static void Normalize()
    {
        if (Data.daily == null) Data.daily = new List<MissionState>();
        if (Data.weekly == null) Data.weekly = new List<MissionState>();
        if (Data.achievements == null) Data.achievements = new List<string>();
        if (Data.history == null) Data.history = new List<MatchRecord>();
        Data.level = Mathf.Clamp(Data.level, 1, MaxLevel);
        RollPeriods();
    }

    // บันทึก: ประทับเวลา savedAt เขียนลง PlayerPrefs และ Firebase (ส่งเลเวล/totalXp ไปด้วย) แล้วยิง Changed
    public static void Save()
    {
        if (Data == null) return;
        Data.savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string json = JsonUtility.ToJson(Data);
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn())
            FirebaseManager.Instance.SaveProgressJson(json, Data.level, Data.totalXp);
        Changed?.Invoke();
    }

    // ทดสอบเท่านั้น (เมนู Test/ บน LobbyManager ใน Editor): ตั้งเลเวลตรง ๆ XP ในเลเวลเริ่มที่ 0 ไม่ให้เหรียญรางวัลเลเวล
    // totalXp ปรับให้เท่ากับ XP รวมที่ต้องใช้ถึงเลเวลนั้น แล้วบันทึกลงเครื่อง + Firebase ตามปกติ
    public static void SetLevelForTesting(int level)
    {
        EnsureLoaded();
        if (Data == null) return;
        Data.level = Mathf.Clamp(level, 1, MaxLevel);
        Data.xp = 0;
        int total = 0;
        for (int l = 1; l < Data.level; l++) total += XpToNext(l);
        Data.totalXp = total;
        Save();
    }

    // ===== ช่วงเวลา (วัน / สัปดาห์) =====
    public static string TodayId => DateTime.Now.ToString("yyyyMMdd");
    public static string WeekId
    {
        get
        {
            var now = DateTime.Now;
            int week = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return now.Year + "W" + week;
        }
    }
    public static TimeSpan UntilTomorrow => DateTime.Today.AddDays(1) - DateTime.Now; // เวลาที่เหลือจนถึงเที่ยงคืน (รีเซ็ตภารกิจรายวัน)

    // ขึ้นวันใหม่ = สุ่มภารกิจรายวัน 3 ข้อใหม่ (seed ตามวันที่) / ขึ้นสัปดาห์ใหม่ = รีเซ็ตภารกิจรายสัปดาห์
    private static void RollPeriods()
    {
        if (Data.dailyId != TodayId || Data.daily.Count != 3)
        {
            Data.dailyId = TodayId;
            Data.daily.Clear();
            // สุ่ม 3 ข้อแบบเดิมทุกครั้งในวันเดียวกัน (seed = วันที่)
            var rng = new System.Random(int.Parse(TodayId));
            var pool = new List<MissionDef>(DailyPool);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int pick = rng.Next(pool.Count);
                Data.daily.Add(new MissionState { id = pool[pick].id });
                pool.RemoveAt(pick);
            }
        }
        if (Data.weeklyId != WeekId || Data.weekly.Count != WeeklyPool.Length)
        {
            Data.weeklyId = WeekId;
            Data.weekly.Clear();
            foreach (var def in WeeklyPool) Data.weekly.Add(new MissionState { id = def.id });
        }
    }

    // หานิยามภารกิจจาก id ทั้งรายวันและรายสัปดาห์ (ไม่พบ = null)
    public static MissionDef FindMission(string id)
    {
        foreach (var def in DailyPool) if (def.id == id) return def;
        foreach (var def in WeeklyPool) if (def.id == id) return def;
        return null;
    }

    // จำนวนภารกิจที่ทำครบแล้วแต่ยังไม่กดรับ (+ รางวัลล็อกอินวันนี้)
    public static int ClaimableCount()
    {
        if (Data == null || !Enabled) return 0;
        RollPeriods();
        int count = 0;
        if (FeatureFlags.Missions)
            foreach (var list in new[] { Data.daily, Data.weekly })
                foreach (var state in list)
                {
                    var def = FindMission(state.id);
                    if (def != null && !state.claimed && state.progress >= def.goal) count++;
                }
        if (FeatureFlags.DailyLogin && CanClaimLogin) count++;
        return count;
    }

    // ===== XP / เลเวล =====
    // เพิ่ม XP แล้วคืนจำนวนเลเวลที่ขึ้น (เหรียญรางวัลเลเวลรวมไว้ใน coins)
    private static int AddXp(int amount, out int coins)
    {
        coins = 0;
        int levels = 0;
        Data.totalXp += amount;
        Data.xp += amount;
        while (Data.level < MaxLevel && Data.xp >= XpToNext(Data.level))
        {
            Data.xp -= XpToNext(Data.level);
            Data.level++;
            levels++;
            coins += LevelUpCoins(Data.level);
        }
        if (Data.level >= MaxLevel) Data.xp = 0;
        return levels;
    }

    // XP จากแมตช์: พื้นฐาน 20 + Kill ละ 6 + ชนะ 40 / เสมอ 20 + โบนัสอันดับ FFA (เล่นกับบอทได้ 60%)
    public static int MatchXp(MatchReport r)
    {
        int xp = 20 + r.kills * 6 + (r.won ? 40 : r.draw ? 20 : 0);
        if (r.mode == "FFA") xp += Mathf.Max(0, r.count - r.place) * 5;
        if (r.vsBots && !r.online) xp = Mathf.RoundToInt(xp * .6f);
        return Mathf.Clamp(xp, 5, 300);
    }

    // บันทึกผลแมตช์: สถิติ / XP / ภารกิจ / Achievement / ประวัติ แล้วบันทึกลง Firebase
    public static ProgressGain RecordMatch(MatchReport r)
    {
        var gain = new ProgressGain();
        if (!Enabled || r == null) return gain;
        EnsureLoaded();
        RollPeriods();
        // 1) สถิติ
        Data.matches++;
        if (r.online) Data.onlineMatches++;
        if (r.won) { Data.wins++; Data.winStreak++; Data.bestStreak = Mathf.Max(Data.bestStreak, Data.winStreak); }
        else if (r.draw) { Data.draws++; Data.winStreak = 0; }
        else { Data.losses++; Data.winStreak = 0; }
        Data.kills += r.kills;
        Data.deaths += r.deaths;
        Data.skillsUsed += r.skills;
        if (r.won && r.mode == "FFA") Data.ffaWins++;
        if (r.won && r.mode == "TEAM") Data.teamWins++;
        if (r.won && r.hardBot) Data.hardBotWins++;
        if (r.won && r.deaths == 0) Data.flawlessWins++;
        // 2) XP + เลเวล
        gain.xp = MatchXp(r);
        gain.levelsGained = AddXp(gain.xp, out gain.levelCoins);
        gain.newLevel = Data.level;
        if (gain.levelCoins > 0 && FirebaseManager.Instance != null) FirebaseManager.Instance.AddCoins(gain.levelCoins);
        // 3) ภารกิจ
        if (FeatureFlags.Missions)
            foreach (var list in new[] { Data.daily, Data.weekly })
                foreach (var state in list)
                {
                    var def = FindMission(state.id);
                    if (def == null || state.claimed) continue;
                    state.progress = Mathf.Min(def.goal, state.progress + Mathf.Max(0, def.count(r)));
                }
        // 4) Achievement
        if (FeatureFlags.Achievements) gain.newAchievements = CheckAchievements(r);
        // 5) ประวัติ (เก็บ 15 แมตช์ล่าสุด)
        Data.history.Insert(0, new MatchRecord
        {
            date = DateTime.Now.ToString("dd/MM HH:mm"), mode = r.mode,
            result = r.won ? "WIN" : r.draw ? "DRAW" : r.mode == "FFA" ? "#" + r.place + " of " + r.count : "LOSE",
            kills = r.kills, deaths = r.deaths, map = r.map, coins = r.coins + gain.levelCoins, xp = gain.xp, online = r.online
        });
        while (Data.history.Count > 15) Data.history.RemoveAt(Data.history.Count - 1);
        gain.missionsReady = ClaimableCount();
        Save();
        return gain;
    }

    // เช็ก Achievement ที่ยังไม่ปลด ถ้าผ่านเงื่อนไขให้ปลดล็อก แล้วคืนรายชื่อที่เพิ่งปลด
    private static List<string> CheckAchievements(MatchReport r)
    {
        var unlocked = new List<string>();
        foreach (var def in Achievements)
        {
            if (Data.achievements.Contains(def.id) || !def.unlocked(Data, r)) continue;
            Data.achievements.Add(def.id);
            unlocked.Add(def.name);
        }
        return unlocked;
    }

    // ===== รับรางวัลภารกิจ =====
    public static bool ClaimMission(MissionState state, out int coins, out int xp)
    {
        coins = xp = 0;
        var def = state != null ? FindMission(state.id) : null;
        if (def == null || state.claimed || state.progress < def.goal) return false;
        state.claimed = true;
        coins = def.coins;
        xp = def.xp;
        AddXp(xp, out int levelCoins);
        coins += levelCoins;
        if (FeatureFlags.Achievements) CheckAchievements(null);
        if (FirebaseManager.Instance != null) FirebaseManager.Instance.AddCoins(coins);
        Save();
        return true;
    }

    // ===== รางวัลล็อกอินรายวัน =====
    public static bool CanClaimLogin => Data != null && FeatureFlags.DailyLogin && Data.lastLoginClaim != TodayId;
    // วันที่จะได้รับถ้ากดวันนี้ (1-7): ล็อกอินต่อเนื่องจากเมื่อวาน = วันถัดไป ไม่งั้นเริ่มวันที่ 1
    public static int NextLoginDay
    {
        get
        {
            if (Data == null) return 1;
            string yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
            if (Data.lastLoginClaim == TodayId) return (Data.loginStreak - 1) % 7 + 1;
            return Data.lastLoginClaim == yesterday ? Data.loginStreak % 7 + 1 : 1;
        }
    }

    // รับรางวัลล็อกอินวันนี้: ต่อเนื่องจากเมื่อวานนับวันต่อ ไม่งั้นเริ่มวันที่ 1 แล้วเพิ่มเหรียญ
    // คืน false ถ้ารับไปแล้ววันนี้
    public static bool ClaimLogin(out int coins)
    {
        coins = 0;
        if (!CanClaimLogin) return false;
        string yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
        Data.loginStreak = Data.lastLoginClaim == yesterday ? Data.loginStreak + 1 : 1;
        Data.lastLoginClaim = TodayId;
        coins = LoginCoins[(Data.loginStreak - 1) % 7];
        if (FirebaseManager.Instance != null) FirebaseManager.Instance.AddCoins(coins);
        Save();
        return true;
    }

    // ===== ฉายา =====
    public static List<string> UnlockedTitles()
    {
        var titles = new List<string>();
        foreach (var entry in LevelTitles) if (Level >= entry.level) titles.Add(entry.title);
        if (Data != null)
            foreach (var def in Achievements)
                if (Data.achievements.Contains(def.id)) titles.Add(def.title);
        return titles;
    }

    // เปลี่ยนฉายาที่โชว์เป็นอันถัดไปในรายการที่ปลดล็อกแล้ว (วนกลับอันแรก) แล้วบันทึก
    public static void CycleTitle()
    {
        if (Data == null) return;
        var titles = UnlockedTitles();
        int index = titles.IndexOf(Title);
        Data.title = titles[(index + 1) % titles.Count];
        Save();
    }

    // ผ่านด่าน Campaign (เฟส 7B): คืน true ถ้าเป็นการผ่านครั้งแรก
    public static bool ClearCampaignStage(int stage)
    {
        EnsureLoaded();
        bool first = stage > Data.campaignStage;
        if (first) Data.campaignStage = stage;
        Save();
        return first;
    }
}
