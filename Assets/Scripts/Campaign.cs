// Campaign.cs — ด่านเนื้อเรื่องเล่นคนเดียว 10 ด่าน (ห้ามสลับ 5 ด่านแรก: มีเซฟผู้เล่นเดิม)
// เล่นผ่านปุ่ม VS BOT เมื่อเลือกโหมด CAMPAIGN (ปุ่มจำนวนบอทหน้าหลัก) — เล่นด่านถัดไปที่ยังไม่ผ่านอัตโนมัติ
// ผ่านด่าน = ได้เหรียญ (ผ่านครั้งแรกได้เต็ม ครั้งต่อไปได้ 25%) และปลดล็อกด่านถัดไป (เก็บใน Progression: campaignStage)
// แก้เนื้อเรื่อง/ความยากได้ที่รายการ Stages ด้านล่าง
public class CampaignStageDef
{
    public StoryObjective objective;
    public int duration = 60; // วินาทีของด่านอยู่รอด/คุ้มกัน
    public int generators = 3;
    public string title; // ชื่อด่านที่แสดงบนจอ (ภาษาอังกฤษ)
    public string briefing;     // ข้อความเล่าเรื่องตอนเริ่มด่าน (ภาษาไทย)
    public int bots;            // จำนวนบอทศัตรู (ไม่รวมบอส)
    public int difficulty;      // 1 ง่าย 2 กลาง 3 ยาก
    public int kills;           // Kill ที่ต้องทำเพื่อผ่าน (ด่านบอส = ทำลายบอส)
    public int map;             // 0 แมงกะพรุน 1 ปริซึม 2 หุ่นยนต์ 3 สถานี 4 ลาวา
    public int MapIndex => map < GameplayManager.MapCount ? map : 2;
    public bool boss; // true = ด่านบอส ต้องทำลายยานแม่ DREADNOUGHT เพื่อผ่าน
    public int reward;          // เหรียญเมื่อผ่านครั้งแรก
}

