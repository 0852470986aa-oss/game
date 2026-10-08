// GameplayManager.Progress.cs — ส่งผลแมตช์เข้าระบบเลเวล/ภารกิจ/Achievement (เฟส 4) และแสดง XP ที่ได้ในหน้าผล
// เรียก ReportProgress จากหน้าผลทุกแบบ (1v1 ชนะ/แพ้/เสมอ, FFA, ทีม) — โหมดบทสอนไม่นับ
using UnityEngine;
using Photon.Pun;
using TMPro;

// ส่วนความก้าวหน้าของ GameplayManager: ส่งผลแมตช์ไปคิด XP/ภารกิจ/Achievement และแสดงผลในหน้าสรุป
public partial class GameplayManager
{
    private bool progressReported; // true = ส่งผลแมตช์นี้เข้าระบบความก้าวหน้าแล้ว กันนับซ้ำ
    // แรงค์ที่เพิ่งขึ้นในแมตช์นี้ (-1 = ไม่ได้ขึ้น) ใช้แสดงฉากฉลอง
    private int rankUpTier = -1;

    // เริ่มแมตช์ใหม่: รีเซ็ตตัวนับสกิล (เรียกจาก Start)
    private void ResetMatchProgress()
    {
        Progression.SkillsThisMatch = 0;
        progressReported = false;
    }

    // won/draw = ผลของเรา, place/count = อันดับ (FFA), coins = เหรียญที่ได้จากแมตช์
    private void ReportProgress(bool won, bool draw, int place, int count, int coins)
    {
        if (progressReported || !PhotonNetwork.InRoom) return;
        var room = PhotonNetwork.CurrentRoom;
        if (MatchRules.IsTutorial(room)) return;
        progressReported = true;
        // เฟส 6: แมตช์แรงค์ → คิด MMR ใหม่
        string rankLine = ReportRanked(won, draw);
        if (!Progression.Enabled) { ShowProgressLine(null, rankLine); return; }
        var report = new MatchReport
        {
            mode = gameMode == MatchRules.ModeKoth ? "HILL" : gameMode == MatchRules.ModeStars ? "STARS" : gameMode == MatchRules.ModeSurvival ? "SURVIVAL"
                : gameMode == MatchRules.ModeRoyale ? "ROYALE" : gameMode == MatchRules.ModeCampaign ? "CAMPAIGN"
                : isTeamMode ? "TEAM" : isFreeForAll ? "FFA" : "1V1",
            won = won,
            draw = draw,
            online = !PhotonNetwork.OfflineMode,
            vsBots = MatchRules.IsBotMatch(room),
            hardBot = MatchRules.IsBotMatch(room) && MatchRules.BotDifficulty(room) >= 3,
            kills = MatchRules.LocalKills(),
            deaths = MatchRules.Deaths(PhotonNetwork.LocalPlayer),
            place = place,
            count = count,
            skills = Progression.SkillsThisMatch,
            coins = coins,
            map = GetCurrentMapName()
        };
        var gain = Progression.RecordMatch(report);
        ShowProgressLine(gain, rankLine);
    }

    // แมตช์แรงค์ (ออนไลน์ 1 VS 1 เท่านั้น): คิด MMR จาก MMR ของคู่แข่ง (Player Property "MMR") คืนข้อความสรุป
    private string ReportRanked(bool won, bool draw)
    {
        var room = PhotonNetwork.CurrentRoom;
        if (!MatchRules.IsRanked(room) || PhotonNetwork.OfflineMode || isFreeForAll || MatchRules.IsBotMatch(room)) return null;
        int opponent = Ranked.StartMmr;
        if (PhotonNetwork.PlayerListOthers.Length > 0 && PhotonNetwork.PlayerListOthers[0].CustomProperties.TryGetValue("MMR", out object value) && value is int mmr)
            opponent = mmr;
        int before = Ranked.Mmr;
        int delta = Ranked.RecordMatch(won, draw, opponent);
        string line = "RANKED " + (delta >= 0 ? "+" : "") + delta + " MMR  (" + Ranked.Mmr + " " + Ranked.TierName(Ranked.Mmr) + ")";
        if (Ranked.TierOf(Ranked.Mmr) > Ranked.TierOf(before)) { line += "  RANK UP!"; rankUpTier = Ranked.TierOf(Ranked.Mmr); }
        else if (Ranked.TierOf(Ranked.Mmr) < Ranked.TierOf(before)) line += "  rank down";
        return line;
    }

    // บรรทัดสรุปใต้การ์ดผล: +XP / เลเวล / Achievement ใหม่ / ภารกิจที่รอรับ
    private void ShowProgressLine(ProgressGain gain, string rankLine = null)
    {
        if (resultSurface == null || (gain == null && rankLine == null)) return;
        var old = resultSurface.Find("ProgressLine");
        if (old != null) Destroy(old.gameObject);
        string text = "";
        if (rankLine != null) text += "<color=#FFCC4D>" + rankLine + "</color>    ";
        if (gain != null)
        {
            text += "+" + gain.xp + " XP    LV " + gain.newLevel;
            if (gain.levelsGained > 0) text += "  <color=#FFD95A>LEVEL UP! +" + gain.levelCoins + " ASTRONIUM</color>";
            if (gain.newAchievements.Count > 0) text += "    <color=#7CFFB2>ACHIEVEMENT: " + string.Join(", ", gain.newAchievements) + "</color>";
            if (gain.missionsReady > 0) text += "    <color=#3AD1EB>" + gain.missionsReady + " REWARD(S) READY IN LOBBY</color>";
        }
        var label = BattleLabel("ProgressLine", resultSurface, text, 0, -228, 1180, 24, 18);
        label.richText = true;
        label.color = new Color(.8f, .9f, 1f);
        QueueCelebrations(gain);
    }

    // ฉากฉลองเต็มจอ (CelebrationOverlay.cs): เลเวลอัป → แรงค์อัป → Achievement ใหม่ แสดงต่อกันทีละอัน
    private void QueueCelebrations(ProgressGain gain)
    {
        var canvas = resultSurface != null ? resultSurface.GetComponentInParent<Canvas>()?.rootCanvas.transform : null;
        if (canvas == null) return;
        var font = matchTimerText != null ? matchTimerText.font : null;
        if (gain != null && gain.levelsGained > 0)
            CelebrationOverlay.Show(canvas, font, "LEVEL UP!", "LV " + gain.newLevel, "+" + gain.levelCoins + " ASTRONIUM", UiIcon.Load("star"), new Color(1f, .85f, .35f));
        if (rankUpTier >= 0)
        {
            var sprite = Resources.Load<Sprite>("Images/Ranks/rank_" + Ranked.TierNames[rankUpTier].ToLowerInvariant());
            CelebrationOverlay.Show(canvas, font, "RANK UP!", Ranked.TierNames[rankUpTier], "MMR " + Ranked.Mmr, sprite != null ? sprite : UiIcon.Load("rank"), Ranked.TierColors[rankUpTier]);
            rankUpTier = -1;
        }
        if (gain != null)
            foreach (var name in gain.newAchievements)
                CelebrationOverlay.Show(canvas, font, "ACHIEVEMENT UNLOCKED", name, "", UiIcon.Load("trophy"), new Color(.48f, 1f, .7f));
    }
}
