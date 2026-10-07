// Lang.cs — ภาษาไทย / อังกฤษ (เฟส 9)
// วิธีทำงาน: UI ทั้งเกมเขียนเป็นภาษาอังกฤษไว้เหมือนเดิม ตัวแปลภาษา (LangTranslator) จะสแกนข้อความบนจอทุก 0.4 วินาที
// ถ้าข้อความตรงกับคำในตาราง Thai ด้านล่างทุกตัวอักษร จะเปลี่ยนเป็นภาษาไทย (สลับกลับเป็นอังกฤษได้ทันที)
// จึงไม่ต้องแก้โค้ด UI เดิมทุกจุด — อยากแปลคำไหนเพิ่ม ให้เพิ่มคู่คำในตาราง Thai
// ฟอนต์ไทย: TextMeshPro เดิม (LiberationSans) ไม่มีตัวอักษรไทย จึงเพิ่มฟอนต์ไทยเป็น Fallback ให้อัตโนมัติ (ใช้กับแชท/เนื้อเรื่องด้วย)
//   1) ถ้ามี Resources/Fonts/ThaiFont (ไฟล์ .ttf/.otf เช่น Sarabun, Kanit, Prompt) จะใช้ไฟล์นั้น (แนะนำ: หน้าตาเหมือนกันทุกเครื่อง)
//   2) ไม่มี = ใช้ฟอนต์ไทยของเครื่อง (Android: Noto Sans Thai, Windows: Tahoma/Leelawadee, iOS: Thonburi)
// ปิด FeatureFlags.Language = ใช้ภาษาอังกฤษอย่างเดียวและซ่อนปุ่มเลือกภาษา
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public static class Lang
{
    private const string PrefsKey = "Opt_Language";
    // true = ผู้เล่นเลือกภาษาไทย
    public static bool Thai => FeatureFlags.Language && PlayerPrefs.GetInt(PrefsKey, 0) == 1;
    public static string CurrentName => Thai ? "ไทย" : "ENGLISH";

    public static void SetThai(bool thai)
    {
        PlayerPrefs.SetInt(PrefsKey, thai ? 1 : 0);
        PlayerPrefs.Save();
        LangTranslator.RefreshNow();
    }

    // แปลข้อความ (ใช้ในโค้ดได้โดยตรง เช่น Lang.T("BACK")) ไม่มีในตาราง = คืนข้อความเดิม
    public static string T(string english)
    {
        if (!Thai || string.IsNullOrEmpty(english)) return english;
        return Table.TryGetValue(english, out string thai) ? thai : english;
    }

    // ตารางคำแปล อังกฤษ -> ไทย (ต้องตรงกับข้อความบนจอทุกตัวอักษร)
    public static readonly Dictionary<string, string> Table = new Dictionary<string, string>
    {
        // ทั่วไป
        { "BACK", "กลับ" }, { "CLOSE", "ปิด" }, { "CANCEL", "ยกเลิก" }, { "LEAVE", "ออก" }, { "JOIN", "เข้าร่วม" },
        { "READY", "พร้อม" }, { "NOT READY", "ยังไม่พร้อม" }, { "CANCEL READY", "ยกเลิกพร้อม" }, { "WAITING", "รอ" },
        { "SETTINGS", "ตั้งค่า" }, { "LOG OUT", "ออกจากระบบ" }, { "LOG OUT?", "ออกจากระบบ?" },
        { "LEAVE MATCH", "ออกจากแมตช์" }, { "LEAVE THIS MATCH?", "ออกจากแมตช์นี้?" }, { "MORE OPTIONS", "ตั้งค่าเพิ่มเติม" },
        { "MASTER", "เสียงรวม" }, { "MUSIC", "เพลง" }, { "EFFECTS", "เสียงเอฟเฟกต์" }, { "AIM SPEED", "ความไวเล็ง" },
        { "Settings are saved on this device.", "ค่าที่ตั้งจะถูกบันทึกในเครื่องนี้" },
        { "Online match continues while this menu is open.", "แมตช์ออนไลน์ยังเดินต่อระหว่างเปิดเมนูนี้" },
        { "You will return to the login screen.", "จะกลับไปหน้าล็อกอิน" },
        { "The current match will end for both players.", "แมตช์นี้จะจบลงสำหรับผู้เล่นทุกคน" },
        { "CONNECTING...", "กำลังเชื่อมต่อ..." }, { "RECONNECTING...", "กำลังเชื่อมต่อใหม่..." }, { "SYNCING...", "กำลังซิงก์..." },
        { "STARTING...", "กำลังเริ่ม..." }, { "OFFLINE", "ออฟไลน์" }, { "COPY", "คัดลอก" }, { "SEND", "ส่ง" },
        { "ADD", "เพิ่ม" }, { "ACCEPT", "รับ" }, { "DECLINE", "ปฏิเสธ" }, { "IGNORE", "ไม่สนใจ" }, { "REMOVE", "ลบ" },
        { "CREATE", "สร้าง" }, { "INVITE", "ชวน" }, { "CLAIM", "รับรางวัล" }, { "CLAIMED", "รับแล้ว" },
        { "IN PROGRESS", "กำลังทำ" }, { "EQUIP", "ใช้งาน" }, { "EQUIPPED", "ใช้งานอยู่" }, { "OWNED", "มีแล้ว" },
        { "SELECTED", "เลือกแล้ว" }, { "AVAILABLE", "ว่าง" }, { "EMPTY", "ว่าง" }, { "MAX", "สูงสุด" }, { "MAX LEVEL", "เลเวลสูงสุด" },
        // หน้าหลัก / ห้อง
        { "BATTLEFIELD OF THE STARS", "สมรภูมิแห่งดวงดาว" }, { "FIND YOUR BATTLE", "เลือกการต่อสู้" },
        { "QUICK MATCH", "จับคู่ด่วน" }, { "CREATE ROOM", "สร้างห้อง" }, { "CREATE / JOIN ROOM", "สร้าง / เข้าห้อง" },
        { "JOIN WITH ROOM CODE", "เข้าห้องด้วยรหัส" }, { "VS BOT", "สู้กับบอท" }, { "TRAINING", "ฝึกซ้อม" }, { "HOW TO PLAY", "วิธีเล่น" },
        { "LEAVE ROOM", "ออกจากห้อง" }, { "COPY CODE", "คัดลอกรหัส" }, { "INVITE FRIENDS", "ชวนเพื่อน" },
        { "START BATTLE", "เริ่มต่อสู้" }, { "READY FOR BATTLE?", "พร้อมรบหรือยัง?" }, { "BATTLE PREPARATION", "เตรียมพร้อมรบ" },
        { "WAITING FOR RIVAL", "รอคู่แข่ง" }, { "WAITING FOR PILOTS", "รอนักบิน" }, { "WAITING FOR FRIEND...", "รอเพื่อน..." },
        { "OPEN SLOT", "ที่ว่าง" }, { "HOST STARTS", "หัวห้องเป็นคนเริ่ม" }, { "HOST SELECTS", "หัวห้องเป็นคนเลือก" },
        { "SELECT MAP", "เลือกแม็พ" }, { "CHOOSE YOUR BATTLEFIELD", "เลือกสนามรบ" }, { "ROOM RECOVERY", "กู้คืนห้อง" },
        { "PILOT HANGAR", "โรงเก็บยาน" }, { "SHIPS", "ยาน" }, { "SKILLS", "สกิล" }, { "SHIPS & SKILLS", "ยานและสกิล" },
        { "YOUR SHIP", "ยานของคุณ" }, { "YOUR PILOT", "นักบินของคุณ" }, { "YOUR PILOT WALLET", "กระเป๋าเงินนักบิน" },
        { "ACTIVE SHIP / HANGAR", "ยานที่ใช้ / โรงเก็บ" }, { "PAINT", "สีย้อม" }, { "LOADING PILOT...", "กำลังโหลดนักบิน..." },
        { "LOADING SKILL...", "กำลังโหลดสกิล..." },
        { "WORKSHOP", "โรงซ่อม" }, { "UPGRADE", "ตีบวก" }, { "UPGRADES", "ตีบวก" }, { "ITEMS", "ไอเท็ม" }, { "SHOP", "ร้านค้า" },
        { "SUPPLY CRATE", "กล่องเสบียง" }, { "INSTALL", "ติดตั้ง" }, { "INSTALLED", "ติดตั้งแล้ว" }, { "REPAIR", "ซ่อม" },
        { "MISSIONS", "ภารกิจ" }, { "MISSIONS & REWARDS", "ภารกิจและรางวัล" }, { "WEEKLY MISSIONS", "ภารกิจรายสัปดาห์" },
        { "PROFILE", "โปรไฟล์" }, { "RECENT MATCHES", "แมตช์ล่าสุด" }, { "CHANGE TITLE", "เปลี่ยนฉายา" }, { "REWARD", "รางวัล" },
        { "RANKED", "แรงค์" }, { "RANKED 1V1", "แรงค์ 1v1" }, { "FIND RANKED MATCH", "หาแมตช์แรงค์" },
        { "LEADERBOARD", "ตารางอันดับ" }, { "LEADERBOARD TOP 20", "อันดับ 20 อันดับแรก" }, { "RANK", "อันดับ" },
        { "SOCIAL", "สังคม" }, { "GUILD", "กิลด์" }, { "GUILD CHAT", "แชทกิลด์" }, { "CHAT", "แชท" }, { "GLOBAL", "รวม" },
        { "LEAVE GUILD", "ออกจากกิลด์" }, { "OR JOIN A GUILD BY TAG", "หรือเข้ากิลด์ด้วย TAG" },
        { "GAME MODE", "โหมดเกม" }, { "DEATHMATCH", "ดวลกัน" }, { "SURVIVAL", "เอาชีวิตรอด" }, { "CAMPAIGN", "เนื้อเรื่อง" },
        { "JELLYFISH CORE", "แกนแมงกะพรุน" }, { "PRISM PLAINS", "ทุ่งปริซึม" }, { "MECH WARZONE", "สุสานหุ่นรบ" },
        { "ASTEROID STATION", "สถานีอุกกาบาต" }, { "MOLTEN NEBULA", "เนบิวลาลาวา" }, { "SELECTED", "เลือกแล้ว" }, { "SELECT MAP", "เลือกแม็พ" },
        { "SOLO", "เล่นคนเดียว" }, { "NORMAL", "ปกติ" }, { "TEAM", "ทีม" },
        // ในสนามรบ
        { "GO!", "ลุย!" }, { "GET READY", "เตรียมตัว" }, { "MOVE", "เคลื่อนที่" }, { "AIM / FIRE", "เล็ง / ยิง" }, { "EMOTE", "อีโมต" },
        { "SKILL", "สกิล" }, { "SHIELD ACTIVE", "โล่ทำงาน" }, { "STUNNED", "ติดสตัน" }, { "STUN / NO CONTROL", "ติดสตัน / บังคับไม่ได้" },
        { "SPAWN PROTECTED", "กันตัวหลังเกิด" }, { "SHIP DESTROYED", "ยานถูกทำลาย" }, { "LOW HULL", "เกราะเหลือน้อย" },
        { "FINDING A SAFE SPAWN...", "กำลังหาจุดเกิดที่ปลอดภัย..." }, { "OUT OF LIVES - SPECTATING", "ชีวิตหมด - กำลังชม" },
        { "GET BACK TO THE ZONE!", "กลับเข้าเขตปลอดภัย!" }, { "DESTROY THE BOSS", "ทำลายบอส" }, { "BOSS DOWN!", "บอสถูกทำลาย!" },
        { "YOU HOLD THE HILL", "คุณยึดจุดอยู่" }, { "ENEMY HOLDS THE HILL", "ศัตรูยึดจุดอยู่" }, { "HILL EMPTY", "จุดยึดว่าง" },
        { "CONTESTED!", "กำลังแย่งจุด!" }, { "MOVE HERE", "มาที่นี่" },
        { "CONNECTION LOST", "การเชื่อมต่อหลุด" }, { "RECONNECTED", "กลับเข้าเกมแล้ว" },
        // หน้าผล
        { "VICTORY", "ชนะ" }, { "DEFEAT", "แพ้" }, { "DEFEATED", "พ่ายแพ้" }, { "DRAW", "เสมอ" }, { "WINNER", "ผู้ชนะ" },
        { "MATCH COMPLETE", "จบแมตช์" }, { "STAGE CLEAR!", "ผ่านด่าน!" }, { "MISSION FAILED", "ภารกิจล้มเหลว" },
        { "SURVIVED!", "รอดแล้ว!" }, { "OVERRUN", "ถูกบุกแตก" }, { "BACK TO LOBBY", "กลับล็อบบี้" },
        { "RETURN TO SAME ROOM", "กลับห้องเดิม" }, { "SCORE", "คะแนน" }, { "KILLS", "ฆ่า" }, { "DEATHS", "ตาย" },
        // หน้า MORE OPTIONS
        { "KILL FEED", "ข้อความใครฆ่าใคร" }, { "DAMAGE NUMBERS", "ตัวเลขดาเมจ" }, { "EMOTES", "อีโมต" },
        { "CAMERA SHAKE", "กล้องสั่น" }, { "FPS LIMIT", "จำกัด FPS" }, { "GRAPHICS", "กราฟิก" }, { "SHOW FPS", "แสดง FPS" },
        { "BUTTON SIZE", "ขนาดปุ่ม" }, { "BUTTON OPACITY", "ความทึบปุ่ม" }, { "CONTROLS", "ฝั่งปุ่ม" }, { "LANGUAGE", "ภาษา" },
        { "ON", "เปิด" }, { "OFF", "ปิด" }, { "LOW", "ต่ำ" }, { "MEDIUM", "กลาง" }, { "HIGH", "สูง" }, { "FULL", "เต็ม" },
        { "RIGHT-HANDED", "มือขวา" }, { "LEFT-HANDED", "มือซ้าย" },
        { "Tap a row to change it. Saved on this device.", "แตะแต่ละแถวเพื่อเปลี่ยนค่า บันทึกในเครื่องนี้" },
    };

    // ===== ฟอนต์ไทย =====
    private static TMP_FontAsset thaiFont;
    private static bool fontTried;

    // เพิ่มฟอนต์ไทยเป็น fallback ของ TextMeshPro ทั้งเกม (ทำครั้งเดียว)
    public static void EnsureThaiFont()
    {
        if (fontTried) return;
        fontTried = true;
        try
        {
            thaiFont = Resources.Load<TMP_FontAsset>("Fonts/ThaiFont");   // ที่เครื่องมือ Editor/ThaiFont.cs สร้างไว้
            if (thaiFont == null) thaiFont = Resources.Load<TMP_FontAsset>("Fonts/ThaiFont SDF");
            if (thaiFont == null)
            {
                var file = Resources.Load<Font>("Fonts/ThaiFont");
                if (file != null) thaiFont = TMP_FontAsset.CreateFontAsset(file);
            }
            if (thaiFont == null) thaiFont = FromOperatingSystem();
            if (thaiFont == null) { Debug.LogWarning("Lang: no Thai font found. Put a Thai .ttf at Resources/Fonts/ThaiFont."); return; }
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks != null && !fallbacks.Contains(thaiFont)) fallbacks.Add(thaiFont);
            var main = TMP_Settings.defaultFontAsset;
            if (main != null)
            {
                if (main.fallbackFontAssetTable == null) main.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!main.fallbackFontAssetTable.Contains(thaiFont)) main.fallbackFontAssetTable.Add(thaiFont);
            }
        }
        catch (System.Exception error)
        {
            Debug.LogWarning("Lang: Thai font setup failed: " + error.Message);
        }
    }

    // หาไฟล์ฟอนต์ไทยในเครื่อง (ตามชื่อไฟล์) แล้วสร้างเป็นฟอนต์ TextMeshPro แบบ Dynamic
    private static TMP_FontAsset FromOperatingSystem()
    {
        string[] preferred = { "notosansthai-regular", "notosansthaiui-regular", "notosansthai", "leelawadeeui", "leelawad", "tahoma", "thonburi", "sarabun", "kanit", "droidsansthai", "thai" };
        string[] paths = Font.GetPathsToOSFonts();
        if (paths == null) return null;
        foreach (string key in preferred)
        {
            foreach (string path in paths)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Replace(" ", "");
                if (!name.Contains(key) || name.Contains("bold") || name.Contains("looped")) continue;
                var font = new Font(path);
                var asset = TMP_FontAsset.CreateFontAsset(font);
                if (asset != null && asset.HasCharacter('ก', false, true)) return asset;
            }
        }
        // วิธีสำรอง: ขอฟอนต์จากชื่อตระกูลฟอนต์ของระบบ
        string[] families = { "Noto Sans Thai", "Leelawadee UI", "Tahoma", "Thonburi" };
        foreach (string family in families)
        {
            var asset = TMP_FontAsset.CreateFontAsset(family, "Regular");
            if (asset != null && asset.HasCharacter('ก', false, true)) return asset;
        }
        return null;
    }
}

