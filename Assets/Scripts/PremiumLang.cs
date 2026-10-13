// PremiumLang.cs — คำแปลภาษาไทยของระบบพรีเมียม (ร้านเติมเงิน / Battle Pass / กาชาสกิน)
// เพิ่มเข้าตาราง Lang.Table ตอนเปิดเกม (ไม่ต้องแก้ Lang.cs) — คำที่มีในตารางอยู่แล้วจะไม่ถูกเขียนทับ
// ข้อความที่มีตัวเลข: Lang แปลทีละช่วงที่คั่นด้วยช่องว่าง 2 ช่อง หรือ " / " และรูปแบบ "คำ ตัวเลข" / "ตัวเลข คำ" / "หัวข้อ: ค่า"
using System.Collections.Generic;
using UnityEngine;

public static class PremiumLang
{
    private static readonly Dictionary<string, string> Thai = new Dictionary<string, string>
    {
        { "DEMO QR ONLY - DO NOT PAY WITH A BANKING APP", "QR จำลองเท่านั้น ห้ามใช้แอปธนาคารชำระเงินจริง" },
        { "CONTINUE TO DEMO APPROVAL", "ต่อไป: ยืนยันการจ่ายจำลอง" },
        { "DEMO PAYMENT AUTHORIZATION", "ยืนยันการชำระเงินจำลอง" },
        { "TEST ACCOUNT", "บัญชีทดสอบ" },
        { "Choose a test result. No bank, wallet or card will be contacted.", "เลือกผลทดสอบ ไม่มีการติดต่อธนาคาร วอลเล็ต หรือบัตรจริง" },
        { "SIMULATE DECLINED", "จำลองจ่ายไม่สำเร็จ" },
        { "SIMULATE APPROVED", "จำลองจ่ายสำเร็จ" },
        { "Demo declined. No crystals granted and no money charged.", "จำลองจ่ายไม่สำเร็จ ไม่เพิ่มคริสตัลและไม่หักเงินจริง" },
        { "Demo payment cancelled. No money charged.", "ยกเลิกการจ่ายจำลองแล้ว ไม่มีการหักเงินจริง" },
        { "DEMO RECEIPT - NO REAL MONEY CHARGED", "ใบเสร็จจำลอง ไม่มีการหักเงินจริง" },
        { "DEMO WALLET", "วอลเล็ตจำลอง" }, { "DEMO CARD", "บัตรจำลอง" },
        { "APPROVAL REQUIRED", "รอการยืนยัน" },
        { "No wallet app is contacted.", "ไม่ติดต่อแอปวอลเล็ตจริง" },
        { "No card details are collected.", "ไม่เก็บข้อมูลบัตรจริง" },
        { "Demo checkout requires a simulated payment provider.", "หน้าทดสอบนี้ใช้ได้กับระบบจ่ายเงินจำลองเท่านั้น" },
        { "Unique ship artwork.", "สกินมีภาพยานเฉพาะตัว" },
        { "Unique ship artwork", "ภาพยานเฉพาะตัว" },
        { "animated previews", "ภาพพรีวิวเคลื่อนไหว" },
        { "no stat changes", "ไม่เปลี่ยนค่าพลัง" },
        { "ORIGINAL SHIP", "ใช้ยานเดิม" },
        { "Original ship restored.", "กลับเป็นภาพยานเดิมแล้ว" },
        { "RANDOM MAP", "สุ่มแผนที่" }, { "STORY MAP", "แผนที่เนื้อเรื่อง" },
        { "Full teams required", "รอผู้เล่นครบทีม" }, { "5 kills per pilot", "5 คิลต่อคน" },
        { "5 min", "5 นาที" }, { "upgrades OFF", "ไม่ใช้ค่าตีบวก" }, { "no bots", "ไม่มีบอท" },
        // ปุ่มหน้าหลัก / แท็บ / หัวข้อ
        { "PASS", "พาส" }, { "GACHA", "กาชา" }, { "SKINS", "สกิน" },
        { "PREMIUM SHOP", "ร้านค้าพรีเมียม" }, { "BATTLE PASS", "แบทเทิลพาส" }, { "STAR FORGE", "สตาร์ฟอร์จ" },
        { "MY SKINS", "สกินของฉัน" }, { "HISTORY", "ประวัติ" }, { "TOP UP", "เติมเงิน" },
        { "CRYSTALS", "คริสตัล" }, { "TICKETS", "ตั๋ว" }, { "TICKET", "ตั๋ว" }, { "SHARDS", "ชิ้นส่วน" },
        { "STAR SHARDS", "ชิ้นส่วนดาว" }, { "GACHA TICKETS", "ตั๋วกาชา" }, { "GACHA TICKET", "ตั๋วกาชา" },
        // เติมเงิน / ชำระเงิน
        { "DEMO PAYMENT", "ระบบชำระเงินจำลอง" }, { "NO REAL MONEY IS CHARGED", "ไม่มีการตัดเงินจริง" },
        { "STARTER PACK", "แพ็กเริ่มต้น" }, { "ONE TIME ONLY", "ซื้อได้ครั้งเดียว" }, { "STARTER ACE SKIN", "สกิน STARTER ACE" },
        { "x2 FIRST", "x2 ครั้งแรก" }, { "BONUS", "โบนัส" },
        { "CHECKOUT", "ชำระเงิน" }, { "(INCLUDES x2 FIRST TOP-UP BONUS)", "(รวมโบนัสเติมครั้งแรก x2)" },
        { "PROMPTPAY", "พร้อมเพย์" }, { "TRUEMONEY", "ทรูมันนี่" }, { "CARD", "บัตร" },
        { "SCAN WITH ANY BANKING APP", "สแกนด้วยแอปธนาคารใดก็ได้" }, { "(DEMO QR)", "(QR จำลอง)" },
        { "TRUEMONEY WALLET", "ทรูมันนี่ วอลเล็ท" }, { "A confirmation request would open in the wallet app.", "ระบบจริงจะเด้งให้กดยืนยันในแอปวอลเล็ท" },
        { "(DEMO — nothing is sent)", "(จำลอง — ไม่มีการส่งข้อมูลใด ๆ)" },
        { "CARD PAYMENT", "ชำระด้วยบัตร" }, { "A secure payment page would open here.", "ระบบจริงจะเปิดหน้าชำระเงินที่ปลอดภัยตรงนี้" },
        { "(DEMO — no card details are collected)", "(จำลอง — ไม่มีการเก็บข้อมูลบัตร)" },
        { "EXPIRES IN", "หมดเวลาใน" }, { "SIMULATE PAYMENT", "จำลองการชำระเงิน" },
        { "PROCESSING PAYMENT...", "กำลังดำเนินการชำระเงิน..." }, { "PAYMENT SUCCESSFUL", "ชำระเงินสำเร็จ" },
        { "PAYMENT FAILED", "ชำระเงินไม่สำเร็จ" }, { "DONE", "เสร็จสิ้น" }, { "DEMO", "จำลอง" },
        { "Payment session expired.", "หมดเวลาชำระเงิน" }, { "Payment failed.", "ชำระเงินไม่สำเร็จ" },
        { "Starter pack already purchased.", "ซื้อแพ็กเริ่มต้นไปแล้ว" },
        // ประวัติ
        { "TOP-UP HISTORY", "ประวัติการเติมเงิน" }, { "TOTAL THB", "ยอดรวม THB" },
        { "ALL PAYMENTS ARE DEMO (NO REAL MONEY)", "ทุกรายการเป็นการชำระเงินจำลอง (ไม่มีเงินจริง)" },
        { "No top-ups yet.", "ยังไม่มีการเติมเงิน" }, { "RECENT PULLS", "การสุ่มล่าสุด" }, { "TOTAL", "ทั้งหมด" },
        { "No pulls yet.", "ยังไม่เคยสุ่ม" }, { "NEW", "ใหม่" },
        // Battle Pass
        { "ENDS IN", "จบใน" }, { "TIER", "ขั้น" }, { "TIERS", "ขั้น" },
        { "UNLOCK PREMIUM", "ปลดล็อกพรีเมียม" }, { "PREMIUM ACTIVE", "พรีเมียมใช้งานอยู่" },
        { "FREE", "ฟรี" }, { "PREMIUM", "พรีเมียม" }, { "SKIN", "สกิน" }, { "CLAIM ALL", "รับทั้งหมด" },
        { "Earn pass XP from every match and mission", "รับ XP พาสจากทุกแมตช์และภารกิจ" },
        { "Premium skins: SEASON ACE (tier 15), STAR SOVEREIGN (tier 30)", "สกินพรีเมียม: SEASON ACE (ขั้น 15), STAR SOVEREIGN (ขั้น 30)" },
        { "NEW SKIN", "สกินใหม่" }, { "REWARDS CLAIMED", "รับรางวัลแล้ว" }, { "Please wait...", "กรุณารอสักครู่..." },
        { "PREMIUM PASS UNLOCKED!", "ปลดล็อกพาสพรีเมียมแล้ว!" }, { "TIER UP!", "เลื่อนขั้น!" },
        { "Pass is already at max tier.", "พาสถึงขั้นสูงสุดแล้ว" }, { "Premium pass is already active.", "พาสพรีเมียมเปิดใช้งานอยู่แล้ว" },
        { "NOT ENOUGH CRYSTALS", "คริสตัลไม่พอ" }, { "NEED", "ต้องใช้" }, { "PASS XP", "XP พาส" },
        // กาชา
        { "SKIN BANNER", "ตู้สกิน" }, { "RATE UP", "เรทอัป" }, { "LEGENDARY", "ตำนาน" },
        { "50% OF LEGENDARY PULLS", "50% ของการได้ระดับตำนาน" }, { "CHANGES IN", "เปลี่ยนใน" },
        { "LEGENDARY GUARANTEED IN", "การันตีระดับตำนานภายใน" }, { "EPIC OR BETTER IN", "การันตีเอพิคขึ้นไปภายใน" }, { "PULLS", "ครั้ง" },
        { "PULL", "สุ่ม" }, { "ODDS & RULES", "โอกาสและกติกา" }, { "EXCHANGE IN MY SKINS", "แลกได้ที่สกินของฉัน" },
        { "Skins change color and glow only.", "สกินเปลี่ยนแค่สีและแสงออร่า" }, { "They never change ship stats.", "ไม่มีผลต่อค่าพลังของยาน" },
        { "SKINS IN THIS BANNER", "สกินในตู้นี้" }, { "RESULT", "ผลการสุ่ม" }, { "NEW!", "ใหม่!" }, { "DUPLICATE", "ซ้ำ" },
        { "Not enough tickets.", "ตั๋วไม่พอ" }, { "Top up in the TOP UP tab.", "เติมคริสตัลได้ที่แท็บเติมเงิน" },
        // สกินของฉัน
        { "USE BASIC PAINT", "ใช้สีพื้นฐาน" }, { "Basic paint restored.", "กลับเป็นสีพื้นฐานแล้ว" },
        { "Skins change color and glow only", "สกินเปลี่ยนแค่สีและแสงออร่า" }, { "they never change ship stats", "ไม่มีผลต่อค่าพลังของยาน" },
        { "basic paints are in the HANGAR", "สีพื้นฐานอยู่ในโรงเก็บยาน" },
        { "UNLOCKED", "ปลดล็อกแล้ว" }, { "NOT ENOUGH STAR SHARDS", "ชิ้นส่วนดาวไม่พอ" },
        { "You already own this skin.", "มีสกินนี้อยู่แล้ว" }, { "This skin cannot be exchanged.", "สกินนี้แลกด้วยชิ้นส่วนไม่ได้" },
    };

    // ใส่คำแปลก่อนโหลดฉากแรก
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        foreach (var pair in Thai) if (!Lang.Table.ContainsKey(pair.Key)) Lang.Table[pair.Key] = pair.Value;
        // กติกากาชา (สร้างจากค่าคงที่ชุดเดียวกับภาษาอังกฤษ)
        var english = SkinGacha.RuleLines;
        var thai = SkinGacha.RuleLinesThai;
        for (int i = 1; i < english.Length && i < thai.Length; i++)
            if (!Lang.Table.ContainsKey(english[i])) Lang.Table[english[i]] = thai[i];
    }
}
