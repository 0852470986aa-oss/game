// GameplayManager.Multi.cs — ระบบห้องหลายคน 2-10 คน แบบทุกคนเป็นศัตรูกัน (Free-for-all) (partial class ของ GameplayManager)
// - จุดเกิดเป็นวงรอบสนาม (แต่ละคน/บอทได้ช่องของตัวเอง หันหน้าเข้ากลาง)
// - ตารางคะแนนย่อมุมขวา (แตะเพื่อเปิดตารางเต็ม) และข้อความ "ใครฆ่าใคร" (Kill Feed) ใต้มินิแม็พ
// - คนหลุดระหว่างแข่ง: คนที่เหลือเล่นต่อ (เหลือผู้ต่อสู้ไม่ถึง 2 = จบแมตช์) / Master หลุด: Master ใหม่ควบคุมบอทต่อ
// - หน้าผลแบบจัดอันดับ + MVP + เหรียญตามอันดับ
// - ลดความถี่ส่งข้อมูลตามจำนวนคน (ไม่ให้เกินโควต้าข้อความของ Photon)
// - โหมดทีม (เฟส 3): BLUE เกิดฝั่งล่าง RED ฝั่งบน, ยิงเพื่อนไม่โดน (MatchRules.IsAlly), นับ Kill รวมของทีม, หน้าผลชนะ/แพ้ทั้งทีม
// ปิดได้ด้วย FeatureFlags.MultiPlayer / Scoreboard / KillFeed / Teams (ดู FeatureFlags.cs)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

// ส่วนแมตช์หลายคนของ GameplayManager: จุดเกิด, Kill Feed, ตารางคะแนน และหน้าผลจัดอันดับ
public partial class GameplayManager
{
    // true = แมตช์นี้มีผู้ต่อสู้เกิน 2 (คน + บอท) ตั้งครั้งเดียวตอน Start (รวมโหมดทีมด้วย)
    private bool isFreeForAll;
    // true = โหมดทีม (เป็นกรณีย่อยของแมตช์หลายคน)
    private bool isTeamMode;
    // ทีมของเรา (0 BLUE / 1 RED)
    private int LocalTeam => PhotonNetwork.LocalPlayer != null ? Mathf.Max(0, MatchRules.TeamOf(PhotonNetwork.LocalPlayer.ActorNumber)) : 0;
    private float nextRivalScan, nextCombatantCheck, nextStandingsRefresh;
    private List<MatchRules.Standing> cachedStandings = new List<MatchRules.Standing>();

    // ===== ตั้งค่าตอนเริ่มแมตช์ =====
    private void ConfigureMultiplayerMatch()
    {
        var room = PhotonNetwork.CurrentRoom;
        // โหมดเกมพิเศษ (ยึดจุด/เก็บดาว/Survival/Battle Royale/Campaign) ใช้ระบบหลายคนเสมอ (ตารางคะแนน/หน้าผลจัดอันดับ/เล่นต่อเมื่อมีคนหลุด)
        isFreeForAll = PhotonNetwork.InRoom && (MatchRules.IsFreeForAll(room) || MatchRules.GameMode(room) != MatchRules.ModeDeathmatch);
        isTeamMode = isFreeForAll && MatchRules.IsTeamMode(room);
        SpawnSideBonus = isTeamMode ? 30f : 3f;
        // เป้าหมาย Kill ของทีม = Kill ที่ตั้ง x จำนวนคนต่อทีม
        if (isTeamMode) targetKills = MatchRules.TeamKillTarget(room);
        // เฟส 7B: โหมดเกม (GameplayManager.Modes.cs)
        ConfigureGameMode();
        // ยานเยอะ: ยอมให้เกิดใกล้คนอื่นได้มากขึ้น (ไม่งั้นหาจุดเกิดไม่เจอ)
        SafeSpawnDistance = isFreeForAll ? 9f : 14f;
        int humans = PhotonNetwork.InRoom ? MatchRules.ExpectedHumans(room) : 1;
        // ค่าเดิมของ PUN: ส่ง 30 ครั้ง/วิ, ตำแหน่ง 10 ครั้ง/วิ — ห้อง 10 คนลดตำแหน่งเหลือ ~5 ครั้ง/วิ (ยานใช้การเดาตำแหน่งช่วยให้ลื่น)
        PhotonNetwork.SerializationRate = MatchRules.SerializationRateFor(humans);
        PhotonNetwork.SendRate = Mathf.Max(PhotonNetwork.SerializationRate * 2, humans > 4 ? 20 : 30);
    }

    // ===== จุดเกิด =====
    // ช่องเกิดของเรา = ลำดับของเราในรายชื่อผู้เล่นเรียงตาม ActorNumber (ทุกเครื่องเรียงเหมือนกัน จึงไม่ชนช่องกัน)
    private static int LocalSpawnSlot()
    {
        var players = PhotonNetwork.PlayerList;
        System.Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        for (int i = 0; i < players.Length; i++) if (players[i].IsLocal) return i;
        return 0;
    }

    // หันหัวยานเข้าหากลางสนาม
    private static Quaternion FaceArenaCenter(Vector2 position, int map)
    {
        Vector2 center = (GetArenaMin(map) + GetArenaMax(map)) * .5f;
        Vector2 dir = center - position;
        if (dir.sqrMagnitude < .01f) return Quaternion.identity;
        return Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
    }

