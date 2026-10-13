using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Photon.Realtime;

// ตรวจใน Editor โดยไม่ล็อกอิน/เขียนบัญชี และไม่แก้ Scene ของผู้ใช้
public static class VisualRankedValidation
{
    private sealed class TestPilot : Player
    {
        public TestPilot(int id) : base("test" + id, id, false) { }
    }

    [MenuItem("Tools/Battlefield/Validate Skins Ranked and Maps")]
    public static void Run()
    {
        int checks = 0;
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("VisualRanked: " + name); checks++; };
        var random = UnityEngine.Random.state;
        bool teams = FeatureFlags.Teams, ranked = FeatureFlags.Ranked, multiplayer = FeatureFlags.MultiPlayer, settings = FeatureFlags.RoomSettings;
        try
        {
            FeatureFlags.Teams = FeatureFlags.Ranked = FeatureFlags.MultiPlayer = FeatureFlags.RoomSettings = true;
            foreach (int size in new[] { 2, 4, 8 })
            {
                var room = new Room("test", new RoomOptions { MaxPlayers = (byte)size });
                room.CustomProperties[MatchRules.RankedKey] = true;
                room.CustomProperties[MatchRules.RankedSizeKey] = size;
                room.CustomProperties[MatchRules.TeamsKey] = size > 2;
                room.CustomProperties[MatchRules.HumansKey] = size;
                room.CustomProperties[MatchRules.KillTargetKey] = 5;
                check(MatchRules.RankedPlayers(room) == size, "queue size " + size);
                check(MatchRules.RankedLabel(room) == "RANKED " + size / 2 + "V" + size / 2, "label " + size);
                check(MatchRules.IsTeamMode(room) == (size > 2), "team mode " + size);
                check(!MatchRules.UpgradesAllowed(room), "ranked upgrades off");
                check(!MatchRules.BotFill(room), "ranked no bot fill");
                if (size > 2)
                {
                    var pilots = new Player[size];
                    for (int i = 0; i < size; i++) pilots[i] = new TestPilot(i + 1);
                    var assigned = MatchRules.AssignTeams(pilots, out int blue, out int red);
                    check(assigned.Count == size && blue == size / 2 && red == size / 2, "balanced teams " + size);
                    check(MatchRules.TeamKillTarget(room) == size / 2 * 5, "team kill target " + size);
                }
            }
            check(MatchRules.RankedPlayers(null) == 2, "old ranked defaults 1v1");
            foreach (int maps in new[] { 1, 3, 5 })
            {
                int last = -1;
                for (int i = 0; i < 100; i++)
                {
                    int map = SoloMapRotation.Next(maps, last);
                    check(map >= 0 && map < maps && (maps == 1 || map != last), "valid non-repeating map");
                    last = map;
                }
            }
            foreach (var skin in SkinCatalog.All)
            {
                var sprite = VisualSkin.SpriteFor(skin);
                check(sprite != null && sprite.name == "Skin_" + skin.id && sprite.rect.width > 30 && sprite.rect.height > 30, "skin art " + skin.id);
                check(SkinCatalog.FromPaint(SkinCatalog.PaintIndex(skin.id)) == skin, "stable skin id " + skin.id);
            }
            check(Resources.Load<Texture2D>(BossRaidAppearance.ResourcePath) != null, "boss v2 imported");
            // สร้างวัตถุชั่วคราวใน Preview Scene เท่านั้น ไม่ทำให้ Scene ผู้ใช้ dirty
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var go = new GameObject("VisualRankedValidationOnly");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, preview);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = BattleLoadoutCatalog.ShipSprite(0);
                check(renderer.sprite != null, "base sprite exists");
                var collider = go.AddComponent<BoxCollider2D>();
                var before = collider.size;
                float height = renderer.sprite.bounds.size.y;
                go.AddComponent<ShipSkinAppearance>().Apply(renderer, VisualSkin.SpriteFor(SkinCatalog.All[0]), height);
                check(Mathf.Abs(renderer.sprite.bounds.size.y - height) < .001f, "skin retains world height");
                check(collider.size == before && go.transform.localScale == Vector3.one, "skin leaves collider and transform untouched");
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
            // บังคับสร้างตารางคำแปล เพื่อตรวจคีย์ซ้ำก่อนเปิด UI จริง
            check(typeof(PremiumLang).GetField("Thai", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) != null, "localization initializes");
        }
        finally
        {
            UnityEngine.Random.state = random;
            FeatureFlags.Teams = teams; FeatureFlags.Ranked = ranked; FeatureFlags.MultiPlayer = multiplayer; FeatureFlags.RoomSettings = settings;
        }
        Debug.Log("VISUAL_RANKED_PASS: " + checks + " checks. No account writes.");
    }
}
