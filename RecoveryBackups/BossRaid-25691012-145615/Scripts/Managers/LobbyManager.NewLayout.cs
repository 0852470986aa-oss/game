// LobbyManager.NewLayout.cs — จัดหน้าใหม่ตามแบบเกม MOBA มือถือ (เปลี่ยนแค่การจัดวาง ปุ่มทุกอันทำงานเหมือนเดิม)
// 1) หน้าเลือกวิธีเล่น (playMenu): เมนูหมวดด้านซ้าย + การ์ดรูปโหมด + ปุ่มสร้าง/เข้าห้องมุมขวาบน + แถบเริ่มเล่นด้านล่าง
//    การ์ดแต่ละใบคือปุ่ม SoloChoice เดิม (กดแล้วเลือกโหมดเหมือนเดิม) แล้วกด VS BOT เพื่อเริ่มเหมือนเดิม
// 2) หน้าเตรียมพร้อมรบ: ดู LayoutPrepRoom ด้านล่าง
// รูปการ์ด: Resources/Images/Modes/mode_<ชื่อ>.png (story, duel, team, ffa, survival, hill, stars, royale, practice)
//           ยังไม่มีรูป = ใช้รูปแผนที่แทน
// ปิด FeatureFlags.NewLobbyLayout = กลับไปหน้าเดิม
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนจัดหน้าใหม่แบบเกม MOBA มือถือของ LobbyManager (partial): หน้าเลือกวิธีเล่นแบบการ์ดและหน้าเตรียมพร้อมรบ
public partial class LobbyManager
{
    private static readonly Color CardFrame = new Color(.08f, .26f, .36f); // สีกรอบการ์ดโหมดในหน้าเลือกวิธีเล่น
    private static readonly Color StartGold = new Color(.86f, .64f, .2f); // สีทองของปุ่มเริ่มเกมและตัวอักษร VS
    private RectTransform[] modeCategories; // ภาชนะการ์ดของแต่ละหมวดโหมด (BATTLE / SPECIAL / PRACTICE)
    private Button[] modeCategoryTabs; // ปุ่มแท็บเลือกหมวดโหมดด้านซ้าย
    private int modeCategory; // หมวดโหมดที่เลือกอยู่

    // หาปุ่มการ์ดโหมด (อยู่ลึกในหมวดได้)
    private Transform FindModeCard(int index)
    {
        if (playMenu == null) return null;
        var direct = playMenu.Find("SoloChoice" + index);
        if (direct != null) return direct;
        foreach (var rect in playMenu.GetComponentsInChildren<RectTransform>(true))
            if (rect.name == "SoloChoice" + index) return rect;
        return null;
    }

