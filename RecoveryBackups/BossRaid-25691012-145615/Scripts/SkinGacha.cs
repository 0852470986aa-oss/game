// SkinGacha.cs — สกินยาน (เปลี่ยนสี + แสงออร่า ไม่เปลี่ยนค่าพลัง) และระบบกาชา "STAR FORGE" (FeatureFlags.SkinGacha)
// สกินต่อท้ายสีย้อมเดิม 6 สี (ShipPaint) — index สกิน = ShipPaint.BaseCount + ลำดับใน SkinCatalog.All (อย่าสลับลำดับ: index ส่งผ่าน Photon)
// กติกากาชา (แสดงในหน้า ODDS ให้ผู้เล่นเห็น):
//   - LEGENDARY 1.6% / EPIC 8.4% / RARE 30% / ชิ้นส่วนดาว 60%
//   - การันตี EPIC ขึ้นไปทุก 10 ครั้ง, การันตี LEGENDARY ภายใน 70 ครั้ง
//   - ได้ LEGENDARY: 50% เป็นตัวเด่นของสัปดาห์ ถ้าไม่ใช่ ครั้งหน้าการันตีตัวเด่น
//   - ได้สกินซ้ำ → แปลงเป็นชิ้นส่วนดาว (RARE 15 / EPIC 50 / LEGENDARY 200) ชิ้นส่วนแลกสกินที่อยากได้ได้
using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkinRarity { Rare, Epic, Legendary }

// ที่มาของสกิน
public enum SkinSource { Gacha, Pass, Starter }

// นิยามสกิน 1 แบบ
public class SkinDef
{
    public string id;          // รหัส (เก็บในข้อมูลผู้เล่น)
    public string name;        // ชื่อที่แสดง
    public SkinRarity rarity;  // ความหายาก
    public SkinSource source;  // ได้จากไหน
    public Color tint;         // สีที่คูณกับภาพยาน
    public Color aura;         // สีแสงออร่ารอบยาน (alpha 0 = ไม่มี)
    public int auraStyle;      // 0 นิ่ง / 1 เต้นเป็นจังหวะ / 2 เปลี่ยนสีรุ้ง
}

// รายชื่อสกินทั้งหมด
public static class SkinCatalog
{
    private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

    // อย่าสลับลำดับ/ลบ: index ส่งผ่าน Photon (เพิ่มใหม่ต่อท้ายได้)
    public static readonly SkinDef[] All =
    {
        new SkinDef { id = "COBALT",    name = "COBALT",         rarity = SkinRarity.Rare,      source = SkinSource.Gacha,   tint = C(.5f, .65f, 1f),   aura = C(.3f, .5f, 1f, .35f),   auraStyle = 0 },
        new SkinDef { id = "MAGMA",     name = "MAGMA",          rarity = SkinRarity.Rare,      source = SkinSource.Gacha,   tint = C(1f, .55f, .35f),  aura = C(1f, .4f, .15f, .35f),  auraStyle = 0 },
        new SkinDef { id = "TOXIC",     name = "TOXIC",          rarity = SkinRarity.Rare,      source = SkinSource.Gacha,   tint = C(.65f, 1f, .35f),  aura = C(.5f, 1f, .2f, .35f),   auraStyle = 0 },
        new SkinDef { id = "ROSE",      name = "ROSE QUARTZ",    rarity = SkinRarity.Rare,      source = SkinSource.Gacha,   tint = C(1f, .65f, .85f),  aura = C(1f, .5f, .8f, .35f),   auraStyle = 0 },
        new SkinDef { id = "ARCTIC",    name = "ARCTIC",         rarity = SkinRarity.Rare,      source = SkinSource.Gacha,   tint = C(.82f, .95f, 1f),  aura = C(.7f, .95f, 1f, .35f),  auraStyle = 0 },
        new SkinDef { id = "NEBULA",    name = "NEBULA VIOLET",  rarity = SkinRarity.Epic,      source = SkinSource.Gacha,   tint = C(.78f, .55f, 1f),  aura = C(.65f, .35f, 1f, .55f), auraStyle = 1 },
        new SkinDef { id = "SOLAR",     name = "SOLAR FLARE",    rarity = SkinRarity.Epic,      source = SkinSource.Gacha,   tint = C(1f, .8f, .35f),   aura = C(1f, .65f, .1f, .55f),  auraStyle = 1 },
        new SkinDef { id = "ABYSS",     name = "ABYSS",          rarity = SkinRarity.Epic,      source = SkinSource.Gacha,   tint = C(.4f, .5f, .8f),   aura = C(.1f, .9f, .85f, .55f), auraStyle = 1 },
        new SkinDef { id = "JADE",      name = "JADE DRAGON",    rarity = SkinRarity.Epic,      source = SkinSource.Gacha,   tint = C(.45f, 1f, .75f),  aura = C(.2f, 1f, .6f, .55f),   auraStyle = 1 },
        new SkinDef { id = "PRISM",     name = "AURORA PRISM",   rarity = SkinRarity.Legendary, source = SkinSource.Gacha,   tint = C(.9f, 1f, 1f),     aura = C(1f, 1f, 1f, .7f),      auraStyle = 2 },
        new SkinDef { id = "VOID",      name = "VOID EMPEROR",   rarity = SkinRarity.Legendary, source = SkinSource.Gacha,   tint = C(.45f, .35f, .6f), aura = C(1f, .2f, .35f, .75f),  auraStyle = 1 },
        new SkinDef { id = "GOLDEN",    name = "GOLDEN COMET",   rarity = SkinRarity.Legendary, source = SkinSource.Gacha,   tint = C(1f, .88f, .45f),  aura = C(1f, .85f, .3f, .75f),  auraStyle = 1 },
        new SkinDef { id = "STARTER_ACE", name = "STARTER ACE",  rarity = SkinRarity.Epic,      source = SkinSource.Starter, tint = C(.55f, .9f, 1f),   aura = C(.35f, .85f, 1f, .5f),  auraStyle = 1 },
        new SkinDef { id = "SEASON_ACE",  name = "SEASON ACE",   rarity = SkinRarity.Epic,      source = SkinSource.Pass,    tint = C(.95f, .6f, .45f), aura = C(1f, .45f, .3f, .55f),  auraStyle = 1 },
        new SkinDef { id = "SOVEREIGN",   name = "STAR SOVEREIGN", rarity = SkinRarity.Legendary, source = SkinSource.Pass,  tint = C(.75f, .85f, 1f),  aura = C(1f, 1f, 1f, .7f),      auraStyle = 2 },
    };

