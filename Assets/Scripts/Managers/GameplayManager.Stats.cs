// GameplayManager.Stats.cs — หน้าสถิติหลังแมตช์ (ปุ่ม MATCH STATS มุมขวาบนของหน้าผล)
// ตาราง: นักบิน / ฆ่า / ตาย / ความแม่นยำ / ดาเมจที่ทำ / ดาเมจที่โดน / ฆ่าต่อเนื่องสูงสุด (ทุกคนรวมบอท)
// ป้ายเกียรติยศ 4 แบบ (ไอคอนจาก Resources/Images/UI):
//   SHARPSHOOTER = แม่นสุด (ยิงอย่างน้อย 5 นัด) / DESTROYER = ดาเมจมากสุด / IRON WALL = รับดาเมจมากสุด / ON FIRE = ฆ่าต่อเนื่องยาวสุด (2+)
// ข้อมูลมาจาก MatchStats.cs (ส่งทุก 1 วิ) อ่านใหม่ทุกครั้งที่เปิด
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนหน้าสถิติหลังแมตช์ของ GameplayManager (partial)
public partial class GameplayManager
{
    private Image statsPanel;
    private TMP_Text statsToggleLabel;

    // สร้างปุ่ม MATCH STATS บนหน้าผล (เรียกจาก FitResultUI ตอนแสดงผล) สร้างครั้งเดียว
    private void EnsureStatsButton()
    {
        if (!FeatureFlags.PostMatchStats || resultSurface == null || resultSurface.Find("StatsToggle") != null) return;
        var toggle = BattlePanel("StatsToggle", resultSurface, 520, 292, 220, 50, new Color(.12f, .2f, .36f));
        toggle.raycastTarget = true;
        var button = toggle.gameObject.AddComponent<Button>();
        button.targetGraphic = toggle;
        button.onClick.AddListener(ToggleStatsPanel);
        statsToggleLabel = BattleLabel("Label", toggle.transform, "MATCH STATS", 0, 0, 200, 40, 19);
        UiIcon.Attach(statsToggleLabel, "accuracy");
    }

    // เปิด/ปิดหน้าสถิติ (สร้างใหม่ทุกครั้งที่เปิด ข้อมูลล่าสุดเสมอ)
    private void ToggleStatsPanel()
    {
        if (statsPanel != null)
        {
            Destroy(statsPanel.gameObject);
            statsPanel = null;
            if (statsToggleLabel != null) statsToggleLabel.text = "MATCH STATS";
            return;
        }
        BuildStatsPanel();
        if (statsToggleLabel != null) statsToggleLabel.text = "RESULTS";
    }

