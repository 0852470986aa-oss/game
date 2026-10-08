// LobbyManager.Progress.cs — หน้าจอระบบความก้าวหน้าในล็อบบี้ (เฟส 4) (partial class ของ LobbyManager)
// หน้าหลัก: ป้ายเลเวล + แถบ XP ในกล่องโปรไฟล์, ปุ่ม MISSIONS / PROFILE (มีตัวเลขบอกรางวัลที่รอรับ)
// หน้า MISSIONS: ภารกิจรายวัน 3 ข้อ, รายสัปดาห์ 3 ข้อ, รางวัลล็อกอิน 7 วัน (กดรับเหรียญ+XP)
// หน้า PROFILE: เลเวล ฉายา (กดเปลี่ยนได้) สถิติ Achievement 12 อัน และประวัติแมตช์ล่าสุด
// ข้อมูล/กติกาทั้งหมดอยู่ใน Progression.cs — ไฟล์นี้ทำแค่หน้าจอ
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ส่วนหน้าจอความก้าวหน้าของ LobbyManager: ป้ายเลเวล/แถบ XP, หน้า MISSIONS และหน้า PROFILE
public partial class LobbyManager
{
    private Button missionsButton, profileButton;
    private TMP_Text levelBadgeText, missionsBadge;
    private Image xpFill;
    private Image progressOverlay;
    private RectTransform progressWindow;
    private RectTransform progressPage;
    private bool progressSubscribed;
    private bool loginPopupShown;
    private int pageSerial;
    // หน้าที่เปิดอยู่ในหน้าต่างความก้าวหน้า (None = ปิดอยู่)
    private enum ProgressPage { None, Missions, Profile }
    private ProgressPage openPage;

    // สร้างส่วนเลเวลบนหน้าหลัก (เรียกจาก BuildHomeScreen)
    private void BuildProgressUI(RectTransform root)
    {
        // ป้ายเลเวลข้างกล่องโปรไฟล์
        var badge = UIPanel("LevelBadge", root, -12, 222, 78, 78, new Color(.08f, .26f, .36f));
        levelBadgeText = UILabel("Level", badge.transform, "LV\n1", 0, 0, 74, 70, 22, Color.white);
        levelBadgeText.textWrappingMode = TextWrappingModes.Normal;
        // แถบ XP ใต้กล่องโปรไฟล์
        var profile = root.Find("PilotProfile");
        if (profile != null)
        {
            var track = UIPanel("XpTrack", profile, 35, -42, 420, 6, new Color(.12f, .15f, .22f));
            xpFill = UIPanel("XpFill", track.transform, -210, 0, 420, 6, accentColor);
            xpFill.rectTransform.pivot = new Vector2(0, .5f);
            xpFill.rectTransform.anchoredPosition = new Vector2(-210, 0);
        }
        // ปุ่ม MISSIONS / PROFILE แทนป้าย "READY FOR BATTLE?"
        var battleTitle = root.Find("BattleTitle");
        if (battleTitle != null) battleTitle.gameObject.SetActive(!FeatureFlags.Progression);
        missionsButton = UIButton("Missions", root, "MISSIONS", 300, 127, 180, 46, () => OpenProgressPage(ProgressPage.Missions));
        profileButton = UIButton("Profile", root, "PROFILE", 490, 127, 180, 46, () => OpenProgressPage(ProgressPage.Profile));
        var dot = UIPanel("Badge", missionsButton.transform, 80, 18, 30, 30, new Color(.9f, .25f, .2f));
        missionsBadge = UILabel("Count", dot.transform, "", 0, 0, 30, 30, 16, Color.white);
        // หน้าต่างลอย (ซ่อนไว้)
        progressOverlay = UIPanel("ProgressOverlay", root, 0, 0, 1280, 720, new Color(0, 0, 0, .72f));
        progressOverlay.raycastTarget = true;
        progressWindow = UIPanel("ProgressWindow", progressOverlay.transform, 0, 0, 1060, 620, panelColor).rectTransform;
        progressOverlay.gameObject.SetActive(false);
        if (!progressSubscribed && Application.isPlaying) { Progression.Changed += OnProgressChanged; progressSubscribed = true; }
        RefreshProgressUI();
    }

