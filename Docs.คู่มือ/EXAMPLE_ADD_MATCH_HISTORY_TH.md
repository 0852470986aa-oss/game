# ตัวอย่างเตรียมเพิ่มฟีเจอร์: ปุ่มเปิดหน้าประวัติแมตช์

เอกสารนี้เป็นตัวอย่างฝึกวิเคราะห์และวางโค้ด หากอาจารย์สั่งว่า “เพิ่มปุ่มดูประวัติการแข่งขัน/แผนที่ที่เคยเล่น” **ยังไม่ได้เพิ่มฟีเจอร์นี้ลงในเกมจริง** ให้ใช้เป็นแผนลงมือ และตรวจโค้ดปัจจุบันใน Unity ก่อนคัดลอกตัวอย่างทุกครั้ง

## เป้าหมายและขอบเขต

เมื่อผู้เล่นกดปุ่ม `MATCH HISTORY` บนหน้า Lobby ให้เปิดหน้ารายการแมตช์ของผู้เล่นคนนั้น แสดงชื่อแผนที่ ผลแพ้/ชนะ/เสมอ วันเวลา และเหรียญรางวัล พร้อมปุ่มกลับหน้า Lobby

ข้อมูลต้นทางที่โค้ดปัจจุบันตั้งใจบันทึกอยู่ที่ Firebase Realtime Database:

```text
match_history/{match_id}/match_id
match_history/{match_id}/user_a
match_history/{match_id}/user_b
match_history/{match_id}/Result
match_history/{match_id}/map_name
match_history/{match_id}/play_date
match_history/{match_id}/reward_a
match_history/{match_id}/reward_b
match_history/{match_id}/room_code
match_history/{match_id}/status
```

ผู้เล่นปัจจุบันอาจเป็น `user_a` หรือ `user_b` จึงต้องตรวจทั้งสองช่อง ไม่ใช่กรองเฉพาะฝั่ง A โดยผล `Win_A` หมายถึงผู้เล่น A ชนะ และ `Win_B` หมายถึงผู้เล่น B ชนะ ต้องแปลงผลตามว่าบัญชีที่เปิดหน้าประวัติอยู่ฝั่งใด

## ไฟล์ที่เกี่ยวข้อง

พาธทั้งหมดนับจากโฟลเดอร์โปรเจกต์:

| ไฟล์ | หน้าที่เมื่อเพิ่มฟีเจอร์ |
|---|---|
| `Assets/Scripts/Managers/FirebaseManager.cs` | อ่าน `match_history` และคัดเฉพาะรายการของ UID ปัจจุบัน |
| `Assets/Scripts/Managers/LobbyManager.Views.cs` | สร้างปุ่มและหน้ารายการประวัติด้วย helper สร้าง UI ของ Lobby |
| `Assets/Scripts/Managers/LobbyManager.cs` | เพิ่มเมธอดเปลี่ยน/กลับหน้า หากโครงสร้าง Panel ปัจจุบันต้องจัดการที่ตัวควบคุมหลัก |
| `Assets/Scripts/Managers/GameplayManager.Results.cs` | จุดที่ผลการแข่งขันเรียก Firebase ให้บันทึกประวัติอยู่แล้ว |

เริ่มจากเปิด `FirebaseManager.RecordMatchResult`, `RecordDrawMatch` และ `WriteMatchRecord` เพื่อดู schema ที่มีจริง ห้ามเดาชื่อ field จาก ER diagram อย่างเดียว เพราะฐานข้อมูลทดสอบอาจมีระเบียนจากบิลด์เก่าที่ใช้ field คนละชื่อหรือไม่มี UID

## ลำดับการทำงาน

```text
กดปุ่ม MATCH HISTORY
        ↓
LobbyManager.Views แสดงหน้า/Panel ประวัติ
        ↓
FirebaseManager ตรวจบัญชีและอ่าน match_history
        ↓
คัดรายการที่ user_a หรือ user_b ตรงกับ Firebase UID ปัจจุบัน
        ↓
แปลงผลตามฝั่งผู้เล่น → แสดงแผนที่ วันเวลา ผล และรางวัล
        ↓
กด BACK → กลับหน้า Lobby
```

## ขั้นที่ 1: เพิ่มเมธอดอ่านข้อมูลใน FirebaseManager.cs

ไฟล์นี้มี `using Firebase.Database;`, `using Firebase.Extensions;`, `using System;` และ `using System.Collections.Generic;` อยู่แล้ว ให้เพิ่มชนิดข้อมูลสำหรับแสดงผลและเมธอดอ่านประวัติใน `FirebaseManager.cs` โดยวางชนิด `MatchHistoryEntry` ไว้นอกคลาส `FirebaseManager` หรือทำเป็นคลาสย่อยตามรูปแบบที่ทีมเลือก

