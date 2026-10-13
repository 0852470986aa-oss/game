// BattlePass.cs — Battle Pass รายเดือน 30 ขั้น เส้นฟรี + เส้นพรีเมียม (FeatureFlags.PremiumPass)
// - ฤดูกาลเดียวกับแรงค์ (เปลี่ยนทุกเดือน Ranked.SeasonId) ขึ้นเดือนใหม่ = XP และรางวัลเริ่มใหม่ (สกินที่ได้ไปแล้วยังอยู่)
// - XP ของ Pass ได้เท่ากับ XP จากแมตช์ (GameplayManager.Progress) และจากการรับรางวัลภารกิจ (LobbyManager.Progress)
// - ขั้นละ 150 XP / ปลดเส้นพรีเมียมด้วยคริสตัล / ซื้อขั้นเพิ่มได้
// - ข้อมูลเก็บใน PremiumData (Premium.cs)
using System;
using UnityEngine;

// ชนิดรางวัล
public enum PassRewardType { Coins, Crystals, Tickets, Skin }

// รางวัล 1 ช่อง
public class PassReward
{
    public PassRewardType type;
    public int amount;      // จำนวน (Skin = 1)
    public string skin;     // id สกิน (type = Skin)

    // ข้อความสั้นบนการ์ด
    public string Label
    {
        get
        {
            switch (type)
            {
                case PassRewardType.Coins: return amount.ToString("N0") + "\nASTRONIUM";
                case PassRewardType.Crystals: return amount + "\nCRYSTALS";
                case PassRewardType.Tickets: return "x" + amount + "\nTICKET";
                default: var def = SkinCatalog.Find(skin); return (def != null ? def.name : skin) + "\nSKIN";
            }
        }
    }
}

public static class BattlePass
{
    public const int Tiers = 30;               // จำนวนขั้น
    public const int XpPerTier = 150;          // XP ต่อขั้น
    public const int PremiumCost = 480;        // คริสตัลปลดเส้นพรีเมียม
    public const int TierSkipCost = 100;       // คริสตัลซื้อเพิ่ม 1 ขั้น

    private static PremiumData D { get { Premium.EnsureLoaded(); return Premium.Data; } }

    public static string SeasonId => Ranked.SeasonId;
    public static string SeasonName => Ranked.SeasonName;
    public static TimeSpan SeasonLeft => Ranked.SeasonLeft;

    // ขั้นปัจจุบัน (0 = ยังไม่ถึงขั้น 1) / XP ในขั้น
    public static int Tier => Mathf.Min(Tiers, D.passXp / XpPerTier);
    public static int XpIntoTier => Tier >= Tiers ? XpPerTier : D.passXp % XpPerTier;
    public static bool IsPremium => D.passPremium;

    // ขึ้นเดือนใหม่: เริ่ม Pass ใหม่ คืน true ถ้ามีการเปลี่ยน (เรียกจาก Premium.Load)
    public static bool RollSeason()
    {
        var data = Premium.Data;
        if (data == null || data.passSeason == SeasonId) return false;
        data.passSeason = SeasonId;
        data.passXp = 0;
        data.passPremium = false;
        data.passFree.Clear();
        data.passPrem.Clear();
        return true;
    }

    // รางวัลเส้นฟรีของขั้น tier (1..30)
    public static PassReward FreeReward(int tier)
    {
        if (tier == Tiers) return new PassReward { type = PassRewardType.Coins, amount = 1000 };
        if (tier % 8 == 0) return new PassReward { type = PassRewardType.Crystals, amount = 40 };
        if (tier % 5 == 0) return new PassReward { type = PassRewardType.Tickets, amount = 1 };
        return new PassReward { type = PassRewardType.Coins, amount = tier % 2 == 0 ? 200 : 150 };
    }

    // รางวัลเส้นพรีเมียมของขั้น tier (1..30)
    public static PassReward PremiumReward(int tier)
    {
        if (tier == Tiers) return new PassReward { type = PassRewardType.Skin, amount = 1, skin = "SOVEREIGN" };
        if (tier == 15) return new PassReward { type = PassRewardType.Skin, amount = 1, skin = "SEASON_ACE" };
        if (tier % 5 == 0) return new PassReward { type = PassRewardType.Tickets, amount = 2 };
        if (tier % 3 == 0) return new PassReward { type = PassRewardType.Crystals, amount = 50 };
        return new PassReward { type = PassRewardType.Coins, amount = 300 };
    }

    public static bool Reached(int tier) => tier <= Tier;
    public static bool FreeClaimed(int tier) => D.passFree.Contains(tier);
    public static bool PremiumClaimed(int tier) => D.passPrem.Contains(tier);
    public static bool CanClaimFree(int tier) => FeatureFlags.PremiumPass && Reached(tier) && !FreeClaimed(tier);
    public static bool CanClaimPremium(int tier) => FeatureFlags.PremiumPass && IsPremium && Reached(tier) && !PremiumClaimed(tier);

