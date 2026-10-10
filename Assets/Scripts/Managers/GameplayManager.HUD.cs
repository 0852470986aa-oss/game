// GameplayManager.HUD.cs — ส่วน HUD ของ partial class GameplayManager (Scene SampleScene)
// สร้าง/ผูก UI ระหว่างแข่ง: ป้ายชื่อเหนือยาน เวลา/คะแนน ปุ่มควบคุม (จอย/ยิง/สกิล) อีโมต และหน้าผลการแข่ง
// ถ้าใน Scene มี BattleHUD ที่บันทึกไว้ (เมนู Editable) จะผูกของเดิม ไม่งั้นสร้างด้วยโค้ดตอนรัน
// เรียกจาก Start/Update ใน GameplayManager.cs; ใช้ UIJoystick, UIButton, ControlRingGraphic, EditableTemplate, BattleSettingsPanel
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

// ส่วน HUD ของ GameplayManager; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
// (partial) คลาสเดียวกับ GameplayManager.cs
public partial class GameplayManager
{
    // battleHud = กรอบ HUD หลัก (ขนาดออกแบบ 1280x720), shipHudLayer = ชั้นวางป้ายชื่อเหนือยาน
    private RectTransform battleHud;
    private RectTransform shipHudLayer;
    // ข้อมูลป้ายชื่อเหนือยาน 1 ลำ: ยานที่ติดตาม, กรอบ, ชื่อ, แถบเลือด, ข้อความอีโมต และเวลาที่อีโมตจะหาย
    private sealed class ShipNameplate
    {
        public PlayerController ship; // ยานที่ป้ายนี้ติดตามตำแหน่ง
        public RectTransform root; // กรอบหลักของป้าย วางเหนือยานบนชั้น shipHudLayer
        public TMP_Text name; // ข้อความชื่อผู้เล่นบนป้าย
        public Image fill; // แถบเลือดของยาน (ปรับ fillAmount ตาม HP)
        public TMP_Text emote; // ข้อความอีโมตที่ผู้เล่นส่ง แสดงเหนือป้าย
        public float emoteUntil; // เวลาที่อีโมตจะถูกซ่อน
    }
    // ป้ายชื่อทั้งหมด (key = InstanceID ของยาน) และรายการ key ของยานที่ถูกลบแล้ว รอเอาออก
    private readonly System.Collections.Generic.Dictionary<int, ShipNameplate> shipNameplates = new System.Collections.Generic.Dictionary<int, ShipNameplate>();
    private readonly System.Collections.Generic.List<int> staleNameplates = new System.Collections.Generic.List<int>();
    // เวลาถัดไปที่จะสแกนหายานใหม่ (ทุก 0.5 วินาที)
    private float nextShipHudScan;

    // LateUpdate: ทุกเฟรมหลังยานขยับแล้ว — วางป้ายชื่อ+แถบเลือดลอยเหนือยานทุกลำ (ทำในเครื่องตัวเอง ไม่เกี่ยวกับเครือข่าย)
    private void LateUpdate()
    {
        if (shipHudLayer == null || !battleHud.gameObject.activeInHierarchy) return;
        // 1) ทุก 0.5 วิ หายานที่ยังไม่มีป้าย แล้วสร้างป้าย (คัดลอกต้นแบบใน Scene หรือสร้างด้วยโค้ด)
        if (Time.unscaledTime >= nextShipHudScan)
        {
            nextShipHudScan = Time.unscaledTime + .5f;
            foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                int key = ship.GetInstanceID();
                if (shipNameplates.ContainsKey(key)) continue;
                var plate = SpawnShipNameplateFromTemplate("ShipNameplate_" + key)
                    ?? CreateShipNameplate("ShipNameplate_" + key);
                plate.ship = ship;
                shipNameplates[key] = plate;
            }
        }
        // 2) ไม่มีกล้องหลัก = ซ่อนชั้นป้ายชื่อ
        var view = Camera.main;
        if (view == null) { shipHudLayer.gameObject.SetActive(false); return; }
        shipHudLayer.gameObject.SetActive(true);
        var canvas = battleHud.GetComponentInParent<Canvas>();
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        // 3) อัปเดตแต่ละป้าย: ยานหายไป -> ลบป้าย, ยานตาย/อยู่นอกจอ -> ซ่อน, ไม่งั้นวางเหนือยาน 36 หน่วย UI
        // ตั้งชื่อ (ของเรามี "YOU / ") และสีแถบเลือด: < 30% แดง, < 60% เหลือง, นอกนั้นเขียว (เรา) / ส้มแดง (ศัตรู)
        staleNameplates.Clear();
        foreach (var entry in shipNameplates)
        {
            var plate = entry.Value;
            if (plate.ship == null) { Destroy(plate.root.gameObject); staleNameplates.Add(entry.Key); continue; }
            Vector3 center = view.WorldToViewportPoint(plate.ship.transform.position);
            bool visible = !plate.ship.isDead && !plate.ship.HasMatchEnded && !plate.ship.HiddenFromLocalViewer && center.z > 0
                && center.x >= 0 && center.x <= 1 && center.y >= 0 && center.y <= 1;
            plate.root.gameObject.SetActive(visible);
            if (!visible) continue;
            // Follow the ship pivot, not its rotating bounds: a fixed screen-space offset avoids bobbing.
            Vector3 screen = view.WorldToScreenPoint(plate.ship.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(shipHudLayer, screen, uiCamera, out Vector2 point);
            plate.root.anchoredPosition = point + Vector2.up * 36;
            // Keep names upright and a constant UI size regardless of ship rotation or camera zoom.
            bool mine = plate.ship == localPlayer;
            string nickname = plate.ship.PilotName;
            plate.name.text = (mine ? "YOU / " : "") + nickname;
            // โหมดทีม: ชื่อเป็นสีทีม (BLUE ฟ้า / RED แดง) และแถบเลือดเพื่อนร่วมทีมเป็นสีเขียว
            int plateTeam = isTeamMode ? MatchRules.TeamOf(plate.ship.CombatantId) : -1;
            bool ally = mine || (plateTeam >= 0 && localPlayer != null && MatchRules.IsAlly(plate.ship.CombatantId, localPlayer.CombatantId));
            plate.name.color = mine ? new Color(.72f, 1f, .95f) : plateTeam >= 0 ? MatchRules.TeamColors[plateTeam] : new Color(1f, .82f, .72f);
            float hp = Mathf.Clamp01(plate.ship.currentHp / Mathf.Max(1, plate.ship.maxHp));
            plate.fill.rectTransform.localScale = new Vector3(hp, 1, 1);
            plate.fill.color = hp < .3f ? new Color(1f, .25f, .2f) : hp < .6f ? new Color(1f, .8f, .2f)
                : ally ? new Color(.2f, 1f, .7f) : new Color(1f, .45f, .35f);
            if (plate.emote != null) plate.emote.gameObject.SetActive(Time.unscaledTime < plate.emoteUntil);
        }
        // 4) เอา key ของยานที่ถูกลบออกจาก Dictionary (ลบระหว่าง foreach ไม่ได้ จึงเก็บไว้ลบทีหลัง)
        foreach (int key in staleNameplates) shipNameplates.Remove(key);
    }

    // ป้ายชื่อเหนือยาน (สร้างด้วยโค้ดแบบเดิม)
    private ShipNameplate CreateShipNameplate(string objectName)
    {
        var root = BattleRect(objectName, shipHudLayer, 0, 0, 156, 38);
        var name = BattleLabel("Pilot", root, "", 0, 11, 156, 22, 15);
        name.richText = false;
        name.outlineWidth = .28f;
        name.outlineColor = new Color(0, 0, 0, 1f);
        var track = BattlePanel("HullTrack", root, 0, -7, 112, 8, new Color(.015f, .025f, .04f, .8f));
        var fill = BattlePanel("HullFill", track.transform, -54, 0, 108, 4, Color.cyan);
        fill.rectTransform.pivot = new Vector2(0, .5f);
        var emote = CreateEmoteBubble(root);
        return new ShipNameplate { root = root, name = name, fill = fill, emote = emote };
    }

