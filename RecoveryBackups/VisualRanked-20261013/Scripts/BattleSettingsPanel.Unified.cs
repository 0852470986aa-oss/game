// BattleSettingsPanel.Unified.cs — หน้าตั้งค่าแบบหน้าเดียว เลื่อนขึ้นลงได้ (เปลี่ยนแค่การจัดวาง)
// เดิม: หน้าตั้งค่ามีแถบเลื่อน 4 แถว + ปุ่ม MORE OPTIONS เปิดอีกหน้า
// ใหม่: ย้ายแถบเลื่อนเดิม และแถวตั้งค่าเดิมของ MORE OPTIONS (ทำงานเหมือนเดิมทุกแถว) มาเรียงในกล่องเลื่อนอันเดียว
//       แบ่งหัวหมวด เสียง / การควบคุม / การแสดงผล / ทั่วไป + แท็บด้านบนกดแล้วเลื่อนไปหมวดนั้น
// ปุ่ม BACK / LOG OUT / LEAVE MATCH อยู่ที่เดิม ไม่เลื่อนตาม
// ปิด FeatureFlags.UnifiedSettings = กลับเป็นหน้าเดิม (มีปุ่ม MORE OPTIONS)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนจัดหน้าตั้งค่าแบบหน้าเดียวของ BattleSettingsPanel (partial): รวมแถบเลื่อนและแถว MORE OPTIONS ไว้ใน ScrollRect เดียว
public partial class BattleSettingsPanel
{
    private ScrollRect unifiedScroll; // ScrollRect ของหน้าตั้งค่าแบบหน้าเดียว ใช้เลื่อนขึ้นลงและกระโดดไปหมวด
    private readonly List<KeyValuePair<string, float>> unifiedSections = new List<KeyValuePair<string, float>>(); // ชื่อหัวหมวดคู่กับตำแหน่ง y ในหน้า ใช้ให้แท็บด้านบนเลื่อนไปหมวดนั้น
    private float unifiedHeight; // ความสูงรวมของเนื้อหาทั้งหน้า ใช้คำนวณระยะเลื่อน

    private static readonly Color UnifiedRowColor = new Color(.08f, .2f, .3f); // สีพื้นหลังของแถวตั้งค่าแต่ละแถว
    private static readonly Color UnifiedHeadColor = new Color(.37f, .89f, .89f); // สีตัวอักษรหัวหมวด (ฟ้าอมเขียว)

    // หมวด: ชื่อหัวข้อ, ชื่อแถบเลื่อนเดิม, ชื่อแถวตั้งค่าเดิม
    private static readonly string[][] UnifiedLayout =
    {
        new[] { "SOUND", "MASTER", "MUSIC", "EFFECTS" },
        new[] { "CONTROL", "AIM SPEED", "BUTTON SIZE", "BUTTON OPACITY", "CONTROLS" },
        new[] { "DISPLAY", "GRAPHICS", "FPS LIMIT", "SHOW FPS", "CAMERA SHAKE", "DAMAGE NUMBERS" },
        new[] { "GENERAL", "LANGUAGE", "KILL FEED", "EMOTES" },
    };
    private static readonly HashSet<string> SliderTitles = new HashSet<string> { "MASTER", "MUSIC", "EFFECTS", "AIM SPEED" }; // ชื่อรายการที่เป็นแถบเลื่อนเดิม (ย้ายแถบเลื่อน ไม่ใช่แถวตั้งค่า)

    // เรียกจาก Show หลังสร้าง/ผูกหน้าตั้งค่าเสร็จ: จัดเป็นหน้าเดียว (ถ้าเปิด FeatureFlags) ถ้าจัดไม่สำเร็จจะ log เตือนแล้วใช้หน้าแบบเดิม
    private void MakeUnified()
    {
        if (!FeatureFlags.UnifiedSettings || !FeatureFlags.PlayerOptions || panel == null) return;
        try { BuildUnified(); }
        catch (System.Exception error) { Debug.LogWarning("Unified settings layout failed, using the old layout: " + error.Message); }
    }

