// ไฟล์ PrismReflector.cs: ตัวทำเครื่องหมายคริสตัลที่สะท้อนกระสุนในแม็พปริซึม
// ถูกเพิ่มโดย GameplayManager.DecoratePrism (GameplayManager.Maps.cs) และ PrismArenaVisuals.Decorate
using UnityEngine;

// ติดที่กลุ่มคริสตัลในแม็พปริซึม: กระสุนที่ชนจะเด้งออก (สูงสุด MaxBounces ครั้ง) แทนการระเบิด
// ตัวคำนวณการเด้งอยู่ใน BulletController.TryReflect
[DisallowMultipleComponent]
// คอมโพเนนต์ติดคริสตัล: BulletController ตรวจว่ามีตัวนี้หรือไม่เพื่อเด้งกระสุน
public class PrismReflector : MonoBehaviour
{
    // จำนวนครั้งสูงสุดที่กระสุน 1 ลูกเด้งได้
    public const int MaxBounces = 2;
    // สีฟ้าของแสงวาบตอนกระสุนเด้ง (ใช้กับแสงเรืองรอบคริสตัลด้วย)
    public static readonly Color FlashColor = new Color(.55f, .95f, 1f);

    // แสดงแสงวาบขนาด 1.8 หน่วยที่จุดกระทบ เรียกจาก BulletController.TryReflect
    public void Flash(Vector2 point) => PrismFx.Burst(point, FlashColor, 1.8f);
}