ตัวอย่างโครงสร้างข้อมูลสำหรับส่งให้ UI:

```csharp
[System.Serializable]
public class MatchHistoryEntry
{
    public string mapName;
    public string result;
    public string playedAt;
    public int reward;
}
```

เพิ่มเมธอดสาธารณะใน `FirebaseManager` ตัวอย่างนี้อ่าน node ประวัติแล้วกรอง UID ทั้งสองฝั่ง เนื่องจากตัวอย่างโปรเจกต์เป็นเกมจำนวนน้อยและต้องรองรับ record schema เดิมด้วย:

```csharp
public void GetMyMatchHistory(Action<List<MatchHistoryEntry>, string> onComplete)
{
    if (user == null || dbReference == null)
    {
        onComplete?.Invoke(null, "กรุณาเข้าสู่ระบบก่อน");
        return;
    }

    string uid = user.UserId;
    dbReference.Child("match_history").GetValueAsync()
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted || task.Result == null)
            {
                onComplete?.Invoke(null, "อ่านประวัติไม่สำเร็จ");
                return;
            }

            var entries = new List<MatchHistoryEntry>();
            foreach (DataSnapshot record in task.Result.Children)
            {
                string userA = record.Child("user_a").Value?.ToString();
                string userB = record.Child("user_b").Value?.ToString();
                bool isA = userA == uid;
                bool isB = userB == uid;
                if (!isA && !isB) continue;

                string storedResult = record.Child("Result").Value?.ToString() ?? "Unknown";
                string resultForThisUser = storedResult;
                if (storedResult == "Win_A") resultForThisUser = isA ? "ชนะ" : "แพ้";
                else if (storedResult == "Win_B") resultForThisUser = isB ? "ชนะ" : "แพ้";
                else if (storedResult == "Draw") resultForThisUser = "เสมอ";
                else if (storedResult == "Loss") resultForThisUser = "แพ้"; // รองรับข้อมูลจากบิลด์เก่า

                string rewardField = isA ? "reward_a" : "reward_b";
                int.TryParse(record.Child(rewardField).Value?.ToString(), out int reward);
                entries.Add(new MatchHistoryEntry
                {
                    mapName = record.Child("map_name").Value?.ToString() ?? "ไม่ทราบแผนที่",
                    result = resultForThisUser,
                    playedAt = record.Child("play_date").Value?.ToString() ?? "ไม่ทราบเวลา",
                    reward = reward
                });
            }

            onComplete?.Invoke(entries, null);
        });
}
```

นี่เป็นตัวอย่างสำหรับเริ่มทำ ไม่ใช่โค้ดที่ผ่านการทดสอบในโปรเจกต์แล้ว ก่อนใช้จริงให้ตรวจ namespace, เวอร์ชัน Firebase SDK, ชื่อ field ของข้อมูลเก่า และจัดลำดับวันเวลา (เช่น ใหม่ไปเก่า) ให้เหมาะสม

## ขั้นที่ 2: เพิ่มปุ่มบนหน้า Lobby

เปิด `Assets/Scripts/Managers/LobbyManager.Views.cs` แล้วหา `BuildHomeScreen()` ปัจจุบันมีการสร้างปุ่ม Quick Match, Create/Join Room, Ships & Skills และ How to Play ด้วย `UIButton(...)` อยู่แล้ว เพิ่มปุ่มด้วย helper เดิม เช่น:

```csharp
UIButton("MatchHistory", root, "MATCH HISTORY", x, y, width, height,
    OnMatchHistoryClicked);
```

แทน `x`, `y`, `width`, `height` ด้วยตำแหน่งที่ไม่ทับปุ่มเดิม และตรวจขนาดมือถือ/อัตราส่วนจอกับ `FitLobbyUI()` อย่าแทรกทับตำแหน่งปุ่ม How to Play โดยไม่ย้ายหรือจัด layout ใหม่

เพิ่ม callback ใน partial class `LobbyManager` (อาจอยู่ `LobbyManager.Views.cs` หรือสร้างไฟล์ partial ใหม่ใน `Assets/Scripts/Managers/`):

```csharp
private void OnMatchHistoryClicked()
{
    // 1) ซ่อนหน้า Lobby ที่ไม่เกี่ยวข้อง
    // 2) แสดง History Panel
    // 3) ตั้งข้อความเป็น "กำลังโหลด..."
    // 4) เรียก FirebaseManager.Instance.GetMyMatchHistory(...)
    // 5) แสดงข้อความผิดพลาด/รายการ/กรณีไม่มีประวัติ
}
```

