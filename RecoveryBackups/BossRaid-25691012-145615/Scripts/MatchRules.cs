// MatchRules.cs — กติกาแมตช์ที่ Host ตั้งได้ในห้องรอ + ตัวช่วยนับคะแนนแบบหลายผู้เล่น
// ค่าตั้งห้องเก็บใน Photon Room Custom Properties ทุกเครื่องในห้องจึงอ่านได้ค่าเดียวกัน
// คะแนนผู้เล่นจริงเก็บใน Player Property "Kills" (เหมือนเดิม) — รองรับกี่คนก็ได้ ไม่ได้ผูกกับ 1v1
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

// ตัวช่วยอ่านกติกาห้อง/โหมด/ทีม และนับคะแนนจาก Photon Custom Properties (static ทั้งคลาส)
public static partial class MatchRules
{
    public const string KillTargetKey = "KillTarget"; // key ค่าตั้งห้อง: จำนวน Kill ที่ต้องได้เพื่อชนะ
    public const string MatchSecondsKey = "MatchSeconds"; // key ค่าตั้งห้อง: เวลาแมตช์ (วินาที)
    public const string HazardsKey = "Hazards"; // key ค่าตั้งห้อง: เปิด/ปิดอันตรายในแม็พ

    // ตัวเลือกที่ Host กดวนได้ในห้องรอ
    public static readonly int[] KillOptions = { 3, 5, 10 };
    public static readonly int[] TimeOptions = { 180, 300, 600 };

    public const int DefaultKillTarget = 3; // Kill เป้าหมายเริ่มต้นเมื่อห้องไม่ได้ตั้ง
    public const int DefaultMatchSeconds = 180; // เวลาแมตช์เริ่มต้น 3 นาที

    // อ่านค่าจากห้อง (ไม่มีค่า หรือปิด FeatureFlags.RoomSettings = ใช้ค่าเริ่มต้นแบบเดิม)
    public static int KillTarget(RoomInfo room) => ReadInt(room, KillTargetKey, DefaultKillTarget, 1, 99);
    // เวลาแมตช์ (วินาที) จำกัดช่วง 30-3600
    public static int MatchSeconds(RoomInfo room) => ReadInt(room, MatchSecondsKey, DefaultMatchSeconds, 30, 3600);
    // เปิดอันตรายในแม็พไหม (ค่าเริ่มต้น = เปิด ถ้าไม่มีค่า หรือปิด FeatureFlags.RoomSettings)
    public static bool Hazards(RoomInfo room)
    {
        if (!FeatureFlags.RoomSettings || room == null) return true;
        return !room.CustomProperties.TryGetValue(HazardsKey, out object value) || !(value is bool on) || on;
    }

    // ค่าถัดไปในรายการตัวเลือก (วนกลับต้นรายการ)
    public static int Next(int[] options, int current)
    {
        int index = System.Array.IndexOf(options, current);
        return options[(index + 1) % options.Length];
    }

    // แปลงวินาทีเป็นข้อความ นาที:วินาที เช่น 185 -> "3:05"
    public static string FormatTime(int seconds) => (seconds / 60) + ":" + (seconds % 60).ToString("00");

    // อ่านค่า int จาก Room Property key แล้วบีบให้อยู่ในช่วง min-max (ไม่มีค่า/ปิดฟีเจอร์ = fallback)
    private static int ReadInt(RoomInfo room, string key, int fallback, int min, int max)
    {
        if (!FeatureFlags.RoomSettings || room == null) return fallback;
        return room.CustomProperties.TryGetValue(key, out object value) && value is int number
            ? UnityEngine.Mathf.Clamp(number, min, max) : fallback;
    }

    // ===== นับคะแนน =====
    // Kill ของผู้เล่นคนหนึ่ง (อ่านจาก Player Property "Kills")
    public static int Kills(Player player)
        => player != null && player.CustomProperties.TryGetValue("Kills", out object value) && value is int kills ? kills : 0;

    // Kill ของเรา
    public static int LocalKills() => Kills(PhotonNetwork.LocalPlayer);

