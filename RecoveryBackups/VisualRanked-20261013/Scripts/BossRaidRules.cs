// BossRaidRules.cs — ค่ากลางของโหมดร่วมมือปราบบอส แยกจากบาลานซ์โหมดเดิม
using UnityEngine;
using Photon.Realtime;

public static class BossRaidRules
{
    public const int Mode = 6;
    public const int SoloChoice = 12;
    public const int MaxPilots = 4;
    public const int Lives = 3;
    public const int Seconds = 480;
    public const string Prefix = "Raid.";
    public const string Spawned = Prefix + "Spawned";
    public const string PhaseKey = Prefix + "Phase";
    public const string MinionsKey = Prefix + "Minions";
    public const string TicketKey = Prefix + "Ticket";
    public const string RoundKey = Prefix + "Round";

    public static bool Enabled(RoomInfo room) => MatchRules.GameMode(room) == Mode;
    // ใช้จำนวนผู้ร่วมรบตอนเริ่ม ไม่ลดเลือดบอสเมื่อเพื่อนหลุดกลางเกม
    public static float HealthMultiplier(int pilots) => 8f + 4f * (Mathf.Clamp(pilots, 1, MaxPilots) - 1);
    public static int Phase(float hp, float maxHp) => hp > maxHp * .66f ? 1 : hp > maxHp * .33f ? 2 : 3;
    public static int Reward(bool won) => won ? 200 : 15;

    // เปลี่ยนข้อมูลในหน่วยความจำอย่างเดียว ผู้เรียกเป็นผู้บันทึกเมื่อรับสำเร็จ
    // แยกไว้เพื่อทดสอบการรับซ้ำโดยไม่แตะ PlayerPrefs/Firebase ของผู้เล่น
    public static bool TryGrantTicket(PremiumData data, string roundToken, bool eligible)
    {
        if (!eligible || data == null || string.IsNullOrEmpty(roundToken) || data.lastRaidTicketToken == roundToken) return false;
        data.lastRaidTicketToken = roundToken;
        data.tickets++;
        return true;
    }
}