    // หาจุดเกิดของช่อง slot จากทั้งหมด total ช่อง (วงรีรอบสนาม เริ่มที่ด้านล่าง)
    // ถ้าจุดหลักทับสิ่งกีดขวาง ลองขยับมุม ±10/20 องศา และระยะเข้า/ออกจากกลาง
    public static bool TryFindSlotSpawn(GameObject ship, int map, int slot, int total, out Vector2 position)
    {
        Vector2 min = GetArenaMin(map), max = GetArenaMax(map);
        Vector2 center = (min + max) * .5f, half = (max - min) * .5f;
        float clearance = ShipClearance(ship);
        float baseAngle = -90f + 360f * slot / Mathf.Max(1, total);
        foreach (float scale in new[] { .78f, .64f, .88f, .5f })
            foreach (float offset in new[] { 0f, 10f, -10f, 20f, -20f })
            {
                float a = (baseAngle + offset) * Mathf.Deg2Rad;
                Vector2 c = center + new Vector2(Mathf.Cos(a) * half.x * scale, Mathf.Sin(a) * half.y * scale);
                if (c.x - clearance < min.x || c.x + clearance > max.x || c.y - clearance < min.y || c.y + clearance > max.y) continue;
                if (IsSpawnAreaBlocked(c, clearance, ship)) continue;
                position = c;
                return true;
            }
        position = default;
        return false;
    }

    // โหมดทีม: BLUE (0) เรียงแถวฝั่งล่างหันขึ้น, RED (1) ฝั่งบนหันลง แต่ละคนได้ตำแหน่งตามลำดับในทีม
    public static bool TryFindTeamSpawn(GameObject ship, int map, int team, int index, int teamSize, out Vector2 position)
    {
        Vector2 min = GetArenaMin(map), max = GetArenaMax(map);
        Vector2 center = (min + max) * .5f, half = (max - min) * .5f;
        float clearance = ShipClearance(ship);
        float side = team == 0 ? -1f : 1f;
        float baseX = Mathf.Lerp(-.62f, .62f, (index + .5f) / Mathf.Max(1, teamSize));
        foreach (float yScale in new[] { .72f, .6f, .82f, .48f })
            foreach (float xShift in new[] { 0f, 3f, -3f, 6f, -6f })
            {
                Vector2 c = center + new Vector2(baseX * half.x + xShift, side * yScale * half.y);
                if (c.x - clearance < min.x || c.x + clearance > max.x || c.y - clearance < min.y || c.y + clearance > max.y) continue;
                if (IsSpawnAreaBlocked(c, clearance, ship)) continue;
                position = c;
                return true;
            }
        position = default;
        return false;
    }

    // ทิศหันหน้าตอนเกิดของแต่ละทีม: RED (ทีม 1) อยู่ฝั่งบนหัน 180 องศา, BLUE หันขึ้นตามปกติ
    private static Quaternion TeamFacing(int team) => team == 1 ? Quaternion.Euler(0, 0, 180f) : Quaternion.identity;

    // รัศมีที่ต้องเว้นว่างรอบยาน (คิดจากรูปทรงตัวชนของ Prefab)
    private static float ShipClearance(GameObject ship)
    {
        float radius = 2.5f;
        if (ship != null)
            foreach (var polygon in ship.GetComponentsInChildren<PolygonCollider2D>(true))
                for (int path = 0; path < polygon.pathCount; path++)
                    foreach (Vector2 point in polygon.GetPath(path))
                        radius = Mathf.Max(radius, ((Vector2)polygon.transform.TransformPoint(point + polygon.offset) - (Vector2)ship.transform.position).magnitude);
        return radius + .65f;
    }

    // มีของแข็งหรือ Hazard ทับพื้นที่นี้ไหม (ไม่นับ Trigger อื่น และชิ้นส่วนของยานตัวเอง)
    internal static bool IsSpawnAreaBlocked(Vector2 point, float radius, GameObject ship)
    {
        foreach (var hit in Physics2D.OverlapCircleAll(point, radius))
        {
            if (ship != null && ship.scene.IsValid() && hit.transform.IsChildOf(ship.transform)) continue;
            if (!hit.isTrigger || hit.GetComponentInParent<HazardController>() != null) return true;
        }
        return false;
    }

