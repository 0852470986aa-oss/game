// GameplayManager.WinRules.cs — ฝั่งสนามรบของเงื่อนไขชนะ (กติกาอยู่ใน MatchRules.WinRules.cs)
// เงื่อนไขแบบใหม่ (MOST KILLS / NET SCORE / MOST DAMAGE / BOUNTY HUNT) ไม่จบกลางคัน เล่นจนหมดเวลาแล้วตัดสินจากคะแนน
using Photon.Pun;
using UnityEngine;

// ส่วนเงื่อนไขชนะฝั่งสนามรบของ GameplayManager (partial)
public partial class GameplayManager
{
    // แมตช์นี้ใช้คะแนนตามเงื่อนไขชนะแบบใหม่ (โหมดปกติเท่านั้น)
    private bool WinRuleActive => gameMode == MatchRules.ModeDeathmatch && MatchRules.UsesRuleScore(PhotonNetwork.CurrentRoom);

    // คะแนนของเรา / คะแนนสูงสุดของคู่แข่ง ตามเงื่อนไขชนะ
    private int MyRuleScore()
    {
        foreach (var entry in MatchRules.AllStandings()) if (entry.isLocal) return MatchRules.RuleScore(entry);
        return 0;
    }

    // คะแนนสูงสุดของคนอื่น (ไม่รวมเรา) ตามเงื่อนไขชนะ
    private int BestRivalRuleScore()
    {
        int best = int.MinValue;
        foreach (var entry in MatchRules.AllStandings()) if (!entry.isLocal) best = Mathf.Max(best, MatchRules.RuleScore(entry));
        return best == int.MinValue ? 0 : best;
    }

    // ข้อความเป้าหมายใต้เวลา (HUD)
    private string WinRuleObjective()
    {
        switch (MatchRules.WinRule(PhotonNetwork.CurrentRoom))
        {
            case MatchRules.WinMostKills: return "MOST KILLS WHEN TIME RUNS OUT";
            case MatchRules.WinNetScore: return "KILL +1  /  DEATH -1";
            case MatchRules.WinDamage: return "MOST DAMAGE WHEN TIME RUNS OUT";
            case MatchRules.WinBounty: return "KILL STREAKERS FOR BONUS POINTS";
        }
        return "FIRST TO " + targetKills + " KILLS";
    }

    // หมดเวลา: รอ 1.2 วิให้คะแนนสุดท้ายของทุกเครื่องซิงก์ถึงกัน แล้วจึงตัดสิน
    private bool endPending;

    // Coroutine จบแมตช์แบบรอซิงก์คะแนน (ดูคำอธิบายด้านบน)
    private System.Collections.IEnumerator EndMatchAfterSync()
    {
        endPending = true;
        if (localPlayer != null) localPlayer.photonView.RPC("SetMatchEndedRPC", RpcTarget.All); // หยุดยิงทันที
        yield return new WaitForSecondsRealtime(1.2f);
        endPending = false;
        // ระหว่างรอ ถ้าออกจากห้อง/กำลังกลับห้องรอ ไม่ต้องสรุปผลแล้ว
        if (intentionalLeave || returningToRoom || resultShown || !PhotonNetwork.InRoom) yield break;
        EndMatch();
    }

    // ป้ายค่าหัวเมื่อเราฆ่าคนที่กำลังฆ่าต่อเนื่อง (เรียกจาก OnCombatantDied)
    private void AwardBountyFor(int killerId, PlayerController victim)
    {
        int points = MatchRules.AwardBounty(killerId, victim);
        if (points > 1 && localPlayer != null && killerId == localPlayer.CombatantId)
            ShowBanner("BOUNTY +" + points, new Color(1f, .82f, .3f));
    }
}