    // ถ้ามีต้นแบบ ShipNameplate_Template ใน Scene ให้คัดลอกต้นแบบนั้น (หน้าตาตามที่แก้ในหน้า Edit)
    private ShipNameplate SpawnShipNameplateFromTemplate(string objectName)
    {
        var template = shipHudLayer.Find(ShipNameplateTemplateName);
        if (template == null) return null;
        var root = (RectTransform)EditableTemplate.Spawn(template.gameObject, shipHudLayer).transform;
        root.name = objectName;
        var name = FindPart<TMP_Text>(root, "Pilot");
        var fill = FindPart<Image>(root, "HullTrack/HullFill");
        if (name != null && fill != null)
        {
            var emote = FindPart<TMP_Text>(root, "Emote");
            if (emote == null) emote = CreateEmoteBubble(root);
            emote.gameObject.SetActive(false);
            return new ShipNameplate { root = root, name = name, fill = fill, emote = emote };
        }
        Destroy(root.gameObject);
        return null;
    }
    // อ้างอิง UI หน้าผล (กรอบ / หัวข้อ / คะแนน) และ resultShown = เปิดหน้าผลแล้ว (กันทำซ้ำ)
    private RectTransform resultSurface;
    private TMP_Text resultHeadline, resultScore;
    private bool resultShown;
    // ข้อความสถานะสกิลและชื่อคู่แข่ง, แผงสถานะการต่อสู้ 6 ช่อง, mask สถานะของเฟรมก่อน (-1 = ยังไม่เคยวาด)
    private TMP_Text battleSkillStatus, battleRivalName;
    private Image combatStatusPanel;
    private TMP_Text[] combatStatusLabels;
    private int previousCombatStatus = -1;

    // สร้าง RectTransform ใหม่ใต้ parent จุดยึดกึ่งกลาง ที่ตำแหน่ง (x, y) ขนาด w x h — ตัวช่วยพื้นฐานของการสร้าง HUD ด้วยโค้ด
    private RectTransform BattleRect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        RegisterEditorCreated(rect.gameObject);
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        if (Application.isPlaying) UiLayout.Placed(rect); // ตำแหน่งที่บันทึกเอง (UiLayout.cs)
        return rect;
    }

    // ---------------- HUD ที่บันทึกลง Scene (แก้ในหน้า Edit ได้) ----------------
    // ถ้าใน Scene มี BattleHUD อยู่แล้ว (สร้างด้วยเมนู Editable/1 บน GameplayManager)
    // โค้ดจะ "หยิบของเดิมมาใช้" แทนการสร้างใหม่ ตำแหน่ง/ขนาด/สี/ฟอนต์จึงเป็นตามที่จัดไว้ในหน้า Edit
    // ถ้าไม่มี BattleHUD ใน Scene โค้ดจะสร้างทุกอย่างเองเหมือนเดิมทุกประการ
    // ชื่อต้นแบบใน Scene ที่คัดลอกตอนรัน (ป้ายชื่อยาน / ข้อความ KILL) และ hudAuthored = ใช้ HUD ที่บันทึกใน Scene อยู่
    private const string ShipNameplateTemplateName = "ShipNameplate_Template";
    private const string KillMessageTemplateName = "KillMessage_Template";
    private bool hudAuthored;

    // หา Component ชนิด T ของลูกตาม path ใต้ root (ไม่เจอคืน null)
    private static T FindPart<T>(Transform root, string path) where T : Component
    {
        if (root == null) return null;
        var part = root.Find(path);
        return part != null ? part.GetComponent<T>() : null;
    }

    // ดึง Component ชนิด T ถ้ายังไม่มีให้เพิ่มเข้าไป
    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    // หาวัตถุ "BattleHUD" ใต้ Canvas ใน Scene เดียวกัน (รวมตัวที่ปิดอยู่) ไม่เจอคืน null
    private Transform FindAuthoredBattleHud()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.gameObject.scene != gameObject.scene) continue;
            var hud = canvas.transform.Find("BattleHUD");
            if (hud != null) return hud;
        }
        return null;
    }

    // ตอนสร้าง UI ในหน้า Edit (ไม่ได้กด Play) ให้ลงทะเบียน Undo เพื่อย้อนได้ ตอนรันเกมไม่ทำอะไร
    private static void RegisterEditorCreated(GameObject created)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(created, "Build Editable Battle UI");
