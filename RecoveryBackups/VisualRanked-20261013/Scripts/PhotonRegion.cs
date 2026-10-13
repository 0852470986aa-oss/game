// PhotonRegion.cs — ล็อก Photon ให้ทุกเครื่องเข้า region เดียวกัน (asia)
// ปัญหาเดิม: PhotonServerSettings ไม่ได้ตั้ง Fixed Region → แต่ละเครื่องเลือก region ที่ ping ดีที่สุดเอง (ใน Editor ใช้ Dev Region asia)
//   ถ้าเพื่อนอยู่คนละ region ห้องจะมองไม่เห็นกัน → จอยด้วยเลขห้องไม่ได้ (Room not found) / รายการห้องว่าง
// แก้: ก่อนโหลดฉากแรก ตั้ง FixedRegion = "asia" (ถ้ายังไม่ได้ตั้งไว้ใน PhotonServerSettings) ทุกการเชื่อมต่อ ConnectUsingSettings จึงไป region เดียวกัน
// เปลี่ยน region ได้ที่ Region ด้านล่าง / ตั้ง Fixed Region ใน PhotonServerSettings เอง = ใช้ค่านั้นแทน / ปิด FeatureFlags.FixedRegion ในโค้ด = แบบเดิม
// (ทำงานเฉพาะในเกมที่ build แล้ว — Editor ใช้ Dev Region asia อยู่แล้ว / สวิตช์นี้อ่านก่อนโหลดค่าจาก Firebase จึงปิดจาก Remote Config ไม่ได้)
using Photon.Pun;
using UnityEngine;

// คลาส static ตั้ง Fixed Region ของ Photon ก่อนเกมเริ่ม
public static class PhotonRegion
{
    // region ที่ทุกเครื่องใช้ (asia = สิงคโปร์ ใกล้ไทยที่สุด)
    public const string Region = "asia";

    // Unity เรียกเองก่อนโหลดฉากแรก (ก่อนทุก ConnectUsingSettings): ตั้ง FixedRegion ถ้ายังว่าง
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_EDITOR
        // ใน Editor ไม่แก้ไฟล์ PhotonServerSettings (ค่าจะติดค้างหลังกด Stop) — Editor ใช้ Dev Region (asia) อยู่แล้ว
        return;
#else
        if (!FeatureFlags.FixedRegion) return;
        var settings = PhotonNetwork.PhotonServerSettings;
        if (settings == null || settings.AppSettings == null) return;
        if (string.IsNullOrEmpty(settings.AppSettings.FixedRegion)) settings.AppSettings.FixedRegion = Region;
#endif
    }
}
