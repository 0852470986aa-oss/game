// GameplayManager.Modes.cs — โหมดเกมเฟส 7B (partial class ของ GameplayManager)
// 1 KING OF THE HILL : ยึดวงกลางสนาม ยืนในวงคนเดียว (หรือทีมเดียว) ได้ 1 คะแนน/วินาที ถึงเป้าก่อนชนะ
// 2 STAR HUNT        : เก็บดาวที่เกิดในแม็พ ตายแล้วทำดาวหล่น 1 ดวง ถึงเป้าก่อนชนะ
// 3 SURVIVAL         : คนจริงร่วมมือกันสู้บอทเป็นระลอก 10 ระลอก (ระลอก 5 และ 10 มีบอส) คนละ 3 ชีวิต
// 4 BATTLE ROYALE    : 1 ชีวิต วงปลอดภัยหดเรื่อยๆ อยู่นอกวงเสียเลือด รอดคนสุดท้ายชนะ
// 5 CAMPAIGN         : ด่านเนื้อเรื่องเล่นคนเดียว 5 ด่าน (Campaign.cs) คน 3 ชีวิต ด่านสุดท้ายมีบอส
// Master Client เป็นคนตัดสินคะแนน/เกิดดาว/เกิดบอท แล้วบอกทุกเครื่องผ่าน Room Properties และ RaiseEvent (รหัส 71-73)
// ปิดทั้งระบบได้ด้วย FeatureFlags.GameModes = false (ทุกห้องกลับเป็นโหมดปกติ)
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;

public partial class GameplayManager : IOnEventCallback
{
    private int gameMode;
    private const byte EvStarSpawn = 71, EvStarClaim = 72, EvStarTaken = 73;
    private const string ZoneXKey = "ZoneX", ZoneYKey = "ZoneY", WaveKey = "Wave", ModeResultKey = "ModeResult";

    // ===== ตั้งค่าตอนเริ่มแมตช์ (เรียกจาก ConfigureMultiplayerMatch) =====
    private void ConfigureGameMode()
    {
        var room = PhotonNetwork.CurrentRoom;
        gameMode = PhotonNetwork.InRoom ? MatchRules.GameMode(room) : MatchRules.ModeDeathmatch;
        MatchRules.ScoreOverride = null;
        deathOrder.Clear();
        if (gameMode == MatchRules.ModeKoth || gameMode == MatchRules.ModeStars)
        {
            MatchRules.ScoreOverride = entry => MatchRules.ModeScore(entry.id);
            targetKills = MatchRules.KillTarget(room) * (gameMode == MatchRules.ModeKoth ? 15 : 3);
        }
        else if (gameMode == MatchRules.ModeRoyale)
        {
            // อันดับ Battle Royale: ยังรอด = สูงสุด, ตายทีหลัง = อันดับดีกว่า
            MatchRules.ScoreOverride = entry =>
            {
                int index = deathOrder.IndexOf(entry.id);
                return index < 0 && !entry.left ? 1000 : index < 0 ? 0 : 10 + index;
            };
        }
        else if (gameMode == MatchRules.ModeCampaign)
        {
            campaign = Campaign.Get(MatchRules.CampaignStage(room));
            targetKills = campaign.boss ? 999 : campaign.kills;
        }
        if (gameMode == MatchRules.ModeRoyale) SetupRoyaleZone();
    }

