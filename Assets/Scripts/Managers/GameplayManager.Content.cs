// GameplayManager.Content.cs — ส่วนเนื้อหาเฟส 7 ฝั่งฉากต่อสู้ (partial class ของ GameplayManager)
// - Kill Streak: นับการฆ่าต่อเนื่องโดยไม่ตายของทุกคน ประกาศใน Kill Feed + ป้ายกลางจอของเรา (DOUBLE KILL / KILLING SPREE / RAMPAGE ...)
// - ป้ายข้อความกลางจอสั้นๆ (ใช้กับ Kill Streak และตอนเก็บไอเท็มในแม็พ)
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public partial class GameplayManager
{
    private readonly Dictionary<int, int> killStreaks = new Dictionary<int, int>();
    private TMP_Text bannerText;
    private float bannerUntil;

    // ชื่อประกาศตามจำนวนฆ่าต่อเนื่อง
    private static string StreakName(int streak)
    {
        switch (streak)
        {
            case 2: return "DOUBLE KILL";
            case 3: return "KILLING SPREE";
            case 5: return "RAMPAGE";
            case 7: return "UNSTOPPABLE";
            case 10: return "GODLIKE";
            default: return null;
        }
    }

    // เรียกทุกเครื่องเมื่อมียานตาย (จาก OnCombatantDied)
    private void TrackKillStreak(int killerId, PlayerController victim)
    {
        if (victim == null) return;
        killStreaks[victim.CombatantId] = 0;
        if (!FeatureFlags.KillStreaks || killerId <= 0 || killerId == victim.CombatantId) return;
        killStreaks.TryGetValue(killerId, out int streak);
        streak++;
        killStreaks[killerId] = streak;
        string title = StreakName(streak);
        if (title == null) return;
        var killer = PlayerController.FindCombatant(killerId);
        string name = killer != null ? killer.PilotName : "PILOT";
        bool mine = killer != null && killer == localPlayer;
        AddFeedLine("<color=#FFB13B>" + title + "</color>  " + CleanName(name), mine ? new Color(1f, .85f, .35f) : Color.white);
        if (mine) ShowBanner(title + "!", new Color(1f, .7f, .25f));
    }

    // ป้ายข้อความกลางจอ 1.6 วินาที
    public void ShowBanner(string text, Color color)
    {
        if (battleHud == null || isMatchEnding) return;
        if (bannerText == null)
        {
            bannerText = BattleLabel("Banner", battleHud, "", 0, 150, 900, 60, 40);
            bannerText.outlineWidth = .25f;
            bannerText.outlineColor = Color.black;
        }
        bannerText.text = text;
        bannerText.color = color;
        bannerText.gameObject.SetActive(true);
        bannerUntil = Time.unscaledTime + 1.6f;
    }

    // เรียกทุกเฟรมจาก UpdateMultiplayer
    private void UpdateBanner()
    {
        if (bannerText != null && bannerText.gameObject.activeSelf && Time.unscaledTime >= bannerUntil) bannerText.gameObject.SetActive(false);
    }
}
