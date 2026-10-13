// ไฟล์ EditableTemplate.cs: ตัวทำเครื่องหมาย "ต้นแบบ UI" ใน Scene
// ใช้โดย GameplayManager.HUD.cs, GameplayManager.StatusUI.cs และ BattleSettingsPanel.cs ผ่าน EditableTemplate.Spawn
using UnityEngine;

// ต้นแบบ UI ที่บันทึกไว้ใน Scene ให้แก้ในหน้า Edit ได้ (เช่น ป้ายชื่อยาน, ข้อความ KILL, หน้าตั้งค่า)
// ตอนเกมรัน ต้นแบบจะซ่อนตัวเองเสมอ และโค้ดจะ "คัดลอก" ต้นแบบไปใช้แทนการสร้างใหม่
// ไม่ต้องลบหรือปิดเอง ถ้าลืมเปิดค้างไว้ตอนกด Play ก็จะไม่โผล่ในเกม
[DisallowMultipleComponent]
// คอมโพเนนต์ติดที่ต้นแบบ UI: ซ่อนต้นแบบตอนเล่น และมีเมธอด Spawn สำหรับคัดลอก
public class EditableTemplate : MonoBehaviour
{
    // ตอนเริ่ม (เฉพาะตอนเกมรัน) ซ่อนต้นแบบไม่ให้แสดงบนจอ
    private void Awake()
    {
        if (Application.isPlaying) gameObject.SetActive(false);
    }

    // คัดลอกต้นแบบโดยไม่พาตัวซ่อนอัตโนมัติไปด้วย
    public static GameObject Spawn(GameObject template, Transform parent)
    {
        bool wasActive = template.activeSelf;
        // ปิดต้นแบบชั่วคราวเพื่อไม่ให้สำเนาเรียก Awake ก่อนเอาตัวซ่อนออก
        if (wasActive) template.SetActive(false);
        GameObject copy = parent != null ? Instantiate(template, parent, false) : Instantiate(template);
        if (wasActive) template.SetActive(true);
        var marker = copy.GetComponent<EditableTemplate>();
        if (marker != null) DestroyImmediate(marker);
        copy.SetActive(true);
        return copy;
    }
}