// ตัวสแกนข้อความบนจอ: สร้างเองตอนเปิดเกม และอยู่ข้ามทุก Scene
public class LangTranslator : MonoBehaviour
{
    private static LangTranslator instance;
    // ข้อความที่แปลไปแล้ว: key = InstanceID ของ TMP_Text, value = (อังกฤษเดิม, ไทย)
    private readonly Dictionary<int, KeyValuePair<string, string>> translated = new Dictionary<int, KeyValuePair<string, string>>();
    // ข้อความที่ไม่ต้องแปล (ช่องพิมพ์ของผู้เล่น)
    private readonly HashSet<int> skip = new HashSet<int>();
    private float nextScan;
    private bool wasThai;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        Lang.EnsureThaiFont();
        if (instance != null) return;
        var go = new GameObject("LangTranslator");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LangTranslator>();
    }

    public static void RefreshNow()
    {
        if (instance != null) instance.nextScan = 0;
    }

    void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .4f;
        bool thai = Lang.Thai;
        if (!thai && !wasThai) return;
        var texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var text in texts)
        {
            int id = text.GetInstanceID();
            if (skip.Contains(id)) continue;
            string current = text.text;
            if (string.IsNullOrEmpty(current)) continue;
            if (thai)
            {
                if (translated.TryGetValue(id, out var done) && done.Value == current) continue;
                if (!Lang.Table.TryGetValue(current.Trim(), out string th)) continue;
                if (text.GetComponentInParent<TMP_InputField>() != null) { skip.Add(id); continue; }
                translated[id] = new KeyValuePair<string, string>(current, th);
                text.text = th;
            }
            else if (translated.TryGetValue(id, out var original) && original.Value == current)
            {
                text.text = original.Key;
            }
        }
        if (!thai) translated.Clear();
        wasThai = thai;
    }
}
