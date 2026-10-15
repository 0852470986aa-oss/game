using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PremiumStoryValidation
{
    [MenuItem("Tools/Battlefield/Validate Premium Story Update")]
    public static void Run()
    {
        int count = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); count++; };
        check(PremiumItemArt.Crystal != null, "Crystal artwork");
        check(PremiumItemArt.Ticket != null, "Ticket artwork");
        check(Campaign.Count == 13, "Thirteen stages; first ten retained");
        check(SkinGacha.RarityForRoll(1, 1, false, .79f) == SkinRarity.Legendary, "Legendary base boundary");
        check(SkinGacha.RarityForRoll(1, 1, false, .81f) == SkinRarity.Epic, "Epic base boundary");
        check(SkinGacha.RarityForRoll(1, 1, false, 5.01f) == SkinRarity.Rare, "Rare base boundary");
        check(SkinGacha.RarityForRoll(1, 1, false, 25.01f) == null, "Shard base boundary");
        check(SkinGacha.RarityForRoll(70, 1, false, 99.99f) == SkinRarity.Legendary, "Existing hard pity retained");
        check(SkinGacha.RarityForRoll(1, 10, false, 99.99f) == SkinRarity.Epic, "Existing epic pity retained");
        check(SkinGacha.RarityForRoll(1, 1, true, 99.99f) == SkinRarity.Epic, "Ten-pull guarantee retained");
        check(SkinGacha.RarityForRoll(56, 1, false, 6f) == SkinRarity.Legendary, "Soft pity retained");
        int level = 4;
        foreach (int expected in new[] { 1, 2, 3, 4 })
        { level = MatchRules.NextPlayableDifficulty(level); check(level == expected, "Difficulty cycle"); }
        var testRoom = new Photon.Realtime.Room("test", new Photon.Realtime.RoomOptions());
        for (int i = 0; i <= 4; i++)
        { testRoom.CustomProperties[MatchRules.BotDifficultyKey] = i; check(MatchRules.BotDifficulty(testRoom) == i, "Preserved difficulty ID " + i); }
        var order = new DemoCheckoutSession(10);
        check(order.OrderId.StartsWith("DEMO-"), "Clearly simulated order");
        check(!order.Approve(11), "Cannot skip authorization");
        check(order.Authorize(11) && order.Approve(12), "Approve flow");
        check(!order.Approve(13), "Double approval rejected");
        order.Cancel(); check(order.Status == DemoCheckoutSession.State.Processing, "Processing cannot be cancelled mid-grant");
        order.Complete(true); check(order.Status == DemoCheckoutSession.State.Paid, "Receipt status");
        var declined = new DemoCheckoutSession(0); declined.Authorize(1);
        check(declined.Decline(2) && !declined.Approve(3), "Declined cannot grant");
        var cancelled = new DemoCheckoutSession(0); cancelled.Cancel();
        check(!cancelled.Authorize(1) && !cancelled.Approve(2), "Cancelled cannot grant");
        var expired = new DemoCheckoutSession(0); expired.Authorize(1);
        check(expired.Expire(300) && !expired.Approve(301), "Expiry cannot grant");
        check(Premium.Payment.IsSimulated, "No live payment provider");
        int[] oldRewards = { 150, 250, 350, 500, 1000 };
        for (int i = 0; i < oldRewards.Length; i++) check(Campaign.Get(i + 1).reward == oldRewards[i], "Original reward unchanged");
        for (int i = 1; i <= Campaign.Count; i++)
        {
            var stage = Campaign.Get(i);
            check(stage.MapIndex >= 0 && stage.MapIndex < GameplayManager.MapCount, "Stage map " + i);
            check(stage.bots > 0 && stage.bots <= 4 && stage.difficulty >= 1 && stage.difficulty <= 3, "Stage setup " + i);
            check(stage.boss || stage.kills > 0, "Stage objective " + i);
        }
        bool maps = FeatureFlags.NewMaps;
        try
        {
            FeatureFlags.NewMaps = false;
            for (int i = 1; i <= Campaign.Count; i++) check(Campaign.Get(i).MapIndex < 3, "Old-map fallback " + i);
        }
        finally { FeatureFlags.NewMaps = maps; }
        for (int i = 0; i < 6; i++)
        {
            var sprite = StoryFleetArt.Get(i);
            check(sprite != null && sprite.rect.width > 20 && sprite.rect.height > 20, "Fleet sprite " + i);
            check(Mathf.Abs(sprite.bounds.size.y - 1f) < .001f, "Fleet normalized " + i);
        }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var botGo = new GameObject("VeryEasyCheck");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(botGo, scene);
            botGo.AddComponent<Photon.Pun.PhotonView>();
            var player = botGo.AddComponent<PlayerController>();
            typeof(PlayerController).GetProperty("BotDifficulty").SetValue(player, 4);
            var ai = botGo.AddComponent<BotController>();
            const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(BotController).GetMethod("Start", hidden).Invoke(ai, null);
            check((float)typeof(BotController).GetField("thinkInterval", hidden).GetValue(ai) >= .9f, "Very easy reaction");
            check((float)typeof(BotController).GetField("skillChance", hidden).GetValue(ai) == 0f, "Very easy no skills");
            check((float)typeof(BotController).GetField("leadFactor", hidden).GetValue(ai) == 0f, "Very easy no predictive aim");
            UnityEngine.Object.DestroyImmediate(botGo);
            check(SkinCatalog.All.Length == SkinProjectileFx.Names.Length, "Every skin has a projectile profile");
            for (int i = 0; i < SkinCatalog.All.Length; i++)
            {
                var go = new GameObject("ProjectileCheck");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = UiFx.Glow;
                var collider = go.AddComponent<CircleCollider2D>(); collider.radius = .123f;
                go.AddComponent<TrailRenderer>();
                Vector3 scale = go.transform.localScale;
                SkinProjectileFx.Apply(go, SkinCatalog.All[i]);
                check(go.GetComponent<SkinProjectileFx>() != null, "Projectile effect " + i);
                check(go.transform.childCount == 3, "Bounded effect renderers " + i);
                check(go.transform.localScale == scale && Mathf.Approximately(collider.radius, .123f), "Collision preserved " + i);
                check(go.GetComponentsInChildren<Collider2D>().Length == 1, "Effects have no damage colliders " + i);
                SkinProjectileFx.Apply(go, SkinCatalog.All[i]);
                check(go.transform.childCount == 3, "No duplicate effects " + i);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Debug.Log("PREMIUM_STORY_PASS: " + count + " checks; no account writes.");
        VisualRankedValidation.Run();
    }
}
