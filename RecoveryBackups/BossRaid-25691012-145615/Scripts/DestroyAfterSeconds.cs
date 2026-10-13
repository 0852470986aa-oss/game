// ไฟล์: DestroyAfterSeconds.cs — ใส่ไว้กับเอฟเฟกต์ชั่วคราว (prefab ที่ Editor/PrefabSetupTool และ SceneSetupTool สร้าง พร้อมตั้ง lifetime)
// ทำงานเฉพาะเครื่องตัวเอง ไม่เกี่ยวกับเครือข่าย
using UnityEngine;

// ตัวช่วยลบเอฟเฟกต์/วัตถุชั่วคราวเมื่อหมดอายุ ป้องกันวัตถุค้างในฉาก
// คอมโพเนนต์ที่ทำลาย GameObject ของตัวเองหลังครบเวลา lifetime
public class DestroyAfterSeconds : MonoBehaviour
{
    // อายุของวัตถุ (วินาที) นับจากตอน Start
    public float lifetime = 1f;

    // Unity เรียกเมื่อวัตถุเริ่มทำงาน: สั่ง Destroy แบบหน่วงเวลา lifetime วินาที
    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