    // Coroutine เกิดยานของเราในห้องหลายคน: ลองช่องของตัวเองก่อน ไม่ว่างค่อยหาจุดปลอดภัยทั่วไป (ลองใหม่ทุก 0.5 วิ)
    private System.Collections.IEnumerator SpawnSlotWhenClear(string prefabName, int slot, int total)
    {
        GameObject prefab = GetPrefab(prefabName);
        Physics2D.SyncTransforms();
        int map = GetCurrentMapIndex();
        // โหมดทีม: หาลำดับของเราในทีม (เรียงตาม ActorNumber)
        int team = -1, teamIndex = 0;
        if (isTeamMode)
        {
            team = LocalTeam;
            foreach (var pilot in PhotonNetwork.PlayerList)
                if (pilot.ActorNumber < PhotonNetwork.LocalPlayer.ActorNumber && MatchRules.TeamOf(pilot.ActorNumber) == team) teamIndex++;
        }
        while (PhotonNetwork.InRoom && !isMatchEnding)
        {
            if (team >= 0 && (TryFindTeamSpawn(prefab, map, team, teamIndex, MatchRules.TeamSize(PhotonNetwork.CurrentRoom), out Vector2 teamPosition)
                || TryFindSafeSpawn(prefab, map, team == 0, out teamPosition)))
            {
                PhotonNetwork.Instantiate(prefabName, teamPosition, TeamFacing(team));
                yield break;
            }
            if (team < 0 && (TryFindSlotSpawn(prefab, map, slot, total, out Vector2 position)
                || TryFindSafeSpawn(prefab, map, slot % 2 == 0, out position)))
            {
                PhotonNetwork.Instantiate(prefabName, position, FaceArenaCenter(position, map));
                yield break;
            }
            yield return new WaitForSeconds(.5f);
        }
    }

    // ===== คนหลุด / Master เปลี่ยน =====
    // ห้องหลายคนที่เริ่มแข่งแล้ว ไม่ยกเลิกแมตช์เมื่อมีคนหาย (1v1 ยังยกเลิกแบบเดิม)
    private bool ContinueWithoutMissingPilots() => isFreeForAll && matchStarted && !isMatchEnding;

    // มีคนออก/หลุดระหว่างแมตช์หลายคน: แจ้งใน Kill Feed และ Master ลบยานของคนนั้นออก — คืน true = จัดการแล้ว (ไม่ต้องยกเลิกแมตช์)
    private bool HandlePilotLeftFreeForAll(Photon.Realtime.Player pilot)
    {
        if (!ContinueWithoutMissingPilots()) return false;
        AddFeedLine(CleanName(pilot.NickName) + (pilot.IsInactive ? "  lost connection" : "  left the battle"), new Color(.7f, .75f, .8f));
        if (PhotonNetwork.IsMasterClient) PhotonNetwork.DestroyPlayerObjects(pilot.ActorNumber);
        if (remotePlayer != null && !remotePlayer.IsBot && remotePlayer.photonView.OwnerActorNr == pilot.ActorNumber) remotePlayer = null;
        return true;
    }

    // Master ใหม่รับช่วงยานบอททุกลำ
    private void TakeOverBots()
    {
        botsSpawned = true; // บอทมีอยู่แล้ว ไม่ต้องสร้างซ้ำ
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (ship.IsBot) ship.OnBotControlTransferred();
    }

    // ===== อัปเดตทุกเฟรม (เรียกจาก Update) =====
    private void UpdateMultiplayer()
    {
        if (!PhotonNetwork.InRoom) return;
        UpdateKillFeed();
        UpdateBanner();
        if (!isFreeForAll) return;
        // คู่แข่งที่โชว์บน HUD = ศัตรูที่ใกล้ที่สุด (ทุก 0.5 วิ)
        if (Time.unscaledTime >= nextRivalScan)
        {
            nextRivalScan = Time.unscaledTime + .5f;
            remotePlayer = NearestEnemy();
        }
        // ตารางคะแนน (4 ครั้ง/วิ)
        if (Time.unscaledTime >= nextStandingsRefresh)
        {
            nextStandingsRefresh = Time.unscaledTime + .25f;
            cachedStandings = MatchRules.AllStandings();
            RefreshScoreboards();
        }
        // เหลือผู้ต่อสู้ไม่ถึง 2 (คนออกหมด) = จบแมตช์ทันที
        // (โหมด Survival/Campaign/Battle Royale ตัดสินจบเองใน GameplayManager.Modes.cs)
        bool modeDecidesEnd = gameMode == MatchRules.ModeSurvival || gameMode == MatchRules.ModeCampaign || gameMode == MatchRules.ModeRoyale;
        if (matchStarted && !isMatchEnding && !resultShown && !modeDecidesEnd && Time.unscaledTime >= nextCombatantCheck)
        {
            nextCombatantCheck = Time.unscaledTime + 1f;
            int alive = CountBots();
            foreach (var pilot in PhotonNetwork.PlayerList) if (PilotCounts(pilot)) alive++; // เฟส 9: คนที่กำลังต่อใหม่ยังนับอยู่
            // โหมดทีม: ทีมไหนไม่เหลือใครเลย = อีกทีมชนะ
            bool teamWiped = false;
            if (isTeamMode)
            {
                int[] perTeam = new int[2];
                foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                {
                    int t = MatchRules.TeamOf(ship.CombatantId);
                    bool online = ship.IsBot || PilotCounts(ship.photonView.Owner);
                    if (t >= 0 && online) perTeam[t]++;
                }
                teamWiped = perTeam[0] == 0 || perTeam[1] == 0;
            }
            if (alive < 2 || teamWiped) { isMatchEnding = true; EndMatch(); }
        }
    }

