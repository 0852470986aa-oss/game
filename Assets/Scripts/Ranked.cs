// Ranked.cs — ระบบแรงค์ (เฟส 6): คะแนน MMR แบบ Elo, ระดับแรงค์ Bronze → Legend, ฤดูกาลรายเดือน, รางวัลจบฤดูกาล
// แมตช์ Ranked = ห้อง 1 VS 1 ที่หาด้วยปุ่ม FIND RANKED MATCH (กติกาตายตัว: 5 Kill / 5 นาที / ปิดตีบวก / ไม่มีบอท)
// คะแนน: ชนะคู่ที่ MMR สูงกว่า = ได้เยอะ, แพ้คู่ที่ต่ำกว่า = เสียเยอะ (สูตร Elo K = 48 ช่วง 10 แมตช์แรก แล้ว K = 32)
// ข้อมูลเก็บ: Firebase users/{uid}/mmr (ตัวเลข ใช้เรียงตารางอันดับ) + users/{uid}/rank_json + PlayerPrefs "rank_{uid}"
// ปิดได้ด้วย FeatureFlags.Ranked / Leaderboard / Seasons
using System;
using UnityEngine;

[Serializable]
public class RankData
{
    public int mmr = 1000;
    public int peak = 1000;
    public int wins, losses, draws;
    public int games;              // จำนวนแมตช์แรงค์ทั้งหมด (ใช้เลือกค่า K)
    public string season = "";
    public int lastSeasonPeak;     // MMR สูงสุดของฤดูกาลที่แล้ว (ใช้คิดรางวัล)
    public bool rewardClaimed = true;
    public long savedAt;
}

public static class Ranked
{
    // ===== ระดับแรงค์ (แก้ช่วงคะแนนได้ที่นี่) =====
    public static readonly string[] TierNames = { "BRONZE", "SILVER", "GOLD", "PLATINUM", "DIAMOND", "MASTER", "LEGEND" };
    public static readonly int[] TierMin = { 0, 1100, 1250, 1400, 1550, 1700, 1850 };
    public static readonly Color[] TierColors =
    {
        new Color(.8f, .5f, .3f), new Color(.78f, .82f, .88f), new Color(1f, .8f, .3f), new Color(.4f, .9f, .85f),
        new Color(.45f, .7f, 1f), new Color(.8f, .45f, 1f), new Color(1f, .35f, .35f)
    };
    public static readonly string[] TierHex = { "#CC804D", "#C7D1E0", "#FFCC4D", "#66E6D9", "#73B3FF", "#CC73FF", "#FF5959" };
    // รางวัลจบฤดูกาลตามแรงค์สูงสุดที่ทำได้
    public static readonly int[] SeasonRewardCoins = { 100, 200, 350, 500, 800, 1200, 2000 };
    public const int StartMmr = 1000;

    public static int TierOf(int mmr)
    {
        int tier = 0;
        for (int i = 0; i < TierMin.Length; i++) if (mmr >= TierMin[i]) tier = i;
        return tier;
    }
    public static string TierName(int mmr) => TierNames[TierOf(mmr)];
    // คะแนนที่ต้องได้อีกถึงแรงค์ถัดไป (-1 = สูงสุดแล้ว)
    public static int PointsToNext(int mmr)
    {
        int tier = TierOf(mmr);
        return tier + 1 < TierMin.Length ? TierMin[tier + 1] - mmr : -1;
    }

    // ===== ฤดูกาล (รายเดือน) =====
    public static string SeasonId => DateTime.Now.ToString("yyyy-MM");
    public static string SeasonName => "SEASON " + DateTime.Now.ToString("MMM yyyy").ToUpperInvariant();
    public static TimeSpan SeasonLeft
    {
        get
        {
            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, 1).AddMonths(1) - now;
        }
    }

    // ===== สถานะ =====
    public static RankData Data { get; private set; }
    public static event Action Changed;
    private static string loadedFor;
    private static string Uid => FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn() ? FirebaseManager.Instance.GetUserId() : "local";
    private static string PrefsKey => "rank_" + Uid;
    public static int Mmr => Data != null ? Data.mmr : StartMmr;

    public static void Load(Action done = null)
    {
        LoadLocal();
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsLoggedIn()) { Changed?.Invoke(); done?.Invoke(); return; }
        string uid = Uid;
        FirebaseManager.Instance.LoadUserJson("rank_json", json =>
        {
            if (uid != Uid) return;
            var remote = Parse(json);
            if (remote != null && remote.savedAt >= Data.savedAt) Data = remote;
            if (RollSeason()) Save();
            Changed?.Invoke();
            done?.Invoke();
        });
    }

    public static void EnsureLoaded()
    {
        if (Data == null || loadedFor != Uid) LoadLocal();
    }

    private static void LoadLocal()
    {
        loadedFor = Uid;
        Data = Parse(PlayerPrefs.GetString(PrefsKey, "")) ?? new RankData();
        RollSeason();
    }

    private static RankData Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<RankData>(json); } catch (Exception) { return null; }
    }

    public static void Save()
    {
        if (Data == null) return;
        Data.savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string json = JsonUtility.ToJson(Data);
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn())
            FirebaseManager.Instance.SaveRank(Data.mmr, json);
        Changed?.Invoke();
    }

    // ขึ้นเดือนใหม่: จำ MMR สูงสุดไว้คิดรางวัล แล้วลดคะแนนลงครึ่งหนึ่งของส่วนที่เกิน 1000 (Soft reset)
    private static bool RollSeason()
    {
        if (!FeatureFlags.Seasons || Data.season == SeasonId) return false;
        if (!string.IsNullOrEmpty(Data.season) && Data.games > 0)
        {
            Data.lastSeasonPeak = Data.peak;
            Data.rewardClaimed = false;
            if (Data.mmr > StartMmr) Data.mmr = StartMmr + (Data.mmr - StartMmr) / 2;
            Data.wins = Data.losses = Data.draws = 0;
        }
        Data.peak = Data.mmr;
        Data.season = SeasonId;
        return true;
    }

    public static bool CanClaimSeasonReward => Data != null && FeatureFlags.Seasons && !Data.rewardClaimed && Data.lastSeasonPeak > 0;

    public static bool ClaimSeasonReward(out int coins)
    {
        coins = 0;
        if (!CanClaimSeasonReward) return false;
        coins = SeasonRewardCoins[TierOf(Data.lastSeasonPeak)];
        Data.rewardClaimed = true;
        if (FirebaseManager.Instance != null) FirebaseManager.Instance.AddCoins(coins);
        Save();
        return true;
    }

    // ===== บันทึกผลแมตช์แรงค์ =====
    // คืนคะแนนที่เปลี่ยน (+/-)
    public static int RecordMatch(bool won, bool draw, int opponentMmr)
    {
        EnsureLoaded();
        RollSeason();
        float expected = 1f / (1f + Mathf.Pow(10f, (opponentMmr - Data.mmr) / 400f));
        float score = won ? 1f : draw ? .5f : 0f;
        int k = Data.games < 10 ? 48 : 32;
        int delta = Mathf.RoundToInt(k * (score - expected));
        if (won && delta < 1) delta = 1;          // ชนะได้อย่างน้อย 1
        Data.mmr = Mathf.Max(0, Data.mmr + delta);
        Data.peak = Mathf.Max(Data.peak, Data.mmr);
        Data.games++;
        if (won) Data.wins++; else if (draw) Data.draws++; else Data.losses++;
        Save();
        return delta;
    }
}