// รายการด่าน Campaign และตัวช่วยเลือกด่านถัดไปจากความคืบหน้าใน Progression
public static class Campaign
{
    // รายการ 10 ด่าน เรียงตามเซฟเดิม; เพิ่มด่านใหม่ต่อท้าย ห้ามสลับเลขด่าน
    public static readonly CampaignStageDef[] Stages =
    {
        new CampaignStageDef { title = "FIRST CONTACT", briefing = "หน่วยลาดตระเวนศัตรูบุกเข้ามาในเขตแมงกะพรุนสายฟ้า ทำลายให้ได้ 3 ลำ!",
            bots = 1, difficulty = 1, kills = 3, map = 0, reward = 150 },
        new CampaignStageDef { title = "TWIN RAIDERS", briefing = "โจรอวกาศสองลำซุ่มอยู่ในทุ่งเสาปริซึม ระวังโดนรุม! ทำลาย 4 ลำ",
            bots = 2, difficulty = 1, kills = 4, map = 1, reward = 250 },
        new CampaignStageDef { title = "AMBUSH", briefing = "กองกำลังศัตรูวางกับดักในสุสานหุ่นยนต์ ฝ่าวงล้อม 3 ลำ ทำลายให้ได้ 5 ลำ",
            bots = 3, difficulty = 2, kills = 5, map = 2, reward = 350 },
        new CampaignStageDef { title = "ELITE SQUADRON", briefing = "หน่วยรบพิเศษของศัตรูมาถึงแล้ว พวกมันเก่งกว่าที่เคยเจอ ทำลาย 6 ลำ",
            bots = 3, difficulty = 3, kills = 6, map = 1, reward = 500 },
        new CampaignStageDef { title = "THE DREADNOUGHT", briefing = "ยานแม่ DREADNOUGHT ปรากฏตัว! ทำลายบอสให้ได้เพื่อปกป้องดวงดาว",
            bots = 2, difficulty = 2, kills = 0, map = 2, boss = true, reward = 1000 },
        new CampaignStageDef { title = "STATION BREACH", briefing = "ศัตรูยึดสถานีอวกาศ! ใช้ซากแท่นและประตูเป็นที่กำบัง แล้วทำลายศัตรู 7 ลำ",
            bots = 3, difficulty = 2, kills = 7, map = 3, reward = 650 },
        new CampaignStageDef { title = "EMBER PATROL", briefing = "ตามรอยกองยานเข้าสู่ทุ่งลาวา ใช้หินบังแนวยิงและทำลายหน่วยลาดตระเวน 8 ลำ",
            bots = 3, difficulty = 3, kills = 8, map = 4, reward = 750 },
        new CampaignStageDef { title = "INFERNO CRUISER", briefing = "ยานบัญชาการ INFERNO เฝ้าเส้นทางลาวา! ฝ่าลูกน้องคุ้มกันแล้วทำลายแกนกลางบอส",
            bots = 2, difficulty = 3, kills = 0, map = 4, boss = true, reward = 1200 },
        new CampaignStageDef { title = "VOID BLOCKADE", briefing = "กองยานม่วงปิดล้อมสถานี! หลบหลังโครงสร้างและทำลายแนวปิดล้อมให้ได้ 10 ลำ",
            bots = 4, difficulty = 3, kills = 10, map = 3, reward = 900 },
        new CampaignStageDef { title = "VOID MOTHERSHIP", briefing = "ยานแม่แห่งความว่างเปล่ามาถึงสุสานหุ่นยนต์! ใช้ซากหุ่นบังการโจมตีแล้วจบสงคราม",
            bots = 3, difficulty = 3, kills = 0, map = 2, boss = true, reward = 1500 },
        new CampaignStageDef { title = "LAST SIGNAL", briefing = "รักษาสัญญาณขอความช่วยเหลือ! อยู่รอด 70 วินาที ใช้สิ่งกีดขวางหลบแนวยิง ศัตรูเกิดใหม่เรื่อย ๆ",
            bots = 3, difficulty = 2, kills = 1, map = 0, objective = StoryObjective.Survive, duration = 70, reward = 1000 },
        new CampaignStageDef { title = "REACTOR SABOTAGE", briefing = "ทำลายเครื่องกำเนิดพลังงานศัตรู 3 จุด! ยิงแกนพลังงานสีแดงเพื่อปลดระบบปิดล้อม",
            bots = 3, difficulty = 2, kills = 1, map = 3, objective = StoryObjective.Generators, generators = 3, reward = 1100 },
        new CampaignStageDef { title = "RESCUE CONVOY", briefing = "คุ้มกันยานกู้ภัยสีฟ้า! อยู่ใกล้ยานไม่เกิน 10 หน่วยเพื่อซ่อมให้ครบ 60 วินาที ห้ามยานกู้ภัยถูกทำลาย",
            bots = 3, difficulty = 2, kills = 1, map = 1, objective = StoryObjective.Escort, duration = 60, reward = 1300 },
    };

    public static int Count => Stages.Length; // จำนวนด่าน Campaign ทั้งหมด
    // คืนข้อมูลด่าน stage (นับจาก 1) บีบให้อยู่ในช่วงด่านที่มี
    public static CampaignStageDef Get(int stage) => Stages[UnityEngine.Mathf.Clamp(stage, 1, Stages.Length) - 1];
    // ด่านที่ผ่านแล้วสูงสุด (0 = ยังไม่ผ่านเลย)
    public static int Cleared => Progression.Data != null ? Progression.Data.campaignStage : 0;
    // ด่านที่จะเล่นต่อ (ผ่านหมดแล้ว = เล่นด่านสุดท้ายซ้ำ)
    public static int NextStage => UnityEngine.Mathf.Clamp(Cleared + 1, 1, Stages.Length);
}