    // หายานศัตรูที่ยังไม่ตายและอยู่ใกล้ยานเราที่สุด (ข้ามเพื่อนร่วมทีม) ใช้เป็นคู่แข่งที่โชว์บน HUD
    // ไม่เจอใครเลยคืน remotePlayer เดิม
    private PlayerController NearestEnemy()
    {
        PlayerController best = null;
        float bestDistance = float.MaxValue;
        Vector2 from = localPlayer != null ? (Vector2)localPlayer.transform.position : Vector2.zero;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (ship == localPlayer || ship.IsLocalHuman || ship.isDead) continue;
            if (localPlayer != null && MatchRules.IsAlly(ship.CombatantId, localPlayer.CombatantId)) continue;
            float d = ((Vector2)ship.transform.position - from).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = ship; }
        }
        return best != null ? best : remotePlayer;
    }

    // อันดับแบบนับเสมอ: 1 + จำนวนคนที่ Kill มากกว่า (หรือ Kill เท่าแต่ตายน้อยกว่า)
    private static int RankOf(MatchRules.Standing who, List<MatchRules.Standing> all)
    {
        int rank = 1;
        foreach (var other in all)
            if (other.score > who.score || (other.score == who.score && (other.kills > who.kills || (other.kills == who.kills && other.deaths < who.deaths)))) rank++;
        return rank;
    }

    // หาแถวคะแนนของเราเองในรายการอันดับ (ไม่เจอคืน null)
    private MatchRules.Standing LocalStanding(List<MatchRules.Standing> all)
    {
        foreach (var entry in all) if (entry.isLocal) return entry;
        return null;
    }

    // ข้อความคะแนนด้านบนจอ (แทน "YOU 0 : 0 RIVAL" ของ 1v1)
    private string FreeForAllScoreLine(int myKills)
    {
        var me = LocalStanding(cachedStandings);
        int lead = cachedStandings.Count > 0 ? DisplayScore(cachedStandings[0]) : 0;
        string rank = me != null ? "#" + RankOf(me, cachedStandings) + "/" + cachedStandings.Count : "--";
        return "YOU " + myKills + "   /   RANK " + rank + "   /   LEAD " + lead;
    }

    // ข้อความคะแนนโหมดทีม: BLUE 7 : 5 RED / ทีมเรา
    private string TeamScoreLine()
    {
        int mine = LocalTeam;
        return "<color=" + MatchRules.TeamHex[0] + ">BLUE " + MatchRules.TeamPoints(0) + "</color>  :  <color=" + MatchRules.TeamHex[1] + ">"
            + MatchRules.TeamPoints(1) + " RED</color>   /   YOU: " + MatchRules.TeamNames[mine];
    }

    // ชื่อพร้อมสีทีม (โหมดทีม) สำหรับข้อความ rich text
    private string TeamTinted(int combatantId, string name)
    {
        int team = isTeamMode ? MatchRules.TeamOf(combatantId) : -1;
        return team >= 0 ? "<color=" + MatchRules.TeamHex[team] + ">" + name + "</color>" : name;
    }

    // ตัวเลขที่แสดงในตาราง: โหมดยึดจุด/เก็บดาว = คะแนนโหมด, นอกนั้น = Kill
    private bool ModeShowsScore => gameMode == MatchRules.ModeKoth || gameMode == MatchRules.ModeStars || WinRuleActive;
    // คืนตัวเลขที่จะแสดงของแถวนั้น: คะแนนโหมด หรือจำนวน Kill ตาม ModeShowsScore
    private int DisplayScore(MatchRules.Standing entry) => ModeShowsScore ? entry.score : entry.kills;

    // ทำความสะอาดชื่อผู้เล่น: ตัด < > ออกกันแทรก rich text, ชื่อว่างใช้ "PILOT"
    private static string CleanName(string name)
        => string.IsNullOrEmpty(name) ? "PILOT" : name.Replace("<", "").Replace(">", "");

    // ===== Kill Feed =====
    private sealed class FeedLine { public string text; public Color color; public float until; }
    private readonly List<FeedLine> killFeed = new List<FeedLine>(); // รายการข้อความใครฆ่าใครที่แสดงอยู่ พร้อมเวลาหายไป
    private TMP_Text[] killFeedLabels; // ป้ายข้อความ Kill Feed มุมจอ (หนึ่งป้ายต่อหนึ่งบรรทัด)
    private const int KillFeedLines = 5; // จำนวนบรรทัด Kill Feed สูงสุดที่แสดงพร้อมกัน

    // เรียกจาก PlayerController.OnPlayerDiedRPC (ทุกเครื่อง) เมื่อยานลำหนึ่งตาย
    public void OnCombatantDied(int killerId, PlayerController victim)
    {
        // เงื่อนไขชนะ BOUNTY HUNT: แต้มค่าหัว (ต้องคิดก่อนล้างสตรีคของเหยื่อ) (GameplayManager.WinRules.cs)
        AwardBountyFor(killerId, victim);
        // เฟส 7: นับ Kill Streak (GameplayManager.Content.cs)
        TrackKillStreak(killerId, victim);
        // เฟส 7B: ผลการตายในโหมดเกม (ลำดับตาย Battle Royale / ดาวหล่น)
        ModeOnDeath(killerId, victim);
        if (!FeatureFlags.KillFeed || victim == null) return;
        string victimName = CleanName(victim.PilotName);
        string line;
        PlayerController killer = null;
        victimName = TeamTinted(victim.CombatantId, victimName);
        if (killerId == victim.CombatantId) line = victimName + "  <color=#9AA6B2>self-destructed</color>";
        else
        {
            string killerName = null;
            if (killerId > 0) killer = PlayerController.FindCombatant(killerId);
            if (killer != null) killerName = killer.PilotName;
            else if (killerId > 0 && killerId < PlayerController.BotIdBase && PhotonNetwork.CurrentRoom != null)
            {
                var player = PhotonNetwork.CurrentRoom.GetPlayer(killerId);
                if (player != null) killerName = player.NickName;
            }
            line = killerName != null
                ? TeamTinted(killerId, CleanName(killerName)) + "  <color=#FF6A50>>></color>  " + victimName
                : victimName + "  <color=#9AA6B2>destroyed by hazard</color>";
        }
        bool involvesMe = victim == localPlayer || (killer != null && killer == localPlayer);
        AddFeedLine(line, involvesMe ? new Color(1f, .85f, .35f) : Color.white);
    }

    // เพิ่มบรรทัดใหม่บนสุดของ Kill Feed (อยู่ 6 วิ เก็บไม่เกิน 5 บรรทัด)
    // ไม่ทำอะไรถ้าปิด Kill Feed ใน FeatureFlags หรือในหน้าตั้งค่าผู้เล่น
    internal void AddFeedLine(string text, Color color)
    {
        if (!FeatureFlags.KillFeed || !GameSettings.KillFeed) return; // เฟส 9: ผู้เล่นปิด Kill Feed ได้
        killFeed.Insert(0, new FeedLine { text = text, color = color, until = Time.unscaledTime + 6f });
        if (killFeed.Count > KillFeedLines) killFeed.RemoveAt(killFeed.Count - 1);
    }

    // วาด Kill Feed ใต้มินิแม็พ (ซ้ายบน) บรรทัดใหม่อยู่บนสุด หายเองใน 6 วินาที
    private void UpdateKillFeed()
    {
        if (battleHud == null) return;
        killFeed.RemoveAll(line => Time.unscaledTime >= line.until);
        if (killFeedLabels == null)
        {
            if (killFeed.Count == 0) return;
            killFeedLabels = new TMP_Text[KillFeedLines];
            for (int i = 0; i < KillFeedLines; i++)
            {
                var label = BattleLabel("KillFeed" + i, battleHud, "", -438, 140 - i * 26, 360, 24, 16);
                label.alignment = TextAlignmentOptions.Left;
                label.richText = true;
                label.outlineWidth = .2f;
                label.outlineColor = Color.black;
                killFeedLabels[i] = label;
            }
        }
        bool hide = isMatchEnding || resultShown;
        for (int i = 0; i < KillFeedLines; i++)
        {
            bool on = !hide && i < killFeed.Count;
            killFeedLabels[i].gameObject.SetActive(on);
            if (!on) continue;
            killFeedLabels[i].text = killFeed[i].text;
            Color c = killFeed[i].color;
            c.a = Mathf.Clamp01(killFeed[i].until - Time.unscaledTime);
            killFeedLabels[i].color = c;
        }
    }

    // ===== ตารางคะแนน =====
    private Image miniBoard;
    private TMP_Text miniBoardText;
    private Image fullBoard;
    private TMP_Text[,] fullBoardCells;
    private Image[] fullBoardHighlights;

    // อัปเดตตารางคะแนนย่อมุมขวา (4 อันดับแรก + แถวของเรา) และตารางเต็มถ้าเปิดอยู่
    // ซ่อนตารางเมื่อแมตช์กำลังจบหรือขึ้นหน้าผลแล้ว (เรียก 4 ครั้ง/วิ)
    private void RefreshScoreboards()
    {
        if (!FeatureFlags.Scoreboard || battleHud == null) return;
        if (miniBoard == null) { CreateMiniBoard(); miniBoardPlaced = false; }
        if (!miniBoardPlaced) AvoidSettingsButton();
        bool show = !isMatchEnding && !resultShown;
        miniBoard.gameObject.SetActive(show);
        if (!show) { if (fullBoard != null) fullBoard.gameObject.SetActive(false); return; }

        // ตารางย่อ: 4 อันดับแรก (ถ้าเราไม่ติด แสดงเราแทนแถวที่ 4)
        var lines = new System.Text.StringBuilder(isTeamMode
            ? "<color=" + MatchRules.TeamHex[0] + ">BLUE " + MatchRules.TeamKills(0) + "</color> : <color=" + MatchRules.TeamHex[1] + ">" + MatchRules.TeamKills(1) + " RED</color>  <size=12><color=#8A9AAA>(TAP)</color></size>\n"
            : "<color=#3AD1EB>SCOREBOARD</color>  <size=12><color=#8A9AAA>(TAP)</color></size>\n");
        int rows = Mathf.Min(4, cachedStandings.Count);
        var me = LocalStanding(cachedStandings);
        int myIndex = me != null ? cachedStandings.IndexOf(me) : -1;
        for (int i = 0; i < rows; i++)
        {
            var entry = i == rows - 1 && myIndex >= rows ? me : cachedStandings[i];
            string row = RankOf(entry, cachedStandings) + ". " + Shorten(CleanName(entry.name), 12) + "  " + DisplayScore(entry);
            int rowTeam = isTeamMode ? MatchRules.TeamOf(entry.id) : -1;
            lines.Append(entry.isLocal ? "<color=#FFD95A>" + row + "</color>" : entry.left ? "<color=#7A8590>" + row + "</color>"
                : rowTeam >= 0 ? "<color=" + MatchRules.TeamHex[rowTeam] + ">" + row + "</color>" : row);
            if (i < rows - 1) lines.Append('\n');
        }
        miniBoardText.text = lines.ToString();
        if (fullBoard != null && fullBoard.gameObject.activeSelf) FillFullBoard();
    }

    // ตัดข้อความให้ยาวไม่เกิน max ตัวอักษร (เกินจะตัดแล้วต่อท้ายด้วย ".")
    private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + ".";

    private bool miniBoardPlaced; // true = จัดตำแหน่งตารางย่อไม่ให้ทับปุ่มตั้งค่าแล้ว

    // ตารางย่อห้ามทับปุ่มตั้งค่า (Btn_Exit ที่วางไว้ใน Scene): ถ้าทับกัน เลื่อนตารางลงไปใต้ปุ่ม
    private void AvoidSettingsButton()
    {
        if (Time.timeSinceLevelLoad < .5f) return; // รอให้ Canvas จัดขนาดเสร็จก่อนวัดตำแหน่ง
        miniBoardPlaced = true;
        var exit = battleHud.parent != null ? battleHud.parent.Find("TopCenter/Btn_Exit") as RectTransform : null;
        var board = miniBoard.rectTransform;
        if (exit == null || !exit.gameObject.activeInHierarchy) return;
        Rect a = WorldRect(exit), b = WorldRect(board);
        if (!a.Overlaps(b)) return;
        float scale = board.lossyScale.y > 0 ? board.lossyScale.y : 1f;
        board.anchoredPosition -= new Vector2(0, (b.yMax - a.yMin) / scale + 8f);
    }

    // แปลง RectTransform เป็นสี่เหลี่ยมในพิกัดโลก ใช้เช็กว่า UI สองชิ้นทับกันหรือไม่
    private static Rect WorldRect(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    // ตารางย่อมุมขวาบน (แตะเพื่อเปิด/ปิดตารางเต็ม)
    private void CreateMiniBoard()
    {
        miniBoard = BattlePanel("MiniScoreboard", battleHud, 520, 180, 230, 118, new Color(.035f, .065f, .12f, .78f));
        miniBoard.raycastTarget = true;
        var button = miniBoard.gameObject.AddComponent<Button>();
        button.targetGraphic = miniBoard;
        button.onClick.AddListener(ToggleFullBoard);
        miniBoardText = BattleLabel("Rows", miniBoard.transform, "", 0, 0, 214, 110, 16);
        miniBoardText.alignment = TextAlignmentOptions.TopLeft;
        miniBoardText.enableAutoSizing = false;
        miniBoardText.fontSize = 15;
        miniBoardText.richText = true;
        miniBoardText.textWrappingMode = TextWrappingModes.Normal;
    }

    // เปิด/ปิดตารางคะแนนเต็มกลางจอ (สร้างครั้งแรกเมื่อกด) และเติมข้อมูลล่าสุดเมื่อเปิด
    private void ToggleFullBoard()
    {
        if (fullBoard == null) CreateFullBoard();
        bool show = !fullBoard.gameObject.activeSelf;
        fullBoard.gameObject.SetActive(show);
        if (show) { fullBoard.transform.SetAsLastSibling(); FillFullBoard(); }
    }

    // ตารางเต็มกลางจอ: อันดับ / ชื่อ / Kill / ตาย (แตะที่ตารางเพื่อปิด)
    private void CreateFullBoard()
    {
        int max = MatchRules.MaxCombatants;
        fullBoard = BattlePanel("FullScoreboard", battleHud, 0, 10, 660, 90 + max * 34, new Color(.02f, .04f, .09f, .94f));
        fullBoard.raycastTarget = true;
        var close = fullBoard.gameObject.AddComponent<Button>();
        close.targetGraphic = fullBoard;
        close.onClick.AddListener(ToggleFullBoard);
        float top = (90 + max * 34) * .5f;
        BattleLabel("Title", fullBoard.transform, "SCOREBOARD  /  " + MatchRules.ModeTitles[gameMode] + (gameMode == MatchRules.ModeDeathmatch ? "  /  FIRST TO " + targetKills + " KILLS" : ""), 0, top - 24, 620, 30, 22);
        float headerY = top - 58;
        string[] headers = { "#", "PILOT", ModeShowsScore ? "SCORE" : "KILLS", "DEATHS" };
        float[] xs = { -280, -80, 160, 260 };
        float[] ws = { 50, 330, 100, 100 };
        for (int c = 0; c < 4; c++)
        {
            var h = BattleLabel("Head" + c, fullBoard.transform, headers[c], xs[c], headerY, ws[c], 26, 16);
            h.color = new Color(.23f, .82f, .92f);
        }
        fullBoardCells = new TMP_Text[max, 4];
        fullBoardHighlights = new Image[max];
        for (int r = 0; r < max; r++)
        {
            float y = headerY - 34 * (r + 1);
            fullBoardHighlights[r] = BattlePanel("RowGlow" + r, fullBoard.transform, 0, y, 630, 30, new Color(1f, .85f, .3f, .14f));
            for (int c = 0; c < 4; c++)
            {
                fullBoardCells[r, c] = BattleLabel("Cell" + r + "_" + c, fullBoard.transform, "", xs[c], y, ws[c], 28, 18);
                fullBoardCells[r, c].richText = false;
                if (c == 1) fullBoardCells[r, c].alignment = TextAlignmentOptions.Left;
            }
        }
        fullBoard.gameObject.SetActive(false);
    }

    // เติมข้อมูลทุกแถวของตารางเต็ม: อันดับ/ชื่อ/คะแนน/ตาย ไฮไลต์แถวของเรา
    // สีตามทีม และแถวคนที่ออกไปแล้วเป็นสีเทา
    private void FillFullBoard()
    {
        int max = MatchRules.MaxCombatants;
        for (int r = 0; r < max; r++)
        {
            bool has = r < cachedStandings.Count;
            fullBoardHighlights[r].gameObject.SetActive(has && cachedStandings[r].isLocal);
            for (int c = 0; c < 4; c++) fullBoardCells[r, c].gameObject.SetActive(has);
            if (!has) continue;
            var e = cachedStandings[r];
            fullBoardCells[r, 0].text = RankOf(e, cachedStandings).ToString();
            fullBoardCells[r, 1].text = CleanName(e.name) + (e.isLocal ? "  (YOU)" : "") + (e.left ? "  (LEFT)" : "");
            fullBoardCells[r, 2].text = DisplayScore(e).ToString();
            fullBoardCells[r, 3].text = e.deaths.ToString();
            int team = isTeamMode ? MatchRules.TeamOf(e.id) : -1;
            Color color = e.left ? new Color(.5f, .55f, .6f) : e.isLocal ? new Color(1f, .87f, .4f) : team >= 0 ? MatchRules.TeamColors[team] : Color.white;
            if (team >= 0) fullBoardCells[r, 0].text = MatchRules.TeamNames[team];
            for (int c = 0; c < 4; c++) fullBoardCells[r, c].color = color;
        }
    }

    // ===== หน้าผลแบบจัดอันดับ =====
    // เหรียญตามอันดับ: ที่ 1 ได้เท่าชนะ 1v1, ที่สุดท้ายได้เท่าแพ้, ระหว่างกลางไล่ลดลงเท่าๆ กัน
    private int PlaceReward(int rank, int count)
    {
        if (count <= 1) return WinReward;
        float t = (float)(count - rank) / (count - 1);
        return LoseReward + Mathf.RoundToInt((WinReward - LoseReward) * Mathf.Clamp01(t));
    }

    // แสดงหน้าผลแมตช์หลายคน: คิดอันดับ/ผลทีม, ให้เหรียญตามอันดับ, บันทึกผลลง Firebase
    // โหมดร่วมมือ (Survival/Campaign) จะไปใช้ ShowCoopResult แทน
    private void ShowFreeForAllResult()
    {
        // เฟส 7B: โหมดร่วมมือ (Survival / Campaign) มีหน้าผลของตัวเอง
        if (MatchRules.IsCoop(PhotonNetwork.CurrentRoom)) { ShowCoopResult(); return; }
        if (resultShown) return;
        resultShown = true;
        isMatchEnding = true;
        var standings = MatchRules.AllStandings();
        cachedStandings = standings;
        var me = LocalStanding(standings);
        int count = standings.Count;
        int myRank = me != null ? RankOf(me, standings) : count;
        int sharedFirst = 0;
        foreach (var entry in standings) if (RankOf(entry, standings) == 1) sharedFirst++;
        bool won = myRank == 1 && sharedFirst == 1;
        int reward = PlaceReward(myRank, count);
        int myKills = me != null ? me.kills : MatchRules.LocalKills();
        // โหมดทีม: ผลตัดสินจาก Kill รวมของทีม (1 = ทีมเราชนะ, 0 = เสมอ, -1 = แพ้) ทั้งทีมได้เหรียญเท่ากัน
        int blueKills = MatchRules.TeamPoints(0), redKills = MatchRules.TeamPoints(1); // เงื่อนไขชนะแบบใหม่ = คะแนนรวมทีม
        int myTeam = LocalTeam;
        int teamOutcome = blueKills == redKills ? 0 : (myTeam == 0 ? blueKills > redKills : redKills > blueKills) ? 1 : -1;
        if (isTeamMode)
        {
            won = teamOutcome == 1;
            reward = won ? WinReward : teamOutcome == 0 ? 0 : LoseReward;
        }

        // 1) บันทึกผล (FFA: ที่ 1 คนเดียว = ชนะ, นอกนั้นนับเป็นแพ้แต่ได้เหรียญตามอันดับ / ทีม: ชนะ-แพ้-เสมอทั้งทีม)
        if (FirebaseManager.Instance != null)
        {
            if (isTeamMode && teamOutcome == 0) FirebaseManager.Instance.RecordDrawMatch("TEAM", myKills, GetCurrentMapName());
            else FirebaseManager.Instance.RecordMatchResult(won, isTeamMode ? "TEAM" : "FFA", reward, myKills, GetCurrentMapName());
        }

        // 2) เปิดหน้าผล
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(resultPanel.transform));
            resultPanel.transform.SetAsLastSibling();
        }
        if (battleHud != null) battleHud.gameObject.SetActive(false);
        if (resultHeadline != null)
        {
            if (isTeamMode)
            {
                resultHeadline.text = teamOutcome == 1 ? "VICTORY" : teamOutcome == 0 ? "DRAW" : "DEFEAT";
                resultHeadline.color = teamOutcome == 1 ? new Color(1f, .8f, .35f) : teamOutcome == 0 ? Color.white : new Color(1f, .4f, .45f);
            }
            else
            {
                resultHeadline.text = won ? "VICTORY" : myRank == 1 ? "DRAW  /  SHARED 1ST" : "#" + myRank + "  OF  " + count;
                resultHeadline.color = won ? new Color(1f, .8f, .35f) : myRank <= 3 ? new Color(.55f, .9f, 1f) : new Color(1f, .5f, .5f);
            }
        }
        if (resultRoomNumber != null)
            resultRoomNumber.text = "MATCH COMPLETE / ROOM " + (PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "--") + (isTeamMode ? " / TEAM BATTLE" : " / FREE-FOR-ALL");
        if (resultScore != null)
            resultScore.text = isTeamMode
                ? "<color=" + MatchRules.TeamHex[0] + ">BLUE " + blueKills + "</color>  :  <color=" + MatchRules.TeamHex[1] + ">" + redKills + " RED</color>    YOUR KILLS " + myKills + "    +" + reward + " ASTRONIUM"
                : "YOUR PLACE  #" + myRank + " / " + count + "    KILLS " + myKills + (WinRuleActive && me != null ? "    SCORE " + me.score : "") + "    +" + reward + " ASTRONIUM";

        // 3) ซ่อนการ์ด 1v1 แล้วแสดงตารางอันดับ
        if (resultSurface != null)
        {
            foreach (string part in new[] { "YourResult", "RivalResult", "Versus" })
            {
                var t = resultSurface.Find(part);
                if (t != null) t.gameObject.SetActive(false);
            }
            BuildRankingTable(standings, teamOutcome, myTeam);
        }
        if (btnReturnToMenu != null)
        {
            btnReturnToMenu.onClick.RemoveAllListeners();
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
        // เฟส 4: XP / ภารกิจ / Achievement
        ReportProgress(won, isTeamMode ? teamOutcome == 0 : myRank == 1 && !won, myRank, count, reward);
        FitResultUI();
    }

    // ตารางอันดับในหน้าผล (โหมดทีม: คอลัมน์แรกเป็นชื่อทีม และเหรียญตามผลของทีม)
    private void BuildRankingTable(List<MatchRules.Standing> standings, int teamOutcome, int myTeam)
    {
        var old = resultSurface.Find("Ranking");
        if (old != null) Destroy(old.gameObject);
        int max = MatchRules.MaxCombatants;
        var table = BattlePanel("Ranking", resultSurface, 0, -25, 940, 390, new Color(.035f, .065f, .12f, .95f));
        string[] headers = { isTeamMode ? "TEAM" : "#", "PILOT", ModeShowsScore ? "SCORE" : "KILLS", "DEATHS", "REWARD" };
        float[] xs = { -420, -170, 140, 250, 375 };
        float[] ws = { 60, 360, 100, 110, 140 };
        for (int c = 0; c < headers.Length; c++)
        {
            var h = BattleLabel("Head" + c, table.transform, headers[c], xs[c], 172, ws[c], 26, 17);
            h.color = new Color(.23f, .82f, .92f);
        }
        int count = standings.Count;
        int mvpKills = count > 0 ? standings[0].kills : 0;
        for (int r = 0; r < Mathf.Min(max, count); r++)
        {
            var e = standings[r];
            int rank = RankOf(e, standings);
            float y = 138 - r * 33;
            if (e.isLocal) BattlePanel("You", table.transform, 0, y, 920, 30, new Color(1f, .85f, .3f, .16f));
            bool mvp = r == 0 && mvpKills > 0;
            int team = isTeamMode ? MatchRules.TeamOf(e.id) : -1;
            int entryReward = PlaceReward(rank, count);
            if (team >= 0)
            {
                int outcome = team == myTeam ? teamOutcome : -teamOutcome;
                entryReward = outcome == 1 ? WinReward : outcome == 0 ? 0 : LoseReward;
            }
            string[] cells = { team >= 0 ? MatchRules.TeamNames[team] : rank.ToString(), CleanName(e.name) + (mvp ? "   MVP" : "") + (e.isLocal ? "  (YOU)" : ""),
                DisplayScore(e).ToString(), e.deaths.ToString(), "+" + entryReward };
            for (int c = 0; c < cells.Length; c++)
            {
                var label = BattleLabel("R" + r + "C" + c, table.transform, cells[c], xs[c], y, ws[c], 28, 19);
                label.richText = false;
                if (c == 1) label.alignment = TextAlignmentOptions.Left;
                label.color = e.isLocal ? new Color(1f, .87f, .4f) : mvp ? new Color(1f, .75f, .3f) : Color.white;
                if (c == 0 && team >= 0) label.color = MatchRules.TeamColors[team];
            }
        }
    }
}
