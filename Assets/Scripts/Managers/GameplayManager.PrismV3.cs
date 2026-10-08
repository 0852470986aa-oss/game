// GameplayManager.PrismV3.cs — แม็พปริซึมรุ่น 3 (FeatureFlags.PrismMapV3) จัดวางใหม่ให้เป็นมุมบนแบบเดียวกับแม็พ 3/4
// - ภาพชิ้นส่วน: Resources/Images/Maps/Prism/*.png (สร้างใหม่ทั้งหมด มุมมองจากด้านบน)
//   pr_core = เสาโอเบลิสก์กลางบนแท่นหินแปดเหลี่ยม, pr_cluster_a/b/c = กองคริสตัลบนหิน, pr_ridge = แนวสันคริสตัล,
//   pr_prism = ปริซึมสามเหลี่ยม (สะท้อนกระสุน), pr_warp = แท่นวาร์ป, pr_shard_a/b/c = เศษคริสตัลลอย (ใช้ใน MapAmbience)
// - พื้นหลัง Images/Map_PrismNebula.png ใส่โดย PrismArenaVisuals (แทนภาพวิวมุมข้าง Map_ObeliskPlains)
// - แก้ตำแหน่งได้ที่ตาราง PrismV3Layout (x, y หน่วยโลก, size = ด้านยาวสุด, rot = องศา, ตัวชน, ชนิด)
//   สมมาตร 180 องศา ทั้งสองฝั่งได้ที่กำบังเท่ากัน / เว้นจุดเกิด 1v1 (0, ±16) ให้โล่ง / แท่นวาร์ป 4 มุมเดิม (±40, ±30)
// ปิด Flag = กลับไปใช้แม็พปริซึมรุ่น 2 (DecoratePrism ใน GameplayManager.Maps.cs) ทุกอย่างเหมือนเดิม
using UnityEngine;

// ส่วนแม็พปริซึมรุ่น 3 ของ GameplayManager (partial class เดียวกัน)
public partial class GameplayManager
{
    // ชื่อกลุ่มเลย์เอาต์ที่ใช้อยู่ (รุ่น 3 เมื่อเปิด Flag) ใช้หาว่าสร้างแล้วหรือยัง
    public static string PrismLayoutName => FeatureFlags.PrismMapV3 ? PrismV3LayoutName : PrismV2LayoutName;
    private const string PrismV2LayoutName = "PrismPlayableLayout_v2";
    private const string PrismV3LayoutName = "PrismPlayableLayout_v3";
    // โฟลเดอร์ภาพชิ้นส่วนรุ่น 3 ใน Resources
    public const string PrismArtFolder = "Images/Maps/Prism/";
    // พื้นหลังรุ่น 3 (เนบิวลามุมบน)
    public const string PrismV3Background = "Images/Map_PrismNebula";

    // ชนิดชิ้นส่วน: Core = เสากลาง, Cover = ที่กำบังธรรมดา, Reflector = ปริซึมสะท้อนกระสุน (ติด PrismReflector)
    public enum PrismPieceKind { Core, Cover, Reflector }

    // ข้อมูลชิ้นส่วน 1 ชิ้นของแม็พปริซึมรุ่น 3 (a/b ใช้แบบเดียวกับ MapPiece: สัดส่วนรัศมีวงกลม หรือกว้าง/สูงของกล่อง)
    public struct PrismPiece
    {
        public string sprite; public Vector2 position; public float size, rotation, a, b; public MapCollider collider; public PrismPieceKind kind; // รูป, ตำแหน่ง, ขนาด, มุมหมุน, สัดส่วนชนกัน a/b, รูปทรงชนกัน และชนิดชิ้นส่วน
        // Constructor สำหรับเขียนตารางสั้น ๆ
        public PrismPiece(string sprite, float x, float y, float size, float rotation, MapCollider collider, float a, float b, PrismPieceKind kind)
        {
            this.sprite = sprite; position = new Vector2(x, y); this.size = size; this.rotation = rotation;
            this.collider = collider; this.a = a; this.b = b; this.kind = kind;
        }
    }

