// ไฟล์: FloatingObjectRotator.cs — ใส่กับวัตถุตกแต่งในแม็พให้หมุนช้า ๆ ตลอดเวลา
// เป็นเอฟเฟกต์ภาพล้วน แต่ละเครื่องหมุนเอง ไม่ซิงก์ผ่านเครือข่าย
using UnityEngine;

/// <summary>
/// ทำให้วัตถุหมุนอย่างต่อเนื่อง (สำหรับคริสตัลลอยใน Map1)
/// </summary>
public class FloatingObjectRotator : MonoBehaviour
{
    // ความเร็วหมุนรอบแกน Z (องศาต่อวินาที) ค่าติดลบ = หมุนกลับทาง
    [SerializeField]
    public float rotationSpeed = 15f;

    // ทุกเฟรม: หมุนวัตถุรอบแกน Z ตาม rotationSpeed คูณ Time.deltaTime (ความเร็วไม่ขึ้นกับ FPS)
    void Update()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }
}
