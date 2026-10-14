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
        public int style; public Vector2[] anchors; public float width; // สไตล์เปลวไฟ, ตำแหน่งหัวฉีด (สัดส่วนบนรูป) และตัวคูณความกว้างไฟ
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

    private float exhaustWidthScale = 1f; // ตัวคูณความกว้างไฟไอพ่นของยานนี้ (จากโปรไฟล์)
    private bool customExhaust;
    private Color customExhaustTint = Color.white;

    // Pixel coordinates measured at the nozzle mouths, not the tips of painted flames.
    // Convert through the actual cropped sprite rect so transparent atlas margins do not shift the flames.
    private ThrusterProfile PixelThrusters(int style, float width, float imageWidth, float imageHeight, params Vector2[] pixels)
    {
        Sprite art = spriteRenderer.sprite;
        customExhaustTint = style == 0 ? new Color(1,.35f,.08f) : style == 2 ? new Color(.4f,1,.2f) : new Color(.35f,.85f,1);
        if (art.name.StartsWith("Skin_"))
        {
            var skin = SkinCatalog.Find(art.name.Substring(5));
            if (skin != null) customExhaustTint = skin.aura;
        }
        else if (art.name == "StoryFleet_2" || art.name == "StoryFleet_5") customExhaustTint = new Color(.8f,.25f,1);
        customExhaustTint.a = 1;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Vector2((pixels[i].x * art.texture.width / imageWidth - art.rect.x) / art.rect.width,
                ((imageHeight - pixels[i].y) * art.texture.height / imageHeight - art.rect.y) / art.rect.height);
        // Neutral flame takes the ship's own color; the legacy red sheet is actually magenta.
        return new ThrusterProfile(style == 0 ? 1 : style, width, pixels);
    }

    // ยานนี้ใช้รูปของตัวเองและมีโปรไฟล์ไอพ่นหรือไม่ (เรียกตอนสร้างไอพ่นครั้งแรกใน LateUpdateVisuals)
    private ThrusterProfile OwnThrusterProfile()
    {
        // สกินเปลี่ยนภาพลำยาน จึงใช้จุดไอพ่นของภาพสกินแทน Prefab ฐาน
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            if (spriteRenderer.sprite.name == "DreadnoughtRaidVisual")
                return PixelThrusters(1, .65f, 1254, 1254, new Vector2(490,1127), new Vector2(627,1174), new Vector2(764,1127));
            if (spriteRenderer.sprite.name.StartsWith("StoryFleet_") && int.TryParse(spriteRenderer.sprite.name.Substring(11), out int fleet))
            {
                switch (fleet)
                {
                    case 0: return PixelThrusters(0,.55f,1536,1024,new Vector2(263,340));
                    case 1: return PixelThrusters(2,.55f,1536,1024,new Vector2(767,345));
                    case 2: return PixelThrusters(1,.55f,1536,1024,new Vector2(1189,340),new Vector2(1353,340));
                    case 3: return PixelThrusters(0,.65f,1536,1024,new Vector2(189,885),new Vector2(340,885));
                    case 4: return PixelThrusters(0,.65f,1536,1024,new Vector2(711,824),new Vector2(768,879),new Vector2(825,824));
                    case 5: return PixelThrusters(1,.55f,1536,1024,new Vector2(1271,874));
                }
            }
            if (spriteRenderer.sprite.name.StartsWith("Skin_"))
            {
                string id = spriteRenderer.sprite.name.Substring(5);
                int style = id == "TOXIC" || id == "JADE" ? 2 : 1;
                switch (id)
                {
                    case "COBALT": return PixelThrusters(style,.65f,1620,971,new Vector2(159,259),new Vector2(214,259));
                    case "MAGMA": return PixelThrusters(0,.65f,1620,971,new Vector2(465,254),new Vector2(526,254));
                    case "TOXIC": return PixelThrusters(style,.65f,1620,971,new Vector2(809,247));
                    case "ROSE": return PixelThrusters(style,.65f,1620,971,new Vector2(1096,251),new Vector2(1163,251));
                    case "ARCTIC": return PixelThrusters(style,.65f,1620,971,new Vector2(1410,259),new Vector2(1469,259));
                    case "NEBULA": return PixelThrusters(style,.65f,1620,971,new Vector2(145,539),new Vector2(224,539));
                    case "SOLAR": return PixelThrusters(style,.65f,1620,971,new Vector2(463,526),new Vector2(495,547),new Vector2(528,526));
                    case "ABYSS": return PixelThrusters(style,.65f,1620,971,new Vector2(779,544),new Vector2(843,544));
                    case "JADE": return PixelThrusters(style,.65f,1620,971,new Vector2(1101,557),new Vector2(1131,548),new Vector2(1163,557));
                    case "PRISM": return PixelThrusters(style,.65f,1620,971,new Vector2(1441,537));
                    case "VOID": return PixelThrusters(0,.65f,1620,971,new Vector2(145,844),new Vector2(222,844));
                    case "GOLDEN": return PixelThrusters(style,.65f,1620,971,new Vector2(453,834),new Vector2(535,834));
                    case "STARTER_ACE": return PixelThrusters(style,.65f,1620,971,new Vector2(769,830),new Vector2(849,830));
                    case "SEASON_ACE": return PixelThrusters(0,.65f,1620,971,new Vector2(1094,828),new Vector2(1165,828));
                    case "SOVEREIGN": return PixelThrusters(style,.65f,1620,971,new Vector2(1407,849),new Vector2(1471,849));
                }
            }
        }
        int ship = ShipIndex;
        if (ship < 0 || ship >= OwnArtThrusters.Length || OwnArtThrusters[ship] == null) return null;
        customExhaustTint = OwnArtThrusters[ship].style == 2 ? new Color(.68f,1,.42f) : Color.white;
        return BattleLoadoutCatalog.HasOwnArt(ship) ? OwnArtThrusters[ship] : null;
    }
}
