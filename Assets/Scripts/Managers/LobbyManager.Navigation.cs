using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนนำทางของล็อบบี้ (partial): หน้าต่างเลือกวิธีเล่น (PlayMenu), หน้าตั้งค่าห้อง (RoomRulesMenu) และแท็บใน Hangar
public partial class LobbyManager
{
    private RectTransform playMenu, roomRulesMenu; // หน้าต่างเลือกวิธีเล่น และหน้าตั้งค่ากติกาห้อง
    private TMP_Text soloSelection; // ข้อความบอกโหมดเล่นคนเดียวที่เลือกอยู่

    // Only these existing controls move into a navigation window. Find them again
    // on rebuild so scene-authored controls and their positions are reused.
    private RectTransform FindNavigationControl(Transform parent, string name)
    {
        if (parent.name != "LobbySurface") return null;
        if (name == "WorkshopOverlay" && inventoryPanel != null)
        {
            var saved = inventoryPanel.transform.Find("LobbySurface/WorkshopOverlay") as RectTransform;
            if (saved != null) return saved;
        }
        switch (name)
        {
            case "CreateJoin": case "Ranked": case "SoloDifficulty": case "SoloPlay":
            case "Training": case "SoloBots": case "Workshop":
            case "RoomModeButton": case "Mode": case "RoomKills": case "RoomTime":
            case "RoomHazards": case "RoomBots": case "RoomUpgrades":
            case "RoomPowerUps": case "RoomGameMode":
                foreach (var rect in parent.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == name) return rect;
                break;
        }
        return null;
    }