ถ้าสร้าง Panel แบบ runtime ให้ใช้ `BuildSurface`, `UIPanel`, `UILabel`, `UIButton` ที่มีอยู่ใน `LobbyManager.Views.cs` แทนการสร้าง Canvas ซ้ำ ตรวจด้วยว่า `ShowMainPanel()` ซ่อนประวัติ, ปุ่ม BACK ปิดประวัติ และ Panel เริ่มต้นไม่บังหน้า Lobby

รูปแบบบรรทัดที่ UI อาจแสดง:

```text
ABANDONED MECH WARZONE  |  ชนะ  |  +190  |  5 ต.ค. 2026 23:40
```

ควรใช้ Scroll View เมื่อรายการมีโอกาสยาว และมีข้อความแยกสำหรับ “ยังไม่มีประวัติ” กับ “โหลดข้อมูลไม่สำเร็จ” เพื่อไม่ให้ผู้ใช้เข้าใจว่าเป็นกรณีเดียวกัน

## ขั้นที่ 3: ตรวจสิทธิ์และรูปแบบข้อมูล

- ผู้ใช้ต้องล็อกอิน Firebase Auth ก่อนจึงอ่านประวัติได้
- กฎ Realtime Database ที่เปิดอ่านทั้งรากสำหรับผู้ใช้ Auth ทุกคนเหมาะกับการทดสอบชั่วคราวเท่านั้น; ก่อนเผยแพร่ควรจำกัดให้ผู้เล่นเห็นเฉพาะ record ของตน หรือย้ายประวัติไปไว้ใต้ UID เช่น `match_history_by_user/{uid}/{match_id}`
- schema ปัจจุบันเก็บประวัติรวมที่ `/match_history/{match_id}` และ client อ่าน node รวมแล้วกรองเอง ตัวอย่างนี้เข้าใจง่ายสำหรับโปรเจกต์ทดลอง แต่ไม่เหมาะเมื่อมีผู้เล่น/ประวัติจำนวนมาก
- ระเบียนจากบิลด์เก่าอาจมี `user_a` เป็นชื่อ Guest แทน UID, `Result: "Loss"`, `map_name: "Arena"` หรือขาด `match_id`; อย่าทิ้ง record เหล่านี้โดยไม่กำหนดวิธีรองรับ
- ชื่อ `Result` ใช้ตัว R ใหญ่ตามตัวเขียนปัจจุบันใน `FirebaseManager.WriteMatchRecord`; Firebase แยกตัวพิมพ์เล็ก/ใหญ่
- อย่าเพิ่มการบันทึกซ้ำจากปุ่มประวัติ หน้านี้ควรอ่านอย่างเดียว; การเขียนเกิดตอนจบเกมใน `GameplayManager.Results.cs`

## ขั้นที่ 4: ทดสอบทีละกรณี

1. Unity Console ไม่มี compile error และไม่มี Missing Script ใหม่
2. ผู้เล่นที่ยังไม่มีประวัติเปิดหน้าแล้วเห็นข้อความว่างที่เหมาะสม
3. บัญชี A และ B เห็น match เดียวกัน แต่ผลชนะ/แพ้และ reward แสดงตามฝั่งของแต่ละคน
4. ทดสอบผลเสมอ, แผนที่ทั้งสาม, เวลาอ่านไม่ได้, และระเบียนเก่าที่ field UID ว่าง
5. ลองเลื่อนรายการยาวและทดสอบปุ่มกลับบนขนาดจอคอม/มือถือ
6. ยืนยันว่าการเปิดหน้า/รีเฟรชประวัติไม่สร้าง record ใหม่ และหลังจบแมตช์ยังมีเพียงหนึ่ง record ตามการออกแบบปัจจุบัน

## วิธีตอบอาจารย์แบบสั้น

“ผมเริ่มจากตามเส้นทางข้อมูลก่อน คือผลแมตช์ถูกเขียนที่ `GameplayManager.Results.cs` ผ่าน `FirebaseManager.cs` จากนั้นเพิ่มเมธอดอ่านและกรองด้วย Firebase UID แล้วให้ `LobbyManager.Views.cs` สร้างปุ่ม/หน้าแสดงผล การแยกแบบนี้ให้ FirebaseManager รับผิดชอบข้อมูล ส่วน Lobby รับผิดชอบ UI และทดสอบทั้งบัญชีผู้ชนะกับผู้แพ้เพื่อกันแปลผลฝั่ง A/B ผิด”

## พาธไฟล์เต็มบนเครื่องนี้

```text
C:\Users\08524\Documents\BattlefieldOfTheStarsprojactGame\My project\Docs.คู่มือ\EXAMPLE_ADD_MATCH_HISTORY_TH.md
```

