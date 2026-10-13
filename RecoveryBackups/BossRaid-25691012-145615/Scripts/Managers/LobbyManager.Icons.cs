// LobbyManager.Icons.cs — ใส่ไอคอนให้ปุ่ม/ป้ายในล็อบบี้ (ไอคอนอยู่ Resources/Images/UI, ตัวจัดตำแหน่งอยู่ UiIcon.cs)
// เรียกครั้งเดียวท้าย BuildLobbyUI (หลังจัดหน้าทุกอย่างเสร็จ) / ปิด FeatureFlags.UiIcons = ไม่มีไอคอน
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนใส่ไอคอนปุ่มในล็อบบี้ของ LobbyManager (partial)
public partial class LobbyManager
{
    // ใส่ไอคอนหน้าข้อความให้ปุ่มทุกหน้าในล็อบบี้ (เรียกท้าย BuildLobbyUI)
    private void ApplyLobbyIcons()
    {
        if (!FeatureFlags.UiIcons || !Application.isPlaying) return;
        // หน้าหลัก
        UiIcon.OnButton(playButton, "quick");
        UiIcon.OnButton(FindIn(mainPanel, "LobbySurface/ChooseMode"), "modes");
        UiIcon.OnButton(inventoryButton, "ship");
        UiIcon.OnButton(rankedButton, "trophy");
        UiIcon.OnButton(missionsButton, "missions");
        UiIcon.OnButton(profileButton, "profile");
        UiIcon.OnButton(socialButton, "friends");
        UiIcon.OnButton(settingsButton, "settings");
        // หน้าเลือกวิธีเล่น
        if (playMenu != null)
        {
            UiIcon.OnButton(playMenu.Find("Back"), "back");
            UiIcon.OnButton(createRoomButton, "home");
            UiIcon.OnButton(soloDifficultyButton, "bot");
            UiIcon.OnButton(soloPlayButton, "play");
            UiIcon.OnButton(trainingButton, "target");
            string[] tabIcons = { "kill", "star", "target" };
            if (modeCategoryTabs != null)
                for (int i = 0; i < modeCategoryTabs.Length && i < tabIcons.Length; i++) UiIcon.OnButton(modeCategoryTabs[i], tabIcons[i]);
        }
        // หน้าค้นหาห้อง
        UiIcon.OnButton(backFromRoomButton, "back");
        UiIcon.OnButton(createRoomConfirmButton, "home");
        UiIcon.OnButton(searchRoomButton, "play");
        // โรงเก็บยาน
        UiIcon.OnButton(backFromInventoryButton, "back");
        var hangar = FindIn(inventoryPanel, "LobbySurface");
        if (hangar != null)
        {
            UiIcon.OnButton(hangar.Find("ShipsTab"), "ship");
            UiIcon.OnButton(hangar.Find("SkillsTab"), "skill");
            UiIcon.OnButton(hangar.Find("WorkshopTab0"), "upgrade");
            UiIcon.OnButton(hangar.Find("WorkshopTab1"), "items");
            UiIcon.OnButton(hangar.Find("WorkshopTab2"), "shop");
        }
        UiIcon.Attach(inventoryShipHP, "hp");
        UiIcon.Attach(inventoryShipATK, "atk");
        UiIcon.Attach(inventoryShipSPD, "spd");
        UiIcon.OnButton(installSkillButton, "ready");
        // ห้องรอ
        var wait = FindIn(waitingRoomPanel, "LobbySurface");
        if (wait != null)
        {
            UiIcon.OnButton(wait.Find("CopyCode"), "copy");
            UiIcon.OnButton(wait.Find("OpenRoomRules"), "settings");
        }
        UiIcon.OnButton(inviteFriendsButton, "invite");
        UiIcon.OnButton(waitReadyButton, "ready");
        UiIcon.OnButton(waitCancelButton, "leave");
        UiIcon.OnButton(waitStartButton, "play");
        // ตั้งค่าห้อง: ไอคอนหน้าหัวข้อแต่ละกติกา (ลำดับเดียวกับ BuildRoomNavigation)
        if (roomRulesMenu != null)
        {
            UiIcon.OnButton(roomRulesMenu.Find("Back"), "back");
            string[] ruleIcons = { "friends", "mode", "target", "time", "hazard", "bot", "upgrade", "powerup", "trophy" };
            for (int i = 0; i < ruleIcons.Length; i++)
                UiIcon.Attach(roomRulesMenu.Find("RuleLabel" + i)?.GetComponent<TMP_Text>(), ruleIcons[i], .8f);
        }
    }

    // หาลูกตามพาธใต้ panel (panel ไม่มี = null)
    private static Transform FindIn(GameObject panel, string path) => panel != null ? panel.transform.Find(path) : null;
}