    // Master ล้างค่าของแมตช์ก่อนในห้องเดิม (คะแนนโหมด, วงยึดจุด, ระลอก, ผล, บอทระลอกเลข 1100+)
    private void ClearModeLeftovers()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        var room = PhotonNetwork.CurrentRoom;
        var props = new ExitGames.Client.Photon.Hashtable();
        foreach (var entry in room.CustomProperties)
        {
            if (!(entry.Key is string key) || entry.Value == null) continue;
            if (key.StartsWith(MatchRules.ModeScorePrefix) || key == ZoneXKey || key == ZoneYKey || key == WaveKey || key == ModeResultKey || key == "Banner")
                props[key] = null;
            foreach (string prefix in new[] { MatchRules.BotKillPrefix, MatchRules.BotDeathPrefix, MatchRules.TeamPrefix })
                if (key.StartsWith(prefix) && int.TryParse(key.Substring(prefix.Length), out int id) && id >= PlayerController.BotIdBase + 100)
                    props[key] = null;
        }
        if (props.Count > 0) room.SetCustomProperties(props);
    }

    // ===== ทุกเฟรม (เรียกจาก Update) =====
    private void UpdateGameMode()
    {
        if (!PhotonNetwork.InRoom || gameMode == MatchRules.ModeDeathmatch || isMatchEnding) return;
        UpdateSpectate();
        switch (gameMode)
        {
            case MatchRules.ModeKoth: UpdateKoth(); break;
            case MatchRules.ModeStars: UpdateStars(); break;
            case MatchRules.ModeSurvival: UpdateSurvival(); break;
            case MatchRules.ModeRoyale: UpdateRoyale(); break;
            case MatchRules.ModeCampaign: UpdateCampaign(); break;
        }
    }

    // ===== เกิดใหม่ได้ไหม (เรียกจาก PlayerController.RespawnRoutine) =====
    public bool CanRespawn(PlayerController ship)
    {
        if (ship == null || !PhotonNetwork.InRoom) return true;
        int mode = MatchRules.GameMode(PhotonNetwork.CurrentRoom);
        if (ship.IsBot)
        {
            if (mode == MatchRules.ModeRoyale || mode == MatchRules.ModeSurvival) return false;
            if (mode == MatchRules.ModeCampaign) return !ship.IsBoss;
            return true;
        }
        int lives = MatchRules.Lives(PhotonNetwork.CurrentRoom);
        return lives <= 0 || MatchRules.Deaths(ship.photonView.Owner) < lives;
    }

    private int LivesLeft()
    {
        int lives = MatchRules.Lives(PhotonNetwork.CurrentRoom);
        return lives <= 0 ? -1 : Mathf.Max(0, lives - MatchRules.Deaths(PhotonNetwork.LocalPlayer));
    }

    // หมดชีวิตแล้ว: กล้องตามยานอื่นที่ยังรอด (ดูคนอื่นเล่น)
    private float nextSpectateSwitch;
    private void UpdateSpectate()
    {
        if (localPlayer == null || !localPlayer.isDead || CanRespawn(localPlayer) || Time.unscaledTime < nextSpectateSwitch) return;
        nextSpectateSwitch = Time.unscaledTime + 4f;
        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow == null) return;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (!ship.isDead && ship != localPlayer && (!MatchRules.IsCoop(PhotonNetwork.CurrentRoom) || !ship.IsBot)) { follow.target = ship.transform; return; }
    }

    // ===== คะแนนบนจอ + เงื่อนไขจบ (เรียกจาก UpdateMatchTimerAndScore) =====
    private string ModeScoreLine()
    {
        var room = PhotonNetwork.CurrentRoom;
        int myId = localPlayer != null ? localPlayer.CombatantId : PhotonNetwork.LocalPlayer.ActorNumber;
        int lives = LivesLeft();
        string livesText = lives >= 0 ? "   LIVES " + lives : "";
        switch (gameMode)
        {
            case MatchRules.ModeKoth:
                return "HILL  YOU " + MatchRules.ModeScore(myId) + "  /  LEAD " + BestModeScore() + "  /  TO " + targetKills + "   " + zoneState;
            case MatchRules.ModeStars:
                return "STARS  YOU " + MatchRules.ModeScore(myId) + "  /  LEAD " + BestModeScore() + "  /  TO " + targetKills;
            case MatchRules.ModeSurvival:
                return "WAVE " + Mathf.Max(1, CurrentWave()) + " / " + SurvivalWaves + "   ENEMIES " + EnemyBotsPresent() + livesText;
            case MatchRules.ModeRoyale:
                return "ALIVE " + AliveCount() + "   ZONE " + Mathf.RoundToInt(RoyaleRadius()) + livesText;
            case MatchRules.ModeCampaign:
                return "STAGE " + MatchRules.CampaignStage(room) + "  " + campaign.title + "   "
                    + (campaign.boss ? (BossAlive() ? "DESTROY THE BOSS" : "BOSS DOWN!") : "KILLS " + MatchRules.LocalKills() + "/" + campaign.kills) + livesText;
        }
        return "";
    }

    private bool ModeShouldEnd()
    {
        var room = PhotonNetwork.CurrentRoom;
        switch (gameMode)
        {
            case MatchRules.ModeKoth:
            case MatchRules.ModeStars:
                return BestModeScore() >= targetKills;
            case MatchRules.ModeRoyale:
                return PhotonNetwork.Time - MatchStartTime() > 3 && AliveCount() <= 1;
            case MatchRules.ModeSurvival:
            case MatchRules.ModeCampaign:
                return room.CustomProperties.TryGetValue(ModeResultKey, out object v) && v is int result && result != 0;
        }
        return false;
    }

    private int BestModeScore()
    {
        int best = 0;
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return 0;
        foreach (var entry in room.CustomProperties)
            if (entry.Key is string key && key.StartsWith(MatchRules.ModeScorePrefix) && entry.Value is int score) best = Mathf.Max(best, score);
        return best;
    }

    private double MatchStartTime()
        => PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("StartTime", out object v) && v is double start ? start : PhotonNetwork.Time;

    // Master เพิ่มคะแนนโหมดหลายคนพร้อมกัน
    private static void AddModeScores(Dictionary<int, int> deltas)
    {
        if (deltas.Count == 0 || !PhotonNetwork.IsMasterClient) return;
        var props = new ExitGames.Client.Photon.Hashtable();
        foreach (var pair in deltas) props[MatchRules.ModeScorePrefix + pair.Key] = Mathf.Max(0, MatchRules.ModeScore(pair.Key) + pair.Value);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // ===== เป้าหมายของบอท (เรียกจาก BotController) =====
    public bool TryGetObjective(PlayerController bot, out Vector2 objective)
    {
        objective = default;
        Vector2 me = bot.transform.position;
        switch (gameMode)
        {
            case MatchRules.ModeKoth:
                if (zoneView == null) return false;
                objective = zoneCenter;
                return Vector2.Distance(me, zoneCenter) > zoneRadius * .6f;
            case MatchRules.ModeStars:
                float best = float.MaxValue;
                foreach (var star in stars.Values)
                {
                    if (star.view == null) continue;
                    float d = Vector2.Distance(me, star.view.transform.position);
                    if (d < best) { best = d; objective = star.view.transform.position; }
                }
                return best < float.MaxValue;
            case MatchRules.ModeRoyale:
                objective = royaleCenter;
                return Vector2.Distance(me, royaleCenter) > RoyaleRadius() * .75f;
        }
        return false;
    }

    // เรียกทุกเครื่องเมื่อมียานตาย (จาก OnCombatantDied)
    private void ModeOnDeath(int killerId, PlayerController victim)
    {
        if (victim == null || gameMode == MatchRules.ModeDeathmatch) return;
        // Battle Royale ทุกคนมี 1 ชีวิต: จดลำดับการตายไว้จัดอันดับ
        if (gameMode == MatchRules.ModeRoyale && !deathOrder.Contains(victim.CombatantId)) deathOrder.Add(victim.CombatantId);
        // เก็บดาว: ตายแล้วดาวหล่น 1 ดวงตรงจุดที่ตาย
        if (gameMode == MatchRules.ModeStars && PhotonNetwork.IsMasterClient && MatchRules.ModeScore(victim.CombatantId) > 0)
        {
            AddModeScores(new Dictionary<int, int> { [victim.CombatantId] = -1 });
            SpawnStar(victim.transform.position);
        }
    }

    // ================= 1) KING OF THE HILL =================
    private Vector2 zoneCenter;
    private const float zoneRadius = 6f;
    private GameObject zoneView;
    private SpriteRenderer zoneFill;
    private LineRenderer zoneRing;
    private float nextZoneTick;
    private string zoneState = "";

    private void UpdateKoth()
    {
        var room = PhotonNetwork.CurrentRoom;
        // Master เลือกจุดตั้งวง (ครั้งเดียว) จุดที่ว่างใกล้กลางสนามที่สุด
        if (!(room.CustomProperties.TryGetValue(ZoneXKey, out object zx) && zx is float x
              && room.CustomProperties.TryGetValue(ZoneYKey, out object zy) && zy is float y))
        {
            if (PhotonNetwork.IsMasterClient && Time.time >= nextZoneTick) { nextZoneTick = Time.time + 1f; PickZone(); }
            return;
        }
        zoneCenter = new Vector2(x, y);
        if (zoneView == null) zoneView = CreateZoneView(zoneCenter, zoneRadius, out zoneFill, out zoneRing);
        // ใครอยู่ในวง (นับเป็นกลุ่ม: โหมดทีม = ทีม, ไม่งั้น = รายคน)
        var groups = new Dictionary<int, List<int>>();
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (ship.isDead || Vector2.Distance(ship.transform.position, zoneCenter) > zoneRadius) continue;
            int team = MatchRules.TeamOf(ship.CombatantId);
            int group = team >= 0 ? -10 - team : ship.CombatantId;
            if (!groups.TryGetValue(group, out var members)) groups[group] = members = new List<int>();
            members.Add(ship.CombatantId);
        }
        bool mineInside = localPlayer != null && !localPlayer.isDead && Vector2.Distance(localPlayer.transform.position, zoneCenter) <= zoneRadius;
        Color color;
        if (groups.Count == 0) { zoneState = "HILL EMPTY"; color = new Color(1f, 1f, 1f, .5f); }
        else if (groups.Count > 1) { zoneState = "CONTESTED!"; color = new Color(1f, .85f, .2f, .7f); }
        else
        {
            bool ours = mineInside || (localPlayer != null && isTeamMode && groups.ContainsKey(-10 - MatchRules.TeamOf(localPlayer.CombatantId)));
            zoneState = ours ? "YOU HOLD THE HILL" : "ENEMY HOLDS THE HILL";
            color = ours ? new Color(.3f, 1f, .5f, .75f) : new Color(1f, .35f, .3f, .75f);
        }
        zoneRing.startColor = zoneRing.endColor = color;
        zoneFill.color = new Color(color.r, color.g, color.b, .12f);
        // Master ให้คะแนนทุก 1 วินาที เมื่อมีกลุ่มเดียวในวง
        if (PhotonNetwork.IsMasterClient && Time.time >= nextZoneTick)
        {
            nextZoneTick = Time.time + 1f;
            if (groups.Count == 1)
            {
                var deltas = new Dictionary<int, int>();
                foreach (var members in groups.Values) foreach (int id in members) deltas[id] = 1;
                AddModeScores(deltas);
            }
        }
    }

    private void PickZone()
    {
        int map = GetCurrentMapIndex();
        Vector2 center = (GetArenaMin(map) + GetArenaMax(map)) * .5f;
        Vector2[] offsets = { Vector2.zero, new Vector2(0, 9), new Vector2(0, -9), new Vector2(10, 0), new Vector2(-10, 0), new Vector2(12, 12), new Vector2(-12, -12) };
        foreach (var offset in offsets)
        {
            Vector2 point = center + offset;
            if (!IsSpawnAreaBlocked(point, 2.5f, null))
            {
                PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [ZoneXKey] = point.x, [ZoneYKey] = point.y });
                return;
            }
        }
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [ZoneXKey] = center.x, [ZoneYKey] = center.y });
    }

    // วงกลมบนพื้น (เส้นขอบ + พื้นโปร่ง) ใช้ทั้งยึดจุดและ Battle Royale
    private GameObject CreateZoneView(Vector2 center, float radius, out SpriteRenderer fill, out LineRenderer ring)
    {
        var go = new GameObject("ModeZone");
        go.transform.position = center;
        fill = go.AddComponent<SpriteRenderer>();
        fill.sprite = PowerUpManager.CircleSprite();
        fill.sortingOrder = -1;
        go.transform.localScale = Vector3.one * radius * 2f;
        var ringObject = new GameObject("Ring");
        ringObject.transform.SetParent(go.transform, false);
        ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 64;
        ring.widthMultiplier = .05f;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.sortingOrder = 4;
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .5f);
        }
        return go;
    }

    // ================= 2) STAR HUNT =================
    private sealed class Star { public GameObject view; public bool claimSent; }
    private readonly Dictionary<int, Star> stars = new Dictionary<int, Star>();
    private float nextStarAt = -1f;
    private int starCounter;

    private void UpdateStars()
    {
        if (!matchStarted) return;
        foreach (var star in stars.Values) if (star.view != null) star.view.transform.Rotate(0, 0, 120f * Time.deltaTime);
        if (PhotonNetwork.IsMasterClient)
        {
            if (nextStarAt < 0) nextStarAt = Time.time + 2f;
            if (Time.time >= nextStarAt && stars.Count < 6)
            {
                nextStarAt = Time.time + 2.5f;
                int map = GetCurrentMapIndex();
                Vector2 min = GetArenaMin(map) + Vector2.one * 5f, max = GetArenaMax(map) - Vector2.one * 5f;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    Vector2 point = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
                    if (!IsSpawnAreaBlocked(point, 1.4f, null)) { SpawnStar(point); break; }
                }
            }
            // บอทเก็บดาว
            foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!ship.IsBot || ship.isDead || !ship.photonView.IsMine) continue;
                int id = TouchingStar(ship);
                if (id >= 0 && !stars[id].claimSent) { stars[id].claimSent = true; SendModeEvent(EvStarTaken, new object[] { id, ship.CombatantId }, ReceiverGroup.All); }
            }
        }
        if (localPlayer != null && !localPlayer.isDead)
        {
            int id = TouchingStar(localPlayer);
            if (id >= 0 && !stars[id].claimSent)
            {
                stars[id].claimSent = true;
                SendModeEvent(EvStarClaim, new object[] { id, localPlayer.CombatantId }, ReceiverGroup.MasterClient);
            }
        }
    }

    private void SpawnStar(Vector2 point)
    {
        int id = PhotonNetwork.LocalPlayer.ActorNumber * 10000 + (++starCounter);
        SendModeEvent(EvStarSpawn, new object[] { id, point.x, point.y }, ReceiverGroup.All);
    }

    private int TouchingStar(PlayerController ship)
    {
        foreach (var pair in stars)
            if (pair.Value.view != null && Vector2.Distance(ship.transform.position, pair.Value.view.transform.position) < 2f) return pair.Key;
        return -1;
    }

    private void SendModeEvent(byte code, object[] data, ReceiverGroup receivers)
    {
        if (PhotonNetwork.OfflineMode) { HandleModeEvent(code, data); return; }
        PhotonNetwork.RaiseEvent(code, data, new RaiseEventOptions { Receivers = receivers }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code >= EvStarSpawn && photonEvent.Code <= EvStarTaken) HandleModeEvent(photonEvent.Code, photonEvent.CustomData as object[]);
    }

    private void HandleModeEvent(byte code, object[] data)
    {
        if (data == null) return;
        if (code == EvStarSpawn && data.Length >= 3)
        {
            int id = (int)data[0];
            if (!stars.ContainsKey(id)) stars[id] = new Star { view = CreateStarView(new Vector2((float)data[1], (float)data[2])) };
        }
        else if (code == EvStarClaim && data.Length >= 2 && PhotonNetwork.IsMasterClient)
        {
            if (stars.ContainsKey((int)data[0])) SendModeEvent(EvStarTaken, new object[] { data[0], data[1] }, ReceiverGroup.All);
        }
        else if (code == EvStarTaken && data.Length >= 2)
        {
            int id = (int)data[0], taker = (int)data[1];
            if (!stars.TryGetValue(id, out var star)) return;
            if (star.view != null) Destroy(star.view);
            stars.Remove(id);
            if (PhotonNetwork.IsMasterClient) AddModeScores(new Dictionary<int, int> { [taker] = 1 });
            if (localPlayer != null && taker == localPlayer.CombatantId && AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ShieldHit");
        }
    }

    // ดาว: รูป Images/PowerUps/star.png ถ้ามี ไม่มีใช้วงกลมสีทอง
    private static GameObject CreateStarView(Vector2 position)
    {
        var go = new GameObject("Star");
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        var art = Resources.Load<Sprite>("Images/PowerUps/star");
        renderer.sortingOrder = 5;
        renderer.sprite = art != null ? art : PowerUpManager.CircleSprite();
        renderer.color = art != null ? Color.white : new Color(1f, .85f, .2f);
        go.transform.localScale = Vector3.one * (art != null ? 1.6f / Mathf.Max(.01f, art.bounds.size.x) : 1.4f);
        return go;
    }

    // ================= 3) SURVIVAL =================
    private const int SurvivalWaves = 10;
    private float nextWaveAt = -1f;
    private int botSequence = 100;

    private int CurrentWave()
        => PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(WaveKey, out object v) && v is int wave ? wave : 0;

    // บอทศัตรู (ทีม 1) ที่ยังอยู่ในฉาก (รวมที่ตายแต่ยังไม่ถูกลบ)
    private int EnemyBotsPresent()
    {
        int count = 0;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (ship.IsBot && !ship.isDead) count++;
        return count;
    }

    private void UpdateSurvival()
    {
        if (!matchStarted || !PhotonNetwork.IsMasterClient) return;
        CheckCoopDefeat();
        int wave = CurrentWave();
        bool botsLeft = false;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None)) if (ship.IsBot) { botsLeft = true; break; }
        if (botsLeft) { nextWaveAt = -1f; return; }
        // ระลอกหมดแล้ว: ครบ 10 = ชนะ / ไม่งั้นรอ 4 วินาทีแล้วปล่อยระลอกถัดไป
        if (wave >= SurvivalWaves) { SetModeResult(1); return; }
        if (nextWaveAt < 0) { nextWaveAt = Time.time + (wave == 0 ? 2f : 4f); return; }
        if (Time.time < nextWaveAt) return;
        nextWaveAt = -1f;
        wave++;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [WaveKey] = wave });
        int count = Mathf.Min(1 + wave, 8);
        int difficulty = wave <= 3 ? 1 : wave <= 6 ? 2 : 3;
        bool boss = wave % 5 == 0;
        SpawnEnemyBots(boss ? count - 1 : count, difficulty, boss);
        BroadcastBanner("WAVE " + wave + (boss ? "  -  BOSS!" : ""));
    }

    // คนจริงทุกคนหมดชีวิตและตายอยู่ = แพ้
    private void CheckCoopDefeat()
    {
        int lives = MatchRules.Lives(PhotonNetwork.CurrentRoom);
        if (lives <= 0) return;
        bool anyoneLeft = false;
        foreach (var pilot in PhotonNetwork.PlayerList)
            if (PilotCounts(pilot) && MatchRules.Deaths(pilot) < lives) { anyoneLeft = true; break; }
        if (!anyoneLeft)
        {
            // คนสุดท้ายเพิ่งตาย (ยานยังไม่หาย) ก็นับว่าแพ้
            SetModeResult(-1);
        }
    }

    private void SetModeResult(int result)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        var room = PhotonNetwork.CurrentRoom;
        if (room.CustomProperties.TryGetValue(ModeResultKey, out object v) && v is int existing && existing != 0) return;
        room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [ModeResultKey] = result });
    }

    // Master สร้างบอทศัตรูทีม 1 (ใช้ทั้ง Survival และ Campaign)
    private void SpawnEnemyBots(int count, int difficulty, bool withBoss)
    {
        var room = PhotonNetwork.CurrentRoom;
        int map = GetCurrentMapIndex();
        int total = count + (withBoss ? 1 : 0);
        var props = new ExitGames.Client.Photon.Hashtable();
        var ids = new int[total];
        for (int i = 0; i < total; i++)
        {
            ids[i] = PlayerController.BotIdBase + (botSequence++);
            props[MatchRules.TeamPrefix + ids[i]] = 1;
            props[MatchRules.BotKillPrefix + ids[i]] = 0;
        }
        room.SetCustomProperties(props);
        for (int i = 0; i < total; i++)
        {
            bool boss = withBoss && i == 0;
            int ship = boss ? 1 : BattleLoadoutCatalog.ValidShip(Random.Range(0, BattleLoadoutCatalog.Ships.Length));
            int skill = boss ? 2 : BattleLoadoutCatalog.ValidSkill(Random.Range(0, BattleLoadoutCatalog.Skills.Length));
            string name = boss ? "DREADNOUGHT [BOSS]" : BotNames[Random.Range(0, BotNames.Length)] + " [BOT]";
            string prefab = BattleLoadoutCatalog.PrefabName(ship);
            if (!TryFindSafeSpawn(GetPrefab(prefab), map, false, out Vector2 position)) position = new Vector2(Random.Range(-10f, 10f), 20f);
            object[] data = { "BOT", ids[i], name, ship, skill, Random.Range(1, ShipPaint.Colors.Length), boss ? 3 : difficulty, boss };
            PhotonNetwork.InstantiateRoomObject(prefab, position, Quaternion.Euler(0, 0, 180f), 0, data);
        }
    }

    // ป้ายกลางจอให้ทุกเครื่อง (ผ่าน Room Property "Banner")
    private void BroadcastBanner(string text)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["Banner"] = text + "|" + Random.Range(0, 100000) });
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changed)
    {
        if (changed.TryGetValue("Banner", out object value) && value is string text)
            ShowBanner(text.Split('|')[0], new Color(1f, .75f, .3f));
    }

    // ================= 4) BATTLE ROYALE =================
    private Vector2 royaleCenter;
    private float royaleStartRadius, royaleEndRadius = 7f;
    private GameObject royaleView;
    private LineRenderer royaleRing;
    private SpriteRenderer royaleFill;
    private readonly List<int> deathOrder = new List<int>();
    private float nextZoneWarning;

    // จุดศูนย์กลางวงสุ่มจาก BattleToken (ทุกเครื่องได้ค่าเดียวกันโดยไม่ต้องส่งข้อมูล)
    private void SetupRoyaleZone()
    {
        int map = GetCurrentMapIndex();
        Vector2 min = GetArenaMin(map), max = GetArenaMax(map);
        PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("BattleToken", out object token);
        var rng = new System.Random((token as string ?? "royale").GetHashCode());
        Vector2 center = (min + max) * .5f, half = (max - min) * .5f;
        royaleCenter = center + new Vector2((float)(rng.NextDouble() * 2 - 1) * half.x * .3f, (float)(rng.NextDouble() * 2 - 1) * half.y * .3f);
        royaleStartRadius = half.magnitude * 1.1f;
    }

    // วงหดจากวินาทีที่ 15 จนถึง 80% ของเวลาแมตช์
    private float RoyaleRadius()
    {
        float elapsed = matchStarted ? (float)(PhotonNetwork.Time - MatchStartTime()) : 0f;
        float t = Mathf.InverseLerp(15f, matchDuration * .8f, elapsed);
        return Mathf.Lerp(royaleStartRadius, royaleEndRadius, t);
    }

    private int AliveCount()
    {
        int alive = 0;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (ship.isDead && !CanRespawn(ship)) continue;
            if (!ship.IsBot && (ship.photonView.Owner == null || ship.photonView.Owner.IsInactive)) continue;
            alive++;
        }
        return alive;
    }

    private void UpdateRoyale()
    {
        if (royaleView == null) royaleView = CreateZoneView(royaleCenter, 1f, out royaleFill, out royaleRing);
        float radius = RoyaleRadius();
        royaleView.transform.localScale = Vector3.one * radius * 2f;
        royaleRing.startColor = royaleRing.endColor = new Color(.35f, .8f, 1f, .9f);
        royaleRing.widthMultiplier = .25f / Mathf.Max(1f, radius * 2f);
        royaleFill.color = new Color(.35f, .8f, 1f, .05f);
        if (!matchStarted) return;
        // นอกวง: เสียเลือดต่อวินาที (เพิ่มขึ้นเมื่อวงเล็กลง) — คนจริงคิดบนเครื่องตัวเอง, บอทคิดบนเครื่อง Master
        float damage = (5f + 10f * Mathf.InverseLerp(royaleStartRadius, royaleEndRadius, radius)) * Time.deltaTime;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!ship.photonView.IsMine || ship.isDead) continue;
            if (Vector2.Distance(ship.transform.position, royaleCenter) > radius) ship.ApplyZoneDamage(damage);
        }
        if (localPlayer != null && !localPlayer.isDead && Vector2.Distance(localPlayer.transform.position, royaleCenter) > radius
            && Time.unscaledTime >= nextZoneWarning)
        {
            nextZoneWarning = Time.unscaledTime + 3f;
            ShowBanner("GET BACK TO THE ZONE!", new Color(1f, .4f, .3f));
        }
    }

    // ================= 5) CAMPAIGN =================
    private CampaignStageDef campaign;
    private bool campaignSpawned;
    private TMP_Text briefingText;
    private float briefingUntil;

    private bool BossAlive()
    {
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (ship.IsBoss && !ship.isDead) return true;
        return false;
    }

    private void UpdateCampaign()
    {
        if (campaign == null) return;
        // บรรยายด่าน 7 วินาทีแรก
        if (briefingText == null && battleHud != null)
        {
            briefingText = BattleLabel("CampaignBriefing", battleHud, "STAGE " + MatchRules.CampaignStage(PhotonNetwork.CurrentRoom) + "  " + campaign.title + "\n" + campaign.briefing,
                0, 165, 1000, 80, 24);
            briefingText.textWrappingMode = TextWrappingModes.Normal;
            briefingText.color = new Color(1f, .9f, .5f);
            briefingUntil = Time.unscaledTime + 9f;
        }
        if (briefingText != null) briefingText.gameObject.SetActive(Time.unscaledTime < briefingUntil);
        if (!matchStarted || !PhotonNetwork.IsMasterClient) return;
        if (!campaignSpawned)
        {
            campaignSpawned = true;
            SpawnEnemyBots(campaign.bots, campaign.difficulty, campaign.boss);
            return;
        }
        CheckCoopDefeat();
        if (campaign.boss ? Time.time > 3f && !BossAlive() && BossSeen() : MatchRules.LocalKills() >= campaign.kills) SetModeResult(1);
    }

    private bool bossSeen;
    private bool BossSeen()
    {
        if (!bossSeen) foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None)) if (ship.IsBoss) { bossSeen = true; break; }
        return bossSeen;
    }

    // ================= หน้าผลโหมดร่วมมือ (Survival / Campaign) =================
    private void ShowCoopResult()
    {
        if (resultShown) return;
        resultShown = true;
        isMatchEnding = true;
        var room = PhotonNetwork.CurrentRoom;
        bool won = room.CustomProperties.TryGetValue(ModeResultKey, out object v) && v is int result && result > 0;
        int kills = MatchRules.LocalKills();
        int reward;
        string headline, detail;
        if (gameMode == MatchRules.ModeSurvival)
        {
            int cleared = won ? SurvivalWaves : Mathf.Max(0, CurrentWave() - 1);
            reward = cleared * 15 + (won ? 100 : 0);
            headline = won ? "SURVIVED!" : "OVERRUN";
            detail = "WAVES CLEARED " + cleared + " / " + SurvivalWaves + "    KILLS " + kills + "    +" + reward + " ASTRONIUM";
        }
        else
        {
            int stage = MatchRules.CampaignStage(room);
            bool first = won && Progression.ClearCampaignStage(stage);
            reward = won ? (first ? campaign.reward : campaign.reward / 4) : LoseReward;
            headline = won ? "STAGE CLEAR!" : "MISSION FAILED";
            detail = "STAGE " + stage + "  " + campaign.title + "    KILLS " + kills + "    +" + reward + " ASTRONIUM"
                + (won && stage < Campaign.Count && first ? "    NEXT STAGE UNLOCKED!" : "");
        }
        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.RecordMatchResult(won, gameMode == MatchRules.ModeSurvival ? "SURVIVAL" : "CAMPAIGN", reward, kills, GetCurrentMapName());
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(resultPanel.transform));
            resultPanel.transform.SetAsLastSibling();
        }
        if (battleHud != null) battleHud.gameObject.SetActive(false);
        if (resultHeadline != null)
        {
            resultHeadline.text = headline;
            resultHeadline.color = won ? new Color(1f, .8f, .35f) : new Color(1f, .4f, .45f);
        }
        if (resultRoomNumber != null) resultRoomNumber.text = MatchRules.ModeTitles[gameMode];
        if (resultScore != null) resultScore.text = detail;
        if (resultSurface != null)
        {
            foreach (string part in new[] { "YourResult", "RivalResult", "Versus" })
            {
                var t = resultSurface.Find(part);
                if (t != null) t.gameObject.SetActive(false);
            }
            cachedStandings = MatchRules.AllStandings();
            BuildRankingTable(cachedStandings, 0, 0);
        }
        if (btnReturnToMenu != null)
        {
            btnReturnToMenu.onClick.RemoveAllListeners();
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
        ReportProgress(won, false, 1, Mathf.Max(1, PhotonNetwork.PlayerList.Length), reward);
        FitResultUI();
    }
}