    // โหลดความก้าวหน้า (เรียกตอนโหลดโปรไฟล์ล็อบบี้)
    private void LoadProgression()
    {
        if (!FeatureFlags.Progression) { RefreshProgressUI(); return; }
        Progression.Load(() =>
        {
            if (this == null) return;
            RefreshProgressUI();
            // เข้าเกมวันแรกของวัน: เปิดหน้ารับรางวัลล็อกอินให้อัตโนมัติ (ครั้งเดียวต่อการเปิดเกม)
            if (!loginPopupShown && Progression.CanClaimLogin && !Photon.Pun.PhotonNetwork.InRoom)
            {
                loginPopupShown = true;
                OpenProgressPage(ProgressPage.Missions);
            }
        });
    }

    // เรียกเมื่อ Progression.Changed: อัปเดตส่วนแสดงผลความก้าวหน้า (ยกเลิกการฟังถ้าล็อบบี้ถูกทำลายแล้ว)
    private void OnProgressChanged()
    {
        if (this == null) { Progression.Changed -= OnProgressChanged; return; }
        RefreshProgressUI();
        // เลเวลเปลี่ยน/โหลดจาก Firebase เสร็จ: สกิลที่ล็อกตามเลเวลอาจปลดแล้ว (SkillUnlock.cs) อัปเดตหน้าสกิลและส่ง loadout ใหม่ถ้าอยู่ในห้อง
        if (FeatureFlags.SkillLevelLock)
        {
            UpdateSkillDisplay(selectedSkillIndex);
            if (Photon.Pun.PhotonNetwork.InRoom && profileLoaded) PublishLocalLoadout(false);
        }
    }

