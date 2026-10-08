// MatchStats.cs — สถิติในแมตช์ของผู้ต่อสู้ทุกคน (ใช้แสดงในหน้าผล ปุ่ม MATCH STATS)
// เก็บ 4 ค่า: [0] ยิงไปกี่นัด [1] ยิงโดนกี่นัด (กระสุนเท่านั้น ไม่นับสกิล) [2] ดาเมจที่ทำได้ [3] ดาเมจที่โดน
// เครื่องเจ้าของยานเป็นคนนับของยานตัวเอง (คน = เครื่องตัวเอง, บอท = Master) เก็บยอดรวมไว้ในเครื่อง แล้วส่ง "ยอดรวม" ขึ้นเครือข่ายทุก 1 วิ
//   (ส่งยอดรวมแทนการอ่านค่าเก่าแล้วบวก: ค่าที่อ่านจาก Photon อาจยังไม่อัปเดตถ้าเน็ตช้า จะทำให้ยอดหาย)
//   คน = Player Property "Sx" (int[4]) / บอท = Room Property "SX{id}" — ไม่มีใครเขียนค่าของคนอื่น จึงไม่ชนกัน
// สตรีคยาวสุดคิดจากการตายที่ทุกเครื่องเห็นเหมือนกัน (MatchRules.BestStreak) ไม่ต้องส่ง
// ปิด FeatureFlags.PostMatchStats = ไม่นับ และไม่มีปุ่ม MATCH STATS
using System.Collections.Generic;
using Photon.Pun;

// คลาส static เก็บ/ส่ง/อ่านสถิติแมตช์ของผู้ต่อสู้ทุกคน
public static class MatchStats
{
    public const string PlayerKey = "Sx", BotPrefix = "SX";
    public const int Shots = 0, Hits = 1, Dealt = 2, Taken = 3;
    // ยอดรวมของยานที่เครื่องนี้เป็นเจ้าของ / ยานที่มีค่าใหม่ยังไม่ได้ส่ง
    private static readonly Dictionary<int, float[]> totals = new Dictionary<int, float[]>();
    private static readonly HashSet<int> dirty = new HashSet<int>();
    private static bool freshBots;
    private static float nextFlush;

    // ยานนี้นับอยู่บนเครื่องนี้หรือไม่ (คนของเครื่องนี้ / บอทบน Master)
    private static bool Owned(int id)
        => PhotonNetwork.InRoom && (PhotonNetwork.LocalPlayer != null && id == PhotonNetwork.LocalPlayer.ActorNumber
            || id >= PlayerController.BotIdBase && PhotonNetwork.IsMasterClient);

    // เริ่มแมตช์: fresh = แมตช์ใหม่ (ทุกค่าเริ่ม 0) / false = กลับเข้าแมตช์เดิมหลังหลุด (ต่อยอดจากค่าที่ส่งไว้แล้ว)
    public static void BeginMatch(bool fresh)
    {
        totals.Clear(); dirty.Clear();
        freshBots = fresh;
        if (fresh && PhotonNetwork.LocalPlayer != null) totals[PhotonNetwork.LocalPlayer.ActorNumber] = new float[4];
    }

    // บวกค่าสถิติให้ยานที่เครื่องนี้เป็นเจ้าของ แล้วทำเครื่องหมายว่าต้องส่ง
    private static void Add(int id, int field, float amount)
    {
        if (!FeatureFlags.PostMatchStats || amount <= 0 || !Owned(id)) return;
        // หลังจบแมตช์ (กระสุนที่ค้างกลางอากาศ) ไม่นับ
        if (GameplayManager.Instance != null && !GameplayManager.Instance.MatchInputAllowed) return;
        if (!totals.TryGetValue(id, out var values))
        {
            // ยังไม่เคยนับยานนี้: แมตช์ใหม่ = เริ่ม 0, รับช่วงต่อ (Master เปลี่ยนคน/กลับเข้าแมตช์) = ต่อจากค่าที่ส่งไว้
            values = new float[4];
            if (!(id >= PlayerController.BotIdBase && freshBots)) { var stored = Read(id); for (int i = 0; i < 4; i++) values[i] = stored[i]; }
            totals[id] = values;
        }
        values[field] += amount;
        dirty.Add(id);
    }

    // ยิงกระสุน n นัด (เรียกจาก BulletController.SpawnLocal บนเครื่องคนยิง)
    public static void Shot(int id, int count = 1) => Add(id, Shots, count);
    // ยิงโดน: กระสุนนับเป็นนัดโดน, ดาเมจนับทั้งกระสุนและสกิล (เรียกจาก PlayerController.SendDamage)
    public static void Hit(int id, float damage, bool skill) { if (!skill) Add(id, Hits, 1); Add(id, Dealt, damage); }
    // ยานโดนดาเมจจริง (หลังโล่) เรียกจาก TakeDamage บนเครื่องเจ้าของยาน
    public static void Took(int id, float damage) => Add(id, Taken, damage);

    // ส่งยอดรวมของยานที่มีค่าใหม่ขึ้นเครือข่าย ทุก 1 วิ (force = ส่งทันที ตอนจบแมตช์)
    public static void Flush(bool force = false)
    {
        if (dirty.Count == 0 || !force && UnityEngine.Time.unscaledTime < nextFlush) return;
        nextFlush = UnityEngine.Time.unscaledTime + 1f;
        if (!PhotonNetwork.InRoom) { dirty.Clear(); return; }
        var roomProps = new ExitGames.Client.Photon.Hashtable();
        foreach (int id in dirty)
        {
            var value = Rounded(totals[id]);
            if (id >= PlayerController.BotIdBase) { if (PhotonNetwork.IsMasterClient) roomProps[BotPrefix + id] = value; }
            else if (PhotonNetwork.LocalPlayer != null && id == PhotonNetwork.LocalPlayer.ActorNumber)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [PlayerKey] = value });
        }
        dirty.Clear();
        if (roomProps.Count > 0) PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
    }

    // แปลงยอดทศนิยมเป็นจำนวนเต็ม (ใช้ส่งขึ้นเครือข่าย/แสดงผล)
    private static int[] Rounded(float[] values)
    {
        var result = new int[4];
        for (int i = 0; i < 4; i++) result[i] = UnityEngine.Mathf.RoundToInt(values[i]);
        return result;
    }

    // อ่านสถิติของผู้ต่อสู้ (สำเนาใหม่เสมอ): ยานของเครื่องนี้ใช้ยอดในเครื่อง (ล่าสุดเสมอ) ยานอื่นอ่านจากเครือข่าย
    public static int[] Read(int id)
    {
        if (totals.TryGetValue(id, out var local)) return Rounded(local);
        var result = new int[4];
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return result;
        object value = null;
        if (id >= PlayerController.BotIdBase) room.CustomProperties.TryGetValue(BotPrefix + id, out value);
        else { var player = room.GetPlayer(id); if (player != null) player.CustomProperties.TryGetValue(PlayerKey, out value); }
        if (value is int[] stored) for (int i = 0; i < 4 && i < stored.Length; i++) result[i] = stored[i];
        return result;
    }

    // ความแม่นยำ % (ยังไม่ยิง = 0)
    public static int Accuracy(int[] stats) => stats[Shots] > 0 ? UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(100f * stats[Hits] / stats[Shots]), 0, 100) : 0;
}
