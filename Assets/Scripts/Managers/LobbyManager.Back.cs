// LobbyManager.Back.cs — ปุ่มย้อนกลับของมือถือ (Android Back) / ปุ่ม Esc บนคอม ในล็อบบี้
// กด 1 ครั้ง = ปิดหน้าที่อยู่บนสุดก่อน ตามลำดับ: หน้าตั้งค่า > กล่องคำชวน > SOCIAL > WORKSHOP > RANKED/ตารางอันดับ > MISSIONS/PROFILE > วิธีเล่น
// ไม่มีหน้าซ้อน: หน้าค้นหาห้อง/โรงเก็บยาน = กลับหน้าหลัก, ห้องรอ = กด 2 ครั้งเพื่อออกจากห้อง, หน้าหลัก = เปิดหน้าตั้งค่า (มี LOG OUT)
using UnityEngine;

// ส่วนจัดการปุ่มย้อนกลับ (Android Back / Esc) ในล็อบบี้ของ LobbyManager (partial)
public partial class LobbyManager
{
    private float leaveRoomArmedUntil;

    // คืน true ในเฟรมที่กดปุ่ม Back/Esc (ใช้ทั้งล็อบบี้และ GameplayManager) ถ้าโปรเจคปิด Input Manager เดิมจะคืน false
    public static bool BackKeyPressed()
    {
        try { return Input.GetKeyDown(KeyCode.Escape); }
        catch (System.InvalidOperationException) { return false; } // โปรเจคปิด Input Manager เดิม
    }

    // เรียกทุกเฟรมจาก Update
    private void HandleBackKey()
    {
        if (!BackKeyPressed()) return;
        if (BattleSettingsPanel.HandleBack()) return;
        if (CloseCrateReveal()) return; // ปิดอนิเมชันเปิดกล่องก่อน
        if (invitePopup != null && invitePopup.gameObject.activeInHierarchy) { invitePopup.gameObject.SetActive(false); return; }
        if (socialOverlay != null && socialOverlay.gameObject.activeInHierarchy) { CloseSocial(); return; }
        if (workshopOverlay != null && workshopOverlay.gameObject.activeInHierarchy) { CloseWorkshop(); return; }
        if (rankedOverlay != null && rankedOverlay.gameObject.activeInHierarchy)
        {
            if (showingLeaderboard) { showingLeaderboard = false; BuildRankedPage(); }
            else CloseRanked();
            return;
        }
        if (progressOverlay != null && progressOverlay.gameObject.activeInHierarchy) { CloseProgressPage(); return; }
        if (playMenu != null && playMenu.gameObject.activeInHierarchy) { ClosePlayMenu(); return; }
        if (roomRulesMenu != null && roomRulesMenu.gameObject.activeInHierarchy) { CloseRoomRules(); return; }
        if (tutorialPanel != null && tutorialPanel.activeInHierarchy) { OnCloseTutorialClicked(); return; }
        if (settingsPanel != null && settingsPanel.activeInHierarchy) { settingsPanel.SetActive(false); return; }
        if (roomPanel != null && roomPanel.activeInHierarchy) { if (!roomRequestPending) ShowMainPanel(); return; }
        if (inventoryPanel != null && inventoryPanel.activeInHierarchy) { if (!hangarBusy) ShowMainPanel(); return; }
        if (waitingRoomPanel != null && waitingRoomPanel.activeInHierarchy)
        {
            if (Time.unscaledTime < leaveRoomArmedUntil) { leaveRoomArmedUntil = 0; OnLeaveWaitingRoom(); }
            else { leaveRoomArmedUntil = Time.unscaledTime + 2.5f; UpdateStatus("Press BACK again to leave the room."); }
            return;
        }
        OnSettingsClicked();
    }
}
