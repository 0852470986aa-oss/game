// MatchRules.WinRules.cs — เงื่อนไขชนะของโหมดปกติ (Host เลือกในหน้าตั้งค่าห้อง ปุ่ม WIN CONDITION)
// 0 FIRST TO X  = ใครฆ่าครบเป้าก่อนชนะทันที (แบบเดิม)
// 1 MOST KILLS  = ไม่มีเป้า เล่นจนหมดเวลา ใครฆ่าเยอะสุดชนะ
// 2 NET SCORE   = ฆ่า +1 / ตาย -1 หมดเวลาคะแนนสูงสุดชนะ
// 3 DAMAGE      = หมดเวลาแล้ววัดจากดาเมจรวมที่ยิงใส่ศัตรู
// 4 BOUNTY      = ฆ่า = 1 แต้ม + ค่าหัวตามจำนวนที่เหยื่อฆ่าต่อเนื่องอยู่ (หยุดคนกำลังเก่ง = แต้มเยอะ)
// โหมดทีม = รวมคะแนนของสมาชิกทั้งทีม / ห้องแรงค์และโหมดพิเศษ (ยึดจุด/ดาว/...) ใช้แบบเดิมเสมอ
// ดาเมจ: คนยิงบันทึกของตัวเองใน Player Property "Dmg" (บอท: Master เก็บใน Room Property "DM{id}")
// ค่าหัว: เครื่องคนฆ่าบันทึก "Bty" ของตัวเอง (บอท: Master เก็บ "BB{id}") — ไม่มีใครเขียนค่าของคนอื่น จึงไม่ชนกัน
// ปิด FeatureFlags.WinRules = ใช้ FIRST TO X อย่างเดียวและซ่อนปุ่ม
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

// ส่วนเงื่อนไขชนะของ MatchRules (partial)
public static partial class MatchRules
{
    public const string WinRuleKey = "WinRule";
    public const int WinFirstTo = 0, WinMostKills = 1, WinNetScore = 2, WinDamage = 3, WinBounty = 4;
    public static readonly string[] WinRuleNames = { "FIRST TO X", "MOST KILLS", "NET SCORE", "MOST DAMAGE", "BOUNTY HUNT" };
    public const string DamageKey = "Dmg", BountyKey = "Bty", BotDamagePrefix = "DM", BotBountyPrefix = "BB";

    // เงื่อนไขชนะของห้องนี้ (โหมดพิเศษ/แรงค์/ปิดสวิตช์ = FIRST TO X)
    public static int WinRule(RoomInfo room)
    {
        if (!FeatureFlags.WinRules || room == null || IsRanked(room) || GameMode(room) != ModeDeathmatch) return WinFirstTo;
        return room.CustomProperties.TryGetValue(WinRuleKey, out object v) && v is int rule && rule >= 0 && rule < WinRuleNames.Length ? rule : WinFirstTo;
    }

    // ห้องนี้ใช้คะแนนแบบใหม่ (ไม่ใช่ FIRST TO X) หรือไม่
    public static bool UsesRuleScore(RoomInfo room) => WinRule(room) != WinFirstTo;

    // คะแนนของแถวหนึ่งตามเงื่อนไขชนะ (ใช้เป็น ScoreOverride ให้ตารางคะแนน/หน้าผลเรียงตามนี้)
    public static int RuleScore(Standing entry)
    {
        switch (WinRule(PhotonNetwork.CurrentRoom))
        {
            case WinNetScore: return entry.kills - entry.deaths;
            case WinDamage: return CombatantInt(entry.id, DamageKey, BotDamagePrefix);
            case WinBounty: return CombatantInt(entry.id, BountyKey, BotBountyPrefix);
            default: return entry.kills;
        }
    }

    // คะแนนรวมของทีม (FIRST TO X = Kill รวมแบบเดิม)
    public static int TeamPoints(int team)
    {
        if (!UsesRuleScore(PhotonNetwork.CurrentRoom)) return TeamKills(team);
        int total = 0;
        foreach (var entry in AllStandings()) if (TeamOf(entry.id) == team) total += RuleScore(entry);
        return total;
    }