    // Apply a default once, then retain subsequent Scene/Inspector adjustments.
    private void ArrangeNavigation(Transform item, Transform parent, float x, float y, float w, float h)
    {
        if (item == null || item.Find("NavigationLayoutV1") != null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(item, "Arrange navigation");
#endif
        item.SetParent(parent, false);
        var rect = item as RectTransform;
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        var button = item.GetComponent<Button>();
        if (button != null)
        {
            var label = item.Find("Label") as RectTransform;
            if (label != null) { label.anchoredPosition = Vector2.zero; label.sizeDelta = new Vector2(w - 24, h - 12); }
        }
        UiLayout.Placed(rect); // ตำแหน่งที่บันทึกเอง (UiLayout.cs)
        UIRect("NavigationLayoutV1", item, 0, 0, 0, 0);
    }

    // สร้างหน้าต่างเต็มจอ (1280x720) พื้นทึบ มีหัวข้อ + ปุ่ม BACK ที่เรียก close แล้วคืน RectTransform ของหน้าต่างนั้น
    private RectTransform NavigationWindow(string name, RectTransform root, string title, UnityEngine.Events.UnityAction close)
    {
        var overlay = UIPanel(name, root, 0, 0, 1280, 720, new Color(.015f, .025f, .06f, .98f));
        overlay.raycastTarget = true;
        UILabel("Heading", overlay.transform, title, -160, 290, 780, 50, 30, accentColor);
        UIButton("Back", overlay.transform, "BACK", 490, 290, 180, 58, close);
        return overlay.rectTransform;
    }

    // จัดหน้าแรกของล็อบบี้: ย้ายปุ่มเดิมเข้าตำแหน่งใหม่ สร้างปุ่ม CHOOSE MODE และหน้าต่าง PlayMenu ที่มีการ์ดโหมดเล่นกับบอท
    private void BuildHomeNavigation(RectTransform root)
    {
        playMenu = NavigationWindow("PlayMenu", root, "CHOOSE HOW TO PLAY", ClosePlayMenu);
        ArrangeNavigation(playButton.transform, root, 395, 100, 370, 82);
        UIButton("ChooseMode", root, "CHOOSE MODE", 395, 0, 370, 70, OpenPlayMenu);
        ArrangeNavigation(inventoryButton.transform, root, 395, -95, 370, 70);
        SetButtonLabel(inventoryButton, "HANGAR");
        ArrangeNavigation(root.Find("HowToPlay"), root, 395, -180, 370, 58);
        ArrangeNavigation(missionsButton.transform, root, -425, -282, 340, 60);
        ArrangeNavigation(profileButton.transform, root, -65, -282, 340, 60);
        ArrangeNavigation(socialButton.transform, root, 395, -282, 370, 60);
        if (missionsBadge != null) ArrangeNavigation(missionsBadge.transform.parent, missionsButton.transform, 146, 23, 30, 30);
        if (socialBadge != null) ArrangeNavigation(socialBadge.transform.parent, socialButton.transform, 161, 23, 30, 30);
        ArrangeNavigation(statusText.transform, root, -170, -330, 840, 28);
        var footer = root.Find("Footer");
        if (footer != null) footer.gameObject.SetActive(false);
        var title = root.Find("BattleTitle");
        if (title != null) title.gameObject.SetActive(false);
        ArrangeNavigation(createRoomButton.transform, playMenu, -295, 198, 550, 66);
        ArrangeNavigation(rankedButton.transform, playMenu, 295, 198, 550, 66);
        UILabel("SoloHeading", playMenu, "SOLO / SELECT A MODE, THEN START", -100, 128, 930, 36, 21, accentColor);
        string[] descriptions = { "Duel", "Free for all", "Free for all", "Free for all", "Team battle", "Team battle", "Team battle", "Survive waves", "Capture the hill", "Collect stars", "Last ship standing", "Story stages" };
        for (int i = 0; i < SoloModeLabels.Length; i++)
        {
            int mode = i;
            var card = UIButton("SoloChoice" + i, playMenu,
                SoloModeLabels[i] + "\n" + descriptions[i], -435 + (i % 4) * 290, 60 - (i / 4) * 82, 276, 70,
                () => { SoloMode = mode; RefreshSoloButtons(); RefreshModeSelection(); });
            card.gameObject.SetActive(FeatureFlags.Bots && (i == 0 || FeatureFlags.MultiPlayer)
                && (!SoloModeTeams[i] || FeatureFlags.Teams) && (SoloModeGame[i] == 0 || FeatureFlags.GameModes));
        }
        ArrangeNavigation(soloDifficultyButton.transform, playMenu, -435, -208, 276, 60);
        ArrangeNavigation(soloPlayButton.transform, playMenu, -145, -208, 276, 60);
        ArrangeNavigation(trainingButton.transform, playMenu, 290, -208, 560, 60);
        // Keep the legacy cycling control available to its existing refresh logic,
        // inside an inactive holder. Direct mode cards replace its interaction.
        var holder = UIRect("LegacyModeHolder", playMenu, 0, 0, 0, 0);
        ArrangeNavigation(soloBotsButton.transform, holder, 0, 0, 125, 36);
        holder.gameObject.SetActive(false);
        soloSelection = UILabel("Selection", playMenu, "", 0, -285, 1120, 44, 20, Color.white);
        RefreshModeSelection();
        playMenu.gameObject.SetActive(false);

        // Workshop entry lives in Hangar; retain the original modal implementation.
        var workshopHolder = UIRect("WorkshopEntryHolder", root, 0, 0, 0, 0);
        ArrangeNavigation(workshopButton.transform, workshopHolder, 0, 0, 190, 50);
        workshopHolder.gameObject.SetActive(false);
        ApplyHomeRankedLayout(root); // HOW TO PLAY -> RANKED (LobbyManager.Polish.cs)
        ApplyModeCardLayout(); // หน้าเลือกวิธีเล่นแบบการ์ด (LobbyManager.NewLayout.cs)
    }

    // ไฮไลต์การ์ดโหมดที่เลือกอยู่ (SoloMode) และปิดการกดการ์ดระหว่างโหลดโปรไฟล์/รอเข้าห้อง/อยู่ในห้อง พร้อมอัปเดตข้อความ SELECTED
    private void RefreshModeSelection()
    {
        if (playMenu == null) return;
        for (int i = 0; i < SoloModeLabels.Length; i++)
        {
            var card = FindModeCard(i); // การ์ดอาจถูกย้ายเข้าหมวด (LobbyManager.NewLayout.cs)
            if (card != null)
            {
                card.GetComponent<Image>().color = i == SoloMode
                    ? new Color(.10f, .49f, .5f) : new Color(.08f, .26f, .36f);
                card.GetComponent<Button>().interactable = profileLoaded && !pendingSolo && !roomRequestPending && !reconnecting && !Photon.Pun.PhotonNetwork.InRoom;
            }
        }
        if (soloSelection != null) soloSelection.text = "SELECTED: " + SoloModeLabels[SoloMode] + "  /  "
            + (FeatureFlags.FunBotRules && SoloGoalText() != "" ? SoloGoalText() : "Set difficulty, then press VS BOT");
    }

    // เปิดหน้าต่างเลือกวิธีเล่น: รีเฟรชปุ่มโซโล่/แรงก์/การ์ดโหมดก่อน แล้วยกหน้าต่างขึ้นบนสุด
    private void OpenPlayMenu()
    {
        RefreshSoloButtons(); RefreshRankedButton(); RefreshModeSelection();
        playMenu.gameObject.SetActive(true); playMenu.SetAsLastSibling();
        SolidOverlay(playMenu.GetComponent<Image>());
    }
    // ปิดหน้าต่างเลือกวิธีเล่น (ปุ่ม BACK ของ PlayMenu)
    private void ClosePlayMenu() { if (playMenu != null) playMenu.gameObject.SetActive(false); }
    // ปิดหน้าต่างตั้งค่าห้อง (ปุ่ม BACK ของ RoomRulesMenu)
    private void CloseRoomRules() { if (roomRulesMenu != null) roomRulesMenu.gameObject.SetActive(false); }

    // จัดหน้าห้องรอ: สร้างหน้าต่าง ROOM SETTINGS ย้ายปุ่มกติกาห้องทั้ง 8 ปุ่มลงตาราง 2 คอลัมน์ และเพิ่มปุ่มเปิดหน้าต่างนี้
    private void BuildRoomNavigation(RectTransform root)
    {
        roomRulesMenu = NavigationWindow("RoomRulesMenu", root, "ROOM SETTINGS", CloseRoomRules);
        UILabel("Hint", roomRulesMenu, "Host changes rules. Changes require everyone to confirm READY again.", 0, 215, 1120, 50, 20, Color.white);
        Button[] controls = { roomModeButton, roomGameModeButton, roomKillsButton, roomTimeButton,
            roomHazardButton, roomBotButton, roomUpgradesButton, roomPowerUpsButton, roomWinRuleButton };
        string[] names = { "PLAYERS / TEAMS", "GAME MODE", "SCORE TARGET", "TIME LIMIT", "MAP HAZARDS", "FILL WITH BOTS", "SHIP UPGRADES", "POWER-UPS", "WIN CONDITION" };
        for (int i = 0; i < controls.Length; i++)
        {
            float x = i % 2 == 0 ? -285 : 285;
            float y = 130 - (i / 2) * 110;
            UILabel("RuleLabel" + i, roomRulesMenu, names[i], x, y + 38, 520, 26, 17, accentColor);
            if (controls[i] != null) ArrangeNavigation(controls[i].transform, roomRulesMenu, x, y - 7, 520, 58);
        }
        ArrangeNavigation(roomModeLabel.transform, roomModeButton.transform, 0, 0, 480, 42);
        UIButton("OpenRoomRules", root, "ROOM SETTINGS", -400, 246, 370, 54, () => {
            RefreshRoomSettingButtons(); roomRulesMenu.gameObject.SetActive(true); roomRulesMenu.SetAsLastSibling();
            SolidOverlay(roomRulesMenu.GetComponent<Image>()); });
        ArrangeNavigation(inviteFriendsButton.transform, root, 0, 246, 370, 54);
        MatchRoomModeButtonStyle();
        roomRulesMenu.gameObject.SetActive(false);
    }

    // ปุ่ม PLAYERS / TEAMS (เช่น "FFA 6P >>") ใช้ป้ายข้อความแยกสีฟ้าอ่อน ทำให้ดูจืดกว่าปุ่มอื่น
    // ทำให้สีพื้นปุ่ม สีกด และตัวหนังสือ เหมือนปุ่มกติกาอื่น (ใช้ปุ่ม KILLS เป็นต้นแบบ) — ทำตอนเล่นเท่านั้น ไม่แตะค่าที่จัดไว้ใน Scene
    private void MatchRoomModeButtonStyle()
    {
        if (!Application.isPlaying || roomModeButton == null || roomModeLabel == null) return;
        var sample = roomKillsButton != null ? roomKillsButton : roomTimeButton;
        if (sample == null) return;
        var sampleImage = sample.GetComponent<Image>();
        var image = roomModeButton.GetComponent<Image>();
        if (sampleImage != null && image != null) { image.color = sampleImage.color; image.sprite = sampleImage.sprite; image.type = sampleImage.type; image.pixelsPerUnitMultiplier = sampleImage.pixelsPerUnitMultiplier; }
        ButtonSkin.Apply(image); // ถ้าปุ่มต้นแบบแต่งแล้ว ปุ่มนี้ก็แต่งให้เหมือนกัน (ขอบ/เงา)
        roomModeButton.colors = sample.colors;
        var sampleLabel = sample.transform.Find("Label")?.GetComponent<TMP_Text>() ?? sample.GetComponentInChildren<TMP_Text>(true);
        if (sampleLabel != null)
        {
            roomModeLabel.color = sampleLabel.color;
            roomModeLabel.fontStyle = sampleLabel.fontStyle;
            roomModeLabel.enableAutoSizing = sampleLabel.enableAutoSizing;
            roomModeLabel.fontSize = sampleLabel.fontSize;
            roomModeLabel.fontSizeMin = sampleLabel.fontSizeMin;
            roomModeLabel.fontSizeMax = sampleLabel.fontSizeMax;
            if (sampleLabel.font != null) roomModeLabel.font = sampleLabel.font;
        }
    }

    // สร้างแท็บด้านบนของ Hangar: จัดแท็บ SHIPS/SKILLS เดิม และเพิ่มแท็บ UPGRADES/ITEMS/SHOP ที่เปิดหน้า Workshop (ซ่อนตาม FeatureFlags)
    private void BuildHangarNavigation()
    {
        var root = inventoryPanel.transform.Find("LobbySurface") as RectTransform;
        if (root == null) return;
        ArrangeNavigation(root.Find("ShipsTab"), root, -464, 231, 222, 54);
        ArrangeNavigation(root.Find("SkillsTab"), root, -232, 231, 222, 54);
        string[] titles = { "SHIP UPGRADE", "ITEMS", "SHOP" };
        for (int i = 0; i < titles.Length; i++)
        {
            int tab = i;
            var button = UIButton("WorkshopTab" + i, root, titles[i], i * 232, 231, 222, 54, () => OpenHangarWorkshop(tab));
            button.gameObject.SetActive(i == 0 ? FeatureFlags.Upgrades : FeatureFlags.Items);
        }
    }

#if UNITY_EDITOR
    // เมนูคลิกขวาใน Editor: สร้างหน้าล็อบบี้แล้วเปิดหน้าเลือกโหมดเพื่อดูตัวอย่าง UI
    [ContextMenu("UI Preview/Show Mode Selection")]
    private void PreviewModeSelection() { BuildEditableLobbyScreens(); OpenPlayMenu(); }
    // เมนูคลิกขวาใน Editor: สร้างหน้าล็อบบี้ แสดงห้องรอ แล้วเปิดหน้าตั้งค่าห้องเพื่อดูตัวอย่าง UI
    [ContextMenu("UI Preview/Show Room Rules")]
    private void PreviewRoomRules() { BuildEditableLobbyScreens(); ShowLobbyEditorPreview(waitingRoomPanel); roomRulesMenu.gameObject.SetActive(true); roomRulesMenu.SetAsLastSibling(); }
#endif
}
