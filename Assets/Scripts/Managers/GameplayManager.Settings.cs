// GameplayManager.Settings.cs — ใช้ค่าตั้งของผู้เล่น (GameSettings, เฟส 9) กับ HUD ในสนามรบ
// - ขนาดปุ่ม จอย/ยิง/สกิล และสลับฝั่งซ้าย-ขวา (มือซ้าย)
// - ซ่อนปุ่ม EMOTE เมื่อปิดอีโมต
// - ตัวนับ FPS ด้านล่างกลางจอ
// ความทึบปุ่มคิดใน UpdateControlFeedback (GameplayManager.HUD.cs)
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ส่วนตั้งค่า HUD ของ GameplayManager: ปรับขนาด/ฝั่งปุ่มตาม GameSettings, ตัวนับ FPS และปุ่มย้อนกลับ
public partial class GameplayManager
{
    // ค่าตำแหน่ง/ขนาดเดิมของปุ่มแต่ละอัน (จำไว้ครั้งแรก เพื่อปรับซ้ำได้ไม่สะสม)
    private readonly Dictionary<RectTransform, Vector3> controlBaseScale = new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, Vector2> controlBasePosition = new Dictionary<RectTransform, Vector2>();
    private readonly HashSet<RectTransform> anchorsMirrored = new HashSet<RectTransform>();
    private string appliedLayoutKey;
    private TMP_Text fpsLabel;
    private UnityEngine.UI.Button hudSettingsButton;

    // ปุ่มย้อนกลับมือถือ/Esc ในสนามรบ: ปิดหน้าตั้งค่า (ถ้าเปิดอยู่) ไม่งั้นเปิดหน้าตั้งค่า (มีปุ่ม LEAVE MATCH)
    // หน้าผลการแข่ง: กลับล็อบบี้
    private void HandleBattleBackKey()
    {
        if (!LobbyManager.BackKeyPressed()) return;
        if (ControlLayoutEditor.HandleBack()) return;
        if (CelebrationOverlay.HandleBack()) return; // ปิดฉากฉลองก่อน
        if (BattleSettingsPanel.HandleBack()) return;
        if (emoteMenu != null && emoteMenu.activeSelf) { emoteMenu.SetActive(false); return; }
        if (resultShown) { LeaveRoom(); return; }
        if (hudSettingsButton != null && hudSettingsButton.isActiveAndEnabled) hudSettingsButton.onClick.Invoke();
        else BattleSettingsPanel.Show(LeaveRoom);
    }
    private float fpsSmoothed, nextFpsText; // ค่า FPS เฉลี่ยแบบนุ่ม และเวลาที่จะอัปเดตข้อความ FPS ครั้งถัดไป (ทุก 0.5 วิ)

    // เรียกจาก FitBattleHUD ทุกเฟรม: ทำงานจริงเฉพาะตอนค่าตั้งเปลี่ยน
    private void ApplyControlLayout()
    {
        if (!Application.isPlaying || battleHud == null) return;
        UpdateFpsCounter();
        string key = GameSettings.ButtonSizeIndex + "|" + GameSettings.LeftHanded + "|" + GameSettings.Emotes
            + "|" + (joystick != null) + (fireButton != null) + (skillButton != null)
            + "|" + ControlLayout.Revision + "|" + battleHud.rect.size;
        if (key == appliedLayoutKey) return;
        appliedLayoutKey = key;
        float scale = GameSettings.ButtonScale;
        bool mirror = GameSettings.LeftHanded;
        var custom = ControlLayout.Load();
        if (joystick != null) LayoutEditableControl(joystick.transform as RectTransform, 0, custom, scale, mirror);
        if (fireButton != null) LayoutEditableControl(fireButton.transform as RectTransform, 1, custom, scale, mirror);
        if (skillButton != null) LayoutEditableControl(skillButton.transform as RectTransform, 2, custom, scale, mirror);
        var emoteButton = battleHud.Find("EmoteButton") as RectTransform;
        var emoteList = battleHud.Find("EmoteMenu") as RectTransform;
        // แถวปุ่มมุมขวาบนจัดเองใน PlaceTopRightButtons (ไม่สลับฝั่งตามโหมดมือซ้าย)
        if (!FeatureFlags.TidyBattleButtons)
        {
            LayoutControl(emoteButton, 1f, mirror);
            LayoutControl(emoteList, 1f, mirror);
        }
        if (emoteButton != null) emoteButton.gameObject.SetActive(GameSettings.Emotes);
        if (!GameSettings.Emotes && emoteList != null) emoteList.gameObject.SetActive(false);
    }

