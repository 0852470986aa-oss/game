using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

// ส่วน HUD ของ GameplayManager; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class GameplayManager
{
    private RectTransform battleHud;
    private RectTransform shipHudLayer;
    private sealed class ShipNameplate
    {
        public PlayerController ship;
        public RectTransform root;
        public TMP_Text name;
        public Image fill;
    }
    private readonly System.Collections.Generic.Dictionary<int, ShipNameplate> shipNameplates = new System.Collections.Generic.Dictionary<int, ShipNameplate>();
    private readonly System.Collections.Generic.List<int> staleNameplates = new System.Collections.Generic.List<int>();
    private float nextShipHudScan;

    private void LateUpdate()
    {
        if (shipHudLayer == null || !battleHud.gameObject.activeInHierarchy) return;
        if (Time.unscaledTime >= nextShipHudScan)
        {
            nextShipHudScan = Time.unscaledTime + .5f;
            foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                int key = ship.GetInstanceID();
                if (shipNameplates.ContainsKey(key)) continue;
                var root = BattleRect("ShipNameplate_" + key, shipHudLayer, 0, 0, 156, 38);
                var name = BattleLabel("Pilot", root, "", 0, 11, 156, 22, 15);
                name.richText = false;
                name.outlineWidth = .18f;
                name.outlineColor = new Color(0, 0, 0, .85f);
                var track = BattlePanel("HullTrack", root, 0, -7, 112, 8, new Color(.015f, .025f, .04f, .8f));
                var fill = BattlePanel("HullFill", track.transform, -54, 0, 108, 4, Color.cyan);
                fill.rectTransform.pivot = new Vector2(0, .5f);
                shipNameplates[key] = new ShipNameplate { ship = ship, root = root, name = name, fill = fill };
            }
        }
        var view = Camera.main;
        if (view == null) { shipHudLayer.gameObject.SetActive(false); return; }
        shipHudLayer.gameObject.SetActive(true);
        var canvas = battleHud.GetComponentInParent<Canvas>();
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        staleNameplates.Clear();
        foreach (var entry in shipNameplates)
        {
            var plate = entry.Value;
            if (plate.ship == null) { Destroy(plate.root.gameObject); staleNameplates.Add(entry.Key); continue; }
            Vector3 center = view.WorldToViewportPoint(plate.ship.transform.position);
            bool visible = !plate.ship.isDead && !plate.ship.HasMatchEnded && center.z > 0
                && center.x >= 0 && center.x <= 1 && center.y >= 0 && center.y <= 1;
            plate.root.gameObject.SetActive(visible);
            if (!visible) continue;
            // Follow the ship pivot, not its rotating bounds: a fixed screen-space offset avoids bobbing.
            Vector3 screen = view.WorldToScreenPoint(plate.ship.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(shipHudLayer, screen, uiCamera, out Vector2 point);
            plate.root.anchoredPosition = point + Vector2.up * 36;
            // Keep names upright and a constant UI size regardless of ship rotation or camera zoom.
            bool mine = plate.ship.photonView.IsMine;
            string nickname = plate.ship.photonView.Owner != null ? plate.ship.photonView.Owner.NickName : "PILOT";
            plate.name.text = (mine ? "YOU / " : "") + nickname;
            plate.name.color = mine ? new Color(.4f, 1f, .9f) : new Color(1f, .7f, .65f);
            float hp = Mathf.Clamp01(plate.ship.currentHp / Mathf.Max(1, plate.ship.maxHp));
            plate.fill.rectTransform.localScale = new Vector3(hp, 1, 1);
            plate.fill.color = hp < .3f ? new Color(1f, .25f, .2f) : hp < .6f ? new Color(1f, .8f, .2f)
                : mine ? new Color(.2f, 1f, .7f) : new Color(1f, .45f, .35f);
        }
        foreach (int key in staleNameplates) shipNameplates.Remove(key);
    }
    private RectTransform resultSurface;
    private TMP_Text resultHeadline, resultScore;
    private bool resultShown;
    private TMP_Text battleSkillStatus, battleRivalName;
    private Image combatStatusPanel;
    private TMP_Text[] combatStatusLabels;
    private int previousCombatStatus = -1;

    private RectTransform BattleRect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private Image BattlePanel(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var img = BattleRect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

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

    private void PlaceBattleControl(Transform control, float x, float y, float scale = 1f)
    {
        if (control == null) return;
        control.SetParent(battleHud, false);
        var rect = control.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.localScale = Vector3.one * scale;
    }

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

    private void StyleBattleHUD()
    {
        if (matchTimerText == null || battleHud != null) return;
        var canvas = matchTimerText.canvas;
        foreach (string name in new[] { "Player1HUD", "Player2HUD" })
        {
            var old = canvas.transform.Find(name);
            if (old != null) old.gameObject.SetActive(false);
        }
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
        combatStatusPanel = BattlePanel("CombatStatuses", battleHud, 0, -264, 640, 86, new Color(.035f, .065f, .12f, .85f));
        combatStatusLabels = new TMP_Text[6];
        for (int i = 0; i < combatStatusLabels.Length; i++)
        {
            combatStatusLabels[i] = BattleLabel("State" + i, combatStatusPanel.transform, "", 0, 0, 198, 32, 17);
            combatStatusLabels[i].gameObject.SetActive(false);
        }
        combatStatusPanel.gameObject.SetActive(false);
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
        var exit = canvas.transform.Find("TopCenter/Btn_Exit");
        if (exit != null)
        {
            PlaceBattleControl(exit, 553, 219);
            var button = exit.GetComponent<Button>();
            var label = exit.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "SETTINGS";
            if (button != null)
            {
                var leave = button.onClick;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => BattleSettingsPanel.Show(() => leave.Invoke()));
            }
        }
        BuildBattleControls();
        FitBattleHUD();
    }

    private ControlRingGraphic moveControlRing, fireControlRing, skillControlRing, moveThumb, fireThumb;
    private TMP_Text battleSkillName;
    private CanvasGroup moveControlOpacity, fireControlOpacity, skillControlOpacity;
    private static readonly Color MoveAccent = new Color(.3f, .88f, 1f);
    private static readonly Color FireAccent = new Color(1f, .65f, .27f);
    private static readonly Color SkillAccent = new Color(.77f, .64f, 1f);

    private ControlRingGraphic ControlCircle(string name, Transform parent, float diameter, float innerRatio, Color tint)
    {
        var graphic = BattleRect(name, parent, 0, 0, diameter, diameter).gameObject.AddComponent<ControlRingGraphic>();
        graphic.raycastTarget = false;
        graphic.color = tint;
        graphic.SetRing(innerRatio);
        return graphic;
    }

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

    private void BuildBattleControls()
    {
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
        ApplyAuthoredControlArt();
    }

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

    private void UpdateControlFeedback()
    {
        bool active = MatchInputAllowed && localPlayer != null && !localPlayer.isDead
            && !localPlayer.IsStunned && !localPlayer.HasMatchEnded;
        if (moveControlOpacity != null) moveControlOpacity.alpha = active ? 1 : .42f;
        if (fireControlOpacity != null) fireControlOpacity.alpha = active ? 1 : .42f;
        bool moving = joystick != null && joystick.IsDragging && active;
        bool firing = fireButton != null && fireButton.isPressed && active;
        if (moveControlRing != null) moveControlRing.color = new Color(MoveAccent.r, MoveAccent.g, MoveAccent.b, moving ? 1 : .55f);
        if (moveThumb != null) moveThumb.color = new Color(.15f, .65f, .8f, moving ? .75f : .42f);
        if (fireControlRing != null) fireControlRing.color = new Color(FireAccent.r, FireAccent.g, FireAccent.b, firing ? 1 : .65f);
        if (fireThumb != null) fireThumb.color = firing ? Color.white : FireAccent;
        if (battleSkillName != null) battleSkillName.text = localPlayer != null ? localPlayer.skillName : "SKILL";
        if (skillControlOpacity != null) skillControlOpacity.alpha = active ? 1 : .42f;
        if (skillControlRing != null)
        {
            float cooldown = localPlayer != null ? Mathf.Clamp01(localPlayer.currentCooldown / Mathf.Max(.01f, localPlayer.maxCooldown)) : 0;
            skillControlRing.SetRing(.95f, cooldown > 0 ? cooldown : 1);
            skillControlRing.color = cooldown > 0 ? new Color(.55f, .55f, .7f, .7f) : SkillAccent;
        }
    }

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
    }

    private void BuildResultUI()
    {
        if (resultPanel == null || resultSurface != null) return;
        foreach (Transform child in resultPanel.transform) child.gameObject.SetActive(false);
        var overlay = resultPanel.GetComponent<Image>();
        if (overlay != null) { overlay.color = new Color(0.015f, 0.025f, 0.05f, 0.96f); overlay.raycastTarget = true; }
        var panelRect = resultPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        resultSurface = BattleRect("ResultSurface", resultPanel.transform, 0, 0, 1280, 720);
        resultHeadline = BattleLabel("Outcome", resultSurface, "MATCH COMPLETE", 0, 292, 1100, 64, 46);
        resultRoomNumber = BattleLabel("Room", resultSurface, "", 0, 242, 1050, 28, 17);
        resultRoomNumber.richText = false;
        resultScore = BattleLabel("FinalScore", resultSurface, "", 0, 196, 1000, 42, 27);
        BuildResultCard(true, -278);
        BuildResultCard(false, 278);
        BattleLabel("Versus", resultSurface, "VS", 0, -5, 70, 45, 25);
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
        BattleLabel("Footer", resultSurface, "BATTLEFIELD OF THE STARS / MATCH REPORT", 0, -324, 1100, 25, 14);
        FitResultUI();
    }

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

    private void FitResultUI()
    {
        if (resultSurface == null || Screen.width <= 0 || Screen.height <= 0) return;
        var parent = resultSurface.parent as RectTransform;
        if (parent == null) return;
        Rect safe = Screen.safeArea;
        Vector2 units = new Vector2(parent.rect.width / Screen.width, parent.rect.height / Screen.height);
        float scale = Mathf.Max(0.01f, Mathf.Min((safe.width - 24) * units.x / 1280f, (safe.height - 24) * units.y / 720f));
        resultSurface.localScale = Vector3.one * scale;
        resultSurface.anchoredPosition = Vector2.Scale(safe.center - new Vector2(Screen.width, Screen.height) * 0.5f, units);
    }

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
        if (other != null && other.CustomProperties.TryGetValue("Kills", out object enemyKills) && enemyKills is int e) rival = e;
        if (resultScore != null) resultScore.text = "FINAL SCORE  /  " + yours + " : " + rival;
        SetResultShip(localResultShip, PhotonNetwork.LocalPlayer);
        SetResultShip(remoteResultShip, other);
        FitResultUI();
    }

    private void SetResultShip(Image image, Photon.Realtime.Player player)
    {
        if (image == null) return;
        Sprite sprite = null;
        if (player != null && player.CustomProperties.TryGetValue("ShipType", out object value) && value is int index && index >= 0 && index < 3)
            sprite = Resources.Load<Sprite>("Images/ship" + (index + 1));
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void CreateMatchUI()
    {
        Canvas canvas = null;
        if (playerInfoText != null) canvas = playerInfoText.canvas;
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        if (canvas != null)
        {
            GameObject timerObj = new GameObject("MatchTimerText");
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
}