    // Kill สูงสุดของคู่แข่งทุกคน รวมบอท (1v1 = คู่แข่งคนเดียว, หลายคน = คนที่ได้มากสุด)
    public static int BestRivalKills()
    {
        int best = 0;
        foreach (var player in PhotonNetwork.PlayerListOthers) best = System.Math.Max(best, Kills(player));
        var room = PhotonNetwork.CurrentRoom;
        if (room != null)
            foreach (var entry in room.CustomProperties)
                if (entry.Key is string key && key.StartsWith(BotKillPrefix) && entry.Value is int kills)
                    best = System.Math.Max(best, kills);
        return best;
    }

    // ===== บอท =====
    // จำนวนบอทในแมตช์นี้ (Host ตั้งตอนเริ่มเกม) / ความยาก 0 = เป้าซ้อม, 1 ง่าย, 2 กลาง, 3 ยาก
    public const string BotCountKey = "BotCount";
    public const string BotDifficultyKey = "BotDifficulty";
    public const string BotFillKey = "BotFill";
    public const string SoloKey = "Solo";
    public const string TutorialKey = "Tutorial";
    public const string BotKillPrefix = "BK";
    public static readonly string[] DifficultyNames = { "TRAINING", "EASY", "NORMAL", "HARD" };

    // จำนวนบอทในห้อง (0 ถึง MaxCombatants-1) ปิด FeatureFlags.Bots = 0
    public static int BotCount(RoomInfo room)
        => FeatureFlags.Bots && room != null && room.CustomProperties.TryGetValue(BotCountKey, out object v) && v is int n ? UnityEngine.Mathf.Clamp(n, 0, MaxCombatants - 1) : 0;
    // ความยากบอทในห้อง 0-3 (ไม่มีค่า = 2 กลาง)
    public static int BotDifficulty(RoomInfo room)
        => room != null && room.CustomProperties.TryGetValue(BotDifficultyKey, out object v) && v is int n ? UnityEngine.Mathf.Clamp(n, 0, 3) : 2;
    // ห้องเปิดเติมบอทแทนที่ว่างไหม (ไม่ใช้ในห้องแรงค์)
    public static bool BotFill(RoomInfo room)
        => FeatureFlags.Bots && !IsRanked(room) && room != null && room.CustomProperties.TryGetValue(BotFillKey, out object v) && v is bool on && on;
    // ห้องนี้เป็นแมตช์เล่นคนเดียว (Room Property "Solo") ไหม
    public static bool IsSolo(RoomInfo room)
        => room != null && room.CustomProperties.TryGetValue(SoloKey, out object v) && v is bool on && on;
    // ห้องนี้เป็นแมตช์สอนเล่น (Room Property "Tutorial") ไหม
    public static bool IsTutorial(RoomInfo room)
        => room != null && room.CustomProperties.TryGetValue(TutorialKey, out object v) && v is bool on && on;
    // แมตช์ที่มีบอทเป็นคู่แข่ง (ได้รางวัลน้อยลง กันปั๊มเหรียญ)
    public static bool IsBotMatch(RoomInfo room) => BotCount(room) > 0 || IsCoop(room);

    // Kill ของบอท (Room Property "BK{id}")
    public static int BotKills(int botId)
    {
        var room = PhotonNetwork.CurrentRoom;
        return room != null && room.CustomProperties.TryGetValue(BotKillPrefix + botId, out object v) && v is int n ? n : 0;
    }