    public static readonly string[] RarityNames = { "RARE", "EPIC", "LEGENDARY" };
    public static readonly Color[] RarityColors = { new Color(.35f, .7f, 1f), new Color(.8f, .45f, 1f), new Color(1f, .78f, .25f) };

    public static string[] Names { get { var names = new string[All.Length]; for (int i = 0; i < All.Length; i++) names[i] = All[i].name; return names; } }
    public static Color[] Tints { get { var tints = new Color[All.Length]; for (int i = 0; i < All.Length; i++) tints[i] = All[i].tint; return tints; } }

    public static SkinDef Find(string id) { foreach (var skin in All) if (skin.id == id) return skin; return null; }
    public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i; return -1; }
    // index ใน ShipPaint ของสกิน
    public static int PaintIndex(string id) { int i = IndexOf(id); return i < 0 ? 0 : ShipPaint.BaseCount + i; }
    // สกินจาก index ของ ShipPaint (ไม่ใช่สกิน = null)
    public static SkinDef FromPaint(int paint) { int i = paint - ShipPaint.BaseCount; return i >= 0 && i < All.Length ? All[i] : null; }
    // ผู้เล่นเครื่องนี้ใช้สกิน index นี้ได้ไหม (สีพื้นฐานใช้ได้เสมอ / สกินต้องเปิดระบบและมีแล้ว)
    public static bool Usable(int paint)
    {
        if (paint < ShipPaint.BaseCount) return true;
        var skin = FromPaint(paint);
        return skin != null && FeatureFlags.SkinGacha && Premium.OwnsSkin(skin.id);
    }
}

// ผลการสุ่ม 1 ครั้ง
public class GachaResult
{
    public SkinDef skin;     // สกินที่ได้ (null = ได้ชิ้นส่วนดาว)
    public bool isNew;       // สกินใหม่
    public int shards;       // ชิ้นส่วนที่ได้
    public SkinRarity rarity; // ระดับ (ผลชิ้นส่วน = Rare)
}

// ระบบกาชา
public static class SkinGacha
{
    public const int PullCost = 150;          // คริสตัลต่อ 1 ครั้ง
    public const int TenPullCost = 1500;      // คริสตัลต่อ 10 ครั้ง
    public const int HardPity = 70;           // การันตี LEGENDARY ภายในกี่ครั้ง
    public const int EpicPity = 10;           // การันตี EPIC ขึ้นไปทุกกี่ครั้ง
    public const float LegendaryRate = 1.6f, EpicRate = 8.4f, RareRate = 30f; // % (ที่เหลือ = ชิ้นส่วนดาว)
    public const int ShardResult = 10;        // ผลชิ้นส่วนดาวได้กี่ชิ้น
    public static readonly int[] DuplicateShards = { 15, 50, 200 };   // ของซ้ำได้ชิ้นส่วน (ตามระดับ)
    public static readonly int[] ExchangeCost = { 60, 200, 600 };     // แลกสกินด้วยชิ้นส่วน (ตามระดับ)

