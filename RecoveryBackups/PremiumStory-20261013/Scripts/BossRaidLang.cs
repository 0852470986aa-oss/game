// คำแปลของหน้าต่าง/การ์ดโหมดบอส ไม่แก้พจนานุกรมฟีเจอร์เดิม
using UnityEngine;

public static class BossRaidLang
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        Lang.Table["BOSS CO-OP"] = "ร่วมมือปราบบอส";
        Lang.Table["BOSS DEFEATED!"] = "ปราบบอสสำเร็จ!";
        Lang.Table["RAID FAILED"] = "ภารกิจล้มเหลว";
        Lang.Table["BOSS ENRAGED!"] = "บอสคลั่งแล้ว!";
        Lang.Table["You + 3 allied bots"] = "คุณและบอทฝ่ายเดียวกัน 3 ลำ";
        Lang.Table["CO-OP / 2-4 PILOTS"] = "ร่วมมือ / ผู้เล่น 2–4 คน";
        Lang.Table["CO-OP / 3 ALLIED BOTS / 3 LIVES / 8:00"] = "ร่วมมือ / บอทฝ่ายเรา 3 ลำ / 3 ชีวิต / 8 นาที";
        Lang.Table["Boss Co-op supports 2-4 pilots."] = "โหมดบอสรองรับผู้เล่นออนไลน์ 2–4 คน";
        Lang.Table["DESTROY DREADNOUGHT / DODGE TELEGRAPHED ATTACKS"] = "ปราบเดรดนอต / หลบแนวโจมตีที่เตือนล่วงหน้า";
    }
}
