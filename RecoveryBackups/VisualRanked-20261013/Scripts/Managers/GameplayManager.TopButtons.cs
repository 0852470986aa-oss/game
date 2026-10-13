// GameplayManager.TopButtons.cs — จัดปุ่มตั้งค่า + ปุ่มอีโมตในสนามรบให้เป็นแถวเดียวมุมขวาบน (partial class ของ GameplayManager)
// เดิม: ปุ่มตั้งค่า (Btn_Exit ที่วางใน Scene) กับปุ่มอีโมตอยู่คนละที่ ขนาด/สีไม่เท่ากัน ดูไม่เป็นระเบียบ
// ใหม่: [⚙ ตั้งค่า] [☺ อีโมต] ขนาดเท่ากัน สีเดียวกัน ชิดขวาบน / เมนูอีโมตเปิดลงด้านล่างปุ่ม / ปิดอีโมตในตัวเลือก = เหลือปุ่มตั้งค่าชิดขวา
// ปุ่มตั้งค่ายังเป็นวัตถุเดิมใน Canvas (คำสั่งปุ่มเดิมทำงานเหมือนเดิม) แค่ย้ายตำแหน่ง/ขนาดตอนเล่นให้ตรงแถว
// โหมดมือซ้ายไม่สลับฝั่งแถวนี้ (มุมซ้ายบนเป็นเรดาร์) / ปิด FeatureFlags.TidyBattleButtons = ตำแหน่งเดิม
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนจัดแถวปุ่มมุมขวาบนในสนามรบของ GameplayManager (partial)
public partial class GameplayManager
{
    // ตำแหน่งแถวปุ่ม (พิกัด battleHud 1280x720 จุดกลางจอ = 0,0)
    private const float TopButtonY = 318f, TopButtonW = 140f, TopButtonH = 46f, TopButtonGap = 10f, TopButtonRight = 630f;
    private static readonly Color TopButtonColor = new Color(.06f, .16f, .26f, .92f);
    private bool topButtonsStyled;

    // เรียกทุกเฟรมจาก FitBattleHUD: วางปุ่มอีโมต/เมนู แล้วย้ายปุ่มตั้งค่าให้ตรงแถว
    private void PlaceTopRightButtons()
    {
        if (!FeatureFlags.TidyBattleButtons || !Application.isPlaying || battleHud == null) return;
        var emote = battleHud.Find("EmoteButton") as RectTransform;
        var menu = battleHud.Find("EmoteMenu") as RectTransform;
        bool emoteShown = emote != null && emote.gameObject.activeSelf;
        // ปุ่มขวาสุด = อีโมต (ถ้าเปิด) ถัดไปทางซ้าย = ตั้งค่า
        float emoteX = TopButtonRight - TopButtonW * .5f;
        float settingsX = emoteShown ? emoteX - TopButtonW - TopButtonGap : emoteX;
        if (emote != null) PlaceInHud(emote, emoteX, TopButtonY);
        if (menu != null)
        {
            // เมนูเปิดลงด้านล่างปุ่มอีโมต (รายการเรียงในเมนูสูง 230)
            menu.anchorMin = menu.anchorMax = new Vector2(.5f, .5f);
            menu.localScale = Vector3.one;
            menu.anchoredPosition = new Vector2(TopButtonRight - 80f, TopButtonY - TopButtonH * .5f - 8f - 115f);
            UiLayout.Placed(menu);
            if (menu.gameObject.activeSelf) menu.SetAsLastSibling();
        }
        var exit = hudSettingsButton != null ? hudSettingsButton.transform as RectTransform : null;
        if (exit != null && exit.gameObject.activeInHierarchy)
        {
            // ปุ่มตั้งค่าอยู่คนละ Canvas parent: แปลงตำแหน่ง/ขนาดจากพิกัด battleHud เป็นของ parent ปุ่ม
            exit.anchorMin = exit.anchorMax = exit.pivot = new Vector2(.5f, .5f); // anchor จุดเดียว sizeDelta = ขนาดจริง
            exit.position = battleHud.TransformPoint(new Vector3(settingsX, TopButtonY, 0));
            float ratio = exit.parent != null && exit.parent.lossyScale.x > 0 ? battleHud.lossyScale.x / exit.parent.lossyScale.x : 1f;
            exit.sizeDelta = new Vector2(TopButtonW, TopButtonH) * ratio / Mathf.Max(.01f, exit.localScale.x);
            UiLayout.Placed(exit);
        }
        if (!topButtonsStyled) StyleTopButtons(emote, exit);
    }

    // ย้ายปุ่มใน battleHud ไปตำแหน่ง (x, y) ขนาดมาตรฐานของแถว
    private static void PlaceInHud(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(TopButtonW, TopButtonH);
        UiLayout.Placed(rect); // ตำแหน่งที่บันทึกเอง (UiLayout.cs)
    }

    // แต่งปุ่มทั้งสองให้เหมือนกัน (ทำครั้งเดียว): สีพื้น, ตัวหนังสือขาวขนาดเดียวกัน, ไอคอนหน้าข้อความ
    private void StyleTopButtons(RectTransform emote, RectTransform exit)
    {
        topButtonsStyled = true;
        foreach (var rect in new[] { emote, exit })
        {
            if (rect == null) continue;
            var image = rect.GetComponent<Image>();
            if (image != null) { image.color = TopButtonColor; image.sprite = null; image.type = Image.Type.Simple; ButtonSkin.Apply(image); }
            var button = rect.GetComponent<Button>();
            if (button != null) { var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.85f, .95f, 1f); colors.pressedColor = new Color(.6f, .8f, 1f); button.colors = colors; }
            var label = rect.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(TopButtonW - 12f, TopButtonH - 8f);
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 18;
            label.fontStyle = FontStyles.Bold;
            UiIcon.Attach(label, rect == emote ? "chat" : "settings", .55f);
        }
        // ขอบบาง ๆ สีฟ้า ให้ดูเป็นชุดเดียวกัน
        foreach (var rect in new[] { emote, exit })
        {
            if (rect == null || rect.GetComponent<Outline>() != null) continue;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.37f, .89f, .89f, .45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
