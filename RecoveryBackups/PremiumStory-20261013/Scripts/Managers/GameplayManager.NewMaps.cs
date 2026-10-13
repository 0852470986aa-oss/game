// GameplayManager.NewMaps.cs — แม็พใหม่จากรูปชุดใหม่ (แม็พ 3 สถานีอวกาศในดงอุกกาบาต, แม็พ 4 เนบิวลาลาวา)
// ต่างจากแม็พ 0-2 ที่จัดวางไว้ใน Scene: แม็พนี้สร้างทั้งหมดด้วยโค้ดตอนเริ่มแมตช์ (ทุกเครื่องได้ตำแหน่งเดียวกัน)
// - พื้นหลัง: Resources/Images/Map_AsteroidStation.png, Map_MoltenNebula.png
// - สิ่งกีดขวาง: Resources/Images/Maps/Station/*.png และ Maps/Lava/*.png ตามตาราง StationLayout / LavaLayout ด้านล่าง
//   แก้ตำแหน่งได้ที่ตาราง (x, y หน่วยโลก, size = ความยาวด้านยาวสุด, rot = องศา, ตัวชน circle/box/poly)
// - แม็พลาวา: หินลาวาแตะแล้วเสียเลือด (MoltenContactSurface) + อุกกาบาตลาวาร่วงจากฟ้า (MapHazardManager)
// ปิด FeatureFlags.NewMaps = เลือกได้แค่ 3 แม็พเดิม
using UnityEngine;

// ส่วนแม็พใหม่ของ GameplayManager: สร้างแม็พ 3 (สถานีอวกาศ) และแม็พ 4 (ลาวา) ด้วยโค้ดตอนเริ่มแมตช์
public partial class GameplayManager
{
    public const int StationMapIndex = 3; // เลขแม็พสถานีอวกาศในดงอุกกาบาต (แม็พ 3)
    public const int LavaMapIndex = 4; // เลขแม็พเนบิวลาลาวา (แม็พ 4)
    // จำนวนแม็พที่เลือกได้ในล็อบบี้
    public static int MapCount => FeatureFlags.NewMaps ? 5 : 3;
    // true = แม็พที่สร้างด้วยโค้ดตอนรันเกม (แม็พ 3 และ 4) ไม่ได้จัดวางไว้ใน Scene
    public static bool IsRuntimeMap(int map) => map == StationMapIndex || map == LavaMapIndex;
    // พื้นหลังของแม็พ (index ตรงกับเลขแม็พ)
    public static readonly string[] MapBackgroundImages =
        { "Images/Map_ThunderJellyfish", "Images/Map_ObeliskPlains", "Images/Map_AncientMech", "Images/Map_AsteroidStation", "Images/Map_MoltenNebula" };

    // ชนิดตัวชนของชิ้นส่วนแม็พ: วงกลม / สี่เหลี่ยม / รูปหลายเหลี่ยมตามรูป Sprite
    public enum MapCollider { Circle, Box, Poly }

    // ข้อมูลชิ้นส่วนแม็พ 1 ชิ้น: รูป, ตำแหน่ง, ขนาด, มุมหมุน, ชนิดตัวชน และค่า a/b
    // (a/b = สัดส่วนรัศมีของ Circle หรือกว้าง/สูงของ Box เทียบกับขนาดรูป)
    public struct MapPiece
    {
        public string sprite; public Vector2 position; public float size, rotation; public MapCollider collider; public float a, b; // รูป, ตำแหน่ง, ขนาด, มุมหมุน, ชนิดตัวชน และสัดส่วนตัวชน a/b
        // Constructor สำหรับเขียนตาราง Layout สั้นๆ: รับ x, y แยกแล้วแปลงเป็น Vector2
        public MapPiece(string sprite, float x, float y, float size, float rotation, MapCollider collider, float a, float b)
        {
            this.sprite = sprite; position = new Vector2(x, y); this.size = size; this.rotation = rotation;
            this.collider = collider; this.a = a; this.b = b;
        }
    }