    // จำนวนรางวัลที่รอรับ (ใช้แสดงจุดแดง)
    public static int ClaimableCount()
    {
        if (!FeatureFlags.PremiumPass || Premium.Data == null) return 0;
        int count = 0;
        for (int tier = 1; tier <= Tier; tier++)
        {
            if (!FreeClaimed(tier)) count++;
            if (IsPremium && !PremiumClaimed(tier)) count++;
        }
        return count;
    }

    // เพิ่ม XP (จากแมตช์/ภารกิจ) คืนจำนวนขั้นที่ขึ้น
    public static int AddXp(int xp)
    {
        if (!FeatureFlags.PremiumPass || xp <= 0) return 0;
        Premium.EnsureLoaded();
        RollSeason();
        int before = Tier;
        D.passXp = Mathf.Min(Tiers * XpPerTier, D.passXp + xp);
        Premium.Save();
        return Tier - before;
    }

    // รับรางวัล 1 ช่อง คืนข้อความผล (null = รับไม่ได้)
    public static string Claim(int tier, bool premium)
    {
        if (premium ? !CanClaimPremium(tier) : !CanClaimFree(tier)) return null;
        var reward = premium ? PremiumReward(tier) : FreeReward(tier);
        (premium ? D.passPrem : D.passFree).Add(tier);
        string text = Give(reward);
        Premium.Save();
        return text;
    }

    // รับทุกอย่างที่รับได้ คืนจำนวนที่รับ
    public static int ClaimAll(out int coins, out int crystals, out int tickets, out int skins)
    {
        coins = crystals = tickets = skins = 0;
        int claimed = 0;
        for (int tier = 1; tier <= Tier; tier++)
            for (int lane = 0; lane < 2; lane++)
            {
                bool premium = lane == 1;
                if (premium ? !CanClaimPremium(tier) : !CanClaimFree(tier)) continue;
                var reward = premium ? PremiumReward(tier) : FreeReward(tier);
                (premium ? D.passPrem : D.passFree).Add(tier);
                if (reward.type == PassRewardType.Coins) coins += reward.amount;
                else if (reward.type == PassRewardType.Crystals) crystals += reward.amount;
                else if (reward.type == PassRewardType.Tickets) tickets += reward.amount;
                else { Premium.GrantSkin(reward.skin); skins++; }
                claimed++;
            }
        if (claimed == 0) return 0;
        D.tickets += tickets;
        if (coins > 0 && FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn()) FirebaseManager.Instance.AddCoins(coins);
        if (crystals > 0) Premium.AddCrystals(crystals);
        Premium.Save();
        return claimed;
    }

    // ให้รางวัลจริง
    private static string Give(PassReward reward)
    {
        switch (reward.type)
        {
            case PassRewardType.Coins:
                if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsLoggedIn()) FirebaseManager.Instance.AddCoins(reward.amount);
                return "+" + reward.amount + " ASTRONIUM";
            case PassRewardType.Crystals:
                Premium.AddCrystals(reward.amount);
                return "+" + reward.amount + " CRYSTALS";
            case PassRewardType.Tickets:
                D.tickets += reward.amount;
                return "+" + reward.amount + " GACHA TICKET";
            default:
                Premium.GrantSkin(reward.skin);
                var def = SkinCatalog.Find(reward.skin);
                return "NEW SKIN: " + (def != null ? def.name : reward.skin);
        }
    }

    // ปลดเส้นพรีเมียมด้วยคริสตัล
    public static void BuyPremium(Action<bool, string> done)
    {
        if (IsPremium) { done?.Invoke(false, "Premium pass is already active."); return; }
        Premium.SpendCrystals(PremiumCost, ok =>
        {
            if (!ok) { done?.Invoke(false, "NOT ENOUGH CRYSTALS  /  NEED " + PremiumCost); return; }
            D.passPremium = true;
            Premium.Save();
            done?.Invoke(true, "PREMIUM PASS UNLOCKED!");
        });
    }

    // ซื้อเพิ่ม 1 ขั้น
    public static void BuyTier(Action<bool, string> done)
    {
        if (Tier >= Tiers) { done?.Invoke(false, "Pass is already at max tier."); return; }
        Premium.SpendCrystals(TierSkipCost, ok =>
        {
            if (!ok) { done?.Invoke(false, "NOT ENOUGH CRYSTALS  /  NEED " + TierSkipCost); return; }
            D.passXp = Mathf.Min(Tiers * XpPerTier, (Tier + 1) * XpPerTier);
            Premium.Save();
            done?.Invoke(true, "TIER UP!  /  TIER " + Tier);
        });
    }
}