#endif
    }

    // สร้างแผงสี่เหลี่ยม Image สีเดียว (ไม่รับการแตะ)
    private Image BattlePanel(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var img = BattleRect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // สร้างข้อความ TextMeshPro จัดกลาง ย่อขนาดอัตโนมัติ (12 ถึง size) ไม่ตัดบรรทัด ใช้ฟอนต์เดียวกับตัวจับเวลา
    private TMP_Text BattleLabel(string name, Transform parent, string value, float x, float y, float w, float h, int size)
    {
        var label = BattleRect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        if (matchTimerText != null) label.font = matchTimerText.font;
        label.text = value;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12;
        label.fontSizeMax = size;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    // ย้าย UI ที่มีอยู่แล้ว (ปุ่ม/ข้อความ) มาไว้ใต้ battleHud ที่ตำแหน่ง (x, y) และปรับ scale
    private void PlaceBattleControl(Transform control, float x, float y, float scale = 1f)
    {
        if (control == null) return;
        control.SetParent(battleHud, false);
        var rect = control.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.localScale = Vector3.one * scale;
        UiLayout.Placed(rect);
    }

    // สร้างการ์ดชื่อ+แถบเลือดของเรา (local) หรือคู่แข่ง ด้านบนจอ (ไม่พบการเรียกใช้ในโค้ดปัจจุบัน)
    private void BuildBattleHealth(string name, float x, bool local)
    {
        var card = BattlePanel(name, battleHud, x, 295, 360, 94, new Color(0.035f, 0.065f, 0.12f, 0.9f));
        var title = BattleLabel("Pilot", card.transform, local ? "YOUR SHIP" : "WAITING FOR RIVAL", 0, 24, 332, 30, 21);
        title.richText = false;
        if (local) playerInfoText = title; else battleRivalName = title;
        var bar = BattlePanel("Hull", card.transform, 0, -14, 328, 25, new Color(0.12f, 0.15f, 0.22f));
        var fill = BattlePanel("Fill", bar.transform, 0, 0, 328, 25, local ? Color.cyan : new Color(1f, 0.4f, 0.5f));
        fill.rectTransform.pivot = new Vector2(0, 0.5f);
        fill.rectTransform.anchoredPosition = new Vector2(-164, 0);
        fill.rectTransform.localScale = new Vector3(0, 1, 1);
        var hp = BattleLabel("HullValue", bar.transform, "-- / --", 0, 0, 310, 25, 17);
        if (local) { p1HpText = hp; p1HpFill = fill.rectTransform; }
        else { p2HpText = hp; p2HpFill = fill.rectTransform; }
    }

    // สร้าง HUD ต่อสู้ด้วยโค้ด (เรียกจาก Start หลัง CreateMatchUI) ถ้ามี HUD ที่บันทึกใน Scene จะไปผูกของเดิมแทน
    // ซ่อน HUD เลือดแบบเก่า จัดเวลา/คะแนน/Ping ไว้ด้านบน สร้างแผงสถานะ ปุ่มควบคุม และปุ่มตั้งค่า
    private void StyleBattleHUD()
    {
        if (matchTimerText == null || battleHud != null) return;
        if (hudAuthored) { BindAuthoredBattleHUD(); return; }
        // 1) ซ่อน Player1HUD / Player2HUD แบบเก่า
        var canvas = matchTimerText.canvas;
        foreach (string name in new[] { "Player1HUD", "Player2HUD" })
        {
            var old = canvas.transform.Find(name);
            if (old != null) old.gameObject.SetActive(false);
        }
        // 2) สร้างกรอบ BattleHUD 1280x720 และชั้นป้ายชื่อ
        battleHud = BattleRect("BattleHUD", canvas.transform, 0, 0, 1280, 720);
        // Keep the results dialog above the in-match controls.
        if (resultPanel != null) resultPanel.transform.SetAsLastSibling();
        shipHudLayer = BattleRect("ShipNameplates", battleHud, 0, 0, 1280, 720);
        // Preserve the legacy canvas reference used by the minimap without showing a fixed player card.
        if (playerInfoText != null) playerInfoText.gameObject.SetActive(false);
        playerInfoText = BattleLabel("PlayerCanvasReference", battleHud, "", 0, 0, 1, 1, 12);
        playerInfoText.gameObject.SetActive(false);
        p1HpText = p2HpText = null;
        p1HpFill = p2HpFill = null;
        // 3) แผงสถานะการต่อสู้ด้านล่าง (6 ช่อง ซ่อนไว้ก่อน)
        combatStatusPanel = BattlePanel("CombatStatuses", battleHud, 0, -264, 640, 86, new Color(.035f, .065f, .12f, .85f));
        combatStatusLabels = new TMP_Text[6];
        for (int i = 0; i < combatStatusLabels.Length; i++)
        {
            combatStatusLabels[i] = BattleLabel("State" + i, combatStatusPanel.transform, "", 0, 0, 198, 32, 17);
            combatStatusLabels[i].gameObject.SetActive(false);
        }
        combatStatusPanel.gameObject.SetActive(false);
        // 4) ด้านบนกลางจอ: แผ่นพื้นหลัง เวลา คะแนน เป้าหมาย "FIRST TO {targetKills} KILLS" และ Ping
        BattlePanel("MatchPlate", battleHud, 0, 291, 320, 102, new Color(0.035f, 0.065f, 0.12f, 0.25f));
        PlaceBattleControl(matchTimerText.transform, 0, 314);
        matchTimerText.fontSize = 32;
        matchTimerText.text = "--:--";
        matchTimerText.raycastTarget = false;
        PlaceBattleControl(scoreText.transform, 0, 281);
        scoreText.fontSize = 20;
        scoreText.color = new Color(0.23f, 0.82f, 0.92f);
        scoreText.text = "YOU  0  :  0  RIVAL";
        scoreText.raycastTarget = false;
        BattleLabel("Objective", battleHud, "FIRST TO " + targetKills + " KILLS", 0, 252, 360, 24, 14);
        if (pingText != null)
        {
            PlaceBattleControl(pingText.transform, 0, 218);
            pingText.fontSize = 14;
            pingText.color = new Color(0.65f, 0.75f, 0.85f);
            pingText.raycastTarget = false;
        }
        // 5) เปลี่ยนปุ่มออกเป็นปุ่ม SETTINGS สร้างปุ่มควบคุม แล้วปรับขนาดให้พอดีจอ
        HookSettingsButton(canvas.transform, true);
        BuildBattleControls();
        FitBattleHUD();
    }

    // หาปุ่ม TopCenter/Btn_Exit ใน Canvas แล้วเปลี่ยนให้กดแล้วเปิด BattleSettingsPanel แทน
    // คำสั่งเดิมของปุ่ม (onClick ที่ตั้งใน Inspector) ถูกเก็บไว้ แล้วส่งเป็น callback ให้ BattleSettingsPanel.Show
    private void HookSettingsButton(Transform canvas, bool setLabel)
    {
        var exit = canvas.Find("TopCenter/Btn_Exit");
        if (exit == null) return;
        // Keep this button in the authored Canvas so its RectTransform can be adjusted in the Scene view.
        var button = exit.GetComponent<Button>();
        var label = exit.GetComponentInChildren<TMP_Text>();
        if (setLabel && label != null) label.text = "SETTINGS";
#if UNITY_EDITOR
        // ตอนบันทึกลง Scene ห้ามแตะ onClick ที่ตั้งไว้ใน Inspector (ไม่งั้นปุ่มออกจะหายตอนรัน)
        if (!Application.isPlaying) return;
#endif
        if (button != null)
        {
            var leave = button.onClick;
            hudSettingsButton = button; // ปุ่มย้อนกลับมือถือ/Esc ในสนามรบกดปุ่มนี้แทน (GameplayManager.Settings.cs)
            button.onClick = new Button.ButtonClickedEvent();
            // กด LEAVE: ทำคำสั่งเดิมของปุ่ม แล้วถ้าคำสั่งเดิมไม่ได้พาออก (ไม่ได้ตั้งไว้/ตั้งผิด) ออกด้วย LeaveRoom เอง
            button.onClick.AddListener(() => BattleSettingsPanel.Show(() => { leave.Invoke(); if (!intentionalLeave && this != null) LeaveRoom(); }));
        }
    }

    // ใช้ BattleHUD ที่บันทึกไว้ใน Scene: แค่ผูกตัวแปรกับวัตถุเดิม ไม่ย้าย/ไม่เปลี่ยนสีอะไร
    private void BindAuthoredBattleHUD()
    {
        battleHud = (RectTransform)FindAuthoredBattleHud();
        // 1) ซ่อน HUD เลือดแบบเก่า และหาชั้นป้ายชื่อ (ไม่มีก็สร้าง)
        var canvas = battleHud.parent;
        foreach (string name in new[] { "Player1HUD", "Player2HUD" })
        {
            var old = canvas.Find(name);
            if (old != null) old.gameObject.SetActive(false);
        }
        if (resultPanel != null) resultPanel.transform.SetAsLastSibling();
        shipHudLayer = battleHud.Find("ShipNameplates") as RectTransform;
        if (shipHudLayer == null) shipHudLayer = BattleRect("ShipNameplates", battleHud, 0, 0, 1280, 720);
        // ตัวอ้างอิง Canvas สำหรับมินิแม็พ สร้างตอนรันเสมอเหมือนเดิม
        if (playerInfoText != null) playerInfoText.gameObject.SetActive(false);
        var oldReference = battleHud.Find("PlayerCanvasReference");
        if (oldReference != null) Destroy(oldReference.gameObject);
        playerInfoText = BattleLabel("PlayerCanvasReference", battleHud, "", 0, 0, 1, 1, 12);
        playerInfoText.gameObject.SetActive(false);
        p1HpText = p2HpText = null;
        p1HpFill = p2HpFill = null;

        // 2) ผูกแผงสถานะการต่อสู้ 6 ช่อง (ช่องไหนหายก็สร้างเพิ่ม)
        combatStatusPanel = FindPart<Image>(battleHud, "CombatStatuses");
        if (combatStatusPanel != null)
        {
            combatStatusLabels = new TMP_Text[6];
            for (int i = 0; i < combatStatusLabels.Length; i++)
            {
                combatStatusLabels[i] = FindPart<TMP_Text>(combatStatusPanel.transform, "State" + i);
                if (combatStatusLabels[i] == null)
                    combatStatusLabels[i] = BattleLabel("State" + i, combatStatusPanel.transform, "", 0, 0, 198, 32, 17);
                combatStatusLabels[i].gameObject.SetActive(false);
            }
            combatStatusPanel.gameObject.SetActive(false);
        }

        // 3) ตั้งค่าเริ่มของเวลา/คะแนน/เป้าหมาย และผูกปุ่มตั้งค่า
        matchTimerText.text = "--:--";
        scoreText.text = "YOU  0  :  0  RIVAL";
        var objective = FindPart<TMP_Text>(battleHud, "Objective");
        if (objective != null) objective.text = "FIRST TO " + targetKills + " KILLS";
        ApplyModeHudText(); // โหมดอื่นใช้ข้อความของโหมดนั้น (GameplayManager.Modes.cs)
        HookSettingsButton(canvas, false);

        // ส่วนที่โผล่เฉพาะบางจังหวะ: เริ่มแบบซ่อนไว้ แล้วโค้ดเดิมจะเปิด/ปิดเอง
        battleCountdownText = FindPart<TMP_Text>(battleHud, "BattleCountdown");
        hitConfirmationText = FindPart<TMP_Text>(battleHud, "HitConfirmation");
        if (hitConfirmationText != null) hitConfirmationText.gameObject.SetActive(false);
        damageDirectionRoot = battleHud.Find("IncomingDamage") as RectTransform;
        damageDirectionMarker = FindPart<Image>(damageDirectionRoot, "Direction");
        if (damageDirectionRoot != null)
        {
            damageDirectionRoot.gameObject.SetActive(false);
            if (damageDirectionMarker == null) damageDirectionRoot = null;
        }
        respawnPanel = FindPart<Image>(battleHud, "RespawnPanel");
        if (respawnPanel != null)
        {
            respawnReasonText = FindPart<TMP_Text>(respawnPanel.transform, "Reason");
            respawnTimerText = FindPart<TMP_Text>(respawnPanel.transform, "Countdown");
            respawnPanel.gameObject.SetActive(false);
            if (respawnReasonText == null || respawnTimerText == null) respawnPanel = null;
        }

        // 4) ผูกปุ่มควบคุม แล้วปรับขนาดให้พอดีจอ
        BindAuthoredBattleControls();
        FitBattleHUD();
    }

    // ผูกปุ่มควบคุมจาก HUD ที่บันทึกใน Scene: เก็บ CanvasGroup (ปรับความจาง) วงแหวน หัวจอย ไอคอน/ข้อความสกิล
    // เปิดการลากเล็งที่ปุ่มยิง และถ้าใน Scene ไม่มีปุ่มอีโมตจะสร้างให้
    private void BindAuthoredBattleControls()
    {
        // 1) จอยสติ๊ก
        if (joystick != null)
        {
            moveControlOpacity = GetOrAdd<CanvasGroup>(joystick.gameObject);
            moveControlRing = FindPart<ControlRingGraphic>(joystick.transform, "OuterRing");
            moveThumb = FindPart<ControlRingGraphic>(joystick.transform, "JoystickHandle/ThumbFill");
        }
        // 2) ปุ่มยิง (ลากเพื่อเล็ง)
        if (fireButton != null)
        {
            fireControlOpacity = GetOrAdd<CanvasGroup>(fireButton.gameObject);
            fireButton.enableDragAim = true;
            var aim = fireButton.transform.Find("AimThumb") as RectTransform;
            if (aim != null) fireButton.aimHandle = aim;
            fireControlRing = FindPart<ControlRingGraphic>(fireButton.transform, "OuterRing");
            fireThumb = FindPart<ControlRingGraphic>(fireButton.transform, "AimThumb/Reticle");
        }
        // 3) ปุ่มสกิล
        if (skillButton != null)
        {
            skillControlOpacity = GetOrAdd<CanvasGroup>(skillButton.gameObject);
            var icon = FindPart<Image>(skillButton.transform, "EquippedSkillIcon");
            if (icon != null)
            {
                skillIconImage = icon;
                skillIconImage.enabled = false;
            }
            skillCooldownImage = null;
            skillControlRing = FindPart<ControlRingGraphic>(skillButton.transform, "CooldownProgress");
            battleSkillName = FindPart<TMP_Text>(skillButton.transform, "AbilityName");
            battleSkillStatus = FindPart<TMP_Text>(skillButton.transform, "AbilityState");
        }
        // ถ้าใช้ภาพปุ่มจาก Resources (AuthoredBase) ระยะเลื่อนปุ่มต้องเท่าเดิม
        if (joystick != null && joystick.transform.Find("AuthoredBase") != null)
        {
            joystick.handleTravelFraction = .1f;
            moveControlRing = moveThumb = null;
        }
        if (fireButton != null && fireButton.transform.Find("AuthoredBase") != null)
        {
            fireButton.handleTravelFraction = .09f;
            fireControlRing = fireThumb = null;
        }
        // HUD ที่บันทึกไว้ก่อนมีอีโมต: สร้างปุ่มอีโมตเพิ่มให้ตอนรัน
        var emoteToggle = battleHud.Find("EmoteButton");
        var emoteItems = battleHud.Find("EmoteMenu");
        if (emoteToggle != null && emoteItems != null) WireEmoteControls(emoteToggle.gameObject, emoteItems);
        else BuildEmoteControls();
    }

    // ---------------- อีโมตระหว่างแข่ง ----------------
    // เมนูเลือกอีโมต (เปิด/ปิดด้วยปุ่ม EMOTE)
    private GameObject emoteMenu;

    // สร้างข้อความอีโมตสีเหลืองขอบดำเหนือป้ายชื่อ (ซ่อนไว้ก่อน)
    private TMP_Text CreateEmoteBubble(RectTransform plateRoot)
    {
        var emote = BattleLabel("Emote", plateRoot, "", 0, 42, 220, 30, 20);
        emote.color = new Color(1f, .92f, .45f);
        emote.outlineWidth = .3f;
        emote.outlineColor = new Color(0, 0, 0, 1f);
        emote.gameObject.SetActive(false);
        return emote;
    }

    // สร้างปุ่ม EMOTE ทางขวาของจอ และเมนูรายการอีโมตตาม PlayerController.Emotes แล้วผูกการกด
    private void BuildEmoteControls()
    {
        var toggle = BattlePanel("EmoteButton", battleHud, 553, 92, 104, 44, new Color(.06f, .14f, .22f, .85f));
        BattleLabel("Label", toggle.transform, "EMOTE", 0, 0, 96, 40, 17);
        // เมนูเรียงขึ้นด้านบนปุ่ม EMOTE (มุมขวาบนว่าง ไม่ทับปุ่มยิง/สกิล)
        var menu = BattleRect("EmoteMenu", battleHud, 553, 236, 160, 230);
        for (int i = 0; i < PlayerController.Emotes.Length; i++)
        {
            var item = BattlePanel("Emote" + i, menu, 0, -92 + i * 54, 156, 46, new Color(.08f, .26f, .36f, .95f));
            BattleLabel("Label", item.transform, PlayerController.Emotes[i], 0, 0, 146, 40, 18);
        }
        menu.gameObject.SetActive(false);
        WireEmoteControls(toggle.gameObject, menu);
    }

    // ผูกการกด: ปุ่ม EMOTE เปิด/ปิดเมนู, กดรายการที่ i แล้วปิดเมนูและให้ยานเราส่งอีโมตผ่าน TrySendEmote(i)
    private void WireEmoteControls(GameObject toggle, Transform menu)
    {
        emoteMenu = menu.gameObject;
        emoteMenu.SetActive(false);
        var toggleImage = toggle.GetComponent<Image>();
        if (toggleImage != null) toggleImage.raycastTarget = true;
        var toggleButton = GetOrAdd<Button>(toggle);
        toggleButton.targetGraphic = toggleImage;
        toggleButton.onClick.AddListener(() => emoteMenu.SetActive(!emoteMenu.activeSelf));
        for (int i = 0; i < PlayerController.Emotes.Length; i++)
        {
            var item = menu.Find("Emote" + i);
            if (item == null) continue;
            int index = i;
            var itemImage = item.GetComponent<Image>();
            if (itemImage != null) itemImage.raycastTarget = true;
            var itemButton = GetOrAdd<Button>(item.gameObject);
            itemButton.targetGraphic = itemImage;
            itemButton.onClick.AddListener(() =>
            {
                emoteMenu.SetActive(false);
                if (localPlayer != null) localPlayer.TrySendEmote(index);
            });
        }
    }

    // เรียกจาก RPC: แสดงข้อความเหนือยานผู้ส่ง 2.5 วินาที
    public void ShowEmote(PlayerController ship, string text)
    {
        if (ship == null || shipHudLayer == null) return;
        if (!GameSettings.Emotes && ship != localPlayer) return; // เฟส 9: ผู้เล่นปิดอีโมตของคนอื่นได้
        if (!shipNameplates.TryGetValue(ship.GetInstanceID(), out var plate))
        {
            // ป้ายชื่อยังไม่ถูกสร้าง (สแกนทุก 0.5 วิ): สร้างทันที
            plate = SpawnShipNameplateFromTemplate("ShipNameplate_" + ship.GetInstanceID())
                ?? CreateShipNameplate("ShipNameplate_" + ship.GetInstanceID());
            plate.ship = ship;
            shipNameplates[ship.GetInstanceID()] = plate;
        }
        if (plate.emote == null) plate.emote = CreateEmoteBubble(plate.root);
        plate.emote.text = text;
        plate.emoteUntil = Time.unscaledTime + 2.5f;
        plate.emote.gameObject.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_Emote"); // ปิด NewSounds = เสียงปุ่มแบบเดิม
    }

    // วงแหวน/หัวจอยที่เปลี่ยนสีตามการกด, ข้อความชื่อสกิล, CanvasGroup ของแต่ละปุ่ม (ทำให้ปุ่มจางตอนกดไม่ได้)
    private ControlRingGraphic moveControlRing, fireControlRing, skillControlRing, moveThumb, fireThumb;
    private TMP_Text battleSkillName;
    private CanvasGroup moveControlOpacity, fireControlOpacity, skillControlOpacity;
    // สีประจำปุ่ม: ฟ้า = เคลื่อนที่, ส้ม = ยิง, ม่วง = สกิล
    private static readonly Color MoveAccent = new Color(.3f, .88f, 1f);
    private static readonly Color FireAccent = new Color(1f, .65f, .27f);
    private static readonly Color SkillAccent = new Color(.77f, .64f, 1f);

    // สร้างวงกลม/วงแหวน (ControlRingGraphic) เส้นผ่านศูนย์กลาง diameter, innerRatio = สัดส่วนรูด้านใน
    private ControlRingGraphic ControlCircle(string name, Transform parent, float diameter, float innerRatio, Color tint)
    {
        var graphic = BattleRect(name, parent, 0, 0, diameter, diameter).gameObject.AddComponent<ControlRingGraphic>();
        graphic.raycastTarget = false;
        graphic.color = tint;
        graphic.SetRing(innerRatio);
        return graphic;
    }

    // ย้ายปุ่มควบคุมเดิมมาไว้ใน battleHud กำหนดขนาด ปิดภาพเก่าทั้งหมด แล้วใส่ Image โปร่งใสไว้รับการแตะ
    // คืน CanvasGroup ไว้ปรับความจางของปุ่ม
    private CanvasGroup PrepareControl(Transform control, float x, float y, float size)
    {
        PlaceBattleControl(control, x, y, 1f);
        var rect = (RectTransform)control;
        rect.sizeDelta = Vector2.one * size;
        // Preserve the input root and cached joystick handle; disable only the old artwork.
        foreach (var graphic in control.GetComponentsInChildren<Graphic>(true))
        {
            graphic.enabled = false;
            graphic.raycastTarget = false;
        }
        var hitArea = control.GetComponent<Image>();
        if (hitArea == null) hitArea = control.gameObject.AddComponent<Image>();
        hitArea.enabled = true;
        hitArea.sprite = null;
        hitArea.type = Image.Type.Simple;
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        var group = control.GetComponent<CanvasGroup>();
        return group != null ? group : control.gameObject.AddComponent<CanvasGroup>();
    }

    // วาดขีดเล็ก 4 ขีด (บน/ขวา/ล่าง/ซ้าย) รอบปุ่ม ห่างจากกลาง distance หน่วย
    private void ControlTicks(Transform parent, float distance, Color tint)
    {
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * .5f;
            var tick = BattlePanel("Tick" + i, parent, Mathf.Sin(angle) * distance, Mathf.Cos(angle) * distance,
                3, 9, tint);
            tick.rectTransform.localRotation = Quaternion.Euler(0, 0, -i * 90);
        }
    }

    // สร้างหน้าตาปุ่มควบคุมด้วยโค้ด: จอยสติ๊กซ้ายล่าง, ปุ่มยิง (ลากเล็ง) ขวาล่าง, ปุ่มสกิลพร้อมวงคูลดาวน์เหนือปุ่มยิง
    // จากนั้นลองใช้ภาพปุ่มจาก Resources และสร้างปุ่มอีโมต
    private void BuildBattleControls()
    {
        // 1) จอยสติ๊ก: พื้นแตะ วงแหวน ขีด หัวจอย และคำว่า MOVE
        if (joystick != null)
        {
            moveControlOpacity = PrepareControl(joystick.transform, -486, -220, 196);
            ControlCircle("TouchPad", joystick.transform, 188, 0, new Color(.025f, .06f, .1f, .32f));
            moveControlRing = ControlCircle("OuterRing", joystick.transform, 190, .982f, MoveAccent * new Color(1, 1, 1, .55f));
            ControlCircle("InnerGuide", joystick.transform, 130, .98f, new Color(.3f, .75f, .9f, .14f));
            ControlTicks(joystick.transform, 85, new Color(.3f, .88f, 1f, .6f));
            var handle = joystick.transform.Find("JoystickHandle") as RectTransform;
            if (handle != null)
            {
                handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(.5f, .5f);
                handle.anchoredPosition = Vector2.zero;
                handle.sizeDelta = Vector2.one * 46;
                handle.SetAsLastSibling();
                moveThumb = ControlCircle("ThumbFill", handle, 44, 0, new Color(.15f, .65f, .8f, .42f));
                ControlCircle("ThumbRim", handle, 44, .94f, MoveAccent);
                ControlCircle("CenterDot", handle, 6, 0, Color.white);
            }
            var label = BattleLabel("MoveCaption", joystick.transform, "MOVE", 0, -111, 150, 22, 15);
            label.color = MoveAccent;
        }
        // 2) ปุ่มยิง: เปิดลากเล็ง สร้างวงแหวนและหัวเล็ง (aimHandle) และคำว่า AIM / FIRE
        if (fireButton != null)
        {
            fireControlOpacity = PrepareControl(fireButton.transform, 478, -226, 164);
            fireButton.enableDragAim = true;
            ControlCircle("TouchPad", fireButton.transform, 160, 0, new Color(.12f, .06f, .025f, .38f));
            fireControlRing = ControlCircle("OuterRing", fireButton.transform, 164, .97f, new Color(1f, .65f, .27f, .65f));
            ControlCircle("InnerGuide", fireButton.transform, 112, .98f, new Color(1f, .65f, .27f, .2f));
            ControlTicks(fireButton.transform, 73, new Color(1f, .65f, .27f, .75f));
            var aim = BattleRect("AimThumb", fireButton.transform, 0, 0, 46, 46);
            fireButton.aimHandle = aim;
            ControlCircle("ThumbFill", aim, 46, 0, new Color(.23f, .1f, .035f, .6f));
            fireThumb = ControlCircle("Reticle", aim, 28, .91f, FireAccent);
            ControlTicks(aim, 18, FireAccent);
            ControlCircle("CenterDot", aim, 4, 0, Color.white);
            var label = BattleLabel("FireCaption", fireButton.transform, "AIM / FIRE", 0, -95, 175, 24, 15);
            label.color = FireAccent;
        }
        // 3) ปุ่มสกิล: ไอคอนสกิล วงคูลดาวน์ ชื่อสกิล และข้อความสถานะ
        if (skillButton != null)
        {
            skillControlOpacity = PrepareControl(skillButton.transform, 553, -53, 112);
            // Use a dedicated image so loading the equipped skill never replaces the input root.
            skillIconImage = BattlePanel("EquippedSkillIcon", skillButton.transform, 0, 0, 96, 96, Color.white);
            skillIconImage.preserveAspect = true;
            skillIconImage.enabled = false;
            skillCooldownImage = null;
            ControlCircle("CooldownTrack", skillButton.transform, 112, .95f, new Color(.7f, .6f, 1f, .18f));
            skillControlRing = ControlCircle("CooldownProgress", skillButton.transform, 112, .95f, SkillAccent);
            battleSkillName = BattleLabel("AbilityName", skillButton.transform, "SKILL", 0, -65, 130, 22, 15);
            battleSkillName.color = SkillAccent;
            battleSkillStatus = BattleLabel("AbilityState", skillButton.transform, "WAITING", 0, -85, 130, 22, 15);
        }
        // 4) ใช้ภาพปุ่มจาก Resources (ถ้ามี) และสร้างปุ่มอีโมต
        ApplyAuthoredControlArt();
        BuildEmoteControls();
    }

    // ถ้ามีรูป Images/{prefix}_Base และ Images/{prefix}_Handle ใน Resources: ปิดภาพที่วาดด้วยโค้ด (ยกเว้นข้อความ) แล้วใช้รูปนั้นแทน
    // คืน true ถ้าเปลี่ยนสำเร็จ
    private bool ApplyControlSprites(Transform control, RectTransform handle, string prefix, float handleSize)
    {
        Sprite baseSprite = Resources.Load<Sprite>("Images/" + prefix + "_Base");
        Sprite handleSprite = Resources.Load<Sprite>("Images/" + prefix + "_Handle");
        if (baseSprite == null || handleSprite == null || handle == null) return false;
        // Keep the transparent input root and captions, replacing only procedural artwork.
        foreach (var graphic in control.GetComponentsInChildren<Graphic>(true))
            if (graphic.transform != control && !(graphic is TMP_Text)) graphic.enabled = false;
        var baseImage = BattlePanel("AuthoredBase", control, 0, 0,
            ((RectTransform)control).sizeDelta.x * .8f, ((RectTransform)control).sizeDelta.y * .8f, Color.white);
        baseImage.sprite = baseSprite;
        baseImage.preserveAspect = true;
        baseImage.transform.SetAsFirstSibling();
        handle.sizeDelta = Vector2.one * handleSize;
        handle.anchoredPosition = Vector2.zero;
        var handleImage = BattlePanel("AuthoredHandle", handle, 0, 0, handleSize, handleSize, Color.white);
        handleImage.sprite = handleSprite;
        handleImage.preserveAspect = true;
        handle.SetAsLastSibling();
        return true;
    }

    // ลองเปลี่ยนจอย (UI_Move) และปุ่มยิง (UI_Fire) เป็นภาพจาก Resources ถ้าสำเร็จจะปรับระยะเลื่อนหัวจอย และเลิกเปลี่ยนสีวงแหวน
    private void ApplyAuthoredControlArt()
    {
        if (joystick != null && ApplyControlSprites(joystick.transform,
            joystick.transform.Find("JoystickHandle") as RectTransform, "UI_Move", 108))
        {
            joystick.handleTravelFraction = .1f;
            moveControlRing = moveThumb = null;
        }
        if (fireButton != null && ApplyControlSprites(fireButton.transform,
            fireButton.aimHandle, "UI_Fire", 92))
        {
            fireButton.handleTravelFraction = .09f;
            fireControlRing = fireThumb = null;
        }
    }

    // เรียกทุกเฟรม (ผ่าน UpdateSkillUI): ปุ่มจางลง (alpha 0.42) เมื่อบังคับไม่ได้ วงแหวนสว่างขึ้นตอนกด
    // และวงสกิลแสดงสัดส่วนคูลดาวน์ที่เหลือ
    private void UpdateControlFeedback()
    {
        bool active = MatchInputAllowed && localPlayer != null && !localPlayer.isDead
            && !localPlayer.IsStunned && !localPlayer.HasMatchEnded;
        float opacity = GameSettings.ButtonOpacity; // เฟส 9: ความทึบปุ่มบนจอ
        if (moveControlOpacity != null) moveControlOpacity.alpha = (active ? 1 : .42f) * opacity;
        if (fireControlOpacity != null) fireControlOpacity.alpha = (active ? 1 : .42f) * opacity;
        bool moving = joystick != null && joystick.IsDragging && active;
        bool firing = fireButton != null && fireButton.isPressed && active;
        if (moveControlRing != null) moveControlRing.color = new Color(MoveAccent.r, MoveAccent.g, MoveAccent.b, moving ? 1 : .55f);
        if (moveThumb != null) moveThumb.color = new Color(.15f, .65f, .8f, moving ? .75f : .42f);
        if (fireControlRing != null) fireControlRing.color = new Color(FireAccent.r, FireAccent.g, FireAccent.b, firing ? 1 : .65f);
        if (fireThumb != null) fireThumb.color = firing ? Color.white : FireAccent;
        if (battleSkillName != null) battleSkillName.text = localPlayer != null ? localPlayer.skillName : "SKILL";
        if (skillControlOpacity != null) skillControlOpacity.alpha = (active ? 1 : .42f) * opacity;
        if (skillControlRing != null)
        {
            float cooldown = localPlayer != null ? Mathf.Clamp01(localPlayer.currentCooldown / Mathf.Max(.01f, localPlayer.maxCooldown)) : 0;
            skillControlRing.SetRing(.95f, cooldown > 0 ? cooldown : 1);
            skillControlRing.color = cooldown > 0 ? new Color(.55f, .55f, .7f, .7f) : SkillAccent;
        }
    }

    // เรียกทุกเฟรม: ย่อ/ขยาย HUD ขนาดออกแบบ 1280x720 ให้พอดี Safe Area ของจอ (เว้นขอบ 24 px) และวางไว้กลาง Safe Area
    private void FitBattleHUD()
    {
        FitResultUI();
        if (battleHud == null || Screen.width <= 0 || Screen.height <= 0) return;
        var parent = battleHud.parent as RectTransform;
        if (parent == null) return;
        Rect safe = Screen.safeArea;
        Vector2 units = new Vector2(parent.rect.width / Screen.width, parent.rect.height / Screen.height);
        float scale = Mathf.Max(0.01f, Mathf.Min((safe.width - 24) * units.x / 1280f, (safe.height - 24) * units.y / 720f));
        battleHud.localScale = Vector3.one * scale;
        battleHud.anchoredPosition = Vector2.Scale(safe.center - new Vector2(Screen.width, Screen.height) * 0.5f, units);
        ApplyControlLayout(); // เฟส 9: ขนาด/ฝั่งปุ่มตามที่ผู้เล่นตั้ง (GameplayManager.Settings.cs)
        PlaceTopRightButtons(); // ปุ่มตั้งค่า + อีโมต แถวเดียวมุมขวาบน (GameplayManager.TopButtons.cs)
    }

    // สร้างหน้าผลการแข่งด้วยโค้ด (เรียกจาก Start) ถ้ามี ResultSurface ใน Scene จะผูกของเดิมแทน
    // ประกอบด้วย หัวข้อผล ห้อง คะแนน การ์ดผู้เล่น 2 ใบ ปุ่มกลับห้องเดิม และปุ่มกลับ Lobby
    private void BuildResultUI()
    {
        if (resultPanel == null || resultSurface != null) return;
        if (BindAuthoredResultUI()) return;
        // 1) ซ่อนของเดิมใน resultPanel และทำพื้นหลังเข้มเต็มจอ
        foreach (Transform child in resultPanel.transform) child.gameObject.SetActive(false);
        var overlay = resultPanel.GetComponent<Image>();
        if (overlay != null) { overlay.color = new Color(0.015f, 0.025f, 0.05f, 0.96f); overlay.raycastTarget = true; }
        var panelRect = resultPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        // 2) หัวข้อ / ห้อง / คะแนน และการ์ดของเรา (ซ้าย) กับคู่แข่ง (ขวา)
        resultSurface = BattleRect("ResultSurface", resultPanel.transform, 0, 0, 1280, 720);
        resultHeadline = BattleLabel("Outcome", resultSurface, "MATCH COMPLETE", 0, 292, 1100, 64, 46);
        resultRoomNumber = BattleLabel("Room", resultSurface, "", 0, 242, 1050, 28, 17);
        resultRoomNumber.richText = false;
        resultScore = BattleLabel("FinalScore", resultSurface, "", 0, 196, 1000, 42, 27);
        BuildResultCard(true, -278);
        BuildResultCard(false, 278);
        BattleLabel("Versus", resultSurface, "VS", 0, -5, 70, 45, 25);
        // 3) ปุ่ม RETURN TO SAME ROOM -> RequestSameRoom, ปุ่ม BACK TO LOBBY -> LeaveRoom
        var again = BattlePanel("ReturnSameRoom", resultSurface, -235, -270, 440, 60, new Color(.08f,.38f,.32f));
        again.raycastTarget = true;
        var rematch = again.gameObject.AddComponent<Button>();
        rematch.targetGraphic = again;
        rematch.onClick.AddListener(RequestSameRoom);
        rematchLabel = BattleLabel("Label", again.transform, "RETURN TO SAME ROOM", 0, 0, 425, 52, 23);
        var back = BattlePanel("ReturnToLobby", resultSurface, 235, -270, 440, 60, new Color(0.08f, 0.32f, 0.42f));
        back.raycastTarget = true;
        btnReturnToMenu = back.gameObject.AddComponent<Button>();
        btnReturnToMenu.targetGraphic = back;
        BattleLabel("Label", back.transform, "BACK TO LOBBY", 0, 0, 415, 52, 25);
        btnReturnToMenu.onClick.AddListener(LeaveRoom);
        ButtonSkin.Apply(again); ButtonSkin.Apply(back); // หน้าตาปุ่มแบบใหม่ (FeatureFlags.FancyButtons)
        BattleLabel("Footer", resultSurface, "BATTLEFIELD OF THE STARS / MATCH REPORT", 0, -324, 1100, 25, 14);
        FitResultUI();
    }

    // สร้างการ์ดผลของผู้เล่น 1 คน (ชื่อ รูปยาน สถานะ เหรียญ ASTRONIUM) แล้วเก็บอ้างอิงลงตัวแปรของเรา/คู่แข่ง
    private void BuildResultCard(bool local, float x)
    {
        var card = BattlePanel(local ? "YourResult" : "RivalResult", resultSurface, x, -24, 470, 390, new Color(0.035f, 0.065f, 0.12f));
        var border = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
        border.effectDistance = new Vector2(2, -2);
        BattleLabel("Side", card.transform, local ? "YOUR PILOT" : "RIVAL PILOT", 0, 162, 420, 25, 16);
        var name = BattleLabel("Name", card.transform, "--", 0, 124, 420, 38, 27);
        name.richText = false;
        var ship = BattlePanel("Ship", card.transform, 0, 18, 280, 160, Color.white);
        ship.preserveAspect = true;
        ship.enabled = false;
        var status = BattleLabel("Status", card.transform, "", 0, -90, 420, 38, 28);
        var coins = BattleLabel("Reward", card.transform, "0", -75, -146, 150, 40, 30);
        coins.color = new Color(1f, 0.8f, 0.35f);
        BattleLabel("Currency", card.transform, "ASTRONIUM", 85, -146, 170, 32, 19);
        if (local)
        {
            localResultOutline = border; localResultName = name; localResultShip = ship;
            localResultStatus = status; localResultCoins = coins;
        }
        else
        {
            remoteResultOutline = border; remoteResultName = name; remoteResultShip = ship;
            remoteResultStatus = status; remoteResultCoins = coins;
        }
    }

    // ใช้หน้าผลแมตช์ (ResultSurface) ที่บันทึกไว้ใน Scene
    private bool BindAuthoredResultUI()
    {
        var surface = resultPanel.transform.Find("ResultSurface") as RectTransform;
        if (surface == null) return false;
        foreach (Transform child in resultPanel.transform)
            if (child != surface) child.gameObject.SetActive(false);
        surface.gameObject.SetActive(true);
        resultSurface = surface;
        resultHeadline = FindPart<TMP_Text>(surface, "Outcome");
        resultRoomNumber = FindPart<TMP_Text>(surface, "Room");
        resultScore = FindPart<TMP_Text>(surface, "FinalScore");
        BindAuthoredResultCard(true);
        BindAuthoredResultCard(false);
        var rematch = FindPart<Button>(surface, "ReturnSameRoom");
        if (rematch != null) rematch.onClick.AddListener(RequestSameRoom);
        rematchLabel = FindPart<TMP_Text>(surface, "ReturnSameRoom/Label");
        var back = FindPart<Button>(surface, "ReturnToLobby");
        if (back != null)
        {
            btnReturnToMenu = back;
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
        if (rematch != null) ButtonSkin.Apply(rematch.targetGraphic as Image); // หน้าตาปุ่มแบบใหม่
        if (back != null) ButtonSkin.Apply(back.targetGraphic as Image);
        FitResultUI();
        return true;
    }

    // ผูกการ์ดผลของเรา/คู่แข่งจาก Scene เข้ากับตัวแปร (ซ่อนรูปยานไว้ก่อน)
    private void BindAuthoredResultCard(bool local)
    {
        var card = resultSurface.Find(local ? "YourResult" : "RivalResult");
        if (card == null) return;
        var border = card.GetComponent<UnityEngine.UI.Outline>();
        var name = FindPart<TMP_Text>(card, "Name");
        var ship = FindPart<Image>(card, "Ship");
        if (ship != null) ship.enabled = false;
        var status = FindPart<TMP_Text>(card, "Status");
        var coins = FindPart<TMP_Text>(card, "Reward");
        if (local)
        {
            localResultOutline = border; localResultName = name; localResultShip = ship;
            localResultStatus = status; localResultCoins = coins;
        }
        else
        {
            remoteResultOutline = border; remoteResultName = name; remoteResultShip = ship;
            remoteResultStatus = status; remoteResultCoins = coins;
        }
    }

    // ปรับขนาดหน้าผลให้พอดี Safe Area แบบเดียวกับ FitBattleHUD
    private void FitResultUI()
    {
        // หน้าผล: ซ่อนปุ่มตั้งค่ามุมจอ (เดิมโผล่ทะลุหน้าผล) และตั้งชื่อปุ่มเล่นต่อของโหมดคนเดียว
        if (resultShown)
        {
            var exit = battleHud != null && battleHud.parent != null ? battleHud.parent.Find("TopCenter/Btn_Exit") : null;
            if (exit != null) exit.gameObject.SetActive(false);
            ApplySoloRematchLabel();
            EnsureStatsButton(); // ปุ่ม MATCH STATS (GameplayManager.Stats.cs)
            // ไอคอนปุ่มหน้าผล (UiIcon.cs): เล่นต่อ / กลับล็อบบี้
            UiIcon.Attach(rematchLabel, "play");
            UiIcon.Attach(resultSurface != null ? FindPart<TMP_Text>(resultSurface, "ReturnToLobby/Label") : null, "home");
        }
        if (resultSurface == null || Screen.width <= 0 || Screen.height <= 0) return;
        var parent = resultSurface.parent as RectTransform;
        if (parent == null) return;
        Rect safe = Screen.safeArea;
        Vector2 units = new Vector2(parent.rect.width / Screen.width, parent.rect.height / Screen.height);
        float scale = Mathf.Max(0.01f, Mathf.Min((safe.width - 24) * units.x / 1280f, (safe.height - 24) * units.y / 720f));
        resultSurface.localScale = Vector3.one * scale;
        resultSurface.anchoredPosition = Vector2.Scale(safe.center - new Vector2(Screen.width, Screen.height) * 0.5f, units);
    }

    // เติมข้อมูลหน้าผล (เรียกจาก ShowResultScreen / ShowDrawResultScreen): ซ่อน HUD, หัวข้อ VICTORY/DEFEAT/DRAW (won = null คือเสมอ)
    // ชื่อห้อง คะแนนสุดท้ายจาก Kills ของทั้งคู่ และรูปยานของทั้งคู่
    private void PresentResult(bool? won)
    {
        if (battleHud != null) battleHud.gameObject.SetActive(false);
        if (resultPanel != null) resultPanel.transform.SetAsLastSibling();
        if (resultHeadline != null)
        {
            resultHeadline.text = !won.HasValue ? "DRAW" : won.Value ? "VICTORY" : "DEFEAT";
            resultHeadline.color = !won.HasValue ? Color.white : won.Value ? new Color(1f, 0.8f, 0.35f) : new Color(1f, 0.4f, 0.45f);
        }
        if (localResultStatus != null) localResultStatus.text = !won.HasValue ? "DRAW" : won.Value ? "WINNER" : "DEFEATED";
        if (remoteResultStatus != null) remoteResultStatus.text = !won.HasValue ? "DRAW" : won.Value ? "DEFEATED" : "WINNER";
        if (resultRoomNumber != null) resultRoomNumber.text = "MATCH COMPLETE / ROOM " + (PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "--");
        int yours = 0, rival = 0;
        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Kills", out object kills) && kills is int k) yours = k;
        var other = PhotonNetwork.PlayerListOthers.Length > 0 ? PhotonNetwork.PlayerListOthers[0] : null;
        // คู่แข่งที่ได้ Kill มากสุด (รวมบอท)
        rival = MatchRules.BestRivalKills();
        if (resultScore != null) resultScore.text = "FINAL SCORE  /  " + yours + " : " + rival;
        SetResultShip(localResultShip, PhotonNetwork.LocalPlayer);
        SetResultShip(remoteResultShip, other);
        FitResultUI();
    }

    // ตั้งรูปยานในการ์ดผลตาม ShipType ของผู้เล่น (Images/ship1-3) และสีตาม ShipPaint ถ้าไม่มีรูปก็ซ่อน
    private void SetResultShip(Image image, Photon.Realtime.Player player)
    {
        if (image == null) return;
        Sprite sprite = null;
        if (player != null && player.CustomProperties.TryGetValue("ShipType", out object value) && value is int index)
            sprite = BattleLoadoutCatalog.ShipSprite(index);
        image.sprite = sprite;
        image.color = ShipPaint.For(player);
        image.enabled = sprite != null;
    }

    // เตรียมข้อความเวลาและคะแนน (เรียกจาก Start ก่อน StyleBattleHUD): ใช้ของใน BattleHUD ของ Scene ถ้าครบ
    // ไม่งั้นสร้าง MatchTimerText และ ScoreText ใหม่ใต้ Canvas
    private void CreateMatchUI()
    {
        // 1) มี HUD ใน Scene: ลองหยิบ MatchTimerText / ScoreText
        var authored = FindAuthoredBattleHud();
        if (authored != null)
        {
            matchTimerText = FindPart<TMP_Text>(authored, "MatchTimerText");
            scoreText = FindPart<TMP_Text>(authored, "ScoreText");
            hudAuthored = matchTimerText != null && scoreText != null;
            if (hudAuthored) return;
            // ข้อมูลใน Scene ไม่ครบ: ปิดของที่บันทึกไว้ แล้วสร้างด้วยโค้ดแบบเดิมแทน
            Debug.LogWarning("BattleHUD in the scene is missing MatchTimerText or ScoreText; using the code-built HUD instead.", authored);
            authored.gameObject.SetActive(false);
            authored.name = "BattleHUD (incomplete)";
        }

        // 2) หา Canvas แล้วสร้างข้อความเวลา (สีขาว) และคะแนน (สีเหลือง) ด้านบนจอ
        Canvas canvas = null;
        if (playerInfoText != null) canvas = playerInfoText.canvas;
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        if (canvas != null)
        {
            GameObject timerObj = new GameObject("MatchTimerText");
            RegisterEditorCreated(timerObj);
            timerObj.transform.SetParent(canvas.transform, false);
            matchTimerText = timerObj.AddComponent<TextMeshProUGUI>();
            RectTransform timerRect = timerObj.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0, -120); // ขยับลงมาไม่ให้บังปุ่มออก
            timerRect.sizeDelta = new Vector2(200, 40);
            matchTimerText.alignment = TextAlignmentOptions.Center;
            matchTimerText.fontSize = 24;
            matchTimerText.color = Color.white;
            matchTimerText.outlineWidth = 0.2f;
            matchTimerText.outlineColor = Color.black;

            GameObject scoreObj = new GameObject("ScoreText");
            RegisterEditorCreated(scoreObj);
            scoreObj.transform.SetParent(canvas.transform, false);
            scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
            RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.pivot = new Vector2(0.5f, 1f);
            scoreRect.anchoredPosition = new Vector2(0, -160); // ขยับลงมาตามเวลา
            scoreRect.sizeDelta = new Vector2(300, 40);
            scoreText.alignment = TextAlignmentOptions.Center;
            scoreText.fontSize = 20;
            scoreText.color = Color.yellow;
            scoreText.outlineWidth = 0.2f;
            scoreText.outlineColor = Color.black;
        }
    }

    // ส่วนด้านล่างคอมไพล์เฉพาะใน Unity Editor: เมนูคลิกขวาสำหรับสร้าง UI ลง Scene เพื่อแก้หน้าตาในหน้า Edit
#if UNITY_EDITOR
    // ================== เมนูสำหรับหน้า Edit ==================
    // วิธีใช้: เปิด SampleScene → เลือกวัตถุที่มี GameplayManager → คลิกขวาที่หัว Component ใน Inspector
    // → Editable/... (ต้องไม่ได้กด Play) จากนั้น Save Scene
    // เมนู Editable/0: สร้างทุกอย่าง (HUD, เลย์เอาต์แม็พ, ต้นแบบแผงตั้งค่า) ลง Scene ในครั้งเดียว
    [ContextMenu("Editable/0. Build Everything into Scene")]
    private void EditableBuildEverything()
    {
        EditableBuildBattleHud();
        EditableBuildMapLayouts();
        EditableBuildSettingsTemplate();
    }

    // เมนู Editable/3: สร้างต้นแบบแผงตั้งค่า (BattleSettingsPanel) ลง Scene (ต้องไม่อยู่ใน Play mode)
    [ContextMenu("Editable/3. Build Settings Panel Template")]
    private void EditableBuildSettingsTemplate()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before building the settings template."); return; }
        BattleSettingsPanel.BuildEditableTemplate(true);
    }

    // สร้าง HUD ทั้งหมดลง Scene ด้วยโค้ดชุดเดียวกับตอนรัน แล้วตอนรันเกมจะหยิบของที่บันทึกไว้มาใช้
    [ContextMenu("Editable/1. Build Battle HUD into Scene")]
    private void EditableBuildBattleHud()
    {
        // 1) ตรวจว่าไม่ได้กด Play อยู่ และยังไม่มี BattleHUD ใน Scene แล้วหา Canvas
        if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before building the HUD into the scene."); return; }
        if (FindAuthoredBattleHud() != null)
        {
            Debug.LogWarning("BattleHUD is already in this scene. Edit it directly, or delete it (and ResultSurface) to rebuild.", this);
            return;
        }
        var originalInfo = playerInfoText;
        Canvas canvas = originalInfo != null ? originalInfo.canvas : null;
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("No Canvas found in this scene.", this); return; }

        // 2) เริ่มกลุ่ม Undo เดียว (Undo ครั้งเดียวย้อนได้ทั้งหมด)
        int group = UnityEditor.Undo.GetCurrentGroup();
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Editable Battle UI");
        if (resultPanel != null && !resultPanel.transform.IsChildOf(canvas.transform))
            UnityEditor.Undo.RegisterFullObjectHierarchyUndo(resultPanel, "Build Editable Battle UI");
        UnityEditor.Undo.RecordObject(this, "Build Editable Battle UI");
        try
        {
            // 3) สร้าง HUD ด้วยโค้ดชุดเดียวกับตอนรัน
            hudAuthored = false;
            CreateMatchUI();
            StyleBattleHUD();
            if (battleHud == null) { Debug.LogError("Could not build BattleHUD.", this); return; }
            // ตอนรันจะปรับขนาดกรอบนี้ให้พอดีจอเองทุกเฟรม ในหน้า Edit จึงเก็บไว้ที่ขนาดออกแบบ 1280x720
            battleHud.localScale = Vector3.one;
            battleHud.anchoredPosition = Vector2.zero;

            // ส่วนที่โผล่เฉพาะบางจังหวะ: สร้างไว้แบบซ่อน (ติ๊กเปิดเพื่อดู/แก้ได้ ตอนรันโค้ดเปิดปิดเอง)
            CreateBattleCountdown();
            battleCountdownText.text = "3";
            battleCountdownText.gameObject.SetActive(false);
            CreateHitConfirmation();
            hitConfirmationText.text = "HIT";
            hitConfirmationText.gameObject.SetActive(false);
            CreateIncomingDamageMarker();
            damageDirectionRoot.gameObject.SetActive(false);
            CreateRespawnPanel();
            respawnPanel.gameObject.SetActive(false);

            // ต้นแบบที่ถูกคัดลอกตอนรัน (ซ่อนตัวเองอัตโนมัติเมื่อกด Play)
            var plate = CreateShipNameplate(ShipNameplateTemplateName);
            plate.name.text = "YOU / PILOT";
            plate.root.gameObject.AddComponent<EditableTemplate>();
            plate.root.gameObject.SetActive(false);
            var kill = CreateKillMessage(battleHud.parent, KillMessageTemplateName, false);
            kill.AddComponent<EditableTemplate>();
            kill.SetActive(false);

            // ตัวอย่างมินิแม็พ แล้วสร้างหน้าผล
            RadarMinimap.BuildEditablePreview(battleHud, matchTimerText.font);

            BuildResultUI();
            if (resultSurface != null)
            {
                resultSurface.localScale = Vector3.one;
                resultSurface.anchoredPosition = Vector2.zero;
            }

            // ตัวอ้างอิง Canvas ของมินิแม็พสร้างตอนรันเท่านั้น
            if (playerInfoText != null && playerInfoText != originalInfo)
                UnityEditor.Undo.DestroyObjectImmediate(playerInfoText.gameObject);
            playerInfoText = originalInfo;
        }
        finally
        {
            ResetEditorBuildState();
        }
        // 4) รวม Undo บันทึกว่า Scene ถูกแก้ไข และเลือก BattleHUD ให้ใน Hierarchy
        UnityEditor.Undo.CollapseUndoOperations(group);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        var built = FindAuthoredBattleHud();
        if (built != null) UnityEditor.Selection.activeTransform = built;
        Debug.Log("Battle HUD saved into the scene under " + canvas.name + "/BattleHUD. Save the scene to keep it.", this);
    }

    // ล้างตัวแปรชั่วคราวหลังสร้างในหน้า Edit (ตอนกด Play ค่าเหล่านี้เริ่มใหม่อยู่แล้ว)
    private void ResetEditorBuildState()
    {
        hudAuthored = false;
        battleHud = shipHudLayer = resultSurface = damageDirectionRoot = null;
        matchTimerText = scoreText = resultHeadline = resultScore = null;
        battleSkillStatus = battleRivalName = battleSkillName = rematchLabel = null;
        battleCountdownText = hitConfirmationText = respawnReasonText = respawnTimerText = null;
        combatStatusPanel = damageDirectionMarker = respawnPanel = null;
        combatStatusLabels = null;
        moveControlRing = fireControlRing = skillControlRing = moveThumb = fireThumb = null;
        moveControlOpacity = fireControlOpacity = skillControlOpacity = null;
        p1HpText = p2HpText = null;
        p1HpFill = p2HpFill = null;
    }
#endif
}