    // ===== ตารางวางสิ่งกีดขวาง (สมมาตร 180 องศา ทั้งสองฝั่งได้ที่กำบังเท่ากัน / เว้นจุดเกิด 0,±16 ให้โล่ง) =====
    private static readonly MapPiece[] StationLayout =
    {
        new MapPiece("Station/st_ring", 0f, 0f, 13f, 0f, MapCollider.Poly, 0f, 0f),
        new MapPiece("Station/st_platform_hex", -28f, 27f, 9f, 0f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Station/st_platform_hex", 28f, -27f, 9f, 180f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Station/st_platform_tri", 28f, 27f, 9f, 0f, MapCollider.Circle, 0.36f, 0f),
        new MapPiece("Station/st_platform_tri", -28f, -27f, 9f, 180f, MapCollider.Circle, 0.36f, 0f),
        new MapPiece("Station/st_gate_long", 0f, 28.5f, 11f, 0f, MapCollider.Box, 0.95f, 0.3f),
        new MapPiece("Station/st_gate_long", 0f, -28.5f, 11f, 180f, MapCollider.Box, 0.95f, 0.3f),
        new MapPiece("Station/st_gate_short", -31f, 0f, 8f, 90f, MapCollider.Box, 0.95f, 0.42f),
        new MapPiece("Station/st_gate_short", 31f, 0f, 8f, 270f, MapCollider.Box, 0.95f, 0.42f),
        new MapPiece("Station/st_cluster_a", -14f, 12f, 9f, 0f, MapCollider.Circle, 0.33f, 0f),
        new MapPiece("Station/st_cluster_a", 14f, -12f, 9f, 180f, MapCollider.Circle, 0.33f, 0f),
        new MapPiece("Station/st_cluster_c", 14f, 12f, 9f, 0f, MapCollider.Circle, 0.33f, 0f),
        new MapPiece("Station/st_cluster_c", -14f, -12f, 9f, 180f, MapCollider.Circle, 0.33f, 0f),
        new MapPiece("Station/st_cluster_d", -21f, -1f, 7f, 0f, MapCollider.Circle, 0.32f, 0f),
        new MapPiece("Station/st_cluster_d", 21f, 1f, 7f, 180f, MapCollider.Circle, 0.32f, 0f),
        new MapPiece("Station/st_arc_a", -12f, 25f, 6f, 0f, MapCollider.Poly, 0f, 0f),
        new MapPiece("Station/st_arc_a", 12f, -25f, 6f, 180f, MapCollider.Poly, 0f, 0f),
        new MapPiece("Station/st_rock_c", -33f, 16f, 5f, 0f, MapCollider.Circle, 0.3f, 0f),
        new MapPiece("Station/st_rock_c", 33f, -16f, 5f, 180f, MapCollider.Circle, 0.3f, 0f),
        new MapPiece("Station/st_rock_a", 33f, 14f, 5f, 0f, MapCollider.Circle, 0.3f, 0f),
        new MapPiece("Station/st_rock_a", -33f, -14f, 5f, 180f, MapCollider.Circle, 0.3f, 0f),
        new MapPiece("Station/st_core", -6f, -24f, 4.5f, 0f, MapCollider.Circle, 0.42f, 0f),
        new MapPiece("Station/st_core", 6f, 24f, 4.5f, 180f, MapCollider.Circle, 0.42f, 0f),
    };
    // ตารางวางหินลาวาของแม็พ 4 (สมมาตร 180 องศา)
    private static readonly MapPiece[] LavaLayout =
    {
        new MapPiece("Lava/lv_big_b", 0f, 0f, 9f, 0f, MapCollider.Circle, 0.36f, 0f),
        new MapPiece("Lava/lv_mid_a", -15f, 9f, 6.5f, 0f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_mid_a", 15f, -9f, 6.5f, 180f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_mid_d", 15f, 9f, 6.5f, 0f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_mid_d", -15f, -9f, 6.5f, 180f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_spike_a", -27f, 25f, 8f, 0f, MapCollider.Circle, 0.34f, 0f),
        new MapPiece("Lava/lv_spike_a", 27f, -25f, 8f, 180f, MapCollider.Circle, 0.34f, 0f),
        new MapPiece("Lava/lv_big_c", 27f, 26f, 9f, 0f, MapCollider.Circle, 0.36f, 0f),
        new MapPiece("Lava/lv_big_c", -27f, -26f, 9f, 180f, MapCollider.Circle, 0.36f, 0f),
        new MapPiece("Lava/lv_mid_b", -30f, 4f, 6f, 0f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_mid_b", 30f, -4f, 6f, 180f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_small_a", -9f, 27f, 4f, 0f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_small_a", 9f, -27f, 4f, 180f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_small_b", 10f, 25f, 4f, 0f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_small_b", -10f, -25f, 4f, 180f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_spike_b", -4f, -31f, 6f, 0f, MapCollider.Circle, 0.34f, 0f),
        new MapPiece("Lava/lv_spike_b", 4f, 31f, 6f, 180f, MapCollider.Circle, 0.34f, 0f),
        new MapPiece("Lava/lv_small_c", -33f, -20f, 4f, 0f, MapCollider.Circle, 0.38f, 0f),
        new MapPiece("Lava/lv_small_c", 33f, 20f, 4f, 180f, MapCollider.Circle, 0.38f, 0f),
    };

