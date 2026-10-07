// GameplayManager.StatusUI.cs — ส่วนแสดงสถานะระหว่างต่อสู้ของ partial class GameplayManager (Scene SampleScene)
// ข้อความ "KILL +1", แผงสถานะของยานเรา (สตัน/ป้องกันตอนเกิด/โล่/ช้า/พิษ/Overload/เลือดต่ำ) และสถานะปุ่มสกิล
// UpdateSkillUI() ถูกเรียกทุกเฟรมจาก Update ใน GameplayManager.cs; ตัว UI สร้างใน GameplayManager.HUD.cs
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

// ส่วน StatusUI ของ GameplayManager; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
// (partial) คลาสเดียวกับ GameplayManager.cs
public partial class GameplayManager
{
    // แสดงข้อความ "KILL +1" กลางจอ 1.5 วินาที — เรียกจาก PlayerController.Health.cs เมื่อเราได้ Kill
    // ถ้ามีต้นแบบใน Scene จะคัดลอกต้นแบบ ไม่งั้นสร้างด้วยโค้ด
    public void ShowKillMessage()
    {
        // Simple floating text in the center
        // ถ้ามีต้นแบบ KillMessage_Template ใน Scene ให้ใช้หน้าตาตามที่แก้ไว้
        var template = battleHud != null && battleHud.parent != null ? battleHud.parent.Find(KillMessageTemplateName) : null;
        if (template != null)
        {
            var message = EditableTemplate.Spawn(template.gameObject, template.parent);
            message.name = "KillMessage";
            message.transform.SetAsLastSibling();
            Destroy(message, 1.5f);
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null) CreateKillMessage(canvas.transform, "KillMessage", true);
    }

    // สร้างข้อความ "KILL +1" สีแดงขอบดำด้วยโค้ด (autoDestroy = true ลบเองหลัง 1.5 วิ, false ใช้ทำต้นแบบในหน้า Edit)
    private GameObject CreateKillMessage(Transform parent, string objectName, bool autoDestroy)
    {
        GameObject txtObj = new GameObject(objectName);
        RegisterEditorCreated(txtObj);
        txtObj.transform.SetParent(parent, false);
        TextMeshProUGUI msgText = txtObj.AddComponent<TextMeshProUGUI>();
        RectTransform rect = txtObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, 100);
        rect.sizeDelta = new Vector2(300, 100);
        msgText.alignment = TextAlignmentOptions.Center;
        msgText.fontSize = 50;
        msgText.color = Color.red;
        msgText.text = "KILL +1";
        msgText.outlineWidth = 0.2f;
        msgText.outlineColor = Color.black;

