// LobbyManager.Polish.cs — ปรับหน้าตา UI ล็อบบี้ให้เป็นชุดเดียวกัน (รอบแก้ UI หลังดูวิดีโอทดสอบ)
// UI ล็อบบี้บางส่วนถูกบันทึกไว้ใน Scene แล้ว (สี/ตำแหน่งที่บันทึกจะไม่ถูกเปลี่ยนด้วยโค้ดสร้าง UI เดิม)
// ไฟล์นี้จึง "บังคับ" ค่าที่ต้องถูกต้องเสมอทุกครั้งที่เปิดหน้า:
// 1) พื้นหลังหน้าต่างซ้อน (MISSIONS / PROFILE / RANKED / SOCIAL / WORKSHOP / เลือกโหมด / ตั้งค่าห้อง) ทึบ ไม่ให้หน้าข้างหลังโผล่
// 2) แท็บที่เลือกอยู่เป็นสีเขียวฟ้าสว่าง (เดิมกลายเป็นสีเทาเหมือนกดไม่ได้)
// 3) การ์ดแม็พ 5 ใบในห้องรอ/หน้าค้นหาห้อง จัดขนาดเท่ากันทุกใบ (เดิม 3 ใบแรกเป็นการ์ดใหญ่ซ้อนทับใบใหม่)
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนปรับหน้าตา UI ล็อบบี้ (partial): สีพื้นหลังทึบ สีแท็บ จัดตำแหน่งปุ่ม/ช่องนักบิน/การ์ดแม็พให้ถูกต้องทุกครั้งที่เปิดหน้า
public partial class LobbyManager
{
    public static readonly Color OverlayShade = new Color(.006f, .01f, .025f, .975f);
    public static readonly Color WindowFill = new Color(.03f, .055f, .1f, 1f);
    public static readonly Color TabIdle = new Color(.08f, .26f, .36f);
    public static readonly Color TabActive = new Color(.10f, .55f, .55f);

    // พื้นหลังมืดทึบ + หน้าต่างทึบ
    private static void SolidOverlay(Image overlay, Component window = null)
    {
        if (overlay != null) { overlay.color = OverlayShade; overlay.raycastTarget = true; }
        var image = window != null ? window.GetComponent<Image>() : null;
        if (image != null) image.color = WindowFill;
    }

    // แท็บ: ที่เลือกอยู่ = สว่าง (ไม่ทำให้เป็นสีเทา), ที่ปิดด้วยสวิตช์ฟีเจอร์ = จาง
    private static void StyleTab(Button tab, bool active, bool enabled = true)
    {
        if (tab == null) return;
        var image = tab.targetGraphic as Image;
        if (image != null) image.color = active ? TabActive : TabIdle;
        var colors = tab.colors;
        colors.disabledColor = active ? Color.white : new Color(.4f, .4f, .4f, .5f);
        tab.colors = colors;
        tab.interactable = enabled && !active;
        var label = tab.GetComponentInChildren<TMP_Text>();
        if (label != null) label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
    }

    // แท็บโรงเก็บยาน 5 แท็บ: 0 SHIPS, 1 SKILLS, 2 UPGRADES, 3 ITEMS, 4 SHOP
    private void StyleHangarTabs(int active)
    {
        if (inventoryPanel == null) return;
        var root = inventoryPanel.transform.Find("LobbySurface");
        if (root == null) return;
        string[] names = { "ShipsTab", "SkillsTab", "WorkshopTab0", "WorkshopTab1", "WorkshopTab2" };
        for (int i = 0; i < names.Length; i++)
        {
            var tab = root.Find(names[i]);
            var button = tab != null ? tab.GetComponent<Button>() : null;
            if (button == null) continue;
            StyleTab(button, i == active);
            button.interactable = true; // กดซ้ำได้ (ไม่เปลี่ยนเป็นสีเทา)
        }
    }
    // ตัวช่วยเดิม: true = ไฮไลต์แท็บ SKILLS, false = ไฮไลต์แท็บ SHIPS (เรียกต่อไปยังเวอร์ชันที่รับเลขแท็บ)
    private void StyleHangarTabs(bool skillsPage) => StyleHangarTabs(skillsPage ? 1 : 0);

