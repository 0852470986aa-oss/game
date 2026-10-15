using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Batch-only preview from real UI builders, isolated scene and in-memory account. Never purchases.
public static class PremiumStoryPreview
{
    private const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Batch-only preview");
        PremiumStoryValidation.Run();
        var dataProperty = typeof(Premium).GetProperty("Data");
        var loadedField = typeof(Premium).GetField("loadedFor", BindingFlags.NonPublic | BindingFlags.Static);
        object oldData = dataProperty.GetValue(null), oldLoaded = loadedField.GetValue(null);
        dataProperty.SetValue(null, new PremiumData { localCrystals = 3040, tickets = 3, shards = 485, passSeason = BattlePass.SeasonId, passXp = 4200, passPremium = true });
        loadedField.SetValue(null, "local");
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        var oldActive = RenderTexture.active;
        try
        {
            Directory.CreateDirectory("Builds/PremiumStoryPreviews");
            var root = new GameObject("Preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
            var lobbyObject = new GameObject("PreviewBuilder"); SceneManager.MoveGameObjectToScene(lobbyObject, scene);
            var manager = lobbyObject.AddComponent<LobbyManager>();
            var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(root.transform, false);
            overlay.GetComponent<RectTransform>().sizeDelta = new Vector2(1060, 620);
            overlay.GetComponent<Image>().color = new Color(.025f, .035f, .075f);
            Set(manager, "premiumOverlay", overlay.GetComponent<Image>());
            Set(manager, "premiumWindow", overlay.GetComponent<RectTransform>());
            Set(manager, "lobbyFont", TMP_Settings.defaultFontAsset);
            var cameraObject = new GameObject("PreviewCamera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 360;
            camera.transform.position = new Vector3(0, 0, -100); camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.01f,.02f,.04f);
            canvas.worldCamera = camera;
            target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
            Call(manager, "BuildPremiumPage"); Save(camera, target, "shop");
            var page = (RectTransform)typeof(LobbyManager).GetField("premiumPage", Fields).GetValue(manager);
            UnityEngine.Object.DestroyImmediate(page.gameObject); Set(manager, "premiumPage", null);
            var tab = typeof(LobbyManager).GetField("premiumTab", Fields);
            tab.SetValue(manager, Enum.ToObject(tab.FieldType, 1));
            Set(manager, "passPage", 4);
            Call(manager, "BuildPremiumPage"); Save(camera, target, "pass-tickets");
            page = (RectTransform)typeof(LobbyManager).GetField("premiumPage", Fields).GetValue(manager);
            page.gameObject.SetActive(false);
            var upgrade = new GameObject("UpgradePreview", typeof(RectTransform));
            upgrade.transform.SetParent(overlay.transform, false);
            upgrade.GetComponent<RectTransform>().sizeDelta = new Vector2(1100, 640);
            Set(manager, "workshopEmbedded", true);
            Call(manager, "BuildUpgradeTab", upgrade.GetComponent<RectTransform>());
            Save(camera, target, "upgrade");
            UnityEngine.Object.DestroyImmediate(upgrade);
            page.gameObject.SetActive(true);
            Call(manager, "OpenCheckout", Premium.Packs[2]); Save(camera, target, "checkout");
            // Do not invoke approve or purchase.
            var modal = (RectTransform)typeof(LobbyManager).GetField("premiumModal", Fields).GetValue(manager);
            UnityEngine.Object.DestroyImmediate(modal.gameObject); Set(manager, "premiumModal", null);
            Call(manager, "ConfirmCheckout"); Save(camera, target, "authorization");
            Debug.Log("PREMIUM_PREVIEW_PASS: shop, pass, checkout, authorization rendered.");
        }
        finally
        {
            RenderTexture.active = oldActive;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
            dataProperty.SetValue(null, oldData); loadedField.SetValue(null, oldLoaded);
        }
    }
    private static void Set(object target, string field, object value) => typeof(LobbyManager).GetField(field, Fields).SetValue(target, value);
    private static void Call(object target, string method, params object[] args) => typeof(LobbyManager).GetMethod(method, Fields).Invoke(target, args);
    private static void Save(Camera camera, RenderTexture target, string name)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
        camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
        File.WriteAllBytes("Builds/PremiumStoryPreviews/" + name + ".png", pixels.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(pixels);
    }
}