        if (autoDestroy) Destroy(txtObj, 1.5f); // Automatically destroy after 1.5 seconds
        return txtObj;
    }

    // อัปเดตแผงสถานะของยานเรา: รวมสถานะเป็น bitmask (1 สตัน, 2 ป้องกันตอนเกิด, 4 โล่, 8 ช้า, 16 Overload, 32 เลือด <= 35%, 64 พิษ)
    // วาดใหม่เฉพาะเมื่อ mask เปลี่ยน แล้วจัดป้ายลงช่อง (แถวละ 3 ช่อง 2 แถว)
    private void UpdateCombatStatuses()
    {
        if (combatStatusPanel == null || combatStatusLabels == null) return;
        // 1) สร้าง bitmask จากสถานะยานเรา (ไม่แสดงอะไรถ้าตายหรือแมตช์จบ)
        int mask = 0;
        if (localPlayer != null && !localPlayer.isDead && !localPlayer.HasMatchEnded)
        {
            if (localPlayer.IsStunned) mask |= 1;
            if (localPlayer.IsSpawnProtected) mask |= 2;
            if (localPlayer.IsShielded) mask |= 4;
            if (localPlayer.IsSlowed) mask |= 8;
            if (localPlayer.IsSwampPoisoned) mask |= 64;
            if (localPlayer.isEnergyOverloaded) mask |= 16;
            if (localPlayer.currentHp > 0 && localPlayer.currentHp / Mathf.Max(1f, localPlayer.maxHp) <= .35f) mask |= 32;
        }
        // 2) เหมือนเฟรมก่อน = ไม่ต้องวาดใหม่
        if (mask == previousCombatStatus) return;
        previousCombatStatus = mask;
        combatStatusPanel.gameObject.SetActive(mask != 0);
        for (int i = 0; i < combatStatusLabels.Length; i++) combatStatusLabels[i].gameObject.SetActive(false);
        // 3) ไล่ bit 0-5 แล้ววางป้ายเรียงตามช่อง (bit 64 พิษ ไม่มีช่องของตัวเอง ใช้เปลี่ยนข้อความป้ายช้าแทน)
        int slot = 0;
        for (int bit = 0; bit < 6; bit++)
        {
            if ((mask & (1 << bit)) == 0) continue;
            var label = combatStatusLabels[slot];
            label.gameObject.SetActive(true);
            label.rectTransform.anchoredPosition = new Vector2((slot % 3 - 1) * 210, slot < 3 ? 19 : -19);
            switch (bit)
            {
                case 0: label.text = "STUN / NO CONTROL"; label.color = new Color(1f, .8f, .25f); break;
                case 1: label.text = "SPAWN PROTECTED"; label.color = new Color(.55f, 1f, .8f); break;
                case 2: label.text = "SHIELD ACTIVE"; label.color = new Color(.35f, .85f, 1f); break;
                case 3:
                    label.text = (mask & 64) != 0 ? "POISON / SLOW -30%" : "SLOW / SPEED -30%";
                    label.color = (mask & 64) != 0 ? new Color(.85f,1f,.15f) : new Color(.6f,1f,.4f);
                    break;
                case 4:
                    label.text = (mask & 4) != 0 ? "OVERLOAD / RAPID FIRE" : "OVERLOAD / HP DRAIN";
                    label.color = new Color(1f, .6f, .25f); break;
                default: label.text = "LOW HULL"; label.color = new Color(1f, .35f, .4f); break;
            }
            slot++;
        }
    }

    // เรียกทุกเฟรมจาก Update: อัปเดตความสว่างปุ่ม แผงสถานะ ข้อความสถานะสกิล (READY / วินาทีคูลดาวน์ / STUNNED ฯลฯ)
    // ชื่อคู่แข่ง และรูปคูลดาวน์ (fillAmount = คูลดาวน์ที่เหลือ / คูลดาวน์เต็ม)
    private void UpdateSkillUI()
    {
        UpdateControlFeedback();
        UpdateCombatStatuses();
        if (battleSkillStatus != null)
        {
            bool ready = MatchInputAllowed && localPlayer != null && !localPlayer.isDead && !localPlayer.IsStunned
                && !localPlayer.HasMatchEnded && localPlayer.currentCooldown <= 0;
            battleSkillStatus.text = localPlayer == null ? "WAITING" : localPlayer.isDead ? "OFFLINE"
                : localPlayer.HasMatchEnded ? "OFFLINE" : !MatchInputAllowed ? "GET READY" : localPlayer.IsStunned ? "STUNNED"
                : ready ? "READY" : Mathf.CeilToInt(localPlayer.currentCooldown) + "s";
            battleSkillStatus.color = ready ? new Color(.65f, 1f, .84f) : Color.white;
        }
        if (battleRivalName != null)
            battleRivalName.text = remotePlayer != null ? remotePlayer.PilotName : "WAITING FOR RIVAL";
        if (localPlayer != null && skillCooldownImage != null)
        {
            if (localPlayer.currentCooldown > 0)
            {
                skillCooldownImage.fillAmount = Mathf.Clamp01(localPlayer.currentCooldown / Mathf.Max(0.01f, localPlayer.maxCooldown));
            }
            else
            {
                skillCooldownImage.fillAmount = 0f;
            }
        }
    }
}