    // ===== UPGRADES / ITEMS / SHOP แสดงในหน้าโรงเก็บยานเหมือนแท็บ SHIPS / SKILLS (ไม่เป็นหน้าต่างลอย) =====
    private bool workshopEmbedded;

    // เปิดหน้า UPGRADES/ITEMS/SHOP (tab 0/1/2) ฝังในหน้าโรงเก็บยาน: ย้าย workshopOverlay เข้า LobbySurface
    // ซ่อนหน้า SHIPS/SKILLS และแถบล่าง แล้วเปิด Workshop พร้อมไฮไลต์แท็บที่เลือก
    private void OpenHangarWorkshop(int tab)
    {
        if (inventoryPanel == null || workshopOverlay == null) return;
        var root = inventoryPanel.transform.Find("LobbySurface");
        if (root == null) return;
        workshopTab = (WorkshopTab)tab;
        workshopMessage = "";
        workshopEmbedded = true;
        workshopOverlay.transform.SetParent(root, false);
        if (hangarShipsPage != null) hangarShipsPage.SetActive(false);
        if (hangarSkillsPage != null) hangarSkillsPage.SetActive(false);
        SetHangarFooterVisible(false);
        OpenWorkshop();
        StyleHangarTabs(2 + tab);
    }

    // ปิดหน้า UPGRADES/ITEMS/SHOP ที่ฝังอยู่ (เปลี่ยนแท็บ / ออกจากโรงเก็บยาน) คืน true ถ้าปิดจริง
    private bool CloseEmbeddedWorkshop(bool showShips = true)
    {
        if (!workshopEmbedded) return false;
        workshopEmbedded = false;
        if (workshopOverlay != null) workshopOverlay.gameObject.SetActive(false);
        SetHangarFooterVisible(true);
        if (showShips && hangarShipsPage != null && hangarSkillsPage != null && !hangarSkillsPage.activeSelf) { hangarShipsPage.SetActive(true); StyleHangarTabs(0); }
        return true;
    }

    // แสดง/ซ่อนข้อความและแถบปุ่มด้านล่างของโรงเก็บยาน (HangarMessage, HangarFooter)
    private void SetHangarFooterVisible(bool on)
    {
        var root = inventoryPanel != null ? inventoryPanel.transform.Find("LobbySurface") : null;
        if (root == null) return;
        foreach (var name in new[] { "HangarMessage", "HangarFooter" })
        {
            var item = root.Find(name);
            if (item != null) item.gameObject.SetActive(on);
        }
    }

    // ฝัง: พื้นหลังใส ไม่บังแท็บด้านบน เนื้อหาเลื่อนลงใต้แถวแท็บ
    private void EmbeddedWorkshopStyle()
    {
        if (workshopOverlay != null) { workshopOverlay.color = Color.clear; workshopOverlay.raycastTarget = false; }
        var window = workshopWindow != null ? workshopWindow.GetComponent<Image>() : null;
        if (window != null) { window.color = Color.clear; window.raycastTarget = false; }
        if (workshopWindow != null) workshopWindow.anchoredPosition = new Vector2(0, -78);
    }

    // ===== หน้าหลัก: เอา HOW TO PLAY ออก ใส่ปุ่ม RANKED แทน / หน้าเลือกโหมด: CREATE / JOIN ROOM เต็มแถว =====
    private void ApplyHomeRankedLayout(RectTransform root)
    {
        var how = root.Find("HowToPlay");
        bool ranked = FeatureFlags.Ranked && rankedButton != null;
        if (how != null) how.gameObject.SetActive(!ranked);
        if (ranked)
        {
            rankedButton.transform.SetParent(root, false);
            Place(rankedButton.transform, 395, -180, 370, 58);
            Place(rankedButton.transform.Find("Label"), 0, 0, 346, 46);
            if (rankedOverlay != null) rankedOverlay.transform.SetAsLastSibling();
        }
        if (createRoomButton != null && playMenu != null && createRoomButton.transform.parent == playMenu)
        {
            Place(createRoomButton.transform, 0, 198, 1140, 66);
            Place(createRoomButton.transform.Find("Label"), 0, 0, 1110, 54);
        }
    }

