// LobbyManager.Content.cs — ส่วนล็อบบี้ของเนื้อหาเฟส 7 (partial class ของ LobbyManager)
// - จัดการ์ดยาน/สกิลใน Hangar ใหม่เมื่อมีมากกว่า 3 ลำ / 4 สกิล (การ์ดเล็กลงให้พอดีจอ) และซ่อนของใหม่เมื่อปิดสวิตช์
// - สกิลที่ยังไม่มีไอคอน: แสดงกล่องสี + ตัวย่อแทน (ใส่รูป Images/icon_*.png แล้วจะเปลี่ยนเอง)
// - ปุ่ม POWER-UPS ในห้องรอ (Host เปิด/ปิดไอเท็มเกิดในแม็พ)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public partial class LobbyManager
{
    // เรียกจาก BuildHangarScreen หลังสร้างการ์ดทั้งหมด
    private void LayoutExtraHangarCards()
    {
        // ===== ยาน =====
        var visibleShips = new System.Collections.Generic.List<int>();
        for (int i = 0; i < ships.Length; i++)
        {
            bool on = i < 3 || FeatureFlags.NewShips;
            if (shipButtons != null && i < shipButtons.Length && shipButtons[i] != null) shipButtons[i].gameObject.SetActive(on);
            if (on) visibleShips.Add(i);
        }
        if (visibleShips.Count > 3 && shipButtons != null)
        {
            // 2 คอลัมน์ x 3 แถว ในพื้นที่ฝั่งซ้ายเดิม
            for (int n = 0; n < visibleShips.Count; n++)
            {
                var card = shipButtons[visibleShips[n]];
                var rect = (RectTransform)card.transform;
                rect.anchoredPosition = new Vector2(n % 2 == 0 ? -497 : -313, 140 - (n / 2) * 142);
                rect.sizeDelta = new Vector2(180, 126);
                Place(card.transform, "ShipArt", 0, 20, 120, 72);
                var name = Place(card.transform, "Name", 0, -27, 170, 26);
                var label = name != null ? name.GetComponent<TMP_Text>() : null;
                if (label != null) { label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 17; }
                var own = Place(card.transform, "Ownership", 0, -50, 170, 20);
                var ownLabel = own != null ? own.GetComponent<TMP_Text>() : null;
                if (ownLabel != null) { ownLabel.enableAutoSizing = true; ownLabel.fontSizeMin = 10; ownLabel.fontSizeMax = 14; }
            }
        }
        // ===== สกิล =====
        var visibleSkills = new System.Collections.Generic.List<int>();
        for (int i = 0; i < skills.Length; i++)
        {
            bool on = i < 4 || FeatureFlags.NewSkills;
            if (hangarSkillCards != null && i < hangarSkillCards.Length && hangarSkillCards[i] != null)
            {
                hangarSkillCards[i].gameObject.SetActive(on);
                SkillIconFallback(hangarSkillCards[i].transform, i);
            }
            if (on) visibleSkills.Add(i);
        }
        if (visibleSkills.Count > 4 && hangarSkillCards != null)
        {
            int m = visibleSkills.Count;
            float width = Mathf.Min(270f, 1140f / m - 10f);
            for (int n = 0; n < m; n++)
            {
                var card = hangarSkillCards[visibleSkills[n]];
                var rect = (RectTransform)card.transform;
                rect.anchoredPosition = new Vector2(-(m - 1) * (width + 10f) / 2f + n * (width + 10f), 116);
                rect.sizeDelta = new Vector2(width, 160);
                Place(card.transform, "State", 0, 65, width - 16, 22);
                Place(card.transform, "Icon", 0, 18, Mathf.Min(88, width - 30), Mathf.Min(88, width - 30));
                var name = Place(card.transform, "SkillName", 0, -55, width - 16, 35);
                var label = name != null ? name.GetComponent<TMP_Text>() : null;
                if (label != null) { label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 20; }
            }
        }
    }

    private static RectTransform Place(Transform parent, string child, float x, float y, float w, float h)
    {
        var rect = parent.Find(child) as RectTransform;
        if (rect == null) return null;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    // สกิลที่ยังไม่มีรูปไอคอน: กล่องสี + ตัวอักษร 2 ตัวแรก
    private void SkillIconFallback(Transform card, int index)
    {
        var icon = card.Find("Icon");
        var image = icon != null ? icon.GetComponent<Image>() : null;
        if (image == null || image.sprite != null) return;
        image.color = accentColor * .45f + new Color(0, 0, 0, .55f);
        UILabel("Initials", icon, skills[index].name.Substring(0, Mathf.Min(2, skills[index].name.Length)), 0, 0, 80, 80, 30, Color.white);
    }

    // ===== ห้องรอ: ปุ่ม POWER-UPS =====
    private Button roomPowerUpsButton;

    private void BuildRoomPowerUpsButton(RectTransform root)
    {
        roomPowerUpsButton = UIButton("RoomPowerUps", root, "POWER-UPS ON", 210, -334, 140, 44, () =>
        {
            if (!CanEditRoomSettings()) return;
            SetRoomRule(MatchRules.PowerUpsKey, !MatchRules.PowerUpsEnabled(PhotonNetwork.CurrentRoom));
        });
    }

    private void RefreshRoomPowerUpsButton()
    {
        if (roomPowerUpsButton == null) return;
        bool visible = PhotonNetwork.InRoom && FeatureFlags.PowerUps;
        roomPowerUpsButton.gameObject.SetActive(visible);
        if (!visible) return;
        roomPowerUpsButton.interactable = CanEditRoomSettings();
        SetButtonLabel(roomPowerUpsButton, MatchRules.PowerUpsEnabled(PhotonNetwork.CurrentRoom) ? "POWER-UPS ON" : "POWER-UPS OFF");
    }

    // ===== ห้องรอ: ปุ่มเลือกโหมดเกม (เฟส 7B) =====
    // ออนไลน์เลือกได้: ปกติ / ยึดจุด / เก็บดาว / Battle Royale / Survival (Campaign เล่นคนเดียวเท่านั้น)
    private static readonly int[] OnlineModes = { MatchRules.ModeDeathmatch, MatchRules.ModeKoth, MatchRules.ModeStars, MatchRules.ModeRoyale, MatchRules.ModeSurvival };
    private Button roomGameModeButton;

    private void BuildRoomGameModeButton(RectTransform root)
    {
        roomGameModeButton = UIButton("RoomGameMode", root, "DEATHMATCH", 545, -37, 170, 34, () =>
        {
            if (!CanEditRoomSettings()) return;
            int current = System.Array.IndexOf(OnlineModes, MatchRules.GameMode(PhotonNetwork.CurrentRoom));
            SetRoomRule(MatchRules.GameModeKey, OnlineModes[(current + 1) % OnlineModes.Length]);
        });
    }

    private void RefreshRoomGameModeButton()
    {
        if (roomGameModeButton == null) return;
        bool visible = PhotonNetwork.InRoom && FeatureFlags.GameModes;
        roomGameModeButton.gameObject.SetActive(visible);
        if (!visible) return;
        roomGameModeButton.interactable = CanEditRoomSettings();
        SetButtonLabel(roomGameModeButton, MatchRules.ModeTitles[MatchRules.GameMode(PhotonNetwork.CurrentRoom)]);
    }
}