    // อ่านค่าตัวเลขของผู้ต่อสู้: คน = Player Property, บอท = Room Property (prefix + id)
    private static int CombatantInt(int id, string playerKey, string botPrefix)
    {
        // ยานของเครื่องนี้: ใช้ยอดในเครื่อง (ล่าสุดเสมอ)
        if (ownedTotals.TryGetValue(playerKey, out var owned) && owned.TryGetValue(id, out float local)) return UnityEngine.Mathf.RoundToInt(local);
        return ReadNetworkInt(id, playerKey, botPrefix);
    }

    // อ่านค่าจากเครือข่าย: คน = Player Property, บอท = Room Property (prefix + id)
    private static int ReadNetworkInt(int id, string playerKey, string botPrefix)
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return 0;
        if (id >= PlayerController.BotIdBase)
            return room.CustomProperties.TryGetValue(botPrefix + id, out object b) && b is int bn ? bn : 0;
        var player = room.GetPlayer(id);
        return player != null && player.CustomProperties.TryGetValue(playerKey, out object v) && v is int n ? n : 0;
    }

    // ===== ยอดรวมดาเมจ/ค่าหัว ที่เครื่องนี้เป็นเจ้าของ (คนของเครื่องนี้ / บอทบน Master) =====
    // เก็บยอดรวมไว้ในเครื่องแล้วส่ง "ยอดรวม" ขึ้นเครือข่าย (ไม่อ่านค่าเก่าจาก Photon มาบวก เพราะถ้าเน็ตช้าค่าที่อ่านยังไม่อัปเดต ยอดจะหาย)
    private static readonly Dictionary<string, Dictionary<int, float>> ownedTotals = new Dictionary<string, Dictionary<int, float>>
    { [DamageKey] = new Dictionary<int, float>(), [BountyKey] = new Dictionary<int, float>() };
    private static readonly HashSet<int> dirtyDamage = new HashSet<int>();
    private static bool freshScores;
    private static float nextDamageFlush;

    // เริ่มแมตช์: fresh = แมตช์ใหม่ (เริ่ม 0) / false = กลับเข้าแมตช์เดิมหลังหลุด (ต่อจากค่าที่ส่งไว้)
    public static void BeginScores(bool fresh)
    {
        foreach (var dict in ownedTotals.Values) dict.Clear();
        dirtyDamage.Clear();
        freshScores = fresh;
    }

    // เครื่องนี้เป็นเจ้าของคะแนนของ id นี้ไหม (ยานตัวเอง / บอทเมื่อเป็น Master)
    private static bool OwnsScore(int id)
        => PhotonNetwork.LocalPlayer != null && id == PhotonNetwork.LocalPlayer.ActorNumber || id >= PlayerController.BotIdBase && PhotonNetwork.IsMasterClient;

    // บวกยอดของยานที่เครื่องนี้เป็นเจ้าของ คืนยอดรวมใหม่ (ครั้งแรก: แมตช์ใหม่ = เริ่ม 0, รับช่วงต่อ = ต่อจากค่าในเครือข่าย)
    private static float AddOwned(string key, string botPrefix, int id, float amount)
    {
        var dict = ownedTotals[key];
        if (!dict.TryGetValue(id, out float total)) total = freshScores ? 0 : ReadNetworkInt(id, key, botPrefix);
        total += amount;
        dict[id] = total;
        return total;
    }

    // บันทึกดาเมจ (เรียกจาก PlayerController.SendDamage บนเครื่องคนยิง)
    public static void RecordDamage(int shooterId, float damage, PlayerController target)
    {
        if (target == null || target.isDead || damage <= 0f || shooterId == target.CombatantId) return;
        if (WinRule(PhotonNetwork.CurrentRoom) != WinDamage || !OwnsScore(shooterId)) return;
        if (GameplayManager.Instance != null && !GameplayManager.Instance.MatchInputAllowed) return; // หลังจบแมตช์ไม่นับ
        float amount = UnityEngine.Mathf.Min(damage, UnityEngine.Mathf.Max(1f, target.currentHp)); // ไม่นับดาเมจเกินเลือดที่เหลือ
        AddOwned(DamageKey, BotDamagePrefix, shooterId, amount);
        dirtyDamage.Add(shooterId);
    }

    // ส่งยอดดาเมจขึ้นเครือข่ายทุก 0.4 วิ (เรียกทุกเฟรมจาก GameplayManager) force = ส่งทันที (ตอนจบแมตช์)
    public static void FlushDamage(bool force = false)
    {
        if (dirtyDamage.Count == 0 || !force && UnityEngine.Time.unscaledTime < nextDamageFlush) return;
        nextDamageFlush = UnityEngine.Time.unscaledTime + .4f;
        if (!PhotonNetwork.InRoom) { dirtyDamage.Clear(); return; }
        var roomProps = new ExitGames.Client.Photon.Hashtable();
        foreach (int id in dirtyDamage)
        {
            int total = UnityEngine.Mathf.RoundToInt(ownedTotals[DamageKey][id]);
            if (id >= PlayerController.BotIdBase) { if (PhotonNetwork.IsMasterClient) roomProps[BotDamagePrefix + id] = total; }
            else PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [DamageKey] = total });
        }
        dirtyDamage.Clear();
        if (roomProps.Count > 0) PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
    }

    // ===== ค่าหัว (เรียกทุกเครื่องเมื่อมียานตาย ก่อนล้างสตรีคของเหยื่อ) =====
    private static readonly Dictionary<int, int> bountyStreaks = new Dictionary<int, int>();

    // ล้างตัวนับตอนเริ่มแมตช์
    public static void ResetBounty() { bountyStreaks.Clear(); bestStreaks.Clear(); }

    // ฆ่าต่อเนื่องสูงสุดในแมตช์นี้ (ทุกเครื่องนับเหมือนกันจากการตายที่เห็น) ใช้ในสถิติหลังแมตช์
    private static readonly Dictionary<int, int> bestStreaks = new Dictionary<int, int>();
    // คืนค่าฆ่าต่อเนื่องสูงสุดของ id (ไม่มี = 0)
    public static int BestStreak(int id) => bestStreaks.TryGetValue(id, out int best) ? best : 0;

    // แต้มที่ได้ = 1 + จำนวนที่เหยื่อฆ่าต่อเนื่องอยู่ / คืนแต้มที่คนฆ่าได้ (ใช้แสดงป้าย) 0 = ไม่ได้แต้ม
    public static int AwardBounty(int killerId, PlayerController victim)
    {
        if (victim == null) return 0;
        bountyStreaks.TryGetValue(victim.CombatantId, out int victimStreak);
        bountyStreaks[victim.CombatantId] = 0;
        if (killerId <= 0 || killerId == victim.CombatantId || IsAlly(killerId, victim.CombatantId)) return 0;
        bountyStreaks.TryGetValue(killerId, out int streak);
        bountyStreaks[killerId] = streak + 1;
        bestStreaks.TryGetValue(killerId, out int best);
        if (streak + 1 > best) bestStreaks[killerId] = streak + 1;
        if (WinRule(PhotonNetwork.CurrentRoom) != WinBounty) return 0;
        int points = 1 + victimStreak;
        // เจ้าของยานคนฆ่าบวกยอดรวมในเครื่องแล้วส่งยอดรวมขึ้นเครือข่ายทันที
        if (PhotonNetwork.InRoom && OwnsScore(killerId))
        {
            int total = UnityEngine.Mathf.RoundToInt(AddOwned(BountyKey, BotBountyPrefix, killerId, points));
            if (killerId >= PlayerController.BotIdBase) PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [BotBountyPrefix + killerId] = total });
            else PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [BountyKey] = total });
        }
        return points;
    }
}