    // ===== ห้องรอหลายคน: จัดช่องนักบินให้อยู่กลาง ขนาดพอดีจำนวนคน =====
    // FFA: ไม่เกิน 5 คน = แถวเดียว, มากกว่านั้น = 2 แถว / ทีม: แถวบน BLUE แถวล่าง RED ทีมละ teamSize ช่อง
    private void LayoutRosterSlots(int maxPlayers, bool teams, int teamSize)
    {
        if (rosterSlots == null) return;
        if (LayoutRosterSlotsPrep(maxPlayers, teams, teamSize)) return; // หน้าเตรียมพร้อมรบแบบใหม่ (LobbyManager.NewLayout.cs)
        int rows = teams ? 2 : (maxPlayers <= 5 ? 1 : 2);
        int cols = teams ? teamSize : Mathf.CeilToInt(maxPlayers / (float)rows);
        const float gap = 14f;
        float w = Mathf.Min(300f, (1160f - gap * (cols - 1)) / cols);
        float h = rows == 1 ? 196f : 102f;
        for (int i = 0; i < rosterSlots.Length; i++)
        {
            int row, col;
            if (teams) { row = i < 5 ? 0 : 1; col = i % 5; }
            else { row = rows == 1 ? 0 : (i < cols ? 0 : 1); col = rows == 1 ? i : (i < cols ? i : i - cols); }
            float x = (col - (cols - 1) * .5f) * (w + gap);
            float y = rows == 1 ? 0 : (row == 0 ? 54 : -54);
            var slot = rosterSlots[i];
            if (slot == null || slot.panel == null) continue;
            Place(slot.panel.transform, x, y, w, h);
            if (rows == 1)
            {
                // ช่องใหญ่: ชื่อบน รูปยานกลาง ชื่อยานกับสถานะล่าง
                Place(slot.name.transform, 0, 74, w - 14, 30);
                Place(slot.ship.transform, 0, 10, Mathf.Min(120, w - 40), 92);
                Place(slot.shipName.transform, 0, -52, w - 14, 26);
                Place(slot.ready.transform, 0, -78, w - 14, 26);
            }
            else
            {
                // ช่องเล็ก: ชื่อบนเต็มความกว้าง รูปยานซ้าย ข้อความขวา
                Place(slot.name.transform, 0, 32, w - 12, 28);
                Place(slot.ship.transform, -w * .5f + 44, -14, 68, 58);
                Place(slot.shipName.transform, 30, 0, w - 92, 24);
                Place(slot.ready.transform, 30, -28, w - 92, 24);
            }
        }
    }

    // จัดการ์ดแม็พให้ทุกใบขนาดเท่ากัน (บังคับทุกครั้ง แม้การ์ดถูกบันทึกใน Scene ด้วยขนาดเดิม)
    private static void LayoutCompactMapCard(Button card, float x, float y, float width)
    {
        var rect = (RectTransform)card.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 202);
        UiLayout.Placed(rect);
        Place(card.transform.Find("MapPreview"), 0, 30, width - 24, 120);
        Place(card.transform.Find("MapName"), 0, -46, width - 14, 28);
        Place(card.transform.Find("SelectionHint"), 0, -76, width - 14, 24);
        var description = card.transform.Find("Description");
        if (description != null) description.gameObject.SetActive(false);
        var label = card.transform.Find("Label");
        if (label != null) label.gameObject.SetActive(false);
        var name = card.transform.Find("MapName");
        var text = name != null ? name.GetComponent<TMP_Text>() : null;
        if (text != null) { text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 16; }
    }

    // วางลูกให้อยู่กึ่งกลาง parent ที่ตำแหน่ง x,y ขนาด w x h (ข้ามถ้าไม่ใช่ RectTransform หรือเป็น null)
    private static void Place(Transform item, float x, float y, float w, float h)
    {
        var rect = item as RectTransform;
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        UiLayout.Placed(rect); // มีตำแหน่งที่บันทึกเอง (UI Layout > Save Position) = ใช้ค่านั้น
    }

    // หน้าตั้งค่า: พื้นหลังข้างหลังมืดขึ้น
    private static void DarkenSettingsShade()
    {
        var shade = GameObject.Find("BattleSettings/ModalShade");
        var image = shade != null ? shade.GetComponent<Image>() : null;
        if (image != null) image.color = new Color(0, 0, 0, .93f);
    }
}