    // อัปเดตป้ายเลเวล แถบ XP ปุ่ม MISSIONS/PROFILE ตัวเลขรางวัลที่รอรับ และวาดหน้าที่เปิดอยู่ใหม่
    private void RefreshProgressUI()
    {
        bool on = FeatureFlags.Progression;
        if (levelBadgeText != null)
        {
            levelBadgeText.transform.parent.gameObject.SetActive(on);
            levelBadgeText.text = "LV\n" + Progression.Level;
        }
        if (xpFill != null)
        {
            xpFill.transform.parent.gameObject.SetActive(on);
            var data = Progression.Data;
            float ratio = data == null || data.level >= Progression.MaxLevel ? 1f : data.xp / (float)Progression.XpToNext(data.level);
            xpFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(ratio), 1, 1);
        }
        if (missionsButton != null) missionsButton.gameObject.SetActive(on && (FeatureFlags.Missions || FeatureFlags.DailyLogin));
        if (profileButton != null) profileButton.gameObject.SetActive(on);
        if (missionsBadge != null)
        {
            int ready = Progression.ClaimableCount();
            missionsBadge.transform.parent.gameObject.SetActive(ready > 0);
            missionsBadge.text = ready.ToString();
        }
        if (openPage != ProgressPage.None && progressOverlay != null && progressOverlay.gameObject.activeSelf) BuildPage(openPage);
    }

    // เปิดหน้าต่างความก้าวหน้าที่หน้า MISSIONS หรือ PROFILE
    private void OpenProgressPage(ProgressPage page)
    {
        if (progressOverlay == null || !FeatureFlags.Progression) return;
        Progression.EnsureLoaded();
        openPage = page;
        progressOverlay.gameObject.SetActive(true);
        progressOverlay.transform.SetAsLastSibling();
        SolidOverlay(progressOverlay, progressWindow);
        BuildPage(page);
    }

    // ปิดหน้าต่างความก้าวหน้า และตั้ง openPage เป็น None
    private void CloseProgressPage()
    {
        openPage = ProgressPage.None;
        if (progressOverlay != null) progressOverlay.gameObject.SetActive(false);
    }

    // สร้างเนื้อหาหน้าใหม่ทุกครั้ง (ลบหน้าเก่าทิ้ง ใช้ชื่อใหม่เพื่อไม่ให้ไปเจอของเก่าที่รอลบ)
    private void BuildPage(ProgressPage page)
    {
        if (progressPage != null) Destroy(progressPage.gameObject);
        progressPage = UIRect("Page" + (++pageSerial), progressWindow, 0, 0, 1060, 620);
        UIButton("Close", progressPage, "BACK", 425, 270, 170, 54, CloseProgressPage);
        if (page == ProgressPage.Missions) BuildMissionsPage(progressPage);
        else BuildProfilePage(progressPage);
    }

    // ===== หน้าภารกิจ =====
    private void BuildMissionsPage(RectTransform page)
    {
        var data = Progression.Data;
        UILabel("Title", page, "MISSIONS & REWARDS", -150, 270, 700, 44, 30, Color.white);
        if (FeatureFlags.Missions)
        {
            var left = Progression.UntilTomorrow;
            UILabel("DailyHead", page, "DAILY MISSIONS   (new set in " + left.Hours + "h " + left.Minutes + "m)", -160, 212, 700, 30, 18, accentColor);
            for (int i = 0; i < data.daily.Count; i++) BuildMissionRow(page, "D" + i, data.daily[i], 170 - i * 50);
            UILabel("WeeklyHead", page, "WEEKLY MISSIONS", -160, 12, 700, 30, 18, accentColor);
            for (int i = 0; i < data.weekly.Count; i++) BuildMissionRow(page, "W" + i, data.weekly[i], -30 - i * 50);
        }
        if (FeatureFlags.DailyLogin) BuildLoginStrip(page);
    }

    // สร้างแถวภารกิจ 1 ข้อ: ข้อความ ความคืบหน้า รางวัล และปุ่ม CLAIM (กดได้เมื่อทำครบและยังไม่รับ)
    private void BuildMissionRow(RectTransform page, string key, MissionState state, float y)
    {
        var def = Progression.FindMission(state.id);
        if (def == null) return;
        bool done = state.progress >= def.goal;
        var row = UIPanel("Row" + key, page, 0, y, 1000, 44, new Color(.06f, .1f, .17f));
        var text = UILabel("Text", row.transform, def.text, -230, 0, 500, 40, 18, state.claimed ? Color.gray : Color.white);
        text.alignment = TextAlignmentOptions.Left;
        UILabel("Progress", row.transform, state.progress + " / " + def.goal, 110, 0, 140, 40, 18, done ? new Color(.35f, 1f, .7f) : Color.white);
        UILabel("Reward", row.transform, "+" + def.coins + " C  +" + def.xp + " XP", 270, 0, 190, 40, 16, new Color(1f, .8f, .35f));
        var claim = UIButton("Claim", row.transform, state.claimed ? "CLAIMED" : done ? "CLAIM" : "IN PROGRESS", 425, 0, 140, 38, () =>
        {
            if (Progression.ClaimMission(state, out int coins, out int xp))
            {
                UpdateStatus("Mission reward: +" + coins + " Astronium, +" + xp + " XP");
                StartCoroutine(RefreshCoinsLater());
            }
        });
        claim.interactable = done && !state.claimed;
    }

    // แถบรางวัลล็อกอิน 7 วัน (วันที่กดรับได้วันนี้มีปุ่ม CLAIM)
    private void BuildLoginStrip(RectTransform page)
    {
        UILabel("LoginHead", page, "DAILY LOGIN REWARD   (streak " + (Progression.Data != null ? Progression.Data.loginStreak : 0) + " days)", -160, -190, 700, 30, 18, accentColor);
        int today = Progression.NextLoginDay;
        bool canClaim = Progression.CanClaimLogin;
        for (int i = 0; i < 7; i++)
        {
            int day = i + 1;
            bool past = day < today || (day == today && !canClaim);
            bool current = day == today && canClaim;
            var box = UIPanel("Day" + day, page, -405 + i * 135, -248, 122, 72,
                current ? new Color(.1f, .45f, .4f) : past ? new Color(.1f, .14f, .2f) : new Color(.06f, .1f, .17f));
            UILabel("Text", box.transform, "DAY " + day + "\n+" + Progression.LoginCoins[i], 0, 0, 118, 66, 17, past ? Color.gray : Color.white)
                .textWrappingMode = TextWrappingModes.Normal;
            if (current)
            {
                if (!box.TryGetComponent(out Button button)) button = box.gameObject.AddComponent<Button>(); // ห้ามใช้ ?? กับ GetComponent (Editor คืน null ปลอม)
                box.raycastTarget = true;
                button.targetGraphic = box;
                button.onClick.AddListener(() =>
                {
                    if (Progression.ClaimLogin(out int coins))
                    {
                        UpdateStatus("Daily login reward: +" + coins + " Astronium");
                        StartCoroutine(RefreshCoinsLater());
                    }
                });
            }
        }
        if (canClaim) UILabel("LoginHint", page, "TAP DAY " + today + " TO CLAIM", 0, -296, 600, 24, 15, new Color(1f, .8f, .35f));
    }

    // อ่านยอดเหรียญใหม่หลังเพิ่มเหรียญ (Firebase เขียนแบบ async จึงรอเล็กน้อย)
    private IEnumerator RefreshCoinsLater()
    {
        yield return new WaitForSeconds(1.2f);
        if (FirebaseManager.Instance != null) FirebaseManager.Instance.GetCoinBalance(UpdateCoinDisplay);
    }

    // ===== หน้าโปรไฟล์ =====
    private void BuildProfilePage(RectTransform page)
    {
        var data = Progression.Data ?? new PlayerProgress();
        string pilot = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetUsername() : Photon.Pun.PhotonNetwork.NickName;
        var name = UILabel("Name", page, pilot + "   /   LV " + data.level, -200, 270, 600, 44, 28, Color.white);
        name.richText = false;
        UILabel("TitleText", page, "TITLE: " + Progression.Title, -200, 232, 600, 28, 18, new Color(1f, .8f, .35f));
        UIButton("ChangeTitle", page, "CHANGE TITLE", 225, 270, 200, 54, Progression.CycleTitle); // อยู่ข้าง BACK ไม่เบียดกัน
        // แถบ XP
        int need = Progression.XpToNext(data.level);
        var track = UIPanel("Xp", page, 0, 196, 1000, 14, new Color(.12f, .15f, .22f));
        var fill = UIPanel("Fill", track.transform, -500, 0, 1000, 14, accentColor);
        fill.rectTransform.pivot = new Vector2(0, .5f);
        fill.rectTransform.anchoredPosition = new Vector2(-500, 0);
        fill.rectTransform.localScale = new Vector3(data.level >= Progression.MaxLevel ? 1 : Mathf.Clamp01(data.xp / (float)need), 1, 1);
        UILabel("XpText", page, data.level >= Progression.MaxLevel ? "MAX LEVEL" : data.xp + " / " + need + " XP", 0, 176, 400, 22, 14, Color.white);
        // สถิติ
        float kd = data.deaths > 0 ? data.kills / (float)data.deaths : data.kills;
        int rate = data.matches > 0 ? Mathf.RoundToInt(data.wins * 100f / data.matches) : 0;
        UILabel("Stats1", page, "MATCHES " + data.matches + "    WINS " + data.wins + "    LOSSES " + data.losses + "    DRAWS " + data.draws + "    WIN RATE " + rate + "%",
            0, 140, 1000, 28, 18, Color.white);
        UILabel("Stats2", page, "KILLS " + data.kills + "    DEATHS " + data.deaths + "    K/D " + kd.ToString("0.00") + "    BEST WIN STREAK " + data.bestStreak + "    ONLINE " + data.onlineMatches,
            0, 112, 1000, 28, 18, Color.white);
        // Achievement 12 อัน (4 คอลัมน์ x 3 แถว)
        if (FeatureFlags.Achievements)
        {
            UILabel("AchHead", page, "ACHIEVEMENTS  " + data.achievements.Count + " / " + Progression.Achievements.Length, -330, 76, 340, 26, 17, accentColor);
            for (int i = 0; i < Progression.Achievements.Length; i++)
            {
                var def = Progression.Achievements[i];
                bool got = data.achievements.Contains(def.id);
                var cell = UIPanel("Ach" + i, page, -378 + (i % 4) * 252, 40 - (i / 4) * 50, 244, 44,
                    got ? new Color(.12f, .38f, .3f) : new Color(.06f, .1f, .17f));
                var label = UILabel("Text", cell.transform, def.name + "\n<size=12>" + def.text + "</size>", 0, 0, 236, 42, 16, got ? Color.white : Color.gray);
                label.textWrappingMode = TextWrappingModes.Normal;
            }
        }
        // ประวัติแมตช์ล่าสุด 6 รายการ
        UILabel("HistHead", page, "RECENT MATCHES", -330, -118, 340, 26, 17, accentColor);
        var lines = new System.Text.StringBuilder();
        int shown = Mathf.Min(6, data.history.Count);
        for (int i = 0; i < shown; i++)
        {
            var m = data.history[i];
            lines.Append(m.date + "   " + m.mode + (m.online ? "" : " (BOT)") + "   " + m.result + "   K " + m.kills + " / D " + m.deaths
                + "   +" + m.coins + " C  +" + m.xp + " XP   " + m.map);
            if (i < shown - 1) lines.Append('\n');
        }
        var history = UILabel("History", page, shown == 0 ? "No matches yet." : lines.ToString(), 0, -210, 1000, 160, 16, Color.white);
        history.richText = false;
        history.alignment = TextAlignmentOptions.TopLeft;
        history.enableAutoSizing = false;
        history.fontSize = 16;
        history.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
