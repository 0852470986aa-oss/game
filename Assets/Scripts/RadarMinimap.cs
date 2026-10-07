// ไฟล์ RadarMinimap.cs: มินิแม็พ/เรดาร์มุมจอระหว่างต่อสู้
// GameplayManager.cs เพิ่มคอมโพเนนต์นี้ตอนเริ่มแมตช์ ส่วนเมนู Editable ใน GameplayManager.HUD.cs เรียก BuildEditablePreview
// รันเฉพาะเครื่องตัวเอง ใช้ตำแหน่งยานที่ซิงก์มาแล้วผ่าน Photon มาแสดง ไม่ส่งข้อมูลเพิ่ม
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// มินิแม็พ HUD: แปลงตำแหน่งยานในสนามเป็นจุดบนเรดาร์และอัปเดตตามผู้เล่นรอบตัว
// ถ้าใน BattleHUD มี ArenaMinimap ที่บันทึกไว้ใน Scene จะใช้อันนั้น (ตำแหน่ง/ขนาด/สีตามหน้า Edit)
// คอมโพเนนต์วาดมินิแม็พ: กรอบสนาม, สิ่งกำบัง, ยานเรา และจุดแดงของคู่แข่งที่อยู่ในระยะเรดาร์
public class RadarMinimap : MonoBehaviour
{
    // ระยะเรดาร์ (หน่วยโลก) คู่แข่งไกลกว่านี้จะไม่แสดง
    public float radarRange = 25f;
    public float radarUIRadius = 50f;
    public GameObject blipPrefab;
    // panel = กรอบนอก, map = พื้นที่สนาม, pilot = ไอคอนยานเรา
    private RectTransform panel, map, pilot;
    private Vector2 arenaMin, arenaMax;
    // scale = ตัวคูณแปลงหน่วยโลก → พิกเซล UI, refresh = ตัวนับเวลาก่อนค้นหาผู้เล่นใหม่
    private float scale, refresh;
    // ไอคอนคู่แข่งแยกตาม PhotonView ViewID
    private readonly Dictionary<int, RectTransform> enemies = new Dictionary<int, RectTransform>();
    private PlayerController[] players;
    private TMP_Text state;