    // ===== ตารางวางชิ้นส่วน (เป็นคู่ ๆ: ชิ้นที่ 2 ของคู่ = ตำแหน่งกลับด้าน + หมุนเพิ่ม 180 องศา) =====
    private static readonly PrismPiece[] PrismV3Layout =
    {
        new PrismPiece("pr_core", 0f, 0f, 12f, 0f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Core),
        new PrismPiece("pr_cluster_a", -18f, 13f, 9.5f, 0f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_a", 18f, -13f, 9.5f, 180f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_b", 18f, 13f, 9.5f, 0f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_b", -18f, -13f, 9.5f, 180f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_c", -32f, 22f, 7.5f, 0f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_c", 32f, -22f, 7.5f, 180f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_c", 32f, 22f, 7.5f, 90f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_c", -32f, -22f, 7.5f, 270f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_c", -9f, 28f, 6.5f, 200f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_cluster_c", 9f, -28f, 6.5f, 20f, MapCollider.Circle, 0.36f, 0f, PrismPieceKind.Cover),
        new PrismPiece("pr_ridge", -39f, 0f, 13f, 90f, MapCollider.Box, 0.92f, 0.26f, PrismPieceKind.Cover),
        new PrismPiece("pr_ridge", 39f, 0f, 13f, 270f, MapCollider.Box, 0.92f, 0.26f, PrismPieceKind.Cover),
        new PrismPiece("pr_prism", -27f, 6f, 5.5f, 0f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Reflector),
        new PrismPiece("pr_prism", 27f, -6f, 5.5f, 180f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Reflector),
        new PrismPiece("pr_prism", 27f, 6f, 5.5f, 0f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Reflector),
        new PrismPiece("pr_prism", -27f, -6f, 5.5f, 180f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Reflector),
        new PrismPiece("pr_prism", -20f, 30f, 5f, 180f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Reflector),
        new PrismPiece("pr_prism", 20f, -30f, 5f, 0f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Reflector),
        new PrismPiece("pr_prism", 20f, 30f, 5f, 180f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Reflector),
        new PrismPiece("pr_prism", -20f, -30f, 5f, 0f, MapCollider.Poly, 0f, 0f, PrismPieceKind.Reflector),
    };

    // สร้างเลย์เอาต์รุ่น 3 ใต้ Map1_Layout (เรียกจาก DecoratePrism เมื่อเปิด Flag)
    // ซ่อนของเดิมทั้งหมดยกเว้น Background (รวมเลย์เอาต์รุ่น 2 ที่อาจบันทึกไว้ใน Scene) แล้ววางชิ้นส่วนตามตาราง + แท่นวาร์ป
    // ชื่อชิ้นส่วนใช้ให้ PrismArenaVisuals ตกแต่ง: CentralCore = เสากลาง, Cluster_ = กองคริสตัล, Crystal_ = ปริซึมสะท้อนกระสุน
    private static void DecoratePrismV3(Transform layout)
    {
        var legacy = layout.Find(LegacyPrismLayoutName);
        if (legacy != null)
        {
            legacy.gameObject.SetActive(false);
            if (Application.isPlaying) Object.Destroy(legacy.gameObject);
        }
        foreach (Transform child in layout)
            if (child.name != "Background") child.gameObject.SetActive(false);
        var group = new GameObject(PrismV3LayoutName);
        group.transform.SetParent(layout, false);
        int covers = 0, mirrors = 0;
        foreach (var piece in PrismV3Layout)
        {
            string name = piece.kind == PrismPieceKind.Core ? "CentralCore"
                : piece.kind == PrismPieceKind.Reflector ? "Crystal_" + mirrors++ : "Cluster_" + covers++;
            var art = AddPrismPiece(group.transform, piece, name);
            if (art != null && piece.kind == PrismPieceKind.Reflector) art.gameObject.AddComponent<PrismReflector>();
        }
        AddWarpPads(group.transform);
        Physics2D.SyncTransforms();
    }

    // สร้างชิ้นส่วน 1 ชิ้น: วัตถุแม่ (ตำแหน่ง/มุมหมุน) + ลูก "Artwork" (ภาพย่อให้ด้านยาวสุด = size) + ตัวชนตามตาราง (Layer Obstacle)
    // คืน SpriteRenderer ของภาพ (null = หาภาพไม่เจอ ข้ามชิ้นนี้)
    private static SpriteRenderer AddPrismPiece(Transform parent, PrismPiece piece, string name)
    {
        var sprite = Resources.Load<Sprite>(PrismArtFolder + piece.sprite);
        if (sprite == null) { Debug.LogWarning("Prism map v3 art is missing: " + PrismArtFolder + piece.sprite); return null; }
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = piece.position;
        holder.transform.localRotation = Quaternion.Euler(0, 0, piece.rotation);
        var art = new GameObject("Artwork");
        art.transform.SetParent(holder.transform, false);
        float scale = piece.size / Mathf.Max(.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        art.transform.localPosition = -sprite.bounds.center * scale;
        art.transform.localScale = Vector3.one * scale;
        var renderer = art.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 2;
        // ขนาดตัวชนในตารางเป็นหน่วยโลก แปลงเป็นหน่วยของภาพที่ถูกย่อ (หาร scale)
        if (piece.collider == MapCollider.Circle)
            art.AddComponent<CircleCollider2D>().radius = piece.size * piece.a / scale;
        else if (piece.collider == MapCollider.Box)
            art.AddComponent<BoxCollider2D>().size = new Vector2(piece.size * piece.a, piece.size * piece.b) / scale;
        else
            art.AddComponent<PolygonCollider2D>(); // รูปทรงตามภาพ (ปริซึม = สามเหลี่ยม กระสุนเด้งจากหน้าเรียบ)
        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer >= 0) art.layer = layer;
        return renderer;
    }
}