    // สร้างกล่องเลื่อน (ScrollRect + RectMask2D) ย้ายแถบเลื่อน/แถวตั้งค่าเดิมเข้าไปเรียงตามหมวดใน UnifiedLayout
    // แล้วสร้างแท็บหมวดด้านบนที่กดแล้วเลื่อนไปหมวดนั้น
    private void BuildUnified()
    {
        float w = Mathf.Max(560f, panel.rect.width), h = Mathf.Max(520f, panel.rect.height);
        // ปุ่ม MORE OPTIONS ไม่ต้องใช้แล้ว (ทุกแถวอยู่ในหน้านี้)
        var more = panel.Find("MORE OPTIONS");
        if (more != null) more.gameObject.SetActive(false);
        // หัวข้อ SETTINGS ชิดบน
        var title = panel.Find("Title") as RectTransform;
        if (title != null) PlaceTop(title, panel, 0, 22, 500, 44);

        // 1) กล่องเลื่อน: ใต้แถวแท็บ ถึงเหนือปุ่มด้านล่าง
        float top = 112f, bottom = 118f;
        var viewport = Rect("UnifiedViewport", panel, 0, 0, 0, 0);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(28, bottom); viewport.offsetMax = new Vector2(-28, -top);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); // ให้ลากเลื่อนตรงที่ว่างได้
        var content = Rect("UnifiedContent", viewport, 0, 0, 0, 0);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
        unifiedScroll = viewport.gameObject.AddComponent<ScrollRect>();
        unifiedScroll.content = content; unifiedScroll.viewport = viewport;
        unifiedScroll.horizontal = false; unifiedScroll.vertical = true;
        unifiedScroll.movementType = ScrollRect.MovementType.Clamped;
        unifiedScroll.scrollSensitivity = 35f;
        float rowWidth = w - 70f;

        // 2) เรียงแถวตามหมวด
        var options = new Dictionary<string, OptionRow>();
        foreach (var row in BuildOptionRows()) options[row.title] = row;
        float y = 6f;
        unifiedSections.Clear();
        foreach (var section in UnifiedLayout)
        {
            var head = Text(content, section[0], 0, 0, rowWidth, "Head " + section[0]);
            head.fontSize = 19; head.color = UnifiedHeadColor; head.alignment = TextAlignmentOptions.Left;
            PlaceTop(head.rectTransform, content, 0, y, rowWidth, 30);
            UiIcon.Attach(head, SectionIcon(section[0]), .9f); // ไอคอนหน้าหัวหมวด (UiIcon.cs)
            var line = Rect("Line " + section[0], content, 0, 0, 0, 0);
            line.gameObject.AddComponent<Image>().color = new Color(.37f, .89f, .89f, .35f);
            PlaceTop(line, content, 0, y + 31, rowWidth, 2);
            unifiedSections.Add(new KeyValuePair<string, float>(section[0], y));
            y += 40f;
            for (int i = 1; i < section.Length; i++)
            {
                string name = section[i];
                if (SliderTitles.Contains(name)) { if (MoveSlider(name, content, y, rowWidth)) y += 58f; }
                else if (options.TryGetValue(name, out var row)) { OptionButton(row, content, y, rowWidth); y += 54f; }
            }
            y += 12f;
        }
        // ข้อความเดิมใต้แถบเลื่อน ย้ายมาไว้ท้ายรายการ
        var message = panel.Find("Message") as RectTransform;
        if (message != null)
        {
            message.SetParent(content, false);
            PlaceTop(message, content, 0, y, rowWidth, 34);
            y += 40f;
        }
        unifiedHeight = y;
        content.sizeDelta = new Vector2(0, y);
        content.anchoredPosition = Vector2.zero;

