// LobbyManager.Ranked.cs — หน้าแรงค์ในล็อบบี้ (เฟส 6) (partial class ของ LobbyManager)
// ปุ่ม RANKED บนหน้าหลัก (แสดงแรงค์ + MMR) → หน้าต่างแรงค์: ระดับ, คะแนน, สถิติฤดูกาล, รางวัลจบฤดูกาล, ปุ่มหาแมตช์แรงค์, ตารางอันดับ
// หาแมตช์แรงค์: JoinRandomRoom เฉพาะห้องที่ Ranked = true ถ้าไม่มีห้องว่างจะสร้างห้องแรงค์ใหม่ (กติกาตายตัว 5 Kill / 5 นาที / ไม่มีตีบวก)
// กติกาและการคิดคะแนนอยู่ใน Ranked.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

// ส่วนหน้าจอแรงค์ของ LobbyManager: ปุ่ม RANKED, หน้าสรุปแรงค์, ตารางอันดับ และการหาแมตช์แรงค์ผ่าน Photon
public partial class LobbyManager
{
    private Button rankedButton;
    private Image rankedOverlay;
    private RectTransform rankedWindow, rankedPage;
    private bool pendingRanked;
    private bool showingLeaderboard;
    private bool rankedSubscribed;
    private System.Collections.Generic.List<LeaderboardEntry> leaderboard;
    private bool leaderboardLoading;
    // แรงค์ที่กดดูในแถบแรงค์ (-1 = แสดงแรงค์ของเราเอง)
    private int rankPreviewTier = -1;