    // สร้างกล่อง Image สี่เหลี่ยมใน UI (ไม่รับคลิก) ถ้าอยู่ในหน้า Edit จะบันทึก Undo ให้ด้วย
    private static RectTransform Box(string name, Transform parent, Vector2 size, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(rect.gameObject, "Build Editable Battle UI");
#endif
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        var image = rect.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    // สร้างมินิแม็พใต้ BattleHUD: ถ้ามีอันที่บันทึกใน Scene ใช้อันนั้น ไม่งั้นสร้างใหม่ด้วยโค้ด (กรอบ 184x178)
    private void Create(Transform hud)
    {
        arenaMin = GameplayManager.GetArenaMin(GameplayManager.GetCurrentMapIndex());
        arenaMax = GameplayManager.GetArenaMax(GameplayManager.GetCurrentMapIndex());
        if (BindAuthored(hud.Find("ArenaMinimap") as RectTransform)) { AddCovers(); return; }
        panel = Box("ArenaMinimap", hud, new Vector2(184, 178), new Color(.025f, .05f, .1f, .9f));
        panel.anchoredPosition = new Vector2(-526, 253);
        Vector2 span = arenaMax - arenaMin;
        scale = Mathf.Min(162f / span.x, 136f / span.y);
        map = Box("ArenaBounds", panel, span * scale, new Color(.12f, .26f, .34f, .9f));
        map.anchoredPosition = new Vector2(0, -8);
        var interior = Box("Interior", map, map.sizeDelta - Vector2.one * 2, new Color(.025f, .06f, .12f, 1));
        AddCovers();
        pilot = Box("YourShip", map, new Vector2(6, 9), new Color(.25f, 1f, .85f));
        var nose = Box("Heading", pilot, new Vector2(2, 5), Color.white);
        nose.anchoredPosition = new Vector2(0, 6);
        state = new GameObject("RadarLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        state.transform.SetParent(panel, false);
        state.rectTransform.sizeDelta = new Vector2(176, 23);
        state.rectTransform.anchoredPosition = new Vector2(0, 73);
        state.fontSize = 12;
        state.alignment = TextAlignmentOptions.Center;
        state.raycastTarget = false;
        if (GameplayManager.Instance.playerInfoText != null) state.font = GameplayManager.Instance.playerInfoText.font;
        state.text = "ARENA / RADAR 25";
    }

    // Authored cover only: no bullets, ships, dynamic hazards or invisible boundary walls.
    // วาดสิ่งกำบังลงมินิแม็พ: ใช้ Collider2D แข็งที่มีภาพและอยู่ในสนาม (ไม่นับยาน กระสุน สกิล และ Trigger)
    private void AddCovers()
    {
        foreach (var collider in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (collider.isTrigger || !collider.enabled || !collider.gameObject.activeInHierarchy
                || collider.GetComponentInParent<PlayerController>() != null
                || collider.GetComponentInParent<BulletController>() != null
                || collider.GetComponentInParent<SkillController>() != null) continue;
            var sprite = collider.GetComponent<SpriteRenderer>();
            if (sprite == null || !sprite.enabled || sprite.sprite == null) continue;
            Bounds bounds = collider.bounds;
            if (bounds.center.x < arenaMin.x || bounds.center.x > arenaMax.x
                || bounds.center.y < arenaMin.y || bounds.center.y > arenaMax.y) continue;
            Vector2 size = (Vector2)bounds.size * scale;
            size = Vector2.Min(size, map.sizeDelta - Vector2.one * 2);
            var cover = Box("Cover", map, Vector2.Max(size, Vector2.one * 2), new Color(.3f, .43f, .49f, .8f));
            cover.anchoredPosition = MapPosition(bounds.center);
        }
    }

    // ใช้มินิแม็พที่บันทึกไว้ใน Scene: เก็บตำแหน่ง/สี/ขนาดกรอบไว้ แต่ขนาดสนามคำนวณตามแม็พที่เล่นเสมอ
    private bool BindAuthored(RectTransform authored)
    {
        if (authored == null) return false;
        var authoredMap = authored.Find("ArenaBounds") as RectTransform;
        var authoredPilot = authoredMap != null ? authoredMap.Find("YourShip") as RectTransform : null;
        if (authoredPilot == null) return false;
        panel = authored;
        map = authoredMap;
        pilot = authoredPilot;
        Vector2 span = arenaMax - arenaMin;
        // ขอบเท่าเดิม (กว้าง 184 → สนาม 162, สูง 178 → สนาม 136) ถ้าขยายกรอบ สนามจะขยายตาม
        scale = Mathf.Min((panel.sizeDelta.x - 22f) / span.x, (panel.sizeDelta.y - 42f) / span.y);
        map.sizeDelta = span * scale;
        var interior = map.Find("Interior") as RectTransform;
        if (interior != null) interior.sizeDelta = map.sizeDelta - Vector2.one * 2;
        for (int i = map.childCount - 1; i >= 0; i--)
        {
            var child = map.GetChild(i);
            if (child.name == "Cover" || child.name == "Rival") Destroy(child.gameObject);
        }
        var label = panel.Find("RadarLabel");
        state = label != null ? label.GetComponent<TMP_Text>() : null;
        return true;
    }

#if UNITY_EDITOR
    // ใช้ตอนกดเมนู Editable บน GameplayManager: สร้างมินิแม็พลง Scene ให้แก้ได้
    // (จุดสิ่งกีดขวางบนเรดาร์ไม่ได้บันทึก เพราะสร้างตามแม็พที่เล่นตอนรัน)
    public static void BuildEditablePreview(Transform hud, TMP_FontAsset font)
    {
        if (hud == null || hud.Find("ArenaMinimap") != null) return;
        int mapIndex = GameplayManager.GetCurrentMapIndex();
        Vector2 span = GameplayManager.GetArenaMax(mapIndex) - GameplayManager.GetArenaMin(mapIndex);
        var panel = Box("ArenaMinimap", hud, new Vector2(184, 178), new Color(.025f, .05f, .1f, .9f));
        panel.anchoredPosition = new Vector2(-526, 253);
        float previewScale = Mathf.Min(162f / span.x, 136f / span.y);
        var map = Box("ArenaBounds", panel, span * previewScale, new Color(.12f, .26f, .34f, .9f));
        map.anchoredPosition = new Vector2(0, -8);
        Box("Interior", map, map.sizeDelta - Vector2.one * 2, new Color(.025f, .06f, .12f, 1));
        var ship = Box("YourShip", map, new Vector2(6, 9), new Color(.25f, 1f, .85f));
        var nose = Box("Heading", ship, new Vector2(2, 5), Color.white);
        nose.anchoredPosition = new Vector2(0, 6);
        var label = new GameObject("RadarLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        UnityEditor.Undo.RegisterCreatedObjectUndo(label.gameObject, "Build Editable Battle UI");
        label.transform.SetParent(panel, false);
        label.rectTransform.sizeDelta = new Vector2(176, 23);
        label.rectTransform.anchoredPosition = new Vector2(0, 73);
        label.fontSize = 12;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (font != null) label.font = font;
        label.text = "ARENA / RADAR 25";
    }
#endif

    // แปลงตำแหน่งในโลกเป็นตำแหน่งบนมินิแม็พ (เทียบกึ่งกลางสนาม) และบีบไม่ให้ออกนอกขอบ
    private Vector2 MapPosition(Vector2 world)
    {
        Vector2 position = (world - (arenaMin + arenaMax) * .5f) * scale;
        Vector2 edge = map.sizeDelta * .5f - Vector2.one * 5;
        return new Vector2(Mathf.Clamp(position.x, -edge.x, edge.x), Mathf.Clamp(position.y, -edge.y, edge.y));
    }

    // ทุกเฟรม: สร้างมินิแม็พเมื่อ HUD พร้อม แล้วอัปเดตตำแหน่ง/ทิศยานเรา และจุดคู่แข่งในระยะ radarRange
    // รายชื่อผู้เล่นค้นใหม่ทุก 1 วิ (ไม่ค้นทุกเฟรมเพื่อประหยัด)
    private void Update()
    {
        var manager = GameplayManager.Instance;
        if (manager == null || manager.playerInfoText == null) return;
        if (panel == null)
        {
            var hud = manager.playerInfoText.canvas.transform.Find("BattleHUD");
            if (hud == null) return;
            Create(hud);
        }
        var local = manager.localPlayer;
        pilot.gameObject.SetActive(local != null && !local.isDead);
        foreach (var marker in enemies.Values) marker.gameObject.SetActive(false);
        if (local == null || local.isDead || local.HasMatchEnded) return;
        pilot.anchoredPosition = MapPosition(local.transform.position);
        pilot.localRotation = Quaternion.Euler(0, 0, local.transform.eulerAngles.z);
        refresh -= Time.unscaledDeltaTime;
        if (refresh <= 0 || players == null)
        {
            refresh = 1f;
            players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        }
        foreach (var other in players)
        {
            // โหมดทีม: เพื่อนร่วมทีมโชว์เสมอ (สีเขียว) ศัตรูโชว์เฉพาะในระยะเรดาร์ (สีแดง)
            bool ally = other != null && MatchRules.IsAlly(other.CombatantId, local.CombatantId);
            if (other == null || other == local || other.isDead || other.HiddenFromLocalViewer
                || (!ally && Vector2.Distance(other.transform.position, local.transform.position) > radarRange)) continue;
            int id = other.photonView.ViewID;
            if (!enemies.TryGetValue(id, out var marker))
            {
                marker = Box("Rival", map, new Vector2(5, 5), new Color(1f, .3f, .25f));
                enemies[id] = marker;
            }
            marker.gameObject.SetActive(true);
            marker.GetComponent<Image>().color = ally ? new Color(.3f, 1f, .55f) : new Color(1f, .3f, .25f);
            marker.anchoredPosition = MapPosition(other.transform.position);
        }
        pilot.SetAsLastSibling();
    }

    // ตอนคอมโพเนนต์ถูกทำลาย ลบมินิแม็พออกจาก HUD ด้วย
    private void OnDestroy()
    {
        if (panel != null) Destroy(panel.gameObject);
    }
}
