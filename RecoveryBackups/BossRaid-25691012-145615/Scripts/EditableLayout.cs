// ไฟล์ EditableLayout.cs: ตัวทำเครื่องหมายว่าเลย์เอาต์แม็พใน Scene ถูกจัดเองแล้ว
// GameplayManager.PrepareMechCover (GameplayManager.Maps.cs) เช็กตัวนี้ก่อนจะย้ายสิ่งกีดขวางแม็พหุ่นยนต์
using UnityEngine;

// ติดไว้ที่ MapX_Layout หลังใช้เมนู "Editable/2. Build Map Layouts into Scene"
// มีตัวนี้ = ตำแหน่ง/ขนาดสิ่งกีดขวางที่จัดใน Scene คือของจริง โค้ดจะไม่ย้ายกลับตอนรัน
// ลบ Component นี้ออก = กลับไปใช้ตำแหน่งที่โค้ดกำหนดแบบเดิม
[DisallowMultipleComponent]
// คลาสว่าง ใช้เป็นแค่ป้ายบอก (ไม่มีโค้ดทำงาน)
public class EditableLayout : MonoBehaviour
{
}