    // ข้อความกติกาในหน้า ODDS & RULES (PremiumLang.cs ใช้สร้างคำแปลไทยจากค่าคงที่ชุดเดียวกัน)
    // อัตรารวม 2.6% / 13.4% มาจากการจำลองสุ่ม 400,000 ครั้งด้วยกติกาด้านล่าง — ถ้าแก้ค่าโอกาส/การันตี ต้องจำลองใหม่แล้วแก้ตัวเลขนี้
    public static string Pct(float value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    public static string[] RuleLines => new[]
    {
        "<color=#FFC740>LEGENDARY  " + Pct(LegendaryRate) + "%</color>    <color=#CC73FF>EPIC  " + Pct(EpicRate) + "%</color>    <color=#59B3FF>RARE  " + Pct(RareRate) + "%</color>    STAR SHARDS  " + Pct(100 - LegendaryRate - EpicRate - RareRate) + "%",
        "Guaranteed EPIC or better every " + EpicPity + " pulls (a x10 pull always has one).",
        "Guaranteed LEGENDARY within " + HardPity + " pulls. Chance rises after pull 55.",
        "LEGENDARY: 50% featured skin. If not, the next LEGENDARY is the featured skin.",
        "Duplicate skins become Star Shards: RARE " + DuplicateShards[0] + " / EPIC " + DuplicateShards[1] + " / LEGENDARY " + DuplicateShards[2] + ".",
        "Exchange shards for any banner skin: RARE " + ExchangeCost[0] + " / EPIC " + ExchangeCost[1] + " / LEGENDARY " + ExchangeCost[2] + ".",
        "Overall rate with guarantees: LEGENDARY about 2.6%, EPIC about 13.4% (simulated 400,000 pulls).",
        "Skins change color and glow only. They never change ship stats.",
    };
    public static string[] RuleLinesThai => new[]
    {
        "",
        "การันตีระดับเอพิคขึ้นไปทุก " + EpicPity + " ครั้ง (สุ่ม x10 ได้อย่างน้อย 1 ใบเสมอ)",
        "การันตีระดับตำนานภายใน " + HardPity + " ครั้ง โอกาสเพิ่มขึ้นหลังครั้งที่ 55",
        "ได้ระดับตำนาน: 50% เป็นสกินเด่น ถ้าไม่ใช่ ครั้งถัดไปได้สกินเด่นแน่นอน",
        "สกินซ้ำแปลงเป็นชิ้นส่วนดาว: หายาก " + DuplicateShards[0] + " / เอพิค " + DuplicateShards[1] + " / ตำนาน " + DuplicateShards[2],
        "แลกชิ้นส่วนเป็นสกินในตู้ได้: หายาก " + ExchangeCost[0] + " / เอพิค " + ExchangeCost[1] + " / ตำนาน " + ExchangeCost[2],
        "อัตรารวมเมื่อนับการันตี: ตำนานประมาณ 2.6%, เอพิคประมาณ 13.4% (จำลองสุ่ม 400,000 ครั้ง)",
        "สกินเปลี่ยนแค่สีและแสงออร่า ไม่มีผลต่อค่าพลังของยาน",
    };

    // สกินในตู้กาชา
    public static List<SkinDef> Pool(SkinRarity rarity)
    {
        var list = new List<SkinDef>();
        foreach (var skin in SkinCatalog.All) if (skin.source == SkinSource.Gacha && skin.rarity == rarity) list.Add(skin);
        return list;
    }

    // ตัวเด่นของสัปดาห์ (หมุนเวียน LEGENDARY ทุกสัปดาห์)
    public static SkinDef Featured
    {
        get
        {
            var legends = Pool(SkinRarity.Legendary);
            int week = (int)((DateTime.UtcNow - new DateTime(2026, 1, 5)).TotalDays / 7);
            return legends[((week % legends.Count) + legends.Count) % legends.Count];
        }
    }
    // เวลาที่เหลือก่อนเปลี่ยนตัวเด่น
    public static TimeSpan FeaturedLeft
    {
        get
        {
            double days = (DateTime.UtcNow - new DateTime(2026, 1, 5)).TotalDays;
            return TimeSpan.FromDays(7 - days % 7);
        }
    }

    public static int PullsToLegendary { get { Premium.EnsureLoaded(); return Mathf.Max(1, HardPity - Premium.Data.pity); } }
    public static int PullsToEpic { get { Premium.EnsureLoaded(); return Mathf.Max(1, EpicPity - Premium.Data.epicPity); } }

    // สุ่ม count ครั้ง: ใช้ตั๋วก่อน ไม่มีตั๋วใช้คริสตัล done(ผล, ข้อความผิดพลาด)
    public static void Pull(int count, bool useTickets, Action<List<GachaResult>, string> done)
    {
        Premium.EnsureLoaded();
        if (useTickets)
        {
            if (Premium.Data.tickets < count) { done?.Invoke(null, "Not enough tickets."); return; }
            Premium.Data.tickets -= count;
            done?.Invoke(RollAndSave(count), null);
            return;
        }
        int cost = count >= 10 ? TenPullCost : PullCost * count;
        Premium.SpendCrystals(cost, ok =>
        {
            if (!ok) { done?.Invoke(null, "NOT ENOUGH CRYSTALS  /  NEED " + cost); return; }
            done?.Invoke(RollAndSave(count), null);
        });
    }

    private static List<GachaResult> RollAndSave(int count)
    {
        var results = new List<GachaResult>();
        bool hadEpicPlus = false;
        for (int i = 0; i < count; i++)
        {
            // x10: ครั้งสุดท้ายการันตี EPIC ขึ้นไปถ้ายังไม่ได้เลย
            bool forceEpic = count >= 10 && i == count - 1 && !hadEpicPlus;
            var result = RollOne(forceEpic);
            if (result.skin != null && result.rarity != SkinRarity.Rare) hadEpicPlus = true;
            results.Add(result);
        }
        Premium.Save();
        return results;
    }

    private static GachaResult RollOne(bool forceEpic)
    {
        var data = Premium.Data;
        data.totalPulls++;
        data.pity++;
        data.epicPity++;
        float roll = UnityEngine.Random.Range(0f, 100f);
        // ยิ่งใกล้การันตี ยิ่งเพิ่มโอกาส LEGENDARY (soft pity หลังครั้งที่ 55)
        float legendary = LegendaryRate + Mathf.Max(0, data.pity - 55) * 6f;
        SkinRarity? rarity = null;
        if (data.pity >= HardPity || roll < legendary) rarity = SkinRarity.Legendary;
        else if (data.epicPity >= EpicPity || forceEpic || roll < legendary + EpicRate) rarity = SkinRarity.Epic;
        else if (roll < legendary + EpicRate + RareRate) rarity = SkinRarity.Rare;

        var result = new GachaResult();
        if (rarity == null)
        {
            result.shards = ShardResult;
            result.rarity = SkinRarity.Rare;
            data.shards += ShardResult;
            data.pulls.Insert(0, new PullRecord { skin = "", rarity = 0, shards = ShardResult, at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            return result;
        }
        SkinDef skin;
        if (rarity == SkinRarity.Legendary)
        {
            var featured = Featured;
            if (data.lostFiftyFifty || UnityEngine.Random.value < .5f) { skin = featured; data.lostFiftyFifty = false; }
            else
            {
                var others = Pool(SkinRarity.Legendary);
                others.Remove(featured);
                skin = others[UnityEngine.Random.Range(0, others.Count)];
                data.lostFiftyFifty = true;
            }
            data.pity = 0;
            data.epicPity = 0;
        }
        else
        {
            var pool = Pool(rarity.Value);
            skin = pool[UnityEngine.Random.Range(0, pool.Count)];
            if (rarity == SkinRarity.Epic) data.epicPity = 0;
        }
        result.skin = skin;
        result.rarity = skin.rarity;
        if (data.skins.Contains(skin.id))
        {
            result.shards = DuplicateShards[(int)skin.rarity];
            data.shards += result.shards;
        }
        else
        {
            result.isNew = true;
            data.skins.Add(skin.id);
        }
        data.pulls.Insert(0, new PullRecord { skin = skin.id, rarity = (int)skin.rarity, shards = result.shards, at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        return result;
    }

    // แลกสกินด้วยชิ้นส่วนดาว (เฉพาะสกินในตู้กาชา)
    public static bool Exchange(SkinDef skin, out string message)
    {
        Premium.EnsureLoaded();
        message = "";
        if (skin == null || skin.source != SkinSource.Gacha) { message = "This skin cannot be exchanged."; return false; }
        if (Premium.Data.skins.Contains(skin.id)) { message = "You already own this skin."; return false; }
        int cost = ExchangeCost[(int)skin.rarity];
        if (Premium.Data.shards < cost) { message = "NOT ENOUGH STAR SHARDS  /  NEED " + cost; return false; }
        Premium.Data.shards -= cost;
        Premium.Data.skins.Add(skin.id);
        Premium.Save();
        message = "UNLOCKED: " + skin.name;
        return true;
    }
}
