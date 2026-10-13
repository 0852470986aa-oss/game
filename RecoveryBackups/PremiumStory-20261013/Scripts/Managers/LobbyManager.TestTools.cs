// LobbyManager.TestTools.cs — เมนูทดสอบ (เฉพาะ Unity Editor, ไม่มีในไฟล์ Build) สำหรับปรับเลเวลเร็ว ๆ ตอนเทส
// วิธีใช้: กด Play ในฉาก Lobby รอโหลดโปรไฟล์เสร็จ > เลือกวัตถุที่มี LobbyManager > Inspector คลิกขวาหัว Component
// (หรือปุ่ม ⋮) > Test/... เลเวลถูกบันทึกลงเครื่องและ Firebase เหมือนเล่นจริง (สกิลที่ปลดล็อกตามเลเวลอัปเดตทันที)
// ปิดได้ด้วย FeatureFlags.TestTools = false
using UnityEngine;

// ส่วนเมนูทดสอบของ LobbyManager (partial)
public partial class LobbyManager
{
#if UNITY_EDITOR
    // เมนู: เพิ่ม 1 เลเวล
    [ContextMenu("Test/Level +1")]
    private void TestLevelUp1() => TestSetLevel(Progression.Level + 1);
    // เมนู: เพิ่ม 5 เลเวล
    [ContextMenu("Test/Level +5")]
    private void TestLevelUp5() => TestSetLevel(Progression.Level + 5);
    // เมนู: ไปเลเวล 10
    [ContextMenu("Test/Set Level 10")]
    private void TestLevel10() => TestSetLevel(10);
    // เมนู: ไปเลเวลสูงสุด
    [ContextMenu("Test/Set Level MAX")]
    private void TestLevelMax() => TestSetLevel(Progression.MaxLevel);
    // เมนู: กลับไปเลเวล 1 (เทสสกิลล็อก)
    [ContextMenu("Test/Reset to Level 1")]
    private void TestLevel1() => TestSetLevel(1);

    // ตั้งเลเวล (ต้องกด Play และโหลดโปรไฟล์เสร็จก่อน)
    private void TestSetLevel(int level)
    {
        if (!FeatureFlags.TestTools) { Debug.LogWarning("Test tools are off (FeatureFlags.TestTools)."); return; }
        if (!Application.isPlaying || !profileLoaded) { Debug.LogWarning("Press Play and wait for the profile to load first."); return; }
        Progression.SetLevelForTesting(level);
        Debug.Log("[Test] Level set to " + Progression.Level);
    }
#endif
}