        // 3) แท็บหมวดด้านบน: กดแล้วเลื่อนไปหมวดนั้น
        float tabWidth = (w - 70f - 3 * 8f) / 4f;
        for (int i = 0; i < unifiedSections.Count; i++)
        {
            float sectionY = unifiedSections[i].Value;
            var tab = Rect("Tab " + unifiedSections[i].Key, panel, 0, 0, 0, 0);
            PlaceTop(tab, panel, -((w - 70f) - tabWidth) * .5f + i * (tabWidth + 8f), 70, tabWidth, 34);
            tab.gameObject.AddComponent<Image>().color = new Color(.12f, .2f, .36f);
            tab.gameObject.AddComponent<Button>().onClick.AddListener(() => ScrollToSection(sectionY));
            var label = Text(tab, unifiedSections[i].Key, 0, 0, tabWidth - 6, "Label"); label.fontSize = 16;
            UiIcon.Attach(label, SectionIcon(unifiedSections[i].Key), .5f);
        }
    }

    // ย้ายแถบเลื่อนเดิม (ชื่อ + แถบ) เข้าแถวในกล่องเลื่อน
    private bool MoveSlider(string name, RectTransform content, float y, float rowWidth)
    {
        var track = panel.Find(name) as RectTransform;
        if (track == null) return false;
        var bg = Rect("Row " + name, content, 0, 0, 0, 0);
        bg.gameObject.AddComponent<Image>().color = UnifiedRowColor;
        PlaceTop(bg, content, 0, y, rowWidth, 50);
        var label = panel.Find(name + " Label") as RectTransform;
        if (label != null)
        {
            label.SetParent(bg, false);
            Center(label, -rowWidth * .5f + 12f + 140f, 0, 280, 44);
            var text = label.GetComponent<TMP_Text>();
            if (text != null) { text.alignment = TextAlignmentOptions.Left; text.fontSize = 18; text.enableAutoSizing = false; }
        }
        track.SetParent(bg, false);
        Center(track, rowWidth * .5f - 18f - 140f, 0, 280, 16);
        return true;
    }

    // แถวตั้งค่าเดิมของ MORE OPTIONS (กดแล้วเปลี่ยนค่าเหมือนเดิม) แบบเต็มความกว้าง
    private void OptionButton(OptionRow row, RectTransform content, float y, float rowWidth)
    {
        var rect = Rect("Opt_" + row.title, content, 0, 0, 0, 0);
        PlaceTop(rect, content, 0, y, rowWidth, 46);
        rect.gameObject.AddComponent<Image>().color = UnifiedRowColor;
        var name = Text(rect, row.title, 0, 0, 280, "Name");
        name.fontSize = 18; name.alignment = TextAlignmentOptions.Left;
        Center(name.rectTransform, -rowWidth * .5f + 12f + 140f, 0, 280, 40);
        var value = Text(rect, row.value(), 0, 0, 240, "Value");
        value.fontSize = 18; value.color = new Color(.45f, .9f, 1f); value.alignment = TextAlignmentOptions.Right;
        Center(value.rectTransform, rowWidth * .5f - 14f - 120f, 0, 240, 40);
        rect.gameObject.AddComponent<Button>().onClick.AddListener(() =>
        {
            row.next();
            value.text = row.value();
            AudioManager.Instance?.PlaySFX("SFX_Click");
        });
    }

    // เลื่อนกล่องเลื่อนไปที่หมวด (y = ระยะหัวหมวดจากด้านบนของเนื้อหา) โดยแปลงเป็น verticalNormalizedPosition แล้วเล่นเสียงคลิก
    private void ScrollToSection(float y)
    {
        if (unifiedScroll == null) return;
        float view = unifiedScroll.viewport.rect.height;
        float range = Mathf.Max(1f, unifiedHeight - view);
        unifiedScroll.StopMovement();
        unifiedScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(y / range);
        AudioManager.Instance?.PlaySFX("SFX_Click");
    }

    // วางลูกโดยยึดขอบบนของ parent (y = ระยะลงจากขอบบน)
    private static void PlaceTop(RectTransform rect, RectTransform parent, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    // วางลูกโดยยึดจุดกึ่งกลางของ parent (anchor/pivot = 0.5) ที่ตำแหน่ง x,y ขนาด w x h
    private static void Center(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
    }

    // ไอคอนของแต่ละหมวด (รูปอยู่ Resources/Images/UI)
    private static string SectionIcon(string section)
    {
        switch (section)
        {
            case "SOUND": return "sound";
            case "CONTROL": return "control";
            case "DISPLAY": return "display";
            default: return "settings";
        }
    }
}
