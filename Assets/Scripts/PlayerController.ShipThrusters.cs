// PlayerController.ShipThrusters.cs — ตำแหน่งไอพ่นของยานใหม่ (ship4-6) ที่ใช้รูปของตัวเอง (partial class ของ PlayerController)
// ยานใหม่ใช้ Prefab ของยานเดิม ตำแหน่งหัวฉีดเดิมจึงไม่ตรงกับรูปใหม่ — ตารางนี้บอกจุดหัวฉีดบนรูปใหม่แทน
// จุดยึด (anchor) = สัดส่วนบนรูปยาน: x 0 = ซ้าย 1 = ขวา / y 0 = ล่าง (ท้ายยาน) 1 = บน (หัวยาน) — รูปต้องหันหัวขึ้นด้านบน
// แก้ตำแหน่งได้ที่ OwnArtThrusters ด้านล่าง (ยังไม่มีรูปของตัวเอง = ใช้ไอพ่นของ Prefab ตามเดิม)
using UnityEngine;

// ส่วนจุดหัวฉีดไอพ่นของยานใหม่ของ PlayerController (partial)
public partial class PlayerController
{
    // โปรไฟล์ไอพ่น 1 ลำ: สไตล์เปลวไฟ (0 แดง, 1 ขาว/ฟ้า, 2 เขียว), จุดหัวฉีด และตัวคูณความกว้างเปลวไฟ
    private class ThrusterProfile
    {
        public int style; public Vector2[] anchors; public float width;
        // constructor: สไตล์, ตัวคูณความกว้าง, จุดหัวฉีดทั้งหมด
        public ThrusterProfile(int style, float width, params Vector2[] anchors) { this.style = style; this.width = width; this.anchors = anchors; }
    }

    // index ยาน → โปรไฟล์ (ยาน 3 Viper Lance = ship4, 4 Aurora Warden = ship5, 5 Hydra Scatter = ship6)
    private static readonly ThrusterProfile[] OwnArtThrusters =
    {
        null, null, null,
        // ship4 เขียว: ท้ายฝักซ้าย/ขวา + แกนกลาง
        new ThrusterProfile(2, .42f, new Vector2(.405f, .05f), new Vector2(.5f, .075f), new Vector2(.595f, .05f)),
        // ship5 ขาว/ฟ้า: หัวฉีดกลมสีฟ้า 2 อันใต้ลำ
        new ThrusterProfile(1, 1.05f, new Vector2(.385f, .15f), new Vector2(.615f, .15f)),
        // ship6 แดง: หัวฉีดกระบอกดำ 2 อันกลางลำ
        new ThrusterProfile(0, 1.15f, new Vector2(.343f, .3f), new Vector2(.655f, .3f)),
    };

    private float exhaustWidthScale = 1f;

    // ยานนี้ใช้รูปของตัวเองและมีโปรไฟล์ไอพ่นหรือไม่ (เรียกตอนสร้างไอพ่นครั้งแรกใน LateUpdateVisuals)
    private ThrusterProfile OwnThrusterProfile()
    {
        int ship = ShipIndex;
        if (ship < 0 || ship >= OwnArtThrusters.Length || OwnArtThrusters[ship] == null) return null;
        return BattleLoadoutCatalog.HasOwnArt(ship) ? OwnArtThrusters[ship] : null;
    }
}