    // ===================== หน้าเลือกวิธีเล่น =====================
    private void ApplyModeCardLayout()
    {
        // จัดเฉพาะตอนเล่นจริง (ไม่บันทึกลง Scene ตอนกด Build ใน Editor เพื่อไม่ให้ปุ่มเดิมหาไม่เจอ)
        if (!Application.isPlaying || !FeatureFlags.NewLobbyLayout || playMenu == null) return;
        // หัวหน้า: ชื่อชิดซ้าย, BACK ขวาสุด, สร้าง/เข้าห้อง ข้าง BACK
        Place(playMenu.Find("Heading"), -440, 290, 380, 50);
        var heading = playMenu.Find("Heading")?.GetComponent<TMP_Text>();
        if (heading != null) heading.alignment = TextAlignmentOptions.Left;
        if (createRoomButton != null)
        {
            createRoomButton.transform.SetParent(playMenu, false);
            Place(createRoomButton.transform, 190, 290, 380, 58);
            Place(createRoomButton.transform.Find("Label"), 0, 0, 360, 46);
        }
        var soloHeading = playMenu.Find("SoloHeading");
        if (soloHeading != null) soloHeading.gameObject.SetActive(false);

        // หมวด (ภาชนะของการ์ด) — ปุ่ม SoloChoice ย้ายเข้าไปอยู่ในหมวด
        string[] names = { "BATTLE", "SPECIAL MODES", "PRACTICE" };
        modeCategories = new RectTransform[names.Length];
        modeCategoryTabs = new Button[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            modeCategories[i] = UIRect("ModeCategory" + i, playMenu, 110, 20, 1020, 420);
            modeCategoryTabs[i] = UIButton("ModeTab" + i, playMenu, names[i], -530, 170 - i * 74, 200, 60, () => SelectModeCategory(index));
        }

        // หมวด ต่อสู้: เนื้อเรื่อง (ใหญ่ซ้าย) / ทีม + ตัวต่อตัว (กลาง) / 1 VS 1 (ขวา)
        var battle = modeCategories[0];
        ModeCard(11, battle, -375, 0, 270, 420, "story", "CAMPAIGN", "Story stages");
        ChipCard(battle, "TeamCard", -55, 109, 340, 202, "team", "TEAM BATTLE", new[] { 4, 5, 6 }, new[] { "2v2", "3v3", "5v5" });
        ChipCard(battle, "FfaCard", -55, -109, 340, 202, "ffa", "FREE FOR ALL", new[] { 1, 2, 3 }, new[] { "3 BOTS", "5 BOTS", "9 BOTS" });
        ModeCard(0, battle, 315, 0, 390, 420, "duel", "1 VS 1", "Duel");
        // หมวด โหมดพิเศษ: การ์ด 2x2
        string[] art = { "survival", "hill", "stars", "royale" };
        string[] titles = { "SURVIVAL", "KING OF THE HILL", "STAR HUNT", "BATTLE ROYALE" };
        string[] subs = { "Survive waves", "Capture the hill", "Collect stars", "Last ship standing" };
        for (int i = 0; i < 4; i++)
            ModeCard(7 + i, modeCategories[1], i % 2 == 0 ? -257 : 257, i < 2 ? 109 : -109, 500, 202, art[i], titles[i], subs[i]);
        // หมวด ฝึกซ้อม: ปุ่ม TRAINING เดิมเป็นการ์ดใหญ่ (กดแล้วเริ่มฝึกเหมือนเดิม)
        if (trainingButton != null)
        {
            trainingButton.transform.SetParent(modeCategories[2], false);
            DressCard(trainingButton.transform, 0, 0, 620, 420, "practice", "TRAINING", "Free practice against target bots");
        }

        // แถบล่าง: โหมดที่เลือก (ซ้าย) / ความยาก / VS BOT (ปุ่มทองใหญ่)
        if (soloSelection != null) { Place(soloSelection.transform, -250, -262, 700, 40); soloSelection.alignment = TextAlignmentOptions.Left; }
        if (soloDifficultyButton != null)
        {
            soloDifficultyButton.transform.SetParent(playMenu, false);
            Place(soloDifficultyButton.transform, 230, -262, 250, 58);
            Place(soloDifficultyButton.transform.Find("Label"), 0, 0, 236, 48);
        }
        if (soloPlayButton != null)
        {
            soloPlayButton.transform.SetParent(playMenu, false);
            Place(soloPlayButton.transform, 500, -262, 260, 70);
            Place(soloPlayButton.transform.Find("Label"), 0, 0, 240, 56);
            var image = soloPlayButton.GetComponent<Image>();
            if (image != null) image.color = StartGold;
            var colors = soloPlayButton.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1f, .95f, .8f);
            colors.disabledColor = new Color(.5f, .5f, .5f); soloPlayButton.colors = colors;
            var label = soloPlayButton.GetComponentInChildren<TMP_Text>();
            if (label != null) { label.color = new Color(.12f, .08f, .02f); label.fontSize = 28; label.enableAutoSizing = false; }
        }
        SelectModeCategory(modeCategory);
    }

    // เลือกหมวดเมนูซ้ายของหน้าเลือกวิธีเล่น: แสดงเฉพาะการ์ดหมวดนั้นและไฮไลต์แท็บ
    // ซ่อนหมวดโหมดพิเศษตาม FeatureFlags และซ่อนแถบเริ่มเล่นเมื่ออยู่หมวดฝึกซ้อม (index 2)
    private void SelectModeCategory(int index)
    {
        if (modeCategories == null) return;
        modeCategory = Mathf.Clamp(index, 0, modeCategories.Length - 1);
        for (int i = 0; i < modeCategories.Length; i++)
        {
            modeCategories[i].gameObject.SetActive(i == modeCategory);
            StyleTab(modeCategoryTabs[i], i == modeCategory, true);
        }
        // หมวดโหมดพิเศษ / ฝึกซ้อม ซ่อนได้ทั้งหมวดตาม FeatureFlags
        modeCategoryTabs[1].gameObject.SetActive(FeatureFlags.GameModes && FeatureFlags.MultiPlayer);
        // แถบเริ่มเล่นด้านล่างใช้กับการ์ดโหมด (หมวดฝึกซ้อมกดที่การ์ดเลย)
        bool practice = modeCategory == 2;
        if (soloSelection != null) soloSelection.gameObject.SetActive(!practice);
        if (soloPlayButton != null) soloPlayButton.transform.localScale = practice ? Vector3.zero : Vector3.one;
    }

    // การ์ดที่เป็นปุ่ม SoloChoice เดิม 1 ใบ
    private void ModeCard(int index, RectTransform parent, float x, float y, float w, float h, string art, string title, string sub)
    {
        var card = FindModeCard(index);
        if (card == null) return;
        bool on = card.gameObject.activeSelf;
        card.SetParent(parent, false);
        DressCard(card, x, y, w, h, art, title, sub);
        card.gameObject.SetActive(on);
    }

    // การ์ดที่มีชิปเลือกขนาด (ชิป = ปุ่ม SoloChoice เดิม)
    private void ChipCard(RectTransform parent, string name, float x, float y, float w, float h, string art, string title, int[] modes, string[] chips)
    {
        var card = UIPanel(name, parent, x, y, w, h, CardFrame);
        Place(card.transform, x, y, w, h);
        CardArt(card.transform, w, h, art);
        var band = UIPanel("CardBand", card.transform, 0, -h * .5f + 52, w - 10, 94, new Color(0, 0, 0, .55f));
        Place(band.transform, 0, -h * .5f + 52, w - 10, 94);
        var label = UILabel("CardTitle", card.transform, title, 0, -h * .5f + 80, w - 20, 34, 24, Color.white);
        Place(label.transform, 0, -h * .5f + 80, w - 20, 34);
        int shown = 0;
        float chipW = (w - 30) / modes.Length - 6;
        for (int i = 0; i < modes.Length; i++)
        {
            var chip = FindModeCard(modes[i]);
            if (chip == null) continue;
            chip.SetParent(card.transform, false);
            Place(chip, -w * .5f + 15 + chipW * .5f + i * (chipW + 6), -h * .5f + 30, chipW, 44);
            Place(chip.Find("Label"), 0, 0, chipW - 8, 38);
            var text = chip.GetComponentInChildren<TMP_Text>();
            if (text != null) { text.text = chips[i]; text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 19; }
            if (chip.gameObject.activeSelf) shown++;
        }
        card.gameObject.SetActive(shown > 0);
    }

    // แต่งปุ่มให้เป็นการ์ด: กรอบสี (โหมดที่เลือก = สีเด่น) + รูปเต็มการ์ด + แถบมืดล่าง + ชื่อโหมด
    private void DressCard(Transform card, float x, float y, float w, float h, string art, string title, string sub)
    {
        Place(card, x, y, w, h);
        CardArt(card, w, h, art);
        var band = card.Find("CardBand") as RectTransform;
        if (band == null) { band = UIPanel("CardBand", card, 0, 0, 0, 0, new Color(0, 0, 0, .55f)).rectTransform; }
        band.GetComponent<Image>().raycastTarget = false;
        Place(band, 0, -h * .5f + 42, w - 10, 74);
        band.SetSiblingIndex(1);
        var label = card.Find("Label");
        if (label != null)
        {
            Place(label, 0, -h * .5f + 42, w - 20, 68);
            label.SetAsLastSibling();
            var text = label.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = "<size=125%>" + title + "</size>\n<color=#BFD6E0>" + sub + "</color>";
                text.richText = true; text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 24;
            }
        }
    }

    // รูปการ์ดเต็มพื้นที่ (ตัดส่วนเกิน ไม่ยืดภาพ)
    private void CardArt(Transform card, float w, float h, string art)
    {
        var mask = card.Find("CardArt") as RectTransform;
        if (mask == null)
        {
            mask = UIRect("CardArt", card, 0, 0, 0, 0);
            mask.gameObject.AddComponent<RectMask2D>();
            var image = UIPanel("Art", mask, 0, 0, 0, 0, Color.white);
            image.raycastTarget = false;
            image.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }
        Place(mask, 0, 0, w - 10, h - 10);
        mask.SetAsFirstSibling();
        var picture = mask.Find("Art").GetComponent<Image>();
        var sprite = Resources.Load<Sprite>("Images/Modes/mode_" + art);
        if (sprite == null && mapImages != null && mapImages.Length > 0)
            sprite = Resources.Load<Sprite>(mapImages[(art.Length + art[0]) % mapImages.Length]);
        picture.sprite = sprite;
        picture.color = sprite != null ? new Color(.85f, .85f, .85f) : new Color(.1f, .18f, .28f);
        var fitter = picture.GetComponent<AspectRatioFitter>();
        var rect = picture.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        if (fitter != null && sprite != null) fitter.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
    }

    // ===================== หน้าเตรียมพร้อมรบ (ห้องรอ) =====================
    // ซ้าย: ช่องนักบิน (1v1 = การ์ด 2 ใบ + VS / ทีม = แถวฟ้า, VS, แถวแดง / ตัวต่อตัว = ตาราง)
    // ขวา: ปุ่มตั้งค่าห้อง + ชวนเพื่อน, สถานะการเชื่อมต่อ, รายการแผนที่แนวตั้ง
    // ล่าง: ออกจากห้อง (ซ้าย) / พร้อม / เริ่มรบ (ปุ่มทอง ขวา)
    private const float PrepX = -180f, PrepY = 20f, PrepW = 860f, PrepH = 420f;
    private TMP_Text prepVersus;
    private bool prepLayout;

    // จัดหน้าห้องรอใหม่ (เรียกตอนสร้างหน้าห้องรอ เฉพาะตอนเล่นและเปิด FeatureFlags.NewLobbyLayout): ช่องนักบิน+VS ด้านซ้าย
    // แผงขวามีปุ่มตั้งค่าห้อง/ชวนเพื่อน สถานะ และรายการแผนที่แนวตั้ง ปุ่มออก/พร้อม/เริ่มรบด้านล่าง
    private void LayoutPrepRoom(RectTransform root)
    {
        if (!Application.isPlaying || !FeatureFlags.NewLobbyLayout || root == null) return;
        prepLayout = true;
        // หัวหน้า
        Place(root.Find("Title"), -400, 314, 460, 42);
        Place(root.Find("RoomCode"), 230, 314, 300, 40);
        Place(root.Find("CopyCode"), 500, 314, 200, 48);
        // การ์ด 1 VS 1 แบบตั้ง (รูปยานใหญ่กลางการ์ด)
        PrepPilotCard(root.Find("PilotOne"), PrepX - 230f);
        PrepPilotCard(root.Find("PilotTwo"), PrepX + 230f);
        prepVersus = UILabel("VersusMark", root, "VS", PrepX, PrepY, 120, 60, 46, StartGold);
        Place(prepVersus.transform, PrepX, PrepY, 120, 60);
        prepVersus.enableAutoSizing = false; prepVersus.fontSize = 46;
        prepVersus.raycastTarget = false;
        prepVersus.fontStyle = FontStyles.Bold | FontStyles.Italic;
        if (rosterRoot != null) Place(rosterRoot, PrepX, PrepY, PrepW, PrepH);

        // แผงขวา (เลื่อนลงจากเดิม ~30: เดิมปุ่มตั้งค่าห้อง/ชวนเพื่อนทับเส้นหัวหน้า และข้อความสถานะเชื่อมต่อชิดปุ่มเกินไป)
        const float px = 460f;
        var rules = root.Find("OpenRoomRules");
        Place(rules, px - 82, 220, 156, 48);
        Place(rules != null ? rules.Find("Label") : null, 0, 0, 148, 42);
        if (inviteFriendsButton != null)
        {
            inviteFriendsButton.transform.SetParent(root, false);
            Place(inviteFriendsButton.transform, px + 82, 220, 156, 48);
            Place(inviteFriendsButton.transform.Find("Label"), 0, 0, 148, 42);
        }
        foreach (var b in new[] { rules, inviteFriendsButton != null ? inviteFriendsButton.transform : null })
        {
            var label = b != null ? b.GetComponentInChildren<TMP_Text>() : null;
            if (label != null) { label.enableAutoSizing = true; label.fontSizeMin = 11; label.fontSizeMax = 17; }
        }
        Place(root.Find("Connection"), px, 178, 320, 24);
        var mapsHeading = root.Find("MapsHeading");
        Place(mapsHeading, px, 154, 320, 24);
        var headingText = mapsHeading != null ? mapsHeading.GetComponent<TMP_Text>() : null;
        if (headingText != null) { headingText.enableAutoSizing = true; headingText.fontSizeMin = 10; headingText.fontSizeMax = 15; }
        var panelBack = UIPanel("PrepSidePanel", root, px, -10, 336, 420, new Color(.03f, .06f, .11f, .85f));
        Place(panelBack.transform, px, -22, 336, 424);
        panelBack.raycastTarget = false;
        panelBack.transform.SetSiblingIndex(1);
        for (int i = 0; i < MapChoices; i++)
        {
            var card = root.Find("MapCard" + i);
            if (card == null) continue;
            float y = 110 - i * 69;
            Place(card, px, y, 316, 62);
            Place(card.Find("MapPreview"), -112, 0, 80, 54);
            var preview = card.Find("MapPreview")?.GetComponent<Image>();
            if (preview != null) preview.preserveAspect = true;
            Place(card.Find("MapName"), 38, 12, 216, 28);
            Place(card.Find("SelectionHint"), 38, -16, 216, 22);
            var name = card.Find("MapName")?.GetComponent<TMP_Text>();
            if (name != null) { name.alignment = TextAlignmentOptions.Left; name.enableAutoSizing = true; name.fontSizeMin = 12; name.fontSizeMax = 17; }
            var hint = card.Find("SelectionHint")?.GetComponent<TMP_Text>();
            if (hint != null) hint.alignment = TextAlignmentOptions.Left;
            foreach (string hide in new[] { "Description", "Label" })
            {
                var item = card.Find(hide);
                if (item != null) item.gameObject.SetActive(false);
            }
        }

        // ข้อความสถานะ + ปุ่มล่าง
        Place(root.Find("Message"), PrepX, -226, PrepW, 24);
        Place(root.Find("ReadyHint"), PrepX, -252, PrepW, 26);
        if (waitCancelButton != null) { Place(waitCancelButton.transform, -500, -326, 230, 58); Place(waitCancelButton.transform.Find("Label"), 0, 0, 214, 48); }
        if (waitReadyButton != null) { Place(waitReadyButton.transform, 130, -326, 250, 62); Place(waitReadyButton.transform.Find("Label"), 0, 0, 236, 52); }
        if (waitStartButton != null)
        {
            Place(waitStartButton.transform, 445, -326, 330, 70);
            Place(waitStartButton.transform.Find("Label"), 0, 0, 310, 58);
            var image = waitStartButton.GetComponent<Image>();
            if (image != null) image.color = StartGold;
            var colors = waitStartButton.colors; colors.normalColor = Color.white; colors.disabledColor = new Color(.45f, .45f, .45f); waitStartButton.colors = colors;
            var label = waitStartButton.GetComponentInChildren<TMP_Text>();
            if (label != null) { label.color = new Color(.12f, .08f, .02f); label.fontSize = 26; label.enableAutoSizing = false; }
        }
    }

    // การ์ด 1 VS 1: ตั้งตรง ชื่อบน ยานกลาง ข้อมูลล่าง
    private void PrepPilotCard(Transform card, float x)
    {
        if (card == null) return;
        const float w = 330f, h = 400f; // ความกว้างและสูงของการ์ดนักบิน 1v1
        Place(card, x, PrepY, w, h);
        Place(card.Find("Accent"), 0, h * .5f - 2, w - 6, 4);
        Place(card.Find("PilotName"), 0, 168, w - 20, 36);
        Place(card.Find("ShipPreview"), 0, 62, 230, 170);
        Place(card.Find("ShipName"), 0, -42, w - 20, 34);
        // ค่าพลังแยกสี (ตีบวก/ไอเท็ม) ใช้ 2 บรรทัด จึงขยายช่องและเลื่อนสกิลลงเล็กน้อย
        bool twoLines = FeatureFlags.ItemStatColor;
        Place(card.Find("Stats"), 0, twoLines ? -86 : -78, w - 20, twoLines ? 52 : 28);
        Place(card.Find("Skill"), 0, twoLines ? -124 : -108, w - 20, 28);
        Place(card.Find("ReadyState"), 0, -158, w - 20, 34);
        foreach (string part in new[] { "PilotName", "ShipName", "Stats", "Skill", "ReadyState" })
        {
            var text = card.Find(part)?.GetComponent<TMP_Text>();
            if (text != null) { text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = true; text.fontSizeMin = 11; text.fontSizeMax = part == "PilotName" ? 21 : part == "ShipName" ? 22 : 16; }
            if (text != null && part == "Stats" && twoLines) text.textWrappingMode = TextWrappingModes.NoWrap; // บรรทัดยาว = ย่อตัวอักษร ไม่ตัดคำ
        }
    }

    // แสดง VS เฉพาะ 1v1 และโหมดทีม
    private void PrepVersus(bool show)
    {
        if (prepVersus != null && prepLayout) prepVersus.gameObject.SetActive(show);
    }

    // ช่องนักบินห้องหลายคน: เต็มพื้นที่ซ้าย ทุกช่องแบบตั้ง (ชื่อบน ยานกลาง ชื่อยาน/สถานะล่าง)
    private bool LayoutRosterSlotsPrep(int maxPlayers, bool teams, int teamSize)
    {
        if (!prepLayout || rosterSlots == null) return false;
        int rows = teams ? 2 : (maxPlayers <= 5 ? 1 : 2);
        int cols = teams ? teamSize : Mathf.CeilToInt(maxPlayers / (float)rows);
        const float gap = 14f; // ระยะห่างระหว่างช่องนักบิน
        float w = Mathf.Min(260f, (PrepW - gap * (cols - 1)) / cols);
        float h = rows == 1 ? 300f : teams ? 168f : 196f;
        float rowY = teams ? 116f : 104f;
        for (int i = 0; i < rosterSlots.Length; i++)
        {
            int row, col;
            if (teams) { row = i < 5 ? 0 : 1; col = i % 5; }
            else { row = rows == 1 ? 0 : (i < cols ? 0 : 1); col = rows == 1 ? i : (i < cols ? i : i - cols); }
            float x = (col - (cols - 1) * .5f) * (w + gap);
            float y = rows == 1 ? 0 : (row == 0 ? rowY : -rowY);
            var slot = rosterSlots[i];
            if (slot == null || slot.panel == null) continue;
            Place(slot.panel.transform, x, y, w, h);
            float ship = Mathf.Min(w - 30, h - 96);
            Place(slot.name.transform, 0, h * .5f - 20, w - 12, 30);
            Place(slot.ship.transform, 0, h * .5f - 40 - ship * .5f, ship, ship);
            Place(slot.shipName.transform, 0, -h * .5f + 46, w - 12, 24);
            Place(slot.ready.transform, 0, -h * .5f + 20, w - 12, 24);
            foreach (var text in new[] { slot.name, slot.shipName, slot.ready })
            { text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = text == slot.name ? 16 : 14; }
        }
        return true;
    }
}