    // บวก Kill ให้บอท (เรียกบน Master เท่านั้น)
    public static void AddBotKill(int botId)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [BotKillPrefix + botId] = BotKills(botId) + 1 });
    }

    // จำนวนคนจริงที่ต้องมีในแมตช์: Host บันทึกไว้ตอนกดเริ่ม (Humans) ถ้าไม่มีใช้สูตร 1v1 เดิม (2 - จำนวนบอท)
    public static int ExpectedHumans(RoomInfo room)
    {
        if (room != null && room.CustomProperties.TryGetValue(HumansKey, out object v) && v is int n && n > 0) return UnityEngine.Mathf.Clamp(n, 1, MaxCombatants);
        return System.Math.Max(1, 2 - BotCount(room));
    }

    // รายชื่อผู้เล่นเรียงตาม Kill มากไปน้อย (ใช้ทำตารางคะแนนในเฟสหลายคน)
    public static List<Player> Standings()
    {
        var list = new List<Player>(PhotonNetwork.PlayerList);
        list.Sort((a, b) => Kills(b).CompareTo(Kills(a)));
        return list;
    }

    // ===== หลายผู้เล่น (เฟส 2: Free-for-all 2-10 คน) =====
    public const string HumansKey = "Humans";
    public const string BotDeathPrefix = "BD";
    public const int MaxCombatants = 10;
    // ขนาดห้องที่ Host เลือกได้ (2 = 1 VS 1 แบบเดิม)
    public static readonly int[] PlayerOptions = { 2, 4, 6, 8, 10 };

    // จำนวนผู้ต่อสู้ทั้งหมดในแมตช์ (คน + บอท)
    public static int TotalCombatants(RoomInfo room) => ExpectedHumans(room) + BotCount(room);
    // แมตช์แบบทุกคนเป็นศัตรูกัน (เกิน 2 ลำ) — ปิด FeatureFlags.MultiPlayer = กลับไปเป็น 1v1 อย่างเดียว
    public static bool IsFreeForAll(RoomInfo room) => FeatureFlags.MultiPlayer && TotalCombatants(room) > 2;
    // ชื่อโหมดสำหรับแสดงบนจอ
    public static string ModeName(int maxPlayers) => maxPlayers <= 2 ? "1 VS 1" : "FFA " + maxPlayers + "P";
    // ชื่อโหมดแบบรองรับทีม: เปิดทีมและ 4 ลำขึ้นไป = "TEAM 2v2" ฯลฯ ไม่งั้นใช้ชื่อแบบเดิม
    public static string ModeName(int maxPlayers, bool teams)
        => teams && maxPlayers >= 4 ? "TEAM " + maxPlayers / 2 + "v" + maxPlayers / 2 : ModeName(maxPlayers);

    // ความถี่ส่งข้อมูลตำแหน่งยาน (ครั้ง/วินาที): คนเยอะส่งถี่น้อยลง เพื่อไม่ให้เกินโควต้าข้อความของ Photon (~500 ข้อความ/วินาที/ห้อง)
    // ข้อความต่อวินาทีโดยประมาณ = คน x (คน - 1) x ความถี่
    public static int SerializationRateFor(int humans)
    {
        if (humans <= 2) return 10;
        return UnityEngine.Mathf.Clamp(400 / (humans * (humans - 1)), 5, 10);
    }

    // จำนวนครั้งที่ตาย (คน = Player Property "Deaths", บอท = Room Property "BD{id}")
    public static int Deaths(Player player)
        => player != null && player.CustomProperties.TryGetValue("Deaths", out object value) && value is int deaths ? deaths : 0;
    // จำนวนครั้งที่บอทตาย (Room Property "BD{id}")
    public static int BotDeaths(int botId)
    {
        var room = PhotonNetwork.CurrentRoom;
        return room != null && room.CustomProperties.TryGetValue(BotDeathPrefix + botId, out object v) && v is int n ? n : 0;
    }
    // บวกจำนวนตายให้บอท (เรียกบน Master เท่านั้น)
    public static void AddBotDeath(int botId)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [BotDeathPrefix + botId] = BotDeaths(botId) + 1 });
    }

    // แถวหนึ่งในตารางคะแนน
    public class Standing
    {
        public int id;          // CombatantId (คน = ActorNumber, บอท = 1000+)
        public string name; // ชื่อนักบินที่แสดงในตาราง
        public int kills; // จำนวน Kill
        public int deaths; // จำนวนครั้งที่ตาย
        public bool isLocal; // true = แถวของผู้เล่นเครื่องนี้ (ไฮไลต์)
        public bool isBot; // true = บอท
        public bool left;       // หลุด/ออกจากห้องแล้ว
        public int ship;        // ยาน (ใช้แสดงรูป)
        public int score;       // คะแนนโหมด (ยึดจุด/เก็บดาว/Battle Royale) ใช้เรียงก่อน Kill
    }

    // ตารางคะแนนของผู้ต่อสู้ทุกคน (คน + บอท) เรียง Kill มาก -> น้อย, เท่ากันดูตายน้อยกว่า, แล้วตามชื่อ
    // โหมดที่ไม่ได้นับ Kill (ยึดจุด/เก็บดาว/Battle Royale) ใส่ฟังก์ชันคะแนนแทนที่นี่ — ตารางคะแนน/หน้าผลจะเรียงตามคะแนนนี้
    public static System.Func<Standing, int> ScoreOverride;

    // สร้างตารางคะแนน: คนจริงจาก PlayerList + บอทจาก PlayerController ในฉาก แล้วเรียงด้วย CompareStanding
    public static List<Standing> AllStandings()
    {
        var list = new List<Standing>();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            int ship = player.CustomProperties.TryGetValue("ShipType", out object s) && s is int si ? si : 0;
            list.Add(new Standing { id = player.ActorNumber, name = player.NickName, kills = Kills(player), deaths = Deaths(player),
                isLocal = player.IsLocal, left = player.IsInactive, ship = ship });
        }
        foreach (var ship in UnityEngine.Object.FindObjectsByType<PlayerController>(UnityEngine.FindObjectsSortMode.None))
        {
            if (!ship.IsBot) continue;
            list.Add(new Standing { id = ship.CombatantId, name = ship.PilotName, kills = BotKills(ship.CombatantId),
                deaths = BotDeaths(ship.CombatantId), isBot = true, ship = ship.ShipIndex });
        }
        if (ScoreOverride != null) foreach (var entry in list) entry.score = ScoreOverride(entry);
        list.Sort(CompareStanding);
        return list;
    }

    // ตัวเปรียบเทียบสำหรับเรียงตาราง: คะแนนโหมดมากก่อน > Kill มากก่อน > ตายน้อยก่อน > ชื่อ
    public static int CompareStanding(Standing a, Standing b)
    {
        if (a.score != b.score) return b.score.CompareTo(a.score);
        if (a.kills != b.kills) return b.kills.CompareTo(a.kills);
        if (a.deaths != b.deaths) return a.deaths.CompareTo(b.deaths);
        return string.CompareOrdinal(a.name, b.name);
    }

    // ล้างคะแนนบอทของแมตช์ก่อน: บอทที่ใช้ = 0, ช่องที่ไม่ใช้ = null (ไม่ถูกนับ)
    public static ExitGames.Client.Photon.Hashtable ResetBotScores(int botCount)
    {
        var props = new ExitGames.Client.Photon.Hashtable();
        for (int i = 0; i < MaxCombatants; i++)
        {
            int id = PlayerController.BotIdBase + i;
            object value = i < botCount ? (object)0 : null;
            props[BotKillPrefix + id] = value;
            props[BotDeathPrefix + id] = value;
            props[BotDamagePrefix + id] = value; // เงื่อนไขชนะ: ดาเมจรวม / ค่าหัวของบอท
            props[BotBountyPrefix + id] = value;
            props[MatchStats.BotPrefix + id] = null; // สถิติหลังแมตช์ของบอท (ล้างทิ้ง)
        }
        return props;
    }

    // ===== ตีบวก/ไอเท็มในแมตช์ (เฟส 5) =====
    // Host ปิดได้ในห้องรอ (ปุ่ม UPGRADES) = ทุกคนใช้ค่าพลังพื้นฐานเท่ากัน
    public const string UpgradesKey = "Upgrades";
    // ห้องนี้ใช้ค่าตีบวก/ไอเท็มได้ไหม (ห้องแรงค์ = ไม่ได้, ไม่มีค่า = ได้)
    public static bool UpgradesAllowed(RoomInfo room)
        => !IsRanked(room) && (room == null || !room.CustomProperties.TryGetValue(UpgradesKey, out object v) || !(v is bool on) || on);

    // ===== โหมดเกม (เฟส 7B) =====
    // ห้องเก็บ "GameMode": 0 ปกติ (นับ Kill), 1 ยึดจุด, 2 เก็บดาว, 3 Survival (ร่วมมือสู้บอทเป็นระลอก), 4 Battle Royale, 5 Campaign (เล่นคนเดียว)
    public const string GameModeKey = "GameMode";
    public const string CampaignStageKey = "CampaignStage";
    public const string ModeScorePrefix = "MS";
    public const int ModeDeathmatch = 0, ModeKoth = 1, ModeStars = 2, ModeSurvival = 3, ModeRoyale = 4, ModeCampaign = 5;
    public static readonly string[] ModeTitles = { "DEATHMATCH", "KING OF THE HILL", "STAR HUNT", "SURVIVAL", "BATTLE ROYALE", "CAMPAIGN" };
    // โหมดเกมของห้อง (ปิด FeatureFlags.GameModes / ห้องแรงค์ / ไม่มีค่า = Deathmatch)
    public static int GameMode(RoomInfo room)
    {
        if (!FeatureFlags.GameModes || room == null || IsRanked(room)) return ModeDeathmatch;
        return room.CustomProperties.TryGetValue(GameModeKey, out object v) && v is int mode ? UnityEngine.Mathf.Clamp(mode, 0, ModeTitles.Length - 1) : ModeDeathmatch;
    }
    // ด่าน Campaign ที่ห้องนี้เล่นอยู่ (ไม่มีค่า = ด่าน 1)
    public static int CampaignStage(RoomInfo room)
        => room != null && room.CustomProperties.TryGetValue(CampaignStageKey, out object v) && v is int stage ? stage : 1;
    // โหมดร่วมมือ: คนจริงทุกคนเป็นทีมเดียวกัน (ทีม 0) สู้บอท (ทีม 1)
    public static bool IsCoop(RoomInfo room) { int mode = GameMode(room); return mode == ModeSurvival || mode == ModeCampaign; }
    // ใช้ระบบทีม (ยิงเพื่อนไม่โดน): โหมดทีม หรือโหมดร่วมมือ
    public static bool UsesTeams(RoomInfo room) => IsTeamMode(room) || IsCoop(room);
    // จำนวนชีวิตของคนจริง (0 = ไม่จำกัด)
    public static int Lives(RoomInfo room)
    {
        int mode = GameMode(room);
        return mode == ModeRoyale ? 1 : mode == ModeSurvival || mode == ModeCampaign ? 3 : 0;
    }
    // คะแนนของโหมด (ยึดจุด/เก็บดาว) เก็บใน Room Property "MS{CombatantId}" โดย Master
    public static int ModeScore(int combatantId)
    {
        var room = PhotonNetwork.CurrentRoom;
        return room != null && room.CustomProperties.TryGetValue(ModeScorePrefix + combatantId, out object v) && v is int n ? n : 0;
    }

    // ===== ไอเท็มเกิดในแม็พ (เฟส 7) =====
    public const string PowerUpsKey = "PowerUps";
    // ห้องนี้มีไอเท็มเกิดในแม็พไหม (ปิดในโหมดสอนเล่น, ไม่มีค่า = เปิด)
    public static bool PowerUpsEnabled(RoomInfo room)
        => FeatureFlags.PowerUps && room != null && !IsTutorial(room)
           && (!room.CustomProperties.TryGetValue(PowerUpsKey, out object v) || !(v is bool on) || on);

    // ===== แรงค์ (เฟส 6) =====
    // ห้องแรงค์: 1 VS 1 กติกาตายตัว (Host แก้ค่าไม่ได้) ไม่มีบอท ไม่ใช้ค่าตีบวก
    public const string RankedKey = "Ranked";
    public const int RankedKills = 5;
    public const int RankedSeconds = 300;
    // ห้องนี้เป็นห้องแรงค์ไหม (Room Property "Ranked" = true และเปิด FeatureFlags.Ranked)
    public static bool IsRanked(RoomInfo room)
        => FeatureFlags.Ranked && room != null && room.CustomProperties.TryGetValue(RankedKey, out object v) && v is bool on && on;

    // ===== โหมดทีม (เฟส 3) =====
    // ห้อง: "Teams" = true เปิดโหมดทีม / ทีมของผู้ต่อสู้แต่ละคน: Room Property "T{CombatantId}" = 0 (BLUE) หรือ 1 (RED)
    // (Host เขียนทีมสุดท้ายของทุกคนตอนกดเริ่ม และเขียนทีมของบอทตอนสร้างบอท จึงทุกเครื่องเห็นตรงกัน)
    // ผู้เล่น: Player Property "Team" = ทีมที่เลือกในห้องรอ
    public const string TeamsKey = "Teams";
    public const string TeamPrefix = "T";
    public const string PlayerTeamProperty = "Team";
    public static readonly string[] TeamNames = { "BLUE", "RED" };
    public static readonly UnityEngine.Color[] TeamColors = { new UnityEngine.Color(.35f, .7f, 1f), new UnityEngine.Color(1f, .4f, .38f) };
    public static readonly string[] TeamHex = { "#5AB2FF", "#FF6661" };

    // ห้องนี้ตั้งเป็นโหมดทีมไหม (ห้องรอ / ในเกม)
    public static bool IsTeamRoom(RoomInfo room)
        => FeatureFlags.Teams && room != null && room.CustomProperties.TryGetValue(TeamsKey, out object v) && v is bool on && on;
    // แมตช์นี้เป็นโหมดทีม (ต้องมีอย่างน้อย 4 ลำ)
    public static bool IsTeamMode(RoomInfo room) => IsTeamRoom(room) && TotalCombatants(room) >= 4;

    // ทีมของผู้ต่อสู้ (-1 = ไม่มีทีม / ไม่ใช่โหมดทีม)
    public static int TeamOf(int combatantId)
    {
        var room = PhotonNetwork.CurrentRoom;
        // ใช้เฉพาะแมตช์ทีมจริง (ห้องตั้งทีมแต่คนไม่ถึง 4 ลำ = เล่นแบบตัวใครตัวมัน)
        if (room == null || !UsesTeams(room)) return -1;
        return room.CustomProperties.TryGetValue(TeamPrefix + combatantId, out object v) && v is int team ? team : -1;
    }

    // เป็นพวกเดียวกันไหม (ตัวเอง หรือทีมเดียวกัน) — ใช้กันยิงโดนเพื่อน
    public static bool IsAlly(int a, int b)
    {
        if (a == b) return true;
        if (a < 0 || b < 0) return false;
        int teamA = TeamOf(a);
        return teamA >= 0 && teamA == TeamOf(b);
    }

    // ผู้ต่อสู้ต่อทีม (โหมดทีมแบ่งครึ่ง)
    public static int TeamSize(RoomInfo room) => System.Math.Max(1, TotalCombatants(room) / 2);
    // Kill เป้าหมายของทีม = Kill ที่ตั้งไว้ x จำนวนคนต่อทีม (2v2 ตั้ง 5 = ทีมแรกที่ถึง 10 ชนะ)
    public static int TeamKillTarget(RoomInfo room) => KillTarget(room) * TeamSize(room);

    // Kill รวมของทีม (คนจริง + บอท)
    public static int TeamKills(int team)
    {
        int total = 0;
        foreach (var player in PhotonNetwork.PlayerList)
            if (TeamOf(player.ActorNumber) == team) total += Kills(player);
        var room = PhotonNetwork.CurrentRoom;
        if (room != null)
            foreach (var entry in room.CustomProperties)
                if (entry.Key is string key && key.StartsWith(BotKillPrefix) && entry.Value is int kills
                    && int.TryParse(key.Substring(BotKillPrefix.Length), out int botId) && TeamOf(botId) == team)
                    total += kills;
        return total;
    }

    // แบ่งทีมตอนเริ่มเกม (Host): ใช้ทีมที่แต่ละคนเลือก แล้วย้ายคนที่เข้าห้องทีหลังสุดจนสองทีมต่างกันไม่เกิน 1 คน
    // คืน Room Properties "T{ActorNumber}" ของทุกคน และจำนวนคนในแต่ละทีม
    public static ExitGames.Client.Photon.Hashtable AssignTeams(Player[] humans, out int blue, out int red)
    {
        var sorted = (Player[])humans.Clone();
        System.Array.Sort(sorted, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        var teams = new int[sorted.Length];
        blue = red = 0;
        for (int i = 0; i < sorted.Length; i++)
        {
            int wanted = sorted[i].CustomProperties.TryGetValue(PlayerTeamProperty, out object v) && v is int t && (t == 0 || t == 1) ? t : (blue <= red ? 0 : 1);
            teams[i] = wanted;
            if (wanted == 0) blue++; else red++;
        }
        for (int i = sorted.Length - 1; i >= 0 && System.Math.Abs(blue - red) > 1; i--)
        {
            int bigger = blue > red ? 0 : 1;
            if (teams[i] != bigger) continue;
            teams[i] = 1 - bigger;
            if (bigger == 0) { blue--; red++; } else { red--; blue++; }
        }
        var props = new ExitGames.Client.Photon.Hashtable();
        for (int i = 0; i < sorted.Length; i++) props[TeamPrefix + sorted[i].ActorNumber] = teams[i];
        return props;
    }
}