    private readonly Dictionary<RectTransform, Vector2> controlBaseMin = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector2> controlBaseMax = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector2> controlBasePivot = new Dictionary<RectTransform, Vector2>();
    private void LayoutEditableControl(RectTransform control, int index, ControlLayout.Profile custom, float scale, bool mirror)
    {
        if (control == null) return;
        if (!controlBaseMin.ContainsKey(control))
        {
            controlBaseMin[control]=control.anchorMin; controlBaseMax[control]=control.anchorMax;
            controlBasePivot[control]=control.pivot;
            controlBasePosition[control]=control.anchoredPosition;controlBaseScale[control]=control.localScale;
        }
        control.anchorMin=controlBaseMin[control];control.anchorMax=controlBaseMax[control];control.pivot=controlBasePivot[control];
        anchorsMirrored.Remove(control);
        if(custom!=null)ControlLayout.Apply(control,custom.slots[index]);else LayoutControl(control,scale,mirror);
    }

    // ปรับขนาดและฝั่ง (กลับด้านแกน x) ของปุ่มหนึ่งอัน จากค่าเดิมที่จำไว้
    private void LayoutControl(RectTransform control, float scale, bool mirror)
    {
        if (control == null) return;
        if (!controlBaseScale.ContainsKey(control))
        {
            controlBaseScale[control] = control.localScale;
            controlBasePosition[control] = control.anchoredPosition;
        }
        control.localScale = controlBaseScale[control] * scale;
        Vector2 position = controlBasePosition[control];
        // ปุ่มที่ยึดมุมจอ: กลับ anchor ด้วยเพื่อให้ไปอยู่อีกฝั่งจริง
        if (mirror) position.x = -position.x;
        bool centered = Mathf.Approximately(control.anchorMin.x, .5f) && Mathf.Approximately(control.anchorMax.x, .5f);
        if (!centered && mirror != anchorsMirrored.Contains(control))
        {
            Vector2 min = control.anchorMin, max = control.anchorMax;
            control.anchorMin = new Vector2(1f - max.x, min.y);
            control.anchorMax = new Vector2(1f - min.x, max.y);
            if (mirror) anchorsMirrored.Add(control); else anchorsMirrored.Remove(control);
        }
        control.anchoredPosition = position;
    }

    // ตัวนับ FPS (เปิดได้ในหน้า MORE OPTIONS)
    private void UpdateFpsCounter()
    {
        bool show = GameSettings.ShowFps;
        if (!show)
        {
            if (fpsLabel != null && fpsLabel.gameObject.activeSelf) fpsLabel.gameObject.SetActive(false);
            return;
        }
        if (fpsLabel == null)
        {
            fpsLabel = BattleLabel("FpsCounter", battleHud, "", 0, -340, 160, 26, 16);
            fpsLabel.color = new Color(.6f, 1f, .6f);
        }
        if (!fpsLabel.gameObject.activeSelf) fpsLabel.gameObject.SetActive(true);
        float dt = Mathf.Max(.0001f, Time.unscaledDeltaTime);
        fpsSmoothed = fpsSmoothed <= 0 ? 1f / dt : Mathf.Lerp(fpsSmoothed, 1f / dt, .1f);
        if (Time.unscaledTime < nextFpsText) return;
        nextFpsText = Time.unscaledTime + .5f;
        fpsLabel.text = "FPS " + Mathf.RoundToInt(fpsSmoothed);
    }
}