    // สร้างหน้าสถิติ: ป้ายเกียรติยศแถวบน + ตารางทุกคน
    private void BuildStatsPanel()
    {
        MatchStats.Flush(true);
        statsPanel = BattlePanel("StatsPanel", resultSurface, 0, -15, 1060, 400, new Color(.025f, .045f, .09f, .98f));
        statsPanel.raycastTarget = true; // บังตาราง/การ์ดด้านหลัง
        statsPanel.transform.SetAsLastSibling();
        var toggle = resultSurface.Find("StatsToggle");
        if (toggle != null) toggle.SetAsLastSibling();
        var t = statsPanel.transform;

        var standings = MatchRules.AllStandings();
        var rows = new List<KeyValuePair<MatchRules.Standing, int[]>>();
        foreach (var entry in standings) rows.Add(new KeyValuePair<MatchRules.Standing, int[]>(entry, MatchStats.Read(entry.id)));

        // ป้ายเกียรติยศ (แถวบน)
        var badges = new List<string[]>(); // [ไอคอน, ชื่อป้าย, ชื่อคน, ค่า]
        AddBadge(badges, rows, "accuracy", "SHARPSHOOTER", r => r.Value[MatchStats.Shots] >= 5 ? MatchStats.Accuracy(r.Value) : -1, v => v + "%");
        AddBadge(badges, rows, "damage", "DESTROYER", r => r.Value[MatchStats.Dealt], v => v + " DMG");
        AddBadge(badges, rows, "shield", "IRON WALL", r => r.Value[MatchStats.Taken], v => v + " TAKEN");
        AddBadge(badges, rows, "streak", "ON FIRE", r => MatchRules.BestStreak(r.Key.id) >= 2 ? MatchRules.BestStreak(r.Key.id) : -1, v => "x" + v);
        float bw = 1040f / Mathf.Max(1, badges.Count);
        for (int i = 0; i < badges.Count; i++)
        {
            float x = -520f + bw * (i + .5f);
            var chip = BattlePanel("Badge" + i, t, x, 160, bw - 10, 60, new Color(.1f, .16f, .28f));
            var icon = BattlePanel("Icon", chip.transform, -bw * .5f + 34, 0, 40, 40, new Color(1f, .82f, .35f));
            icon.sprite = UiIcon.Load(badges[i][0]); icon.preserveAspect = true;
            var title = BattleLabel("Title", chip.transform, badges[i][1], 22, 14, bw - 70, 24, 15);
            title.color = new Color(1f, .82f, .35f);
            BattleLabel("Who", chip.transform, badges[i][2] + "  " + badges[i][3], 22, -12, bw - 70, 26, 17).richText = false;
        }
        if (badges.Count == 0) BattleLabel("NoBadges", t, "No badges this match", 0, 160, 600, 30, 17).color = Color.gray;

        // ตาราง
        string[] heads = { "PILOT", "KILLS", "DEATHS", "ACCURACY", "DAMAGE", "TAKEN", "BEST STREAK" };
        string[] icons = { "profile", "kill", "death", "accuracy", "damage", "shield", "streak" };
        float[] xs = { -360, -150, -60, 50, 170, 290, 420 };
        float[] ws = { 300, 90, 90, 120, 120, 120, 150 };
        for (int c = 0; c < heads.Length; c++)
        {
            var head = BattleLabel("Head" + c, t, heads[c], xs[c], 110, ws[c], 26, 15);
            head.color = new Color(.45f, .85f, 1f);
            UiIcon.Attach(head, icons[c], .7f);
        }
        int max = Mathf.Min(rows.Count, 10);
        for (int r = 0; r < max; r++)
        {
            var e = rows[r].Key; var s = rows[r].Value;
            float y = 80 - r * 28;
            if (e.isLocal) BattlePanel("You", t, 0, y, 1040, 26, new Color(1f, .85f, .3f, .14f));
            string[] cells = { (r + 1) + ". " + CleanName(e.name) + (e.isBot ? " [BOT]" : ""), e.kills.ToString(), e.deaths.ToString(),
                s[MatchStats.Shots] > 0 ? MatchStats.Accuracy(s) + "%" : "-", s[MatchStats.Dealt].ToString(), s[MatchStats.Taken].ToString(),
                MatchRules.BestStreak(e.id).ToString() };
            for (int c = 0; c < cells.Length; c++)
            {
                var cell = BattleLabel("R" + r + "C" + c, t, cells[c], xs[c], y, ws[c], 24, 16);
                cell.richText = false;
                if (c == 0) cell.alignment = TextAlignmentOptions.Left;
                cell.color = e.isLocal ? new Color(1f, .9f, .55f) : e.left ? Color.gray : Color.white;
            }
        }
        BattleLabel("Note", t, "Accuracy counts weapon shots only. Damage includes skills.", 0, -196, 1000, 22, 13).color = Color.gray;
    }

    // หาคนที่ค่ามากสุด (ค่าติดลบ/0 = ไม่ได้ป้าย) แล้วเพิ่มป้าย
    private static void AddBadge(List<string[]> badges, List<KeyValuePair<MatchRules.Standing, int[]>> rows, string icon, string title,
        System.Func<KeyValuePair<MatchRules.Standing, int[]>, int> value, System.Func<int, string> format)
    {
        int best = 0; string who = null;
        foreach (var row in rows)
        {
            int v = value(row);
            if (v > best) { best = v; who = CleanName(row.Key.name); }
        }
        if (who != null) badges.Add(new[] { icon, title, who, format(best) });
    }
}
