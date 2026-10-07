// ไฟล์ CheckSprites.cs — เครื่องมือ Unity Editor (ไม่ได้รันในเกม / ไม่ถูก build ลงมือถือ)
// เพิ่มเมนู Tools > Check Sprites ไว้ตรวจว่าไฟล์ภาพ Assets/Resources/Images/Obs_Asteroids.png
// ถูกตัดเป็น sub-asset (Sprite ย่อย) กี่ชิ้น แล้วพิมพ์จำนวนออก Console ใช้ตอนดีบักภาพอุกกาบาต
// โค้ดทั้งคลาสเขียนไว้ในบรรทัดเดียว: คลาส CheckSprites มีเมธอด static Check() ที่ถูกเรียกเมื่อกดเมนู
// Check() ใช้ AssetDatabase.LoadAllAssetsAtPath โหลดทุก asset ในไฟล์ภาพนั้นแล้ว Debug.Log จำนวน
using UnityEditor; using UnityEngine; public class CheckSprites { [MenuItem("Tools/Check Sprites")] public static void Check() { Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Images/Obs_Asteroids.png"); Debug.Log("Obs_Asteroids sub-assets: " + assets.Length); } }

