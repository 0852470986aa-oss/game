// Lang.cs — ภาษาไทย / อังกฤษ (เฟส 9)
// วิธีทำงาน: UI ทั้งเกมเขียนเป็นภาษาอังกฤษไว้เหมือนเดิม ตัวแปลภาษา (LangTranslator) เสียบเข้าไปใน TextMeshPro
// ตอนวาดข้อความ ถ้าตรงกับคำในตาราง Thai ด้านล่าง จะวาดเป็นภาษาไทยแทน (ไม่กระพริบ, สลับกลับเป็นอังกฤษได้ทันที)
// จึงไม่ต้องแก้โค้ด UI เดิมทุกจุด — อยากแปลคำไหนเพิ่ม ให้เพิ่มคู่คำในตาราง Thai
// ฟอนต์ไทย: TextMeshPro เดิม (LiberationSans) ไม่มีตัวอักษรไทย จึงเพิ่มฟอนต์ไทยเป็น Fallback ให้อัตโนมัติ (ใช้กับแชท/เนื้อเรื่องด้วย)
//   1) ถ้ามี Resources/Fonts/ThaiFont (ไฟล์ .ttf/.otf เช่น Sarabun, Kanit, Prompt) จะใช้ไฟล์นั้น (แนะนำ: หน้าตาเหมือนกันทุกเครื่อง)
//   2) ไม่มี = ใช้ฟอนต์ไทยของเครื่อง (Android: Noto Sans Thai, Windows: Tahoma/Leelawadee, iOS: Thonburi)
// ปิด FeatureFlags.Language = ใช้ภาษาอังกฤษอย่างเดียวและซ่อนปุ่มเลือกภาษา
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ตัวจัดการภาษา: เก็บภาษาที่ผู้เล่นเลือกใน PlayerPrefs และแปลข้อความอังกฤษเป็นไทยจากตาราง Table / Patterns
public static class Lang
{
    private const string PrefsKey = "Opt_Language"; // key ใน PlayerPrefs ที่เก็บภาษาที่ผู้เล่นเลือก
    // true = ผู้เล่นเลือกภาษาไทย
    private static int thaiCache = -1;
    public static bool Thai
    {
        get
        {
            if (!FeatureFlags.Language) return false;
            if (thaiCache < 0) thaiCache = PlayerPrefs.GetInt(PrefsKey, 0);
            return thaiCache == 1;
        }
    }
    public static string CurrentName => Thai ? "ไทย" : "ENGLISH"; // ชื่อภาษาปัจจุบันที่แสดงบนปุ่มเลือกภาษา

    // เปลี่ยนภาษา (true = ไทย) บันทึกลง PlayerPrefs แล้วสั่ง LangTranslator วาดข้อความทุกอันใหม่ทันที
    public static void SetThai(bool thai)
    {
        thaiCache = thai ? 1 : 0;
        PlayerPrefs.SetInt(PrefsKey, thaiCache);
        PlayerPrefs.Save();
        LangTranslator.RefreshNow();
    }

    // แปลข้อความ (ใช้ในโค้ดได้โดยตรง เช่น Lang.T("BACK")) ไม่มีในตาราง = คืนข้อความเดิม
    public static string T(string english)
    {
        if (!Thai || string.IsNullOrEmpty(english)) return english;
        return Translate(english);
    }

    // แปลข้อความบนจอ (เรียงตามลำดับที่ลอง):
    // 1) ตรงทั้งข้อความ  2) แยกแท็ก <color>/<size> แปลเฉพาะตัวหนังสือ  3) แยกทีละบรรทัด
    // 4) รูปแบบที่มีตัวเลข (Patterns)  5) แยกช่วงด้วยช่องว่าง 2 ช่องขึ้นไป หรือ " / " แล้วแปลทีละช่วง
    // 6) "หัวข้อ: ค่า"  7) "คำ ตัวเลข" (KILLS 4)  8) "ตัวเลข คำ" (+250 ASTRONIUM)
    public static string Translate(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (Table.TryGetValue(text, out string thai)) return thai;
        // จำผลที่แปลแล้ว (ข้อความที่เปลี่ยนบ่อย เช่น ตัวเลขดาเมจ/เวลา ไม่ต้องไล่รูปแบบใหม่ทุกครั้ง) เกิน 3000 รายการล้างทิ้ง
        if (Cache.TryGetValue(text, out thai)) return thai;
        thai = TranslateUncached(text);
        if (Cache.Count > 3000) Cache.Clear();
        Cache[text] = thai;
        return thai;
    }

    private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(); // จำผลแปลข้อความที่เคยแปลแล้ว ไม่ต้องแปลซ้ำ

