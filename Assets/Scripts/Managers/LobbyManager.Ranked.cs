// LobbyManager.Ranked.cs — หน้าแรงค์ในล็อบบี้ (เฟส 6) (partial class ของ LobbyManager)
// ปุ่ม RANKED บนหน้าหลัก (แสดงแรงค์ + MMR) → หน้าต่างแรงค์: ระดับ, คะแนน, สถิติฤดูกาล, รางวัลจบฤดูกาล, ปุ่มหาแมตช์แรงค์, ตารางอันดับ
// หาแมตช์แรงค์: JoinRandomRoom เฉพาะห้องที่ Ranked = true ถ้าไม่มีห้องว่างจะสร้างห้องแรงค์ใหม่ (กติกาตายตัว 5 Kill / 5 นาที / ไม่มีตีบวก)
// กติกาและการคิดคะแนนอยู่ใน Ranked.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

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

    private void OnRankChanged()
    {
        if (this == null) { Ranked.Changed -= OnRankChanged; return; }
        RefreshRankedButton();
        if (rankedOverlay != null && rankedOverlay.gameObject.activeSelf) BuildRankedPage();
    }

    private void RefreshRankedButton()
    {
        if (rankedButton == null) return;
        rankedButton.gameObject.SetActive(FeatureFlags.Ranked);
        int mmr = Ranked.Mmr;
        SetButtonLabel(rankedButton, "RANKED  " + Ranked.TierName(mmr) + " " + mmr + (Ranked.CanClaimSeasonReward ? "  !" : ""));
    }

    private void OpenRanked()
    {
        if (rankedOverlay == null || !FeatureFlags.Ranked) return;
        Ranked.EnsureLoaded();
        showingLeaderboard = false;
        rankedOverlay.gameObject.SetActive(true);
        rankedOverlay.transform.SetAsLastSibling();
        BuildRankedPage();
    }

    private void CloseRanked()
    {
        if (rankedOverlay != null) rankedOverlay.gameObject.SetActive(false);
    }

    private void BuildRankedPage()
    {
        if (rankedWindow == null) return;
        if (rankedPage != null) Destroy(rankedPage.gameObject);
        rankedPage = UIRect("RPage" + (++pageSerial), rankedWindow, 0, 0, 900, 620);
        UIButton("Close", rankedPage, "CLOSE", 355, 270, 130, 44, CloseRanked);
        if (showingLeaderboard) BuildLeaderboardPage(rankedPage);
        else BuildRankSummary(rankedPage);
    }

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
        // ป้ายแรงค์ใหญ่ (รูป Images/Ranks/rank_{tier}.png ถ้ามี ไม่งั้นใช้กล่องสี)
        var badge = UIPanel("Badge", page, -250, 90, 200, 200, Ranked.TierColors[tier] * .6f + new Color(0, 0, 0, .4f));
        var sprite = Resources.Load<Sprite>("Images/Ranks/rank_" + Ranked.TierNames[tier].ToLowerInvariant());
        if (sprite != null) { badge.sprite = sprite; badge.color = Color.white; badge.preserveAspect = true; }
        else UILabel("Tier", badge.transform, Ranked.TierNames[tier], 0, 0, 190, 60, 30, Color.white);
        var tierLabel = UILabel("TierName", page, Ranked.TierNames[tier], 120, 150, 420, 50, 40, Ranked.TierColors[tier]);
        tierLabel.richText = false;
        UILabel("Mmr", page, data.mmr + " MMR", 120, 100, 420, 40, 28, Color.white);
        int toNext = Ranked.PointsToNext(data.mmr);
        UILabel("Next", page, toNext < 0 ? "Highest rank reached!" : toNext + " MMR to " + Ranked.TierNames[tier + 1], 120, 62, 420, 28, 17, Color.gray);
        UILabel("Record", page, "SEASON  W " + data.wins + "  /  L " + data.losses + "  /  D " + data.draws + "     PEAK " + data.peak, 120, 24, 520, 28, 17, Color.white);
        // แถบแรงค์ทั้งหมด
        for (int i = 0; i < Ranked.TierNames.Length; i++)
        {
            var step = UIPanel("Tier" + i, page, -360 + i * 120, -70, 112, 44, i == tier ? Ranked.TierColors[i] * .7f + new Color(0, 0, 0, .3f) : new Color(.06f, .1f, .17f));
            UILabel("Text", step.transform, Ranked.TierNames[i] + "\n<size=11>" + Ranked.TierMin[i] + "+</size>", 0, 0, 110, 42, 14, i == tier ? Color.white : Ranked.TierColors[i])
                .textWrappingMode = TextWrappingModes.Normal;
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
        var board = UIButton("Board", page, "LEADERBOARD", 190, -250, 280, 58, () => { showingLeaderboard = true; LoadLeaderboard(); BuildRankedPage(); });
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

    private void BuildLeaderboardPage(RectTransform page)
    {
        UILabel("Title", page, "LEADERBOARD  TOP 20", -200, 270, 460, 44, 28, Color.white);
        UIButton("Back", page, "< BACK", 190, 270, 150, 44, () => { showingLeaderboard = false; BuildRankedPage(); });
        string me = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetUserId() : "";
        if (leaderboard == null)
        {
            UILabel("Loading", page, FirebaseManager.Instance == null ? "Leaderboard needs an online login." : "Loading...", 0, 0, 700, 40, 20, Color.gray);
            return;
        }
        if (leaderboard.Count == 0) { UILabel("Empty", page, "No ranked players yet. Be the first!", 0, 0, 700, 40, 20, Color.gray); return; }
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
            ["SkillType"] = equippedSkillIndex,
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