    // ===== แรงค์ชวนเพื่อนไม่ได้ (FeatureFlags.RankedNoInvite) =====
    // ห้องแรงค์ต้องหาคู่แบบสุ่มจากปุ่ม FIND RANKED MATCH เท่านั้น กันจับคู่กับเพื่อนเพื่อปั๊มคะแนน:
    // ไม่มีปุ่ม INVITE FRIENDS / COPY CODE ในห้องแรงค์, ไม่แสดงในรายการห้อง, จอยด้วยรหัส/คำชวน/ปุ่ม JOIN ของเพื่อนไม่ได้
    // ห้องแรงค์ตั้งชื่อ "R" + ตัวเลข (CreateRankedRoom) จึงดูจากชื่อห้องได้เลย
    public static bool IsRankedRoomName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 2 || (name[0] != 'R' && name[0] != 'r')) return false;
        for (int i = 1; i < name.Length; i++) if (!char.IsDigit(name[i])) return false;
        return true;
    }

    // ห้องนี้ห้ามชวน/จอยแบบส่วนตัวไหม (ห้องแรงค์ + เปิดสวิตช์)
    private static bool RankedPrivate(RoomInfo room) => FeatureFlags.RankedNoInvite && room != null && MatchRules.IsRanked(room);

    // ปุ่ม RANKED บนหน้าหลัก + หน้าต่างลอย (เรียกจาก BuildHomeScreen)
    private void BuildRankedUI(RectTransform root)
    {
        rankedButton = UIButton("Ranked", root, "RANKED", 460, -306, 240, 36, OpenRanked);
        rankedOverlay = UIPanel("RankedOverlay", root, 0, 0, 1280, 720, new Color(0, 0, 0, .72f));
        rankedOverlay.raycastTarget = true;
        rankedWindow = UIPanel("RankedWindow", rankedOverlay.transform, 0, 0, 900, 620, panelColor).rectTransform;
        rankedOverlay.gameObject.SetActive(false);
        if (!rankedSubscribed && Application.isPlaying) { Ranked.Changed += OnRankChanged; rankedSubscribed = true; }
        RefreshRankedButton();
    }

    // เรียกเมื่อ Ranked.Changed: อัปเดตปุ่ม RANKED และวาดหน้าต่างแรงค์ใหม่ถ้าเปิดอยู่
    private void OnRankChanged()
    {
        if (this == null) { Ranked.Changed -= OnRankChanged; return; }
        RefreshRankedButton();
        if (rankedOverlay != null && rankedOverlay.gameObject.activeSelf) BuildRankedPage();
    }

    // อัปเดตปุ่ม RANKED: แสดงชื่อแรงค์ + MMR และใส่ "!" เมื่อมีรางวัลจบฤดูกาลรอรับ
    private void RefreshRankedButton()
    {
        if (rankedButton == null) return;
        rankedButton.gameObject.SetActive(FeatureFlags.Ranked);
        int mmr = Ranked.Mmr;
        SetButtonLabel(rankedButton, "RANKED  " + Ranked.TierName(mmr) + " " + mmr + (Ranked.CanClaimSeasonReward ? "  !" : ""));
    }

    // เปิดหน้าต่างแรงค์ (เริ่มที่หน้าสรุปแรงค์ ไม่ใช่ตารางอันดับ)
    private void OpenRanked()
    {
        if (rankedOverlay == null || !FeatureFlags.Ranked) return;
        Ranked.EnsureLoaded();
        showingLeaderboard = false;
        rankPreviewTier = -1;
        rankedOverlay.gameObject.SetActive(true);
        rankedOverlay.transform.SetAsLastSibling();
        SolidOverlay(rankedOverlay, rankedWindow);
        BuildRankedPage();
    }

    // ปิดหน้าต่างแรงค์ (ซ่อน overlay)
    private void CloseRanked()
    {
        if (rankedOverlay != null) rankedOverlay.gameObject.SetActive(false);
    }

    // วาดหน้าต่างแรงค์ใหม่: ลบหน้าเก่า แล้วสร้างหน้าตารางอันดับหรือหน้าสรุปแรงค์ตาม showingLeaderboard
    private void BuildRankedPage()
    {
        if (rankedWindow == null) return;
        if (rankedPage != null) Destroy(rankedPage.gameObject);
        rankedPage = UIRect("RPage" + (++pageSerial), rankedWindow, 0, 0, 900, 620);
        UIButton("Close", rankedPage, "BACK", 335, 270, 170, 54, CloseRanked);
        if (showingLeaderboard) BuildLeaderboardPage(rankedPage);
        else BuildRankSummary(rankedPage);
    }

    // หน้าสรุปแรงค์: ป้ายแรงค์, MMR, สถิติฤดูกาล, แถบทุกแรงค์, ปุ่มรับรางวัลฤดูกาล, ปุ่มหาแมตช์ และปุ่มตารางอันดับ
    private void BuildRankSummary(RectTransform page)
    {
        var data = Ranked.Data ?? new RankData();
        int tier = Ranked.TierOf(data.mmr);
        UILabel("Title", page, "RANKED", -280, 270, 300, 44, 30, Color.white);
        if (FeatureFlags.Seasons)
        {
            var left = Ranked.SeasonLeft;
            UILabel("Season", page, Ranked.SeasonName + "   ends in " + left.Days + "d " + left.Hours + "h", 0, 222, 800, 28, 17, accentColor);
        }
        // แรงค์ที่แสดงในป้ายใหญ่: แรงค์ของเรา หรือแรงค์ที่กดดูจากแถบด้านล่าง
        int shown = rankPreviewTier >= 0 && rankPreviewTier < Ranked.TierNames.Length ? rankPreviewTier : tier;
        bool preview = shown != tier;
        // ป้ายแรงค์ใหญ่ (รูป Images/Ranks/rank_{tier}.png ถ้ามี ไม่งั้นใช้กล่องสี)
        var badge = UIPanel("Badge", page, -250, 90, 200, 200, Ranked.TierColors[shown] * .6f + new Color(0, 0, 0, .4f));
        var sprite = Resources.Load<Sprite>("Images/Ranks/rank_" + Ranked.TierNames[shown].ToLowerInvariant());
        if (sprite != null) { badge.sprite = sprite; badge.color = Color.white; badge.preserveAspect = true; }
        else UILabel("Tier", badge.transform, Ranked.TierNames[shown], 0, 0, 190, 60, 30, Color.white);
        var tierLabel = UILabel("TierName", page, Ranked.TierNames[shown], 120, 150, 420, 50, 40, Ranked.TierColors[shown]);
        tierLabel.richText = false;
        if (preview)
        {
            // กำลังดูแรงค์อื่น: ช่วง MMR, รางวัลจบฤดูกาล, ห่างจากเราเท่าไร
            int min = Ranked.TierMin[shown];
            string range = shown + 1 < Ranked.TierMin.Length ? min + " - " + (Ranked.TierMin[shown + 1] - 1) + " MMR" : min + "+ MMR";
            UILabel("Mmr", page, range, 120, 100, 420, 40, 28, Color.white);
            int gap = min - data.mmr;
            UILabel("Next", page, gap > 0 ? gap + " MMR above you" : "You have passed this rank", 120, 62, 420, 28, 17, Color.gray);
            UILabel("Record", page, "SEASON REWARD  +" + Ranked.SeasonRewardCoins[shown] + " ASTRONIUM", 120, 24, 520, 28, 17, new Color(1f, .85f, .4f));
            UILabel("PreviewHint", page, "Tap your rank to go back", -250, -22, 300, 24, 14, Color.gray);
        }
        else
        {
            UILabel("Mmr", page, data.mmr + " MMR", 120, 100, 420, 40, 28, Color.white);
            int toNext = Ranked.PointsToNext(data.mmr);
            UILabel("Next", page, toNext < 0 ? "Highest rank reached!" : toNext + " MMR to " + Ranked.TierNames[tier + 1], 120, 62, 420, 28, 17, Color.gray);
            UILabel("Record", page, "SEASON  W " + data.wins + "  /  L " + data.losses + "  /  D " + data.draws + "     PEAK " + data.peak, 120, 24, 520, 28, 17, Color.white);
            UILabel("PreviewHint", page, "Tap a rank below to view it", -250, -22, 300, 24, 14, Color.gray);
        }
        // แถบแรงค์ทั้งหมด (กดเพื่อดูรูปและข้อมูลของแรงค์นั้น / กดแรงค์ของเรา = กลับ)
        for (int i = 0; i < Ranked.TierNames.Length; i++)
        {
            bool mine = i == tier, viewing = i == shown;
            var step = UIPanel("Tier" + i, page, -360 + i * 120, -70, 112, 44, viewing ? Ranked.TierColors[i] * .7f + new Color(0, 0, 0, .3f) : new Color(.06f, .1f, .17f));
            UILabel("Text", step.transform, Ranked.TierNames[i] + "\n<size=11>" + (mine ? "YOU" : Ranked.TierMin[i] + "+") + "</size>", 0, 0, 110, 42, 14, viewing ? Color.white : Ranked.TierColors[i])
                .textWrappingMode = TextWrappingModes.Normal;
            step.raycastTarget = true;
            if (!step.TryGetComponent(out Button stepButton)) stepButton = step.gameObject.AddComponent<Button>(); // ห้ามใช้ ?? กับ GetComponent (Editor คืน null ปลอม)
            stepButton.targetGraphic = step;
            int pick = i;
            stepButton.onClick.AddListener(() => { rankPreviewTier = pick == tier ? -1 : pick; BuildRankedPage(); });
        }
        // รางวัลจบฤดูกาล
        if (Ranked.CanClaimSeasonReward)
        {
            int lastTier = Ranked.TierOf(data.lastSeasonPeak);
            UIButton("Reward", page, "CLAIM LAST SEASON REWARD  (" + Ranked.TierNames[lastTier] + "  +" + Ranked.SeasonRewardCoins[lastTier] + ")", 0, -140, 640, 48, () =>
            {
                if (Ranked.ClaimSeasonReward(out int coins))
                {
                    UpdateStatus("Season reward: +" + coins + " Astronium");
                    StartCoroutine(RefreshCoinsLater());
                }
            });
        }
        else UILabel("RewardInfo", page, "End-of-season reward by best rank: " + string.Join(" / ", System.Array.ConvertAll(Ranked.SeasonRewardCoins, c => c.ToString())) + " Astronium",
            0, -140, 820, 28, 15, Color.gray);
        UILabel("Rules", page, "Ranked = 1 VS 1, " + MatchRules.RankedKills + " kills / " + MatchRules.RankedSeconds / 60 + " min, upgrades OFF, no bots. Win vs stronger = more MMR.",
            0, -185, 840, 28, 14, Color.gray);
        var find = UIButton("Find", page, "FIND RANKED MATCH", -170, -250, 320, 58, OnRankedMatchClicked);
        find.interactable = profileLoaded && PhotonNetwork.InLobby && !roomRequestPending && !reconnecting && !pendingSolo;
        var board = UIButton("Board", page, "LEADERBOARD", 190, -250, 280, 58, () => { showingLeaderboard = true; leaderboard = null; LoadLeaderboard(); BuildRankedPage(); }); // โหลดใหม่ทุกครั้งที่เปิด
        board.gameObject.SetActive(FeatureFlags.Leaderboard);
    }

    // ===== ตารางอันดับ (Top 20 จาก Firebase) =====
    private void LoadLeaderboard()
    {
        if (leaderboardLoading || FirebaseManager.Instance == null) return;
        leaderboardLoading = true;
        FirebaseManager.Instance.GetLeaderboard(20, list =>
        {
            leaderboardLoading = false;
            if (this == null) return;
            leaderboard = list;
            if (showingLeaderboard && rankedOverlay != null && rankedOverlay.gameObject.activeSelf) BuildRankedPage();
        });
    }

    // หน้าตารางอันดับ Top 20 (อันดับ ชื่อ แรงค์ MMR เลเวล) ไฮไลต์แถวของเรา และแสดงแรงค์ของเราด้านล่าง
    private void BuildLeaderboardPage(RectTransform page)
    {
        UILabel("Title", page, "LEADERBOARD  TOP 20", -200, 270, 460, 44, 28, Color.white);
        UIButton("Back", page, "< RANK", 130, 270, 160, 54, () => { showingLeaderboard = false; BuildRankedPage(); });
        string me = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetUserId() : "";
        if (leaderboard == null)
        {
            UILabel("Loading", page, FirebaseManager.Instance == null ? "Leaderboard needs an online login." : "Loading...", 0, 0, 700, 40, 20, Color.gray);
            return;
        }
        if (leaderboard.Count == 0)
        {
            // อ่านไม่ได้ (เน็ต/สิทธิ์/ยังไม่ล็อกอิน) ≠ ยังไม่มีผู้เล่น — บอกให้ชัดและให้กดลองใหม่
            string error = FirebaseManager.Instance != null ? FirebaseManager.Instance.LastLeaderboardError : null;
            UILabel("Empty", page, string.IsNullOrEmpty(error) ? "No ranked players yet. Be the first!" : "Could not load the leaderboard: " + error, 0, 20, 820, 60, 20, Color.gray)
                .textWrappingMode = TextWrappingModes.Normal;
            UIButton("Retry", page, "RETRY", 0, -60, 200, 50, () => { leaderboard = null; LoadLeaderboard(); BuildRankedPage(); });
            return;
        }
        string[] heads = { "#", "PILOT", "RANK", "MMR", "LV" };
        float[] xs = { -380, -170, 120, 260, 360 };
        float[] ws = { 60, 360, 180, 110, 80 };
        for (int c = 0; c < heads.Length; c++) UILabel("H" + c, page, heads[c], xs[c], 222, ws[c], 26, 16, accentColor);
        for (int r = 0; r < leaderboard.Count && r < 20; r++)
        {
            var e = leaderboard[r];
            float y = 194 - r * 23;
            bool mine = e.uid == me;
            if (mine) UIPanel("Me", page, 0, y, 860, 22, new Color(1f, .85f, .3f, .16f));
            int tier = Ranked.TierOf(e.mmr);
            string[] cells = { (r + 1).ToString(), e.name, Ranked.TierNames[tier], e.mmr.ToString(), e.level.ToString() };
            for (int c = 0; c < cells.Length; c++)
            {
                var label = UILabel("C" + r + "_" + c, page, cells[c], xs[c], y, ws[c], 22, 15, c == 2 ? Ranked.TierColors[tier] : mine ? new Color(1f, .87f, .4f) : Color.white);
                label.richText = false;
            }
        }
        UILabel("You", page, "YOU:  " + Ranked.TierName(Ranked.Mmr) + "  " + Ranked.Mmr + " MMR", 0, -290, 600, 26, 16, new Color(1f, .87f, .4f));
    }

    // ===== หาแมตช์แรงค์ =====
    private void OnRankedMatchClicked()
    {
        if (!FeatureFlags.Ranked || !BeginRoomRequest()) return;
        CloseRanked();
        pendingRanked = true;
        UpdateStatus("Searching for a ranked opponent...");
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        {
            ["ShipType"] = equippedShipIndex,
            ["SkillType"] = SkillUnlock.Usable(equippedSkillIndex),
            [ShipPaint.Property] = ShipPaint.Local,
            ["MMR"] = Ranked.Mmr
        });
        if (!PhotonNetwork.JoinRandomRoom(new ExitGames.Client.Photon.Hashtable { [MatchRules.RankedKey] = true }, 2)) { pendingRanked = false; RoomRequestFailed(); }
    }

    // ไม่มีห้องแรงค์ว่าง: สร้างห้องแรงค์ (แม็พสุ่ม กติกาตายตัว) แล้วรอคู่แข่งเข้ามา
    private void CreateRankedRoom()
    {
        UpdateStatus("No ranked room found. Waiting for an opponent...");
        string roomId = "R" + Random.Range(100000, 999999);
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [MapProperty] = Random.Range(0, MapChoices),
            ["MapRevision"] = 0,
            ["Starting"] = false,
            [MatchRules.RankedKey] = true,
            [MatchRules.UpgradesKey] = false,
            [MatchRules.BotFillKey] = false,
            [MatchRules.KillTargetKey] = MatchRules.RankedKills,
            [MatchRules.MatchSecondsKey] = MatchRules.RankedSeconds,
            [MatchRules.HazardsKey] = true
        };
        var options = new RoomOptions
        {
            PlayerTtl = 60000,
            EmptyRoomTtl = 60000,
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = props,
            CustomRoomPropertiesForLobby = new string[] { "MapIndex", MatchRules.TeamsKey, MatchRules.RankedKey }
        };
        if (!PhotonNetwork.CreateRoom(roomId, options, TypedLobby.Default)) { pendingRanked = false; RoomRequestFailed(); }
    }
}