    // แปลจริง (ไม่ผ่านตัวจำ)
    private static string TranslateUncached(string text)
    {
        if (text.IndexOf('<') >= 0)
        {
            var parts = TagSplit.Split(text);
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0 && parts[i][0] != '<') parts[i] = TranslatePlain(parts[i]);
            return string.Concat(parts);
        }
        return TranslatePlain(text);
    }

    private static readonly System.Text.RegularExpressions.Regex TagSplit = new System.Text.RegularExpressions.Regex("(<[^>]*>)"); // แยกแท็ก rich text <...> ออกจากตัวหนังสือก่อนแปล
    private static readonly System.Text.RegularExpressions.Regex SegmentSplit = new System.Text.RegularExpressions.Regex("( {2,}| / )"); // แยกช่วงข้อความด้วยช่องว่าง 2 ช่องขึ้นไป หรือ " / "

    // แปลข้อความที่ไม่มีแท็ก: ถ้ามีหลายบรรทัดจะแยกแปลทีละบรรทัด (ตัด \r ท้ายบรรทัด) แล้วต่อกลับด้วย \n
    private static string TranslatePlain(string text)
    {
        if (text.IndexOf('\n') < 0) return TranslateLine(text);
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) lines[i] = TranslateLine(lines[i].TrimEnd('\r'));
        return string.Join("\n", lines);
    }

    // แปลหนึ่งบรรทัด: ลองทั้งบรรทัดก่อน ถ้าไม่ได้แยกช่วงด้วยช่องว่าง 2 ช่อง/" / " แล้วแปลทีละช่วง
    // แปลไม่ได้เลย = คืนบรรทัดเดิม
    private static string TranslateLine(string line)
    {
        string core = line.Trim();
        if (core.Length == 0) return line;
        string result = TranslateWhole(core);
        if (result == null && SegmentSplit.IsMatch(core))
        {
            var parts = SegmentSplit.Split(core);
            bool any = false;
            for (int i = 0; i < parts.Length; i += 2)
            {
                string part = TranslateWhole(parts[i]) ?? TranslateSegment(parts[i]);
                if (part != null) { parts[i] = part; any = true; }
            }
            if (any) result = string.Concat(parts);
        }
        if (result == null) result = TranslateSegment(core);
        if (result == null) return line;
        // คงช่องว่างหน้า/หลังเดิมไว้
        int lead = line.Length - line.TrimStart().Length;
        return line.Substring(0, lead) + result + line.Substring(lead + core.Length);
    }

    // ตรงทั้งท่อน หรือเข้ารูปแบบที่มีตัวเลข
    private static string TranslateWhole(string text)
    {
        if (text.Length == 0) return null;
        if (Table.TryGetValue(text, out string thai)) return thai;
        foreach (var pattern in Patterns)
            if (pattern.Key.IsMatch(text)) return pattern.Key.Replace(text, pattern.Value);
        return null;
    }

    // ท่อนเดียว: "หัวข้อ: ค่า" / "คำ ตัวเลข" / "ตัวเลข คำ" (คืน null = แปลไม่ได้)
    private static string TranslateSegment(string seg)
    {
        string thai;
        int colon = seg.IndexOf(": ");
        if (colon > 0 && Table.TryGetValue(seg.Substring(0, colon), out thai)) return thai + seg.Substring(colon);
        int space = seg.LastIndexOf(' ');
        if (space > 0 && space < seg.Length - 1 && (char.IsDigit(seg[space + 1]) || seg[space + 1] == 'x' || seg[space + 1] == '+' || seg[space + 1] == '-'))
            if (Table.TryGetValue(seg.Substring(0, space), out thai)) return thai + seg.Substring(space);
        int first = seg.IndexOf(' ');
        if (first > 0 && first < seg.Length - 1 && (char.IsDigit(seg[0]) || seg[0] == '+' || seg[0] == '-'))
            if (Table.TryGetValue(seg.Substring(first + 1), out thai)) return seg.Substring(0, first + 1) + thai;
        return null;
    }

    // ข้อความที่มีตัวเลขอยู่กลางประโยค ($1, $2 = ตัวเลขที่จับได้)
    private static readonly KeyValuePair<System.Text.RegularExpressions.Regex, string>[] Patterns =
    {
        P(@"^YOU\s+(-?\d+)\s+:\s+(-?\d+)\s+RIVAL$", "คุณ  $1  :  $2  คู่แข่ง"),
        P(@"^BOUNTY \+(\d+)$", "ค่าหัว +$1"),
        P(@"^FIRST TO (\d+) KILLS$", "ฆ่าครบ $1 ก่อนชนะ"),
        P(@"^MASTER\s+(\d+)%$", "เสียงรวม  $1%"), // แถบเสียงรวม (คำว่า MASTER เฉยๆ = แรงค์มาสเตอร์)
        P(@"^Ping: (\d+) ms$", "ปิง $1 ms"),
        P(@"^ARENA / RADAR (\d+)$", "เรดาร์สนาม $1"),
        P(@"^RETURN TO ROOM\s+(\d+)/(\d+)$", "กลับห้องเดิม  $1/$2"),
        P(@"^(\d+) REWARD\(S\) READY IN LOBBY$", "มีรางวัล $1 รายการรอรับในล็อบบี้"),
        P(@"^DESTROY (\d+) ENEMIES$", "ทำลายศัตรูให้ได้ $1 ลำ"),
        P(@"^SURVIVE (\d+) WAVES$", "เอาชีวิตรอดให้ครบ $1 ระลอก"),
        P(@"^WAVES CLEARED (\d+)$", "ผ่านระลอก $1"),
        // ===== รูปแบบเพิ่ม 8 ต.ค. (ข้อความที่ต่อจากชื่อ/ตัวเลขตอนรันเกม) =====
        // ห้อง / ห้องรอ
        P(@"^(.+) joined the room$", "$1 เข้าห้องแล้ว"),
        P(@"^(.+) left the room$", "$1 ออกจากห้องแล้ว"),
        P(@"^(.+) lost connection\. Slot reserved for (\d+) seconds\.$", "$1 หลุดการเชื่อมต่อ จองที่ไว้ให้ $2 วินาที"),
        P(@"^(.+) is now the room host$", "$1 เป็นเจ้าห้องแล้ว"),
        P(@"^Joined room! \((\d+)/(\d+)\)$", "เข้าห้องแล้ว! ($1/$2)"),
        P(@"^Joining room (.+)\.\.\.$", "กำลังเข้าห้อง $1..."),
        P(@"^Creating room (.+)\.\.\.$", "กำลังสร้างห้อง $1..."),
        P(@"^Tap (.+) again to remove them from the room\.$", "แตะ $1 อีกครั้งเพื่อเตะออกจากห้อง"),
        P(@"^(.+) was removed from the room\.$", "$1 ถูกเตะออกจากห้องแล้ว"),
        P(@"^Connection cannot recover automatically: (.+)\. Check account/server settings\.$", "เชื่อมต่อใหม่อัตโนมัติไม่ได้: $1 ตรวจสอบการตั้งค่าบัญชี/เซิร์ฟเวอร์"),
        P(@"^All (\d+) pilots ready \(\+(\d+) bots\)\. Host can launch the battle\.$", "นักบินพร้อมครบ $1 คน (+บอท $2) เจ้าห้องกดเริ่มได้เลย"),
        P(@"^All (\d+) pilots ready\. Host can launch the battle\.$", "นักบินพร้อมครบ $1 คน เจ้าห้องกดเริ่มได้เลย"),
        P(@"^Pilots (\d+)/(\d+)\. Host can start with 2\+ ready pilots \(empty slots become bots\)\.$", "นักบิน $1/$2 คน เจ้าห้องเริ่มได้เมื่อพร้อม 2 คนขึ้นไป (ที่ว่างจะเป็นบอท)"),
        P(@"^Pilots (\d+)/(\d+)\. Host can start with 2\+ ready pilots\.$", "นักบิน $1/$2 คน เจ้าห้องเริ่มได้เมื่อพร้อม 2 คนขึ้นไป"),
        P(@"^FFA (\d+)P$", "FFA $1 คน"),
        P(@"^TEAM (\d+)v(\d+)$", "ทีม $1v$2"),
        P(@"^HILL (\d+)P$", "ยึดเนิน $1 คน"),
        P(@"^STARS (\d+)P$", "ล่าดาว $1 คน"),
        P(@"^Astronium Coins : (.+)$", "เหรียญแอสโทรเนียม : $1"),
        P(@"^Login successful! Welcome (.+)$", "เข้าสู่ระบบสำเร็จ! ยินดีต้อนรับ $1"),
        // ในสนามรบ
        P(@"^RESPAWNING IN (\d+)$", "เกิดใหม่ใน $1"),
        P(@"^DESTROYED BY (.+)$", "ถูกทำลายโดย $1"),
        P(@"^lost connection - waiting (\d+)s$", "หลุดการเชื่อมต่อ - รอ $1 วิ"),
        P(@"^RANK #(\d+)/(\d+)$", "อันดับ #$1/$2"),
        P(@"^Cooldown ([\d.]+) sec$", "คูลดาวน์ $1 วินาที"),
        // หน้าผล / ประวัติแมตช์ / แถบความก้าวหน้า
        P(@"^#(\d+)  OF  (\d+)$", "อันดับ $1 จาก $2"),
        P(@"^#(\d+) of (\d+)$", "อันดับ $1 จาก $2"),
        P(@"^(\S+) \(BOT\)$", "$1 (บอท)"),
        P(@"^RANKED ([+-]?\d+) MMR$", "แรงค์ $1 MMR"),
        P(@"^LEVEL UP! \+(\d+) ASTRONIUM$", "เลเวลอัป! +$1 แอสโทรเนียม"),
        P(@"^\((\d+) BRONZE\)$", "($1 บรอนซ์)"),
        P(@"^\((\d+) SILVER\)$", "($1 ซิลเวอร์)"),
        P(@"^\((\d+) GOLD\)$", "($1 โกลด์)"),
        P(@"^\((\d+) PLATINUM\)$", "($1 แพลทินัม)"),
        P(@"^\((\d+) DIAMOND\)$", "($1 ไดมอนด์)"),
        P(@"^\((\d+) MASTER\)$", "($1 มาสเตอร์)"),
        P(@"^\((\d+) LEGEND\)$", "($1 เลเจนด์)"),
        // ภารกิจ / รางวัลล็อกอิน
        P(@"^DAILY MISSIONS   \(new set in (\d+)h (\d+)m\)$", "ภารกิจรายวัน   (ชุดใหม่ใน $1 ชม. $2 นาที)"),
        P(@"^Mission reward: \+(\d+) Astronium, \+(\d+) XP$", "รางวัลภารกิจ: +$1 แอสโทรเนียม, +$2 XP"),
        P(@"^DAILY LOGIN REWARD   \(streak (\d+) days\)$", "รางวัลล็อกอินรายวัน   (ต่อเนื่อง $1 วัน)"),
        P(@"^Daily login reward: \+(\d+) Astronium$", "รางวัลล็อกอินรายวัน: +$1 แอสโทรเนียม"),
        P(@"^TAP DAY (\d+) TO CLAIM$", "แตะวันที่ $1 เพื่อรับรางวัล"),
        // แรงค์ / ฤดูกาล
        P(@"^SEASON ([A-Z]{3}) (\d{4})$", "ฤดูกาล $1 $2"),
        P(@"^ends in (\d+)d (\d+)h$", "จบใน $1 วัน $2 ชม."),
        P(@"^(\d+) MMR to SILVER$", "อีก $1 MMR ถึงซิลเวอร์"),
        P(@"^(\d+) MMR to GOLD$", "อีก $1 MMR ถึงโกลด์"),
        P(@"^(\d+) MMR to PLATINUM$", "อีก $1 MMR ถึงแพลทินัม"),
        P(@"^(\d+) MMR to DIAMOND$", "อีก $1 MMR ถึงไดมอนด์"),
        P(@"^(\d+) MMR to MASTER$", "อีก $1 MMR ถึงมาสเตอร์"),
        P(@"^(\d+) MMR to LEGEND$", "อีก $1 MMR ถึงเลเจนด์"),
        P(@"^Season reward: \+(\d+) Astronium$", "รางวัลฤดูกาล: +$1 แอสโทรเนียม"),
        P(@"^End-of-season reward by best rank: (.+) Astronium$", "รางวัลจบฤดูกาลตามแรงค์สูงสุด: $1 แอสโทรเนียม"),
        P(@"^Ranked = 1 VS 1, (\d+) kills / (\d+) min, upgrades OFF, no bots\. Win vs stronger = more MMR\.$", "แรงค์ = 1 ต่อ 1, ฆ่า $1 / $2 นาที, ปิดตีบวกยาน, ไม่มีบอท  ชนะคนที่เก่งกว่า = ได้ MMR มากขึ้น"),
        // โรงเก็บยาน / โรงซ่อม / ร้านค้า / กล่องเสบียง
        P(@"^Buy \((\d+)\)$", "ซื้อ ($1)"),
        P(@"^Purchased (.+)!$", "ซื้อ $1 แล้ว!"),
        P(@"^Installed (.+) Skill!$", "ติดตั้งสกิล $1 แล้ว!"),
        P(@"^Not enough Astronium \((\d+) needed\)\.$", "แอสโทรเนียมไม่พอ (ต้องใช้ $1)"),
        P(@"^This ship is already at its upgrade limit \(\+(\d+)\)\.$", "ยานลำนี้ตีบวกถึงขีดจำกัดแล้ว (+$1)"),
        P(@"^Upgrade limit for this ship: \+(\d+)   \(cheap ships cap lower, expensive ships cap higher\)$", "ยานลำนี้ตีบวกได้สูงสุด +$1   (ยานราคาถูกตีได้น้อย ยานราคาแพงตีได้สูงกว่า)"),
        P(@"^HULL \(HP\) upgraded to \+(\d+)!$", "ตีบวกตัวยาน (HP) เป็น +$1 สำเร็จ!"),
        P(@"^ENGINE \(SPD\) upgraded to \+(\d+)!$", "ตีบวกเครื่องยนต์ (SPD) เป็น +$1 สำเร็จ!"),
        P(@"^WEAPON \(ATK\) upgraded to \+(\d+)!$", "ตีบวกอาวุธ (ATK) เป็น +$1 สำเร็จ!"),
        P(@"^Item upgraded to \+(\d+)!$", "ตีบวกไอเท็มเป็น +$1 สำเร็จ!"),
        P(@"^Bought (.+)!$", "ซื้อ $1 แล้ว!"),
        P(@"^Crate: \+(\d+) Astronium$", "กล่องเสบียง: +$1 แอสโทรเนียม"),
        P(@"^Crate: COMMON (.+)!$", "กล่องเสบียง: ไอเท็มธรรมดา $1!"),
        P(@"^Crate: RARE (.+)!$", "กล่องเสบียง: ไอเท็มหายาก $1!"),
        P(@"^Crate: EPIC (.+)!$", "กล่องเสบียง: ไอเท็มเอพิค $1!"),
        P(@"^All (\d+) slots are full\. Remove an item first\.$", "ช่องไอเท็มเต็มทั้ง $1 ช่องแล้ว ถอดไอเท็มออกก่อน"),
        P(@"^Equipped (.+)$", "ใส่ $1 แล้ว"),
        P(@"^Removed (.+)\.$", "ลบ $1 แล้ว"),
        P(@"^Removed (.+)$", "ถอด $1 แล้ว"),
        P(@"^Chances:  Astronium 200-500 (\d+)%   COMMON item (\d+)%   RARE item (\d+)%   EPIC item (\d+)%$", "โอกาส:  แอสโทรเนียม 200-500 $1%   ไอเท็มธรรมดา $2%   ไอเท็มหายาก $3%   ไอเท็มเอพิค $4%"),
        P(@"^Hull HP \+([\d.]+)%( \(\+\d+\))?$", "HP ตัวยาน +$1%$2"),
        P(@"^Speed \+([\d.]+)%( \(\+\d+\))?$", "ความเร็ว +$1%$2"),
        P(@"^Damage \+([\d.]+)%( \(\+\d+\))?$", "พลังโจมตี +$1%$2"),
        P(@"^Fire interval -([\d.]+)%( \(\+\d+\))?$", "ยิงถี่ขึ้น $1%$2"),
        P(@"^Skill cooldown -([\d.]+)%( \(\+\d+\))?$", "คูลดาวน์สกิล -$1%$2"),
        P(@"^Regenerate ([\d.]+) HP/sec out of combat( \(\+\d+\))?$", "ฟื้น $1 HP/วินาที เมื่อไม่ได้ต่อสู้$2"),
        P(@"^Spawn shield \+([\d.]+)s( \(\+\d+\))?$", "โล่ตอนเกิด +$1 วิ$2"),
        P(@"^Heal ([\d.]+)% HP on kill( \(\+\d+\))?$", "ฟื้น $1% HP เมื่อฆ่าได้$2"),
        // เพื่อน / กิลด์
        P(@"^FRIENDS \((\d+)\)$", "เพื่อน ($1)"),
        P(@"^REQUESTS \((\d+)\)$", "คำขอ ($1)"),
        P(@"^(\d+)m ago$", "$1 นาทีที่แล้ว"),
        P(@"^(\d+)h ago$", "$1 ชม.ที่แล้ว"),
        P(@"^(\d+)d ago$", "$1 วันที่แล้ว"),
        P(@"^Invite sent to (.+)\.$", "ส่งคำชวนให้ $1 แล้ว"),
        P(@"^Tap X again to remove (.+)\.$", "แตะ X อีกครั้งเพื่อลบ $1"),
        P(@"^You and (.+) are now friends!$", "คุณกับ $1 เป็นเพื่อนกันแล้ว!"),
        P(@"^(.+) invites you to room (\S+)$", "$1 ชวนคุณเข้าห้อง $2"),
        P(@"^No pilot found with code (.+)\.$", "ไม่พบนักบินที่มีรหัส $1"),
        P(@"^Tag \[(.+)\] is already taken\.$", "TAG [$1] ถูกใช้ไปแล้ว"),
        P(@"^Guild \[(.+?)\] (.+) created!$", "สร้างกิลด์ [$1] $2 แล้ว!"),
        P(@"^No guild with tag \[(.+)\]\.$", "ไม่พบกิลด์ที่มี TAG [$1]"),
        P(@"^Joined guild \[(.+)\]!$", "เข้ากิลด์ [$1] แล้ว!"),
        P(@"^You left \[(.+)\]\.$", "ออกจากกิลด์ [$1] แล้ว"),
        P(@"^\((\d+) ASTRONIUM\)$", "($1 แอสโทรเนียม)"),
        P(@"^Could not load the leaderboard: (.+)$", "โหลดตารางอันดับไม่ได้: $1"),
        // ===== ร้านค้ารายวัน =====
        P(@"^NEW ITEMS IN (\d+):(\d+):(\d+)$", "ของใหม่ในอีก $1:$2:$3"),
        P(@"^BUY\s+([\d,]+)$", "ซื้อ  $1"),
        P(@"^Each slot: COMMON (\d+)%\s+/\s+RARE (\d+)%\s+/\s+EPIC (\d+)%$", "โอกาสออกแต่ละช่อง: ธรรมดา $1%  /  หายาก $2%  /  เอพิค $3%"),
        // ===== ขายไอเท็ม =====
        P(@"^Tap again to sell (.+) for (\d+) Astronium \(it will be unequipped\)\.$", "แตะอีกครั้งเพื่อขาย $1 ได้ $2 แอสโทรเนียม (จะถูกถอดออกจากยาน)"),
        P(@"^Tap again to sell (.+) for (\d+) Astronium\.$", "แตะอีกครั้งเพื่อขาย $1 ได้ $2 แอสโทรเนียม"),
        P(@"^Sold (.+) for (\d+) Astronium\.$", "ขาย $1 ได้ $2 แอสโทรเนียม"),
        // ===== เพิ่ม 8 ต.ค. รอบ 2: แรงค์ / สกิลล็อก / แชทส่วนตัว / แท็กกิลด์อัตโนมัติ =====
        P(@"^(\d+) MMR above you$", "สูงกว่าคุณ $1 MMR"),
        P(@"^SEASON REWARD\s+\+(\d+) ASTRONIUM$", "รางวัลจบฤดูกาล  +$1 แอสโทรเนียม"),
        P(@"^(.+)\n<size=11>YOU</size>$", "$1\n<size=11>คุณ</size>"),
        P(@"^LOCKED\s+LV (\d+)$", "ล็อก  เลเวล $1"),
        P(@"^UNLOCK AT LV (\d+)$", "ปลดล็อกที่เลเวล $1"),
        P(@"^Reach level (\d+) to unlock (.+)\.$", "ต้องถึงเลเวล $1 ก่อนจึงจะปลดล็อก $2"),
        P(@"^PRIVATE\s+/\s+(.+)$", "แชทส่วนตัว  /  $1"),
        P(@"^Message (.+)\.\.\.$", "ส่งข้อความถึง $1..."),
        P(@"^No messages yet\. Say hi to (.+)!$", "ยังไม่มีข้อความ ทักทาย $1 เลย!"),
        P(@"^TAG\s+\[(.+)\]$", "แท็ก  [$1]"),
    };

    // ตัวช่วยสร้างคู่ (Regex, ข้อความไทย) สำหรับตาราง Patterns
    private static KeyValuePair<System.Text.RegularExpressions.Regex, string> P(string pattern, string thai)
        => new KeyValuePair<System.Text.RegularExpressions.Regex, string>(new System.Text.RegularExpressions.Regex(pattern), thai);

    // ตารางคำแปล อังกฤษ -> ไทย (ต้องตรงกับข้อความบนจอทุกตัวอักษร)
    public static readonly Dictionary<string, string> Table = new Dictionary<string, string>
    {
        { "CHOOSE MODE", "เลือกโหมด" }, { "CHOOSE HOW TO PLAY", "เลือกวิธีเล่น" }, { "HANGAR", "โรงเก็บยาน" },
        { "ROOM SETTINGS", "ตั้งค่าห้อง" }, { "PLAYERS / TEAMS", "ผู้เล่น / ทีม" },
        { "SCORE TARGET", "คะแนนเป้าหมาย" }, { "TIME LIMIT", "เวลาการแข่งขัน" }, { "MAP HAZARDS", "อันตรายในแผนที่" },
        { "FILL WITH BOTS", "เติมผู้เล่นด้วยบอท" }, { "SHIP UPGRADES", "ตีบวกยาน" },
        { "SOLO / SELECT A MODE, THEN START", "เล่นคนเดียว / เลือกโหมดแล้วกดเริ่ม" },
        { "Host changes rules. Changes require everyone to confirm READY again.", "เจ้าห้องเปลี่ยนกติกาได้ เมื่อเปลี่ยนทุกคนต้องกดพร้อมอีกครั้ง" },
        { "DIFFICULTY: EASY", "ความยาก: ง่าย" }, { "DIFFICULTY: NORMAL", "ความยาก: ปกติ" }, { "DIFFICULTY: HARD", "ความยาก: ยาก" },
        { "< RANK", "< แรงค์" },
        // ทั่วไป
        { "BACK", "กลับ" }, { "CLOSE", "ปิด" }, { "CANCEL", "ยกเลิก" }, { "LEAVE", "ออก" }, { "JOIN", "เข้าร่วม" },
        { "READY", "พร้อม" }, { "NOT READY", "ยังไม่พร้อม" }, { "CANCEL READY", "ยกเลิกพร้อม" }, { "WAITING", "รอ" },
        { "SETTINGS", "ตั้งค่า" }, { "LOG OUT", "ออกจากระบบ" }, { "LOG OUT?", "ออกจากระบบ?" },
        { "LEAVE MATCH", "ออกจากแมตช์" }, { "LEAVE THIS MATCH?", "ออกจากแมตช์นี้?" }, { "MORE OPTIONS", "ตั้งค่าเพิ่มเติม" },
        { "MASTER", "มาสเตอร์" }, { "MUSIC", "เพลง" }, { "EFFECTS", "เสียงเอฟเฟกต์" }, { "AIM SPEED", "ความไวเล็ง" },
        { "Settings are saved on this device.", "ค่าที่ตั้งจะถูกบันทึกในเครื่องนี้" },
        { "Online match continues while this menu is open.", "แมตช์ออนไลน์ยังเดินต่อระหว่างเปิดเมนูนี้" },
        { "You will return to the login screen.", "จะกลับไปหน้าล็อกอิน" },
        { "The current match will end for both players.", "แมตช์นี้จะจบลงสำหรับผู้เล่นทุกคน" },
        { "CONNECTING...", "กำลังเชื่อมต่อ..." }, { "RECONNECTING...", "กำลังเชื่อมต่อใหม่..." }, { "SYNCING...", "กำลังซิงก์..." },
        { "STARTING...", "กำลังเริ่ม..." }, { "OFFLINE", "ออฟไลน์" }, { "COPY", "คัดลอก" }, { "SEND", "ส่ง" },
        { "ADD", "เพิ่ม" }, { "ACCEPT", "รับ" }, { "DECLINE", "ปฏิเสธ" }, { "IGNORE", "ไม่สนใจ" }, { "REMOVE", "ลบ" }, { "UNEQUIP", "ถอด" },
        // สถิติหลังแมตช์ / ฉากฉลอง
        { "ACHIEVEMENT UNLOCKED", "ปลดล็อกความสำเร็จ" }, { "MATCH STATS", "สถิติแมตช์" }, { "RESULTS", "ผลการแข่ง" }, { "SHARPSHOOTER", "มือแม่นปืน" }, { "DESTROYER", "จอมถล่ม" }, { "IRON WALL", "กำแพงเหล็ก" }, { "ON FIRE", "ร้อนแรง" }, { "ACCURACY", "ความแม่นยำ" }, { "DAMAGE", "ดาเมจ" }, { "TAKEN", "ดาเมจที่โดน" }, { "BEST STREAK", "ฆ่าต่อเนื่องสูงสุด" }, { "DMG", "ดาเมจ" }, { "No badges this match", "แมตช์นี้ไม่มีใครได้ป้าย" }, { "Accuracy counts weapon shots only. Damage includes skills.", "ความแม่นยำนับเฉพาะกระสุนปืน ดาเมจรวมสกิลด้วย" },
        // เงื่อนไขชนะ (MatchRules.WinRules.cs)
        { "WIN CONDITION", "เงื่อนไขชนะ" }, { "FIRST TO X", "ฆ่าครบก่อนชนะ" }, { "MOST KILLS", "คิลมากสุด" }, { "NET SCORE", "คะแนนสุทธิ" }, { "MOST DAMAGE", "ดาเมจมากสุด" }, { "BOUNTY HUNT", "ล่าค่าหัว" }, { "MOST KILLS WHEN TIME RUNS OUT", "หมดเวลา ใครคิลมากสุดชนะ" }, { "DEATH -1", "ตาย -1" }, { "MOST DAMAGE WHEN TIME RUNS OUT", "หมดเวลา ใครทำดาเมจมากสุดชนะ" }, { "KILL STREAKERS FOR BONUS POINTS", "ฆ่าคนที่กำลังฆ่าต่อเนื่อง ได้แต้มพิเศษ" }, { "KILLS  -", "คิล  -" }, { "TAP TO CONTINUE", "แตะเพื่อไปต่อ" }, { "SOUND", "เสียง" }, { "CONTROL", "การควบคุม" }, { "DISPLAY", "การแสดงผล" }, { "GENERAL", "ทั่วไป" }, { "BATTLE", "ต่อสู้" }, { "SPECIAL MODES", "โหมดพิเศษ" }, { "PRACTICE", "ฝึกซ้อม" },
        // ===== ข้อความเพิ่ม 8 ต.ค. (ล็อบบี้/สถานะ/ภารกิจ/ผลแข่ง) =====
        // สถานะการเชื่อมต่อ / ล็อกอิน / ล็อบบี้
        { "System Initializing...", "กำลังเริ่มระบบ..." }, { "Ready to login", "พร้อมเข้าสู่ระบบ" }, { "Login failed", "เข้าสู่ระบบไม่สำเร็จ" },
        { "Logging in as Guest...", "กำลังเข้าสู่ระบบแบบผู้เยี่ยมชม..." }, { "Logging in with Google...", "กำลังเข้าสู่ระบบด้วย Google..." },
        { "Loading Firebase...", "กำลังโหลด Firebase..." }, { "Firebase connection failed", "เชื่อมต่อ Firebase ไม่สำเร็จ" },
        { "Account changed.", "เปลี่ยนบัญชีแล้ว" }, { "Account is not ready.", "บัญชียังไม่พร้อม" },
        { "Connecting...", "กำลังเชื่อมต่อ..." }, { "CONNECTING", "กำลังเชื่อมต่อ" }, { "Connecting to server...", "กำลังเชื่อมต่อเซิร์ฟเวอร์..." },
        { "Cannot connect to server", "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้" }, { "Please check your internet connection", "กรุณาตรวจสอบการเชื่อมต่ออินเทอร์เน็ต" },
        { "Connecting to the lobby automatically...", "กำลังเชื่อมต่อล็อบบี้อัตโนมัติ..." }, { "Entering Lobby...", "กำลังเข้าล็อบบี้..." },
        { "Connection lost. Reconnecting automatically...", "การเชื่อมต่อหลุด กำลังเชื่อมต่อใหม่อัตโนมัติ..." },
        { "Connection lost. Returning to your room...", "การเชื่อมต่อหลุด กำลังกลับห้องเดิม..." },
        { "Waiting for internet. Reconnection is automatic.", "รออินเทอร์เน็ต ระบบจะเชื่อมต่อใหม่ให้อัตโนมัติ" },
        { "Reconnecting to your room automatically...", "กำลังกลับเข้าห้องเดิมอัตโนมัติ..." },
        { "Battle interrupted. Reconnecting to your waiting room...", "การต่อสู้ถูกขัดจังหวะ กำลังกลับไปห้องรอ..." },
        { "Room recovery expired. Reconnecting to the lobby automatically...", "หมดเวลากู้คืนห้อง กำลังกลับล็อบบี้อัตโนมัติ..." },
        { "Room recovery unavailable. Retrying the lobby connection automatically...", "กู้คืนห้องไม่ได้ กำลังลองเชื่อมต่อล็อบบี้ใหม่อัตโนมัติ..." },
        { "Room unavailable. Returning to lobby...", "ห้องนี้ใช้ไม่ได้แล้ว กำลังกลับล็อบบี้..." },
        { "The room expired or the slot is no longer available.", "ห้องหมดอายุ หรือที่ว่างของคุณไม่มีแล้ว" },
        { "Could not return to the room.", "กลับห้องเดิมไม่ได้" }, { "Left room successfully", "ออกจากห้องแล้ว" },
        { "Loading...", "กำลังโหลด..." }, { "Loading pilot data...", "กำลังโหลดข้อมูลนักบิน..." }, { "Loading ship...", "กำลังโหลดยาน..." },
        { "Loading your ship and skill...", "กำลังโหลดยานและสกิลของคุณ..." }, { "Wait for your loadout to finish loading.", "รอโหลดยานและสกิลให้เสร็จก่อน" },
        { "Loadout delayed. Retrying automatically...", "โหลดยานและสกิลล่าช้า กำลังลองใหม่อัตโนมัติ..." },
        { "Loadout ready. Connecting...", "ยานและสกิลพร้อมแล้ว กำลังเชื่อมต่อ..." },
        { "Loadout ready. Create or join a room.", "ยานและสกิลพร้อมแล้ว สร้างหรือเข้าห้องได้เลย" },
        { "Ready! Create or join a room.", "พร้อมแล้ว! สร้างหรือเข้าห้องได้เลย" }, { "Ready! Press button to enter room", "พร้อมแล้ว! กดปุ่มเพื่อเข้าห้อง" },
        { "Coin balance is missing or invalid.", "ยอดเหรียญหายหรือไม่ถูกต้อง" }, { "Could not load coins. Check connection.", "โหลดเหรียญไม่ได้ ตรวจสอบการเชื่อมต่อ" },
        { "Astronium Coins : unavailable", "เหรียญแอสโทรเนียม : ไม่พร้อมใช้งาน" },
        { "PILOT HUB  /  1 VS 1 MULTIPLAYER", "ศูนย์นักบิน  /  ผู้เล่นหลายคน 1 ต่อ 1" }, { "1 VS 1 (Quick Match)", "1 ต่อ 1 (จับคู่ด่วน)" },
        { "LOADOUT / YOUR NEXT BATTLE STARTS HERE", "ยานและสกิล / เตรียมพร้อมสำหรับการต่อสู้ครั้งถัดไป" },
        { "Change your ship and skill in the hangar.", "เปลี่ยนยานและสกิลได้ที่โรงเก็บยาน" },
        { "HIGH SCORE", "คะแนนสูงสุด" }, { "ONLINE", "ออนไลน์" }, { "Player", "ผู้เล่น" },
        // หาห้อง / สร้างห้อง / ห้องรอ
        { "Searching for room (Quick Match)...", "กำลังหาห้อง (จับคู่ด่วน)..." }, { "No room found. Creating a new one...", "ไม่พบห้อง กำลังสร้างห้องใหม่..." },
        { "Creating...", "กำลังสร้าง..." }, { "Joining...", "กำลังเข้าร่วม..." }, { "Leaving room...", "กำลังออกจากห้อง..." },
        { "Enter a room code first.", "ใส่รหัสห้องก่อน" }, { "Room is full!", "ห้องเต็มแล้ว!" },
        { "No open rooms yet. Create one or enter a code.", "ยังไม่มีห้องว่าง สร้างห้องใหม่หรือใส่รหัสห้อง" },
        { "Failed to join room", "เข้าห้องไม่สำเร็จ" }, { "Failed to create room", "สร้างห้องไม่สำเร็จ" }, { "Room", "ห้อง" }, { "ROOM", "ห้อง" },
        { "Room code copied. Share it with your friend.", "คัดลอกรหัสห้องแล้ว ส่งให้เพื่อนได้เลย" },
        { "Share the room code to invite a friend", "ส่งรหัสห้องเพื่อชวนเพื่อน" }, { "Invite a friend using the room code.", "ชวนเพื่อนด้วยรหัสห้อง" },
        { "Waiting for a pilot", "รอนักบิน" }, { "You are now the room host", "คุณเป็นเจ้าห้องแล้ว" },
        { "Press BACK again to leave the room.", "กดกลับอีกครั้งเพื่อออกจากห้อง" }, { "The host removed you from that room.", "เจ้าห้องเตะคุณออกจากห้องนั้น" },
        { "Connecting or preparing battle...", "กำลังเชื่อมต่อหรือเตรียมการต่อสู้..." }, { "Preparing battle...", "กำลังเตรียมการต่อสู้..." },
        { "Starting solo battle...", "กำลังเริ่มเล่นคนเดียว..." }, { "Starting training...", "กำลังเริ่มฝึกซ้อม..." },
        { "All pilots ready. Host can launch the battle.", "นักบินพร้อมครบแล้ว เจ้าห้องกดเริ่มได้เลย" },
        { "Both pilots must confirm READY for the selected battlefield.", "นักบินทั้งสองต้องกดพร้อมสำหรับสนามที่เลือก" },
        { "Both players must be ready before starting.", "ผู้เล่นทั้งสองต้องพร้อมก่อนเริ่ม" },
        { "No rival yet: press READY, then START to fight a bot.", "ยังไม่มีคู่แข่ง: กดพร้อม แล้วกดเริ่มเพื่อสู้กับบอท" },
        { "A pilot is no longer ready. Confirm readiness again.", "มีนักบินยกเลิกพร้อม กดยืนยันพร้อมอีกครั้ง" },
        { "Match cancelled: a pilot left or disconnected. Wait for both pilots, then Ready again.", "แมตช์ถูกยกเลิก: มีนักบินออกหรือหลุด รอนักบินครบแล้วกดพร้อมใหม่" },
        { "Cancel READY before switching team.", "ยกเลิกพร้อมก่อนย้ายทีม" },
        { "SELECT BATTLEFIELD  /  HOST CONTROLS MAP", "เลือกสนามรบ  /  เจ้าห้องเป็นคนเลือกแม็พ" },
        { "RECONNECTING", "กำลังเชื่อมต่อใหม่" }, { "RECONNECTING (60s slot)", "กำลังเชื่อมต่อใหม่ (จองที่ไว้ 60 วิ)" },
        { "TAP = SWITCH TEAM", "แตะ = ย้ายทีม" }, { "TAP AGAIN = KICK", "แตะอีกครั้ง = เตะออก" },
        { "BOT WILL JOIN", "บอทจะเข้าร่วม" }, { "BOT OFF", "ไม่มีบอท" }, { "BOT TRAINING", "บอท ฝึกซ้อม" }, { "BOT EASY", "บอท ง่าย" },
        { "BOT NORMAL", "บอท ปกติ" }, { "BOT HARD", "บอท ยาก" }, { "EASY", "ง่าย" }, { "HARD", "ยาก" }, { "DIFFICULTY: TRAINING", "ความยาก: ฝึกซ้อม" },
        { "BLUE SLOT", "ช่องทีมฟ้า" }, { "RED SLOT", "ช่องทีมแดง" },
        { "BLUE team is full.", "ทีมฟ้าเต็มแล้ว" }, { "RED team is full.", "ทีมแดงเต็มแล้ว" },
        { "Switched to BLUE team.", "ย้ายไปทีมฟ้าแล้ว" }, { "Switched to RED team.", "ย้ายไปทีมแดงแล้ว" },
        { "HAZARDS ON", "อันตรายในแม็พ: เปิด" }, { "HAZARDS OFF", "อันตรายในแม็พ: ปิด" }, { "UPGRADES ON", "ตีบวกยาน: เปิด" }, { "UPGRADES OFF", "ตีบวกยาน: ปิด" },
        { "POWER-UPS", "ไอเท็มเสริมพลัง" }, { "POWER-UPS ON", "ไอเท็มเสริมพลัง: เปิด" }, { "POWER-UPS OFF", "ไอเท็มเสริมพลัง: ปิด" }, { "TIME", "เวลา" },
        // โหมดเล่นคนเดียว / ชื่อแม็พ / คำอธิบายแม็พ
        { "ROYALE", "แบทเทิลรอยัล" }, { "FREE-FOR-ALL", "ทุกคนเป็นศัตรู" },
        { "Electric Jellyfish Core", "แกนแมงกะพรุนไฟฟ้า" }, { "Obelisk Plains of Prism", "ทุ่งเสาปริซึม" }, { "Abandoned Mech Warzone", "สุสานหุ่นรบร้าง" },
        { "Asteroid Station", "สถานีอุกกาบาต" }, { "Molten Nebula", "เนบิวลาลาวา" },
        { "Cover and side routes.", "มีที่กำบังและทางอ้อมด้านข้าง" }, { "Energy core: trade HP for fire rate.", "แกนพลังงาน: แลก HP เพื่อยิงเร็วขึ้น" },
        { "Hazard: lightning strikes.", "อันตราย: ฟ้าผ่า" }, { "Crystals bounce your shots.", "คริสตัลสะท้อนกระสุน" },
        { "Warp gates link the corners.", "ประตูวาร์ปเชื่อมมุมแม็พ" }, { "Hazard: slowing pools.", "อันตราย: แอ่งที่ทำให้ช้าลง" },
        { "12 rocks / 2 turrets / 6 red cores.", "หิน 12 ก้อน / ป้อมปืน 2 ป้อม / แกนแดง 6 ลูก" }, { "Use cover around the wreck.", "ใช้ซากยานเป็นที่กำบัง" },
        { "Hazard: falling lava asteroids.", "อันตราย: อุกกาบาตลาวาตกใส่" }, { "Station ring in an asteroid field.", "สถานีวงแหวนกลางดงอุกกาบาต" },
        { "Energy gates guard the lanes.", "ประตูพลังงานกั้นเส้นทาง" }, { "Hazard: meteor strikes.", "อันตราย: อุกกาบาตพุ่งชน" },
        { "Molten rocks burn on contact.", "หินหลอมเหลวเผาไหม้เมื่อสัมผัส" }, { "Open lava field, fast fights.", "ทุ่งลาวาโล่ง สู้กันดุเดือด" },
        // วิธีเล่น
        { "Left stick: move. Hold FIRE and drag to aim.", "จอยซ้าย: เคลื่อนที่  กดปุ่มยิงค้างแล้วลากเพื่อเล็ง" },
        { "Tap the separate SKILL button when ready.", "แตะปุ่มสกิลเมื่อสกิลพร้อมใช้" },
        { "STUN: a seeking wave briefly disables an enemy.", "STUN: คลื่นติดตามที่ทำให้ศัตรูขยับไม่ได้ชั่วครู่" },
        { "SHIELD: temporary protection and a movement boost.", "SHIELD: โล่ป้องกันชั่วคราวและบินเร็วขึ้น" },
        { "NOVA: place a mine that explodes after a short delay.", "NOVA: วางทุ่นระเบิดที่ระเบิดหลังหน่วงเวลาสั้นๆ" },
        { "SEEKER: launch a homing missile; solid cover blocks it.", "SEEKER: ยิงมิสไซล์ติดตาม สิ่งกำบังทึบกันได้" },
        { "BLINK: warp forward. HEAL: repair 35% hull. CLOAK: vanish for 3 sec.", "BLINK: วาร์ปไปข้างหน้า  HEAL: ซ่อมยาน 35%  CLOAK: ล่องหน 3 วินาที" },
        { "BATTLEFIELDS", "สนามรบ" }, { "MORE", "อื่นๆ" },
        { "MECH: avoid hot rocks, turret fire and falling meteors.", "สุสานหุ่นรบ: หลบหินร้อน ป้อมปืน และอุกกาบาตที่ตกลงมา" },
        { "PRISM: use pillars as cover. Green pools slow you by 30%.", "ทุ่งปริซึม: ใช้เสาเป็นที่กำบัง แอ่งสีเขียวทำให้ช้าลง 30%" },
        { "JELLYFISH: escape energy-orb pull and lightning warnings.", "แกนแมงกะพรุน: หนีแรงดูดของลูกพลังงานและจุดเตือนฟ้าผ่า" },
        { "The central core boosts firing speed but drains HP.", "แกนกลางช่วยให้ยิงเร็วขึ้น แต่จะดูด HP" },
        { "STATION: dodge meteor strikes (red warning circle).", "สถานีอุกกาบาต: หลบอุกกาบาตที่พุ่งลงมา (วงเตือนสีแดง)" },
        { "MOLTEN NEBULA: lava rocks burn on contact.", "เนบิวลาลาวา: หินลาวาเผาไหม้เมื่อสัมผัส" },
        { "Pick up crystals for repair, speed, shield and damage.", "เก็บคริสตัลเพื่อซ่อมยาน เพิ่มความเร็ว โล่ และพลังโจมตี" },
        { "Game modes, upgrades and options are in the room and SETTINGS.", "โหมดเกม การตีบวกยาน และตัวเลือกอื่นๆ อยู่ในห้องและเมนูตั้งค่า" },
        // ในสนามรบ: ป้าย / สถานะ / Kill Streak / อีโมต / ไอเท็มเสริมพลัง
        { "HIT", "โดน" }, { "SHIELD HIT", "โดนโล่" }, { "BLOCKED", "กันได้" }, { "KILL +1", "ฆ่า +1" }, { "BOSS!", "บอส!" },
        { "OUT OF LIVES  -  SPECTATING", "ชีวิตหมด  -  กำลังชม" }, { "SELF DESTRUCTION", "ทำลายตัวเอง" },
        { "DESTROYED BY BATTLEFIELD HAZARD", "ถูกอันตรายในสนามทำลาย" },
        { "self-destructed", "ทำลายตัวเอง" }, { "destroyed by hazard", "ถูกอันตรายในแม็พทำลาย" },
        { "lost connection", "หลุดการเชื่อมต่อ" }, { "left the battle", "ออกจากการต่อสู้" }, { "reconnected", "กลับเข้าเกมแล้ว" },
        { "POISON / SLOW -30%", "ติดพิษ / ช้าลง 30%" }, { "SLOW / SPEED -30%", "ติดช้า / ความเร็ว -30%" },
        { "OVERLOAD / RAPID FIRE", "โอเวอร์โหลด / ยิงรัว" }, { "OVERLOAD / HP DRAIN", "โอเวอร์โหลด / เสีย HP" },
        { "DOUBLE KILL", "ดับเบิลคิล" }, { "KILLING SPREE", "คิลต่อเนื่อง" }, { "RAMPAGE", "อาละวาด" }, { "UNSTOPPABLE", "หยุดไม่อยู่" }, { "GODLIKE", "ระดับเทพ" },
        { "DOUBLE KILL!", "ดับเบิลคิล!" }, { "KILLING SPREE!", "คิลต่อเนื่อง!" }, { "RAMPAGE!", "อาละวาด!" }, { "UNSTOPPABLE!", "หยุดไม่อยู่!" }, { "GODLIKE!", "ระดับเทพ!" },
        { "REPAIR!", "ซ่อมยาน!" }, { "OVERDRIVE!", "โอเวอร์ไดรฟ์!" }, { "BOOST!", "บูสต์ความเร็ว!" }, { "SHIELD!", "โล่พลังงาน!" }, { "DAMAGE!", "พลังโจมตีเพิ่ม!" },
        { "NICE SHOT!", "ยิงสวย!" }, { "OOPS!", "อุ๊ปส์!" }, { "HELLO!", "สวัสดี!" }, { "NONE", "ไม่มี" }, { "(LEFT)", "(ออกแล้ว)" },
        // หน้าผลการแข่ง / แถบความก้าวหน้า
        { "FINAL SCORE", "คะแนนสุดท้าย" }, { "YOUR PLACE", "อันดับของคุณ" }, { "YOUR KILLS", "ฆ่าของคุณ" }, { "SHARED 1ST", "ร่วมอันดับ 1" },
        { "WIN", "ชนะ" }, { "LOSE", "แพ้" }, { "RANK UP!", "แรงค์อัป!" }, { "rank down", "แรงค์ลด" }, { "LEVEL UP!", "เลเวลอัป!" },
        { "ACHIEVEMENT", "ความสำเร็จใหม่" }, { "YOU:", "คุณ:" }, { "YOU: BLUE", "คุณ: ทีมฟ้า" }, { "YOU: RED", "คุณ: ทีมแดง" },
        // ทักษะ (คำอธิบายสั้นในการ์ดสกิล)
        { "Paralyze wave", "คลื่นสตันศัตรู" }, { "Invincibility bubble", "โล่อมตะ" }, { "Area explosion", "ระเบิดรอบตัว" },
        { "Homing missile", "มิสไซล์ติดตาม" }, { "Teleport forward 9 units", "วาร์ปไปข้างหน้า 9 หน่วย" },
        { "Repair 35% of your hull", "ซ่อมยาน 35% ของเลือดสูงสุด" }, { "Invisible to enemies for 3 sec (firing reveals you)", "ล่องหน 3 วินาที (ยิงแล้วจะปรากฏตัว)" },
        { "Instantly teleports up to 9 units forward, stopping before obstacles and the arena edge. Dodge shots or chase enemies.", "วาร์ปไปข้างหน้าทันทีสูงสุด 9 หน่วย หยุดก่อนชนสิ่งกีดขวางและขอบสนาม ใช้หลบกระสุนหรือไล่ตามศัตรู" },
        { "Instantly repairs 35% of your max HP (cannot exceed full HP). Use it when low to stay in the fight.", "ซ่อมยานทันที ฟื้นเลือด 35% ของเลือดสูงสุด (เกินเลือดเต็มไม่ได้) ใช้ตอนเลือดเหลือน้อยเพื่อสู้ต่อ" },
        { "Become invisible to enemies for 3 sec. Firing reveals you immediately. Sneak up on targets or slip away.", "ล่องหน 3 วินาที ศัตรูมองไม่เห็นยานคุณ ยิงเมื่อไหร่จะปรากฏตัวทันที ใช้ย่องเข้าใกล้หรือหนีออกจากวงล้อม" },
        // โรงเก็บยาน / สีย้อม
        { "Choose a ship or open the SKILLS tab.", "เลือกยาน หรือเปิดแท็บสกิล" }, { "Choose a ship, then equip it for your next battle.", "เลือกยานแล้วกดใช้งานสำหรับการต่อสู้ครั้งถัดไป" },
        { "EQUIPPED SKILL", "สกิลที่ใช้" }, { "COINS", "เหรียญ" }, { "Paint", "สีย้อม" },
        { "Paint: ORIGINAL  /  applies to every ship.", "สีย้อม: สีเดิม  /  ใช้กับยานทุกลำ" }, { "Paint: CRIMSON  /  applies to every ship.", "สีย้อม: แดงเข้ม  /  ใช้กับยานทุกลำ" },
        { "Paint: AZURE  /  applies to every ship.", "สีย้อม: ฟ้าคราม  /  ใช้กับยานทุกลำ" }, { "Paint: EMERALD  /  applies to every ship.", "สีย้อม: มรกต  /  ใช้กับยานทุกลำ" },
        { "Paint: GOLD  /  applies to every ship.", "สีย้อม: ทอง  /  ใช้กับยานทุกลำ" }, { "Paint: SHADOW  /  applies to every ship.", "สีย้อม: เงามืด  /  ใช้กับยานทุกลำ" },
        // โรงซ่อม: ตีบวก / ไอเท็ม / ร้านค้า / กล่องเสบียง
        { "HULL (HP)", "ตัวยาน (HP)" }, { "ENGINE (SPD)", "เครื่องยนต์ (SPD)" }, { "WEAPON (ATK)", "อาวุธ (ATK)" },
        { "COMMON", "ธรรมดา" }, { "RARE", "หายาก" }, { "EPIC", "เอพิค" }, { "SAFE", "ชัวร์" }, { "CHANCE", "ลุ้น" },
        { "SAFE = always succeeds.   CHANCE = half price, may fail (coins spent, level is never lost).   Host can turn upgrades off per room.", "ชัวร์ = สำเร็จทุกครั้ง   ลุ้น = ราคาครึ่งเดียว อาจพลาด (เสียเหรียญ แต่เลเวลไม่ลด)   เจ้าห้องปิดการตีบวกยานได้ในแต่ละห้อง" },
        { "Upgrade failed... (coins spent, level kept)", "ตีบวกไม่สำเร็จ... (เสียเหรียญ แต่เลเวลไม่ลด)" },
        { "Item upgrade failed (level kept).", "ตีบวกไอเท็มไม่สำเร็จ (เลเวลไม่ลด)" }, { "Item is already at max level.", "ไอเท็มนี้เลเวลสูงสุดแล้ว" },
        { "IN BATTLE (upgrades + items):", "ในการต่อสู้ (ตีบวกยาน + ไอเท็ม):" }, { "FIRE RATE", "อัตรายิง" }, { "SKILL CD", "คูลดาวน์สกิล" },
        { "LOCKED", "ล็อก" }, { "(better ship)", "(ใช้ยานที่ดีกว่า)" },
        { "No items yet. Buy some in the SHOP tab or open a Supply Crate.", "ยังไม่มีไอเท็ม ซื้อได้ที่แท็บร้านค้าหรือเปิดกล่องเสบียง" },
        { "ITEM SHOP  (EPIC items only from Supply Crates)", "ร้านไอเท็ม  (ไอเท็ม EPIC ได้จากกล่องเสบียงเท่านั้น)" },
        { "This item is only found in Supply Crates.", "ไอเท็มนี้ได้จากกล่องเสบียงเท่านั้น" }, { "BUY", "ซื้อ" }, { "OPEN", "เปิด" }, { "Opened", "เปิดไปแล้ว" },
        { "Processing...", "กำลังดำเนินการ..." }, { "Purchase not confirmed. Check coins/connection and try again.", "การซื้อยังไม่ยืนยัน ตรวจสอบเหรียญ/การเชื่อมต่อแล้วลองใหม่" },
        // ภารกิจ / รางวัลล็อกอิน / โปรไฟล์
        { "DAILY MISSIONS", "ภารกิจรายวัน" }, { "DAY", "วันที่" }, { "MATCHES", "แมตช์" }, { "WINS", "ชนะ" }, { "LOSSES", "แพ้" }, { "DRAWS", "เสมอ" },
        { "WIN RATE", "อัตราชนะ" }, { "BEST WIN STREAK", "ชนะติดกันสูงสุด" }, { "ACHIEVEMENTS", "ความสำเร็จ" }, { "TITLE", "ฉายา" },
        { "No matches yet.", "ยังไม่มีประวัติแมตช์" },
        { "Play 3 matches", "เล่น 3 แมตช์" }, { "Win 1 match", "ชนะ 1 แมตช์" }, { "Destroy 10 enemy ships", "ทำลายยานศัตรู 10 ลำ" },
        { "Use your skill 8 times", "ใช้สกิล 8 ครั้ง" }, { "Play 2 online matches", "เล่นออนไลน์ 2 แมตช์" },
        { "Finish top 3 in Free-for-all", "ติด 3 อันดับแรกในโหมดทุกคนเป็นศัตรู" }, { "Win a team battle", "ชนะโหมดทีม 1 ครั้ง" },
        { "Win with 2 deaths or fewer", "ชนะโดยตายไม่เกิน 2 ครั้ง" }, { "Play 20 matches this week", "เล่น 20 แมตช์ในสัปดาห์นี้" },
        { "Destroy 60 ships this week", "ทำลายยาน 60 ลำในสัปดาห์นี้" }, { "Win 10 matches this week", "ชนะ 10 แมตช์ในสัปดาห์นี้" },
        { "First Blood", "เลือดแรก" }, { "First Victory", "ชัยชนะแรก" }, { "Ace Pilot", "นักบินเอซ" }, { "Star Destroyer", "ผู้ทำลายดวงดาว" },
        { "Veteran", "ทหารผ่านศึก" }, { "Frequent Flyer", "นักบินขาประจำ" }, { "Unstoppable", "หยุดไม่อยู่" }, { "Untouchable", "ไร้รอยขีดข่วน" },
        { "Last Pilot Standing", "นักบินคนสุดท้าย" }, { "Squad Leader", "หัวหน้าหน่วย" }, { "Machine Breaker", "ผู้พิฆาตจักรกล" }, { "Rising Star", "ดาวรุ่ง" },
        { "Destroy your first ship", "ทำลายยานลำแรก" }, { "Win your first match", "ชนะแมตช์แรก" }, { "Destroy 100 ships", "ทำลายยาน 100 ลำ" },
        { "Destroy 500 ships", "ทำลายยาน 500 ลำ" }, { "Win 25 matches", "ชนะ 25 แมตช์" }, { "Play 50 matches", "เล่น 50 แมตช์" },
        { "Win 5 matches in a row", "ชนะติดกัน 5 แมตช์" }, { "Win a match without dying", "ชนะโดยไม่ตายเลย" }, { "Win a Free-for-all", "ชนะโหมดทุกคนเป็นศัตรู" },
        { "Win 10 team battles", "ชนะโหมดทีม 10 ครั้ง" }, { "Beat a HARD bot", "ชนะบอทระดับยาก" }, { "Reach level 10", "ถึงเลเวล 10" },
        // ฉายา (เลเวล + Achievement)
        { "RECRUIT", "ทหารใหม่" }, { "CADET", "นักเรียนนักบิน" }, { "STAR CAPTAIN", "กัปตันดวงดาว" }, { "COMMANDER", "ผู้บัญชาการ" },
        { "ADMIRAL", "พลเรือเอก" }, { "LEGEND OF THE STARS", "ตำนานแห่งดวงดาว" }, { "ROOKIE", "มือใหม่" }, { "VICTOR", "ผู้พิชิต" },
        { "ACE", "เอซ" }, { "STAR DESTROYER", "ผู้ทำลายดวงดาว" }, { "VETERAN", "ทหารผ่านศึก" }, { "FREQUENT FLYER", "นักบินขาประจำ" },
        { "UNTOUCHABLE", "ไร้รอยขีดข่วน" }, { "LAST PILOT STANDING", "นักบินคนสุดท้าย" }, { "SQUAD LEADER", "หัวหน้าหน่วย" },
        { "MACHINE BREAKER", "ผู้พิฆาตจักรกล" }, { "RISING STAR", "ดาวรุ่ง" },
        { "TITLE: RECRUIT", "ฉายา: ทหารใหม่" }, { "TITLE: CADET", "ฉายา: นักเรียนนักบิน" }, { "TITLE: STAR CAPTAIN", "ฉายา: กัปตันดวงดาว" },
        { "TITLE: COMMANDER", "ฉายา: ผู้บัญชาการ" }, { "TITLE: ADMIRAL", "ฉายา: พลเรือเอก" }, { "TITLE: LEGEND OF THE STARS", "ฉายา: ตำนานแห่งดวงดาว" },
        { "TITLE: ROOKIE", "ฉายา: มือใหม่" }, { "TITLE: VICTOR", "ฉายา: ผู้พิชิต" }, { "TITLE: ACE", "ฉายา: เอซ" },
        { "TITLE: STAR DESTROYER", "ฉายา: ผู้ทำลายดวงดาว" }, { "TITLE: VETERAN", "ฉายา: ทหารผ่านศึก" }, { "TITLE: FREQUENT FLYER", "ฉายา: นักบินขาประจำ" },
        { "TITLE: UNSTOPPABLE", "ฉายา: หยุดไม่อยู่" }, { "TITLE: UNTOUCHABLE", "ฉายา: ไร้รอยขีดข่วน" }, { "TITLE: LAST PILOT STANDING", "ฉายา: นักบินคนสุดท้าย" },
        { "TITLE: SQUAD LEADER", "ฉายา: หัวหน้าหน่วย" }, { "TITLE: MACHINE BREAKER", "ฉายา: ผู้พิฆาตจักรกล" }, { "TITLE: RISING STAR", "ฉายา: ดาวรุ่ง" },
        // แรงค์ / ฤดูกาล / ตารางอันดับ
        { "BRONZE", "บรอนซ์" }, { "SILVER", "ซิลเวอร์" }, { "GOLD", "โกลด์" }, { "PLATINUM", "แพลทินัม" }, { "DIAMOND", "ไดมอนด์" }, { "LEGEND", "เลเจนด์" },
        { "SEASON", "ฤดูกาล" }, { "PEAK", "สูงสุด" }, { "Highest rank reached!", "ถึงแรงค์สูงสุดแล้ว!" },
        { "CLAIM LAST SEASON REWARD", "รับรางวัลฤดูกาลที่แล้ว" },
        { "Searching for a ranked opponent...", "กำลังหาคู่แข่งแรงค์..." }, { "No ranked room found. Waiting for an opponent...", "ไม่พบห้องแรงค์ กำลังรอคู่แข่ง..." },
        { "No ranked players yet. Be the first!", "ยังไม่มีผู้เล่นแรงค์ มาเป็นคนแรกกัน!" }, { "Leaderboard needs an online login.", "ตารางอันดับต้องล็อกอินแบบออนไลน์" },
        // เพื่อน / กิลด์ / แชท
        { "FRIENDS", "เพื่อน" }, { "REQUESTS", "คำขอ" }, { "YOUR FRIEND CODE:", "รหัสเพื่อนของคุณ:" }, { "Friend code copied", "คัดลอกรหัสเพื่อนแล้ว" },
        { "ONLINE  /  IN LOBBY", "ออนไลน์  /  อยู่ในล็อบบี้" }, { "wants to be your friend", "อยากเป็นเพื่อนกับคุณ" },
        { "Friend request sent!", "ส่งคำขอเป็นเพื่อนแล้ว!" }, { "Friend code must be 8 characters.", "รหัสเพื่อนต้องมี 8 ตัวอักษร" },
        { "That is your own code.", "นี่คือรหัสของคุณเอง" }, { "You are already friends.", "เป็นเพื่อนกันอยู่แล้ว" },
        { "No friend requests.", "ไม่มีคำขอเป็นเพื่อน" }, { "No friends yet. Share your code or add a friend's code.", "ยังไม่มีเพื่อน แชร์รหัสของคุณหรือใส่รหัสเพื่อน" },
        { "Log in online to add friends.", "ล็อกอินออนไลน์เพื่อเพิ่มเพื่อน" },
        { "Log in with an online account to use friends, chat and guilds.", "ล็อกอินด้วยบัญชีออนไลน์เพื่อใช้ระบบเพื่อน แชท และกิลด์" },
        { "Could not send the request.", "ส่งคำขอไม่สำเร็จ" }, { "Could not send the request. Please try again.", "ส่งคำขอไม่สำเร็จ ลองใหม่อีกครั้ง" },
        { "Sending...", "กำลังส่ง..." }, { "CREATE A GUILD", "สร้างกิลด์" }, { "MEMBERS", "สมาชิก" }, { "* = guild leader", "* = หัวหน้ากิลด์" },
        { "Guild members get a [TAG] before their name, a private guild chat and a member list.", "สมาชิกกิลด์จะมี [TAG] หน้าชื่อ มีแชทกิลด์ส่วนตัว และรายชื่อสมาชิก" },
        { "Guild name must be 3-20 characters.", "ชื่อกิลด์ต้องยาว 3-20 ตัวอักษร" },
        { "Tag must be 3-5 letters or numbers (A-Z, 0-9).", "TAG ต้องเป็นตัวอักษรหรือตัวเลข 3-5 ตัว (A-Z, 0-9)" },
        { "That guild is full.", "กิลด์นี้เต็มแล้ว" }, { "Leave your current guild first.", "ออกจากกิลด์ปัจจุบันก่อน" }, { "You are not in a guild.", "คุณยังไม่มีกิลด์" },
        { "Tap LEAVE GUILD again to confirm.", "แตะออกจากกิลด์อีกครั้งเพื่อยืนยัน" }, { "Guilds are offline.", "ระบบกิลด์ออฟไลน์อยู่" },
        { "Join a guild to use guild chat.", "เข้ากิลด์ก่อนจึงจะใช้แชทกิลด์ได้" }, { "Chat is offline.", "แชทออฟไลน์อยู่" },
        { "GLOBAL CHAT  (all pilots)", "แชทรวม  (นักบินทุกคน)" }, { "Slow down a little.", "ส่งช้าลงหน่อยนะ" },
        { "TEAM BATTLE", "ต่อสู้แบบทีม" }, { "FREE FOR ALL", "ตัวต่อตัว" }, { "1 VS 1", "1 ต่อ 1" }, { "Duel", "ดวลเดี่ยวกับบอท" },
        { "Free for all", "ทุกคนเป็นศัตรู" }, { "Team battle", "แบ่งทีมสู้กัน" }, { "Survive waves", "รอดจากศัตรูทุกระลอก" },
        { "Capture the hill", "ยึดพื้นที่ให้ได้นานที่สุด" }, { "Collect stars", "แย่งเก็บดาว" }, { "Last ship standing", "อยู่รอดเป็นลำสุดท้าย" },
        { "Story stages", "ด่านเนื้อเรื่อง 5 ด่าน" }, { "Free practice against target bots", "ฝึกบินและยิงเป้าซ้อมได้อิสระ" },
        { "BOTS", "บอท" }, { "BOT", "บอท" }, { "Set difficulty, then press VS BOT", "ตั้งความยากแล้วกด สู้กับบอท" }, // สนามรบ / หน้าผล (เพิ่ม 8 ต.ค.)
        { "YOU", "คุณ" }, { "(YOU)", "(คุณ)" }, { "RIVAL", "คู่แข่ง" }, { "RIVAL PILOT", "นักบินคู่แข่ง" }, { "PILOT", "นักบิน" },
        { "STAGE", "ด่าน" }, { "ASTRONIUM", "แอสโทรเนียม" }, { "LIVES", "ชีวิต" }, { "WAVE", "ระลอก" }, { "ENEMIES", "ศัตรู" },
        { "ALIVE", "รอด" }, { "ZONE", "วงพื้นที่" }, { "LEAD", "ผู้นำ" }, { "TO", "เป้า" }, { "HILL", "ยึดเนิน" }, { "STARS", "ดาว" },
        { "SCOREBOARD", "ตารางคะแนน" }, { "(TAP)", "(แตะ)" }, { "BLUE", "ฟ้า" }, { "RED", "แดง" },
        { "NEXT STAGE UNLOCKED!", "ปลดล็อกด่านถัดไปแล้ว!" }, { "NEXT STAGE", "ด่านถัดไป" }, { "PLAY AGAIN", "เล่นอีกครั้ง" },
        { "MATCH REPORT", "สรุปผลการแข่ง" }, { "KING OF THE HILL", "ยึดเนิน" }, { "STAR HUNT", "ล่าดาว" }, { "BATTLE ROYALE", "แบทเทิลรอยัล" },
        { "HOLD THE ZONE", "ยึดพื้นที่ในวงไว้ให้นาน" }, { "COLLECT STARS", "เก็บดาวให้ได้มากที่สุด" }, { "LAST SHIP STANDING", "อยู่รอดเป็นลำสุดท้าย" },
        { "FIRST CONTACT", "การปะทะครั้งแรก" }, { "TWIN RAIDERS", "โจรคู่แฝด" }, { "AMBUSH", "ซุ่มโจมตี" },
        { "ELITE SQUADRON", "ฝูงบินหัวกะทิ" }, { "THE DREADNOUGHT", "ยานแม่เดรดนอต" }, { "Select a skill to see its effect and cooldown. Install it to change your loadout.", "เลือกสกิลเพื่อดูความสามารถและคูลดาวน์ กดติดตั้งเพื่อใช้ในการแข่งครั้งต่อไป" }, { "SHIP SLOTS", "ช่องไอเท็มของยาน" }, { "tap to unequip", "แตะเพื่อถอด" }, { "YOUR ITEMS", "ไอเท็มของฉัน" }, { "MOVE TO SHIP", "ย้ายมาใส่" }, { "ALL ITEMS", "ไอเท็มทั้งหมด" }, { "ALL ITEMS (rarest first)", "ไอเท็มทั้งหมด (เรียงจากหายากสุด)" },
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
        { "WORKSHOP", "โรงซ่อม" }, { "UPGRADE", "ตีบวกยาน" }, { "UPGRADES", "ตีบวกยาน" }, { "SHIP UPGRADE", "ตีบวกยาน" }, { "ITEMS", "ไอเท็ม" }, { "SHOP", "ร้านค้า" },
        { "SUPPLY CRATE", "กล่องเสบียง" }, { "INSTALL", "ติดตั้ง" }, { "INSTALLED", "ติดตั้งแล้ว" }, { "REPAIR", "ซ่อม" },
        { "MISSIONS", "ภารกิจ" }, { "MISSIONS & REWARDS", "ภารกิจและรางวัล" }, { "WEEKLY MISSIONS", "ภารกิจรายสัปดาห์" },
        { "PROFILE", "โปรไฟล์" }, { "RECENT MATCHES", "แมตช์ล่าสุด" }, { "CHANGE TITLE", "เปลี่ยนฉายา" }, { "REWARD", "รางวัล" },
        { "RANKED", "แรงค์" }, { "RANKED 1V1", "แรงค์ 1v1" }, { "FIND RANKED MATCH", "หาแมตช์แรงค์" },
        { "LEADERBOARD", "ตารางอันดับ" }, { "LEADERBOARD TOP 20", "อันดับ 20 อันดับแรก" }, { "RANK", "อันดับ" },
        { "SOCIAL", "สังคม" }, { "GUILD", "กิลด์" }, { "GUILD CHAT", "แชทกิลด์" }, { "CHAT", "แชท" }, { "GLOBAL", "รวม" },
        { "LEAVE GUILD", "ออกจากกิลด์" }, { "OR JOIN A GUILD BY TAG", "หรือเข้ากิลด์ด้วย TAG" },
        { "GAME MODE", "โหมดเกม" }, { "DEATHMATCH", "ดวลกัน" }, { "SURVIVAL", "เอาชีวิตรอด" }, { "CAMPAIGN", "เนื้อเรื่อง" },
        { "JELLYFISH CORE", "แกนแมงกะพรุน" }, { "PRISM PLAINS", "ทุ่งปริซึม" }, { "MECH WARZONE", "สุสานหุ่นรบ" },
        { "ASTEROID STATION", "สถานีอุกกาบาต" }, { "MOLTEN NEBULA", "เนบิวลาลาวา" },
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
        // ===== เพิ่ม 8 ต.ค. รอบ 2 =====
        { "Room not found. Check the code - the room may have closed.", "ไม่พบห้องนี้ ตรวจรหัสอีกครั้ง - ห้องอาจปิดไปแล้ว" },
        { "That room is closed or the match has already started.", "ห้องนี้ปิดแล้วหรือเริ่มแข่งไปแล้ว" },
        { "Connecting to the server... try again in a moment.", "กำลังเชื่อมต่อเซิร์ฟเวอร์... ลองใหม่อีกสักครู่" },
        { "Tap your rank to go back", "แตะแรงค์ของคุณเพื่อกลับ" },
        { "Tap a rank below to view it", "แตะแรงค์ด้านล่างเพื่อดูรายละเอียด" },
        { "You have passed this rank", "คุณผ่านแรงค์นี้มาแล้ว" },
        { "NEW MSG", "ข้อความใหม่" },
        { "Only you and your friend can see this chat.", "เห็นแค่คุณกับเพื่อนเท่านั้น" },
        { "Messages disappear after 2 minutes.", "ข้อความจะหายไปเองหลัง 2 นาที" },
        { "Could not make a guild tag. Try again.", "สร้างแท็กกิลด์ไม่สำเร็จ ลองใหม่อีกครั้ง" },
        { "Type a message...", "พิมพ์ข้อความ..." },
        { "No messages yet. Say hello!", "ยังไม่มีข้อความ ทักทายกันเลย!" },
        { "TAG", "แท็ก" },
        { "PRIVATE", "ส่วนตัว" },
        // ===== แรงค์ชวนเพื่อนไม่ได้ =====
        { "Ranked matches can't be joined with a code or invite. Use FIND RANKED MATCH.", "ห้องแรงค์จอยด้วยรหัสหรือคำชวนไม่ได้ กดหาแมตช์แรงค์แทน" },
        { "Ranked rooms can't be shared.", "ห้องแรงค์แชร์รหัสไม่ได้" },
        { "IN RANKED MATCH", "อยู่ในแมตช์แรงค์" }, { "RANKED MATCH", "แมตช์แรงค์" },
        // ===== ขายไอเท็ม =====
        { "SELL", "ขาย" }, { "This item can't be sold.", "ไอเท็มนี้ขายไม่ได้" },
        { "Could not sell (connection). Your item was returned.", "ขายไม่สำเร็จ (การเชื่อมต่อ) คืนไอเท็มให้แล้ว" },
        // ===== ร้านค้ารายวัน =====
        { "DAILY SHOP  (new items every day, 1 of each)", "ร้านค้ารายวัน  (ของใหม่ทุกวัน ซื้อได้อย่างละ 1 ชิ้น)" },
        { "SOLD OUT", "ขายหมดแล้ว" }, { "This offer is no longer available.", "สินค้านี้หมดเวลาแล้ว" },
        { "Already bought today. The shop refreshes at midnight.", "วันนี้ซื้อไปแล้ว ร้านจะสุ่มของใหม่ตอนเที่ยงคืน" },
        { "RETRY", "ลองใหม่" },
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

// ตัวแปลภาษาตอนวาดข้อความ (ไม่กระพริบ)
// ใช้ ITextPreprocessor ของ TextMeshPro: ข้อความในโค้ดยังเป็นอังกฤษ (โค้ดเดิมเทียบข้อความได้ตามปกติ)
// แต่ตอน TextMeshPro สร้างตัวอักษรบนจอ จะเปลี่ยนเป็นภาษาไทยก่อนวาด -> ผู้เล่นเห็นภาษาไทยตั้งแต่เฟรมแรก
// ติดตั้งให้ข้อความทุกอันอัตโนมัติ (ทันทีที่ข้อความถูกวาดครั้งแรก + ตรวจซ้ำทุก 2 วิเผื่อหลุด)
// ข้ามช่องพิมพ์ของผู้เล่น (TMP_InputField)
public class LangTranslator : MonoBehaviour, ITextPreprocessor
{
    private static LangTranslator instance; // ตัวแปลภาษาตัวเดียวที่อยู่ข้ามฉาก (DontDestroyOnLoad)
    private float nextScan; // เวลาที่จะสแกนหาข้อความใหม่รอบถัดไป (ทุก 2 วินาที)
    private bool appliedThai; // ภาษาที่ใช้วาดล่าสุด ถ้าไม่ตรง Lang.Thai จะวาดข้อความใหม่ทั้งหมด

    // เรียกอัตโนมัติหลังโหลดฉากแรก: เตรียมฟอนต์ไทย สร้าง LangTranslator แบบ DontDestroyOnLoad
    // สมัครรับ TEXT_CHANGED_EVENT ของ TextMeshPro และติดตัวแปลให้ข้อความที่มีอยู่ทั้งหมด
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        Lang.EnsureThaiFont();
        if (instance != null) return;
        var go = new GameObject("LangTranslator");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LangTranslator>();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(instance.OnTextChanged);
        instance.appliedThai = Lang.Thai;
        instance.AttachAll();
    }

    // ถูกทำลาย: ถอนการสมัคร TEXT_CHANGED_EVENT (เฉพาะตัวหลัก) กัน callback ค้าง
    void OnDestroy()
    {
        if (instance == this) TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    // เปลี่ยนภาษา: สร้างข้อความใหม่ทุกอัน (รวมหน้าที่ซ่อนอยู่) ทันที
    public static void RefreshNow()
    {
        if (instance == null) return;
        instance.appliedThai = Lang.Thai;
        instance.AttachAll(true);
    }

    // ITextPreprocessor: TextMeshPro เรียกก่อนสร้างตัวอักษรทุกครั้งที่ข้อความเปลี่ยน
    public string PreprocessText(string text) => Lang.Thai ? Lang.Translate(text) : text;

    // ข้อความถูกวาดครั้งแรก/เปลี่ยน: ถ้ายังไม่มีตัวแปล ติดให้แล้ววาดใหม่ในเฟรมเดียวกัน
    private void OnTextChanged(Object changed)
    {
        var text = changed as TMP_Text;
        if (text == null || text.textPreprocessor != null) return;
        if (!Attach(text) || !Lang.Thai) return;
        if (Lang.Translate(text.text) != text.text) text.ForceMeshUpdate(true, true);
    }

    // ติดตัวแปลนี้เป็น textPreprocessor ของข้อความ (ข้ามข้อความในช่องพิมพ์ TMP_InputField)
    // คืน true ถ้าข้อความนี้ใช้ตัวแปลของเราอยู่แล้วหรือเพิ่งติดสำเร็จ
    private bool Attach(TMP_Text text)
    {
        if (text.textPreprocessor != null) return text.textPreprocessor == (ITextPreprocessor)this;
        if (text.GetComponentInParent<TMP_InputField>(true) != null) return false;
        text.textPreprocessor = this;
        return true;
    }

    // ไล่ติดตัวแปลให้ TMP_Text ทุกอันในฉาก (รวมที่ซ่อนอยู่) rebuild = true จะบังคับวาดใหม่ทุกอัน
    private void AttachAll(bool rebuild = false)
    {
        var texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var text in texts)
        {
            bool had = text.textPreprocessor == (ITextPreprocessor)this;
            if (!Attach(text)) continue;
            if (rebuild || (!had && Lang.Thai)) text.ForceMeshUpdate(true, true);
        }
    }

    // ทุกเฟรม: ถ้าภาษาเปลี่ยนให้วาดใหม่ทั้งหมด และทุก 2 วินาทีสแกนหาข้อความใหม่ที่ยังไม่ได้ติดตัวแปล
    void Update()
    {
        if (appliedThai != Lang.Thai) { RefreshNow(); return; }
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 2f;
        AttachAll();
    }
}
