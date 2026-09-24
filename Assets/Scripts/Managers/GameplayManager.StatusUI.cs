using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

// ส่วน StatusUI ของ GameplayManager; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class GameplayManager
{
    public void ShowKillMessage()
    {
        // Simple floating text in the center
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            GameObject txtObj = new GameObject("KillMessage");
            txtObj.transform.SetParent(canvas.transform, false);
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

            Destroy(txtObj, 1.5f); // Automatically destroy after 1.5 seconds
        }
    }

    private void UpdateCombatStatuses()
    {
        if (combatStatusPanel == null || combatStatusLabels == null) return;
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
        if (mask == previousCombatStatus) return;
        previousCombatStatus = mask;
        combatStatusPanel.gameObject.SetActive(mask != 0);
        for (int i = 0; i < combatStatusLabels.Length; i++) combatStatusLabels[i].gameObject.SetActive(false);
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
            battleRivalName.text = remotePlayer != null && remotePlayer.photonView.Owner != null
                ? remotePlayer.photonView.Owner.NickName : "WAITING FOR RIVAL";
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