    // สร้างพื้นหลังของแม็พใหม่ (เรียกจาก ApplySelectedMapLayout)
    private void BuildRuntimeMapBackground(int map)
    {
        if (!IsRuntimeMap(map)) return;
        var sprite = Resources.Load<Sprite>(MapBackgroundImages[map]);
        if (sprite == null) return;
        // พื้นหลังเดิมใน Scene (ถ้ามีและไม่ได้อยู่ใต้ MapX_Layout) ซ่อนไว้
        if (backgroundSprite != null) backgroundSprite.enabled = false;
        var root = new GameObject("Map" + map + "_Runtime");
        var background = new GameObject("Background");
        background.transform.SetParent(root.transform, false);
        var renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -6;
        FitBackgroundToArena(renderer);
        backgroundSprite = renderer;
    }

    // สร้างสิ่งกีดขวางของแม็พใหม่ (เรียกจาก GenerateMapObstacles หลังสร้างกำแพงขอบ)
    private void BuildRuntimeMapObstacles(int map, Transform container)
    {
        MapPiece[] layout = map == StationMapIndex ? StationLayout : map == LavaMapIndex ? LavaLayout : null;
        if (layout == null) return;
        const string folder = "Images/Maps/"; // โฟลเดอร์รูปชิ้นส่วนแม็พใน Resources
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int id = 1;
        foreach (var piece in layout)
        {
            var sprite = Resources.Load<Sprite>(folder + piece.sprite);
            var item = new GameObject("MapPiece_" + id++ + "_" + piece.sprite.Replace('/', '_'));
            item.transform.SetParent(container, false);
            item.transform.position = piece.position;
            item.transform.rotation = Quaternion.Euler(0, 0, piece.rotation);
            if (obstacleLayer >= 0) item.layer = obstacleLayer;
            float scale = 1f;
            if (sprite != null)
            {
                var renderer = item.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 1;
                scale = piece.size / Mathf.Max(.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
                item.transform.localScale = new Vector3(scale, scale, 1f);
            }
            // ตัวชน (ขนาดเป็นหน่วยโลก แปลงเป็นหน่วยของวัตถุที่ถูกย่อ/ขยาย)
            if (piece.collider == MapCollider.Circle || sprite == null)
                item.AddComponent<CircleCollider2D>().radius = Mathf.Max(.5f, piece.size * (piece.a > 0 ? piece.a : .35f)) / scale;
            else if (piece.collider == MapCollider.Box)
                item.AddComponent<BoxCollider2D>().size = new Vector2(piece.size * piece.a, piece.size * piece.b) / scale;
            else
                item.AddComponent<PolygonCollider2D>(); // สร้างรูปทรงตามภาพอัตโนมัติ
            if (map == LavaMapIndex) item.AddComponent<MoltenContactSurface>();
        }
    }
}
