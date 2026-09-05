using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SceneSetupTool
{
    [MenuItem("Battlefield/Setup Initial Scenes")]
    public static void SetupScenes()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        BuildLoginScene();
        BuildLobbyScene();
        BuildSampleScene();
        SetupBuildSettings();
        ConfigureImagesAsSprites();
        BuildShipPrefabs();
        BuildBulletPrefab();
        CreateSkillPrefabs();
        CreateHazardPrefabs();
        CreateJuicePrefabs();

        Debug.Log("=== สร้างฉากทั้งหมดเรียบร้อย! ===");
        EditorSceneManager.OpenScene("Assets/Scenes/LoginScene.unity");
    }

    [MenuItem("Battlefield/Configure Images as Sprites")]
    public static void ConfigureImagesAsSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Images" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = new Vector2(0.5f, 0.5f); // บังคับให้จุดหมุนอยู่ตรงกลางเสมอ
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
        Debug.Log("=== อัปเดตตั้งค่ารูปภาพทั้งหมดเป็น Sprite เรียบร้อย! ===");
    }

    [MenuItem("Battlefield/Build Ship Prefabs")]
    public static void BuildShipPrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/ShipPrefabs"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "ShipPrefabs");
        }

        CreateShipPrefab("Ship1", "Images/ship1", 0.4f);  // Nebula Ghost - เล็กสุด
        CreateShipPrefab("Ship2", "Images/ship2", 0.65f); // Comet Crusher - ใหญ่สุด
        CreateShipPrefab("Ship3", "Images/ship3", 0.5f);  // Stellar Striker - กลาง
        Debug.Log("=== สร้าง Ship Prefabs สำหรับ Gameplay เรียบร้อย! ===");
    }

    private static void CreateShipPrefab(string prefabName, string spritePath, float shipScale = 0.5f)
    {
        string path = $"Assets/Resources/ShipPrefabs/{prefabName}.prefab";
        GameObject go = new GameObject(prefabName);
        
        // Sprite
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Sprite sp = Resources.Load<Sprite>(spritePath);
        if (sp != null) sr.sprite = sp;
        
        // Scale ตามประเภทยาน (PHASE 1)
        go.transform.localScale = new Vector3(shipScale, shipScale, 1f);

        // Physics & Collision
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.isKinematic = true; // PlayerController handles movement directly
        go.AddComponent<PolygonCollider2D>();

        // Networking
        go.AddComponent<Photon.Pun.PhotonView>();
        var ptv = go.AddComponent<Photon.Pun.PhotonTransformView>();
        ptv.m_SynchronizePosition = true;
        ptv.m_SynchronizeRotation = false;
        ptv.m_SynchronizeScale = false;

        go.AddComponent<PlayerController>();
        
        // Ensure PhotonView observes the TransformView
        go.GetComponent<Photon.Pun.PhotonView>().ObservedComponents = new System.Collections.Generic.List<Component> { ptv };

        // Ensure PhotonView observes the PlayerController
        go.GetComponent<Photon.Pun.PhotonView>().ObservedComponents.Add(go.GetComponent<PlayerController>());

        // FirePoint Setup (ปรับตาม Scale)
        GameObject fpObj = new GameObject("FirePoint");
        fpObj.transform.SetParent(go.transform);
        fpObj.transform.localPosition = new Vector3(0, 1.2f / shipScale * 0.5f, 0);
        go.GetComponent<PlayerController>().firePoint = fpObj.transform;

        // Shield Visual (ปรับตาม Scale)
        GameObject shieldObj = new GameObject("ShieldVisual");
        shieldObj.transform.SetParent(go.transform);
        shieldObj.transform.localPosition = Vector3.zero;
        var shieldSr = shieldObj.AddComponent<SpriteRenderer>();
        shieldSr.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        shieldSr.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        float shieldScale = 3f / shipScale * 0.5f;
        shieldObj.transform.localScale = new Vector3(shieldScale, shieldScale, 1f);
        shieldObj.SetActive(false);
        go.GetComponent<PlayerController>().shieldVisual = shieldObj;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    [MenuItem("Battlefield/Build Bullet Prefab")]
    public static void BuildBulletPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        string texPath = "Assets/Resources/Images/laser_bullet.png";
        if (!System.IO.File.Exists(texPath))
        {
            Texture2D tex = new Texture2D(4, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, new Color(0.4f, 0.8f, 1f, (x == 0 || x == 3) ? 0.5f : 1f));
            tex.Apply();
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.Refresh();
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null) { importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 16; importer.SaveAndReimport(); }
        }

        string path = "Assets/Resources/BulletPrefab.prefab";
        GameObject go = new GameObject("BulletPrefab");
        
        // Visual
        var sr = go.AddComponent<SpriteRenderer>();
        Sprite laserSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
        if (laserSprite != null) sr.sprite = laserSprite;
        else sr.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        
        // Trail Renderer for visual flair
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.15f;
        tr.startWidth = 0.1f;
        tr.endWidth = 0f;
        tr.material = new Material(Shader.Find("Sprites/Default"));
        Gradient gradient = new Gradient();
        gradient.SetKeys(new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.8f, 1f), 0.0f), new GradientColorKey(Color.white, 1.0f) }, new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
        tr.colorGradient = gradient;

        // Physics
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // ป้องกันทะลุ
        
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.2f, 0.8f);
        col.isTrigger = true;

        // Networking
        go.AddComponent<Photon.Pun.PhotonView>();
        var ptv = go.AddComponent<Photon.Pun.PhotonTransformView>();
        ptv.m_SynchronizePosition = true;
        ptv.m_SynchronizeRotation = true;
        ptv.m_SynchronizeScale = false;
        go.GetComponent<Photon.Pun.PhotonView>().ObservedComponents = new System.Collections.Generic.List<Component> { ptv };

        // Controller
        var bc = go.AddComponent<BulletController>();
        bc.speed = 15f;
        bc.lifeTime = 2f;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("=== สร้าง Bullet Prefab สำหรับ Gameplay เรียบร้อย! ===");
    }

    private static void CreateSkillPrefabs()
    {
        string[] skills = { "Skill_StunWave", "Skill_NovaBlast", "Skill_SeekerMissile" };
        foreach (string skillName in skills)
        {
            string path = "Assets/Resources/" + skillName + ".prefab";
            GameObject go = new GameObject(skillName);
            
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("Images/icon_" + skillName.Split('_')[1].Replace("Wave", "stun").Replace("Blast", "nova").Replace("Missile", "seeker").ToLower());
            if (sr.sprite == null) sr.sprite = Resources.Load<Sprite>("Images/icon_stun");
            sr.color = new Color(1, 1, 1, 0.8f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            go.AddComponent<Photon.Pun.PhotonView>();
            var ptv = go.AddComponent<Photon.Pun.PhotonTransformView>();
            ptv.m_SynchronizePosition = true;
            ptv.m_SynchronizeRotation = true;
            go.GetComponent<Photon.Pun.PhotonView>().ObservedComponents = new System.Collections.Generic.List<Component> { ptv };

            var sc = go.AddComponent<SkillController>();
            
            if (skillName == "Skill_StunWave")
            {
                sc.behavior = SkillController.SkillBehavior.StunWave;
                sc.speed = 15f;
                sc.lifeTime = 2f;
                sr.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                sr.color = new Color(0.4f, 1f, 1f, 0.8f); // Cyan wave
                go.transform.localScale = new Vector3(3f, 0.5f, 1f); // กว้างๆ แบนๆ เหมือนคลื่น
                col.radius = 1f;
            }
            else if (skillName == "Skill_NovaBlast")
            {
                sc.behavior = SkillController.SkillBehavior.NovaBlast;
                sc.speed = 0f;
                sc.lifeTime = 0.5f;
                sr.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                sr.color = new Color(1f, 0.5f, 0.2f, 0.5f); // Orange Nova
                go.transform.localScale = new Vector3(5f, 5f, 1f); // ระเบิดวงกว้างมาก
                col.radius = 1.2f;

                // Add ParticleSystem for explosive effect
                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.5f;
                main.startLifetime = 0.5f;
                main.startSpeed = 10f;
                main.startSize = 0.5f;
                main.startColor = new Color(1f, 0.5f, 0.2f, 1f);
                main.loop = false;
                
                var emission = ps.emission;
                emission.rateOverTime = 0;
                emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            else if (skillName == "Skill_SeekerMissile")
            {
                sc.behavior = SkillController.SkillBehavior.SeekerMissile;
                sc.speed = 12f;
                sc.lifeTime = 4f;
                go.transform.localScale = new Vector3(1f, 1f, 1f);
                col.radius = 0.4f;

                // Trail Renderer for smoke
                var tr = go.AddComponent<TrailRenderer>();
                tr.time = 0.5f;
                tr.startWidth = 0.3f;
                tr.endWidth = 0.1f;
                tr.material = new Material(Shader.Find("Sprites/Default"));
                Gradient gradient = new Gradient();
                gradient.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.gray, 0.0f), new GradientColorKey(Color.darkGray, 1.0f) }, new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
                tr.colorGradient = gradient;
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
        Debug.Log("=== สร้าง Skill Prefabs เรียบร้อย! ===");
    }

    private static void CreateHazardPrefabs()
    {
        string[] hazards = { "Hazard_Lightning", "Hazard_SlowZone", "Hazard_Meteor", "Hazard_MoltenAsteroid", "Hazard_EnergyCore" };
        foreach (string h in hazards)
        {
            string path = "Assets/Resources/" + h + ".prefab";
            GameObject go = new GameObject(h);
            
            // Warning Area (Red Blink)
            GameObject warnObj = new GameObject("WarningArea");
            warnObj.transform.SetParent(go.transform);
            warnObj.transform.localPosition = Vector3.zero;
            var warnSr = warnObj.AddComponent<SpriteRenderer>();
            warnSr.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            warnSr.color = new Color(1f, 0f, 0f, 0.3f);
            
            // Effect Visual
            GameObject effObj = new GameObject("EffectVisual");
            effObj.transform.SetParent(go.transform);
            effObj.transform.localPosition = Vector3.zero;
            var effSr = effObj.AddComponent<SpriteRenderer>();
            effSr.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            
            // Collider
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.enabled = false; // Enabled only during effect phase

            // Scale and Color based on type
            var hc = go.AddComponent<HazardController>();
            
            if (h == "Hazard_Lightning")
            {
                hc.type = HazardController.HazardType.Lightning;
                effSr.color = new Color(0.8f, 1f, 1f, 0.9f); // Bright white/cyan lightning flash
                go.transform.localScale = new Vector3(3f, 3f, 1f); // PHASE 5: ใหญ่ขึ้น
                col.radius = 0.5f;
            }
            else if (h == "Hazard_SlowZone")
            {
                hc.type = HazardController.HazardType.SlowZone;
                effSr.color = new Color(0.2f, 0.2f, 0.8f, 0.5f); // Purple slow pool
                go.transform.localScale = new Vector3(5f, 5f, 1f); // PHASE 5: ใหญ่ขึ้น
                col.radius = 0.5f;
            }
            else if (h == "Hazard_Meteor")
            {
                hc.type = HazardController.HazardType.Meteor;
                effSr.color = new Color(1f, 0.5f, 0f, 0.9f); // Orange meteor explosion
                go.transform.localScale = new Vector3(3f, 3f, 1f);
                col.radius = 0.5f;
            }
            else if (h == "Hazard_MoltenAsteroid")
            {
                hc.type = HazardController.HazardType.MoltenAsteroid;
                effSr.color = new Color(1f, 0.3f, 0f, 1f); // Lava Red
                go.transform.localScale = new Vector3(2f, 2f, 1f);
                col.radius = 0.5f;
            }
            else if (h == "Hazard_EnergyCore")
            {
                hc.type = HazardController.HazardType.EnergyCore;
                effSr.color = new Color(0f, 1f, 1f, 0.5f); // Cyan transparent core
                go.transform.localScale = new Vector3(6f, 6f, 1f); // Large buff zone
                col.radius = 0.5f;
            }

            hc.warningArea = warnSr;
            hc.effectVisual = effSr;
            hc.hitCollider = col;

            // Networking
            go.AddComponent<Photon.Pun.PhotonView>();
            var ptv = go.AddComponent<Photon.Pun.PhotonTransformView>();
            ptv.m_SynchronizePosition = true;
            ptv.m_SynchronizeRotation = false;
            go.GetComponent<Photon.Pun.PhotonView>().ObservedComponents = new System.Collections.Generic.List<Component> { ptv };

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // Create Black Pillar separately
        string pillarPath = "Assets/Resources/Hazard_BlackPillar.prefab";
        GameObject pillarObj = new GameObject("Hazard_BlackPillar");
        var pSr = pillarObj.AddComponent<SpriteRenderer>();
        pSr.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        pSr.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Black pillar
        pillarObj.transform.localScale = new Vector3(2f, 2f, 1f);
        var pCol = pillarObj.AddComponent<CircleCollider2D>();
        pCol.isTrigger = false; // Solid!
        var prb = pillarObj.AddComponent<Rigidbody2D>();
        prb.isKinematic = true; // Static body
        
        pillarObj.AddComponent<Photon.Pun.PhotonView>();
        var pPtv = pillarObj.AddComponent<Photon.Pun.PhotonTransformView>();
        pPtv.m_SynchronizePosition = true;
        pillarObj.GetComponent<Photon.Pun.PhotonView>().ObservedComponents = new System.Collections.Generic.List<Component> { pPtv };
        
        PrefabUtility.SaveAsPrefabAsset(pillarObj, pillarPath);
        Object.DestroyImmediate(pillarObj);
    }

    private static void CreateJuicePrefabs()
    {
        // 1. Floating Text
        string textPath = "Assets/Resources/FloatingText.prefab";
        GameObject textObj = new GameObject("FloatingText");
        var tmp = textObj.AddComponent<TextMeshPro>();
        tmp.fontSize = 6;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.red;
        tmp.fontStyle = FontStyles.Bold;
        textObj.AddComponent<FloatingText>();
        PrefabUtility.SaveAsPrefabAsset(textObj, textPath);
        Object.DestroyImmediate(textObj);

        // 2. Death Explosion
        string deathPath = "Assets/Resources/DeathExplosion.prefab";
        GameObject deathObj = new GameObject("DeathExplosion");
        var ps = deathObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.5f;
        main.startLifetime = 1f;
        main.startSpeed = 8f;
        main.startSize = 1.5f;
        main.startColor = new Color(1f, 0.4f, 0f, 1f); // Orange Explosion
        main.loop = false;
        
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1f;

        var renderer = deathObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        deathObj.AddComponent<DestroyAfterSeconds>().lifetime = 2f;
        
        PrefabUtility.SaveAsPrefabAsset(deathObj, deathPath);
        Object.DestroyImmediate(deathObj);
    }

    // ============================
    //  LOGIN SCENE
    // ============================
    private static void BuildLoginScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera(new Color(0.02f, 0.02f, 0.08f));

        GameObject firebaseObj = new GameObject("FirebaseManager");
        firebaseObj.AddComponent<FirebaseManager>();

        GameObject audioManagerObj = new GameObject("AudioManager");
        audioManagerObj.AddComponent<AudioManager>();

        GameObject loginManagerObj = new GameObject("LoginManager");
        LoginManager loginManager = loginManagerObj.AddComponent<LoginManager>();

        GameObject canvasObj = CreateCanvas();
        CreateEventSystem();

        // พื้นหลัง
        CreateBackground(canvasObj.transform, "Images/LoginBG_HQ");

        // ชื่อเกม
        CreateTMPText("TitleLine1", "BATTLEFIELD", canvasObj.transform,
            new Vector2(0, 320), 60, new Color(0.4f, 0.75f, 1f), FontStyles.Bold);
        CreateTMPText("TitleLine2", "OF THE STARS", canvasObj.transform,
            new Vector2(0, 250), 50, new Color(0.9f, 0.7f, 0.3f), FontStyles.Bold);
        CreateTMPText("SubTitle", "ONLINE ARENA", canvasObj.transform,
            new Vector2(0, 200), 22, new Color(0.7f, 0.8f, 0.9f), FontStyles.Normal);

        // สถานะ
        var statusObj = CreateTMPText("StatusText", "System Initializing...", canvasObj.transform,
            new Vector2(0, -50), 20, new Color(0.7f, 0.9f, 1f), FontStyles.Normal);

        // ปุ่ม Login Google (สีฟ้า)
        var btnGoogle = CreateTMPButton("Btn_LoginGoogle", "Login Google", canvasObj.transform,
            new Vector2(0, -130), new Vector2(380, 65),
            new Color(0.35f, 0.55f, 0.85f), Color.white, 26);

        // ปุ่ม Guest (สีเทา)
        var btnGuest = CreateTMPButton("Btn_LoginGuest", "Guest", canvasObj.transform,
            new Vector2(0, -215), new Vector2(380, 65),
            new Color(0.45f, 0.45f, 0.5f), Color.white, 26);

        // Error
        var errorObj = CreateTMPText("ErrorText", "", canvasObj.transform,
            new Vector2(0, -300), 18, new Color(1f, 0.3f, 0.3f), FontStyles.Normal);
        errorObj.SetActive(false);

        // Version
        CreateTMPText("VersionText", "v0.1 Alpha", canvasObj.transform,
            new Vector2(0, -490), 14, new Color(0.4f, 0.4f, 0.5f), FontStyles.Normal);

        // ผูก UI
        loginManager.statusText = statusObj.GetComponent<TMP_Text>();
        loginManager.errorText = errorObj.GetComponent<TMP_Text>();
        loginManager.googleButton = btnGoogle.GetComponent<Button>();
        loginManager.guestButton = btnGuest.GetComponent<Button>();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnGoogle.GetComponent<Button>().onClick, loginManager.LoginGoogle);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnGuest.GetComponent<Button>().onClick, loginManager.LoginGuest);

        btnGoogle.GetComponent<Button>().interactable = false;
        btnGuest.GetComponent<Button>().interactable = false;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LoginScene.unity");
    }

    // ============================
    //  LOBBY SCENE
    // ============================
    private static void BuildLobbyScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera(new Color(0.02f, 0.04f, 0.1f));

        GameObject lobbyManagerObj = new GameObject("LobbyManager");
        LobbyManager lm = lobbyManagerObj.AddComponent<LobbyManager>();

        GameObject canvasObj = CreateCanvas();
        CreateEventSystem();

        CreateBackground(canvasObj.transform, "Images/LobbyBG_HQ");

        // =============================================
        //  MAIN PANEL
        // =============================================
        GameObject mainPanel = new GameObject("MainPanel");
        mainPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform mpRT = mainPanel.AddComponent<RectTransform>();
        mpRT.anchorMin = Vector2.zero; mpRT.anchorMax = Vector2.one;
        mpRT.offsetMin = Vector2.zero; mpRT.offsetMax = Vector2.zero;

        // --- TOP BAR (full width) ---
        var topBar = CreatePanel("TopBar", mainPanel.transform, Vector2.zero, Vector2.zero, new Color(0.05f, 0.1f, 0.2f, 0.85f));
        SetAnchor(topBar, new Vector2(0, 1), new Vector2(1, 1));
        topBar.GetComponent<RectTransform>().offsetMin = new Vector2(0, -55);
        topBar.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        var gameTitleObj = CreateTMPText("GameTitle", "Battlefield of\nthe Star", topBar.transform, Vector2.zero, 22, new Color(0.4f, 0.85f, 1f), FontStyles.Bold | FontStyles.Italic);
        SetAnchor(gameTitleObj, new Vector2(0, 0), new Vector2(0, 1));
        gameTitleObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(120, 0);
        gameTitleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 55);
        gameTitleObj.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        var winsObj = CreateTMPText("WinsText", "Total Wins : 0", topBar.transform, Vector2.zero, 18, Color.white, FontStyles.Normal);
        SetAnchor(winsObj, new Vector2(1, 0), new Vector2(1, 1));
        winsObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-350, 0);
        winsObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 55);

        var coinObj = CreateTMPText("CoinText", "Astronium Coins : 0", topBar.transform, Vector2.zero, 18, new Color(1f, 0.85f, 0.2f), FontStyles.Normal);
        SetAnchor(coinObj, new Vector2(1, 0), new Vector2(1, 1));
        coinObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-120, 0);
        coinObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 55);

        // --- LEFT SECTION ---
        var playerPanel = CreatePanel("PlayerPanel", mainPanel.transform, Vector2.zero, new Vector2(280, 100), new Color(0.1f, 0.15f, 0.3f, 0.85f));
        SetAnchor(playerPanel, new Vector2(0, 1), new Vector2(0, 1));
        playerPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(160, -100);

        var avatarObj = CreatePanel("Avatar", playerPanel.transform, new Vector2(-90, 0), new Vector2(70, 70), new Color(0.3f, 0.35f, 0.45f));
        SetImageSprite(avatarObj, "Images/หน้าตัวละครเอก");
        var playerNameObj = CreateTMPText("PlayerNameText", "Player\nplayer 1", playerPanel.transform, new Vector2(30, 0), 20, Color.white, FontStyles.Normal);
        playerNameObj.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 80);
        playerNameObj.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        var btnInventory = CreateTMPButton("Btn_Inventory", "Inventory", mainPanel.transform,
            Vector2.zero, new Vector2(240, 65), new Color(0.15f, 0.3f, 0.55f, 0.9f), Color.white, 26);
        SetAnchor(btnInventory, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        btnInventory.GetComponent<RectTransform>().anchoredPosition = new Vector2(140, 60);

        var btnSettings = CreateTMPButton("Btn_Settings", "Settings", mainPanel.transform,
            Vector2.zero, new Vector2(240, 65), new Color(0.15f, 0.3f, 0.55f, 0.9f), Color.white, 26);
        SetAnchor(btnSettings, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        btnSettings.GetComponent<RectTransform>().anchoredPosition = new Vector2(140, -30);

        var btnTutorial = CreateTMPButton("Btn_Tutorial", "How to Play", mainPanel.transform,
            Vector2.zero, new Vector2(240, 65), new Color(0.6f, 0.4f, 0.1f, 0.9f), Color.white, 26);
        SetAnchor(btnTutorial, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        btnTutorial.GetComponent<RectTransform>().anchoredPosition = new Vector2(140, -120);

        // --- CENTER SECTION: Ship Display ---
        var shipPanel = CreatePanel("ShipPanel", mainPanel.transform, Vector2.zero, new Vector2(380, 480), new Color(0.08f, 0.12f, 0.25f, 0.7f));
        SetAnchor(shipPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        shipPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 20);

        var shipImgArea = CreatePanel("ShipImgArea", shipPanel.transform, new Vector2(0, 90), new Vector2(250, 250), new Color(0, 0, 0, 0));

        var shipNameObj = CreateTMPText("ShipName", "Nebula Ghost", shipPanel.transform, new Vector2(0, -60), 28, Color.white, FontStyles.Bold);
        var shipHPObj = CreateTMPText("ShipHP", "HP: 70", shipPanel.transform, new Vector2(-80, -100), 20, new Color(1f, 0.4f, 0.4f), FontStyles.Normal);
        var shipATKObj = CreateTMPText("ShipATK", "ATK: 0.8", shipPanel.transform, new Vector2(80, -100), 20, new Color(1f, 0.6f, 0.3f), FontStyles.Normal);
        var shipSPDObj = CreateTMPText("ShipSPD", "SPD: 7.5", shipPanel.transform, new Vector2(-80, -135), 20, new Color(0.3f, 0.8f, 1f), FontStyles.Normal);
        var shipSkillObj = CreateTMPText("ShipSkill", "STUN", shipPanel.transform, new Vector2(80, -135), 20, new Color(0.3f, 1f, 0.5f), FontStyles.Bold);

        // Status Text
        var statusObj = CreateTMPText("StatusText", "Connecting...", mainPanel.transform, Vector2.zero, 18, new Color(0.7f, 0.9f, 1f), FontStyles.Normal);
        SetAnchor(statusObj, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        statusObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 50);

        // --- RIGHT SECTION ---
        var btnPlay = CreateTMPButton("Btn_Play", "Play\nQuick Match", mainPanel.transform,
            Vector2.zero, new Vector2(300, 100), new Color(0.2f, 0.65f, 0.3f), Color.white, 24);
        SetAnchor(btnPlay, new Vector2(1, 1), new Vector2(1, 1));
        btnPlay.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180, -130);

        var btnCreateRoom = CreateTMPButton("Btn_CreateRoom", "Create Room", mainPanel.transform,
            Vector2.zero, new Vector2(300, 70), new Color(0.2f, 0.5f, 0.55f), Color.white, 22);
        SetAnchor(btnCreateRoom, new Vector2(1, 1), new Vector2(1, 1));
        btnCreateRoom.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180, -220);

        var playersOnlineObj = CreateTMPText("PlayersOnline", "Players Online: ...", mainPanel.transform,
            Vector2.zero, 16, new Color(0.5f, 0.8f, 0.5f), FontStyles.Normal);
        SetAnchor(playersOnlineObj, new Vector2(1, 1), new Vector2(1, 1));
        playersOnlineObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180, -270);

        // =============================================
        //  INVENTORY PANEL
        // =============================================
        GameObject invPanel = new GameObject("InventoryPanel");
        invPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform ipRT = invPanel.AddComponent<RectTransform>();
        ipRT.anchorMin = Vector2.zero; ipRT.anchorMax = Vector2.one;
        ipRT.offsetMin = Vector2.zero; ipRT.offsetMax = Vector2.zero;
        invPanel.SetActive(false);

        CreatePanel("InvBG", invPanel.transform, Vector2.zero, new Vector2(1920, 1080), new Color(0f, 0f, 0f, 0.7f));

        // Header
        var invTitle = CreateTMPText("InvTitle", "INVENTORY", invPanel.transform, Vector2.zero, 32, Color.white, FontStyles.Bold);
        SetAnchor(invTitle, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        invTitle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -30);

        // Player Info (top-left)
        var invPlayerPanel = CreatePanel("InvPlayerInfo", invPanel.transform, Vector2.zero, new Vector2(250, 80), new Color(0, 0, 0, 0));
        SetAnchor(invPlayerPanel, new Vector2(0, 1), new Vector2(0, 1));
        invPlayerPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(150, -50);
        
        var invAvatarObj = CreatePanel("Avatar", invPlayerPanel.transform, new Vector2(-70, -10), new Vector2(60, 60), new Color(0.3f, 0.35f, 0.45f));
        SetImageSprite(invAvatarObj, "Images/หน้าตัวละครเอก");
        
        CreateTMPText("InvPlayerName", "player 1", invPlayerPanel.transform, new Vector2(10, 10), 22, Color.white, FontStyles.Bold);
        CreateTMPText("InvPlayerCoins", "Coins: 1800", invPlayerPanel.transform, new Vector2(10, -20), 16, new Color(1f, 0.85f, 0.2f), FontStyles.Normal);

        // Back Button (top-right)
        var btnBackInv = CreateTMPButton("Btn_BackInventory", "< Back", invPanel.transform,
            Vector2.zero, new Vector2(130, 45), new Color(0.4f, 0.2f, 0.2f), Color.white, 18);
        SetAnchor(btnBackInv, new Vector2(1, 1), new Vector2(1, 1));
        btnBackInv.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90, -35);

        // Ship List (left)
        var shipListPanel = CreatePanel("ShipListPanel", invPanel.transform, Vector2.zero, new Vector2(350, 480), new Color(0.05f, 0.1f, 0.15f, 0.9f));
        SetAnchor(shipListPanel, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        shipListPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(250, -40);
        var shipListOutline = shipListPanel.AddComponent<UnityEngine.UI.Outline>();
        shipListOutline.effectColor = new Color(0.4f, 0.9f, 0.9f, 0.8f); // Cyan outline
        shipListOutline.effectDistance = new Vector2(2, -2);

        var btnShip0 = CreateTMPButton("Btn_Ship0", "Nebula\nGhost", shipListPanel.transform,
            new Vector2(0, 160), new Vector2(300, 100), new Color(0.1f, 0.6f, 0.3f), Color.white, 22);
        var btnShip1 = CreateTMPButton("Btn_Ship1", "Comet\nCrusher", shipListPanel.transform,
            new Vector2(0, 30), new Vector2(300, 100), new Color(0.15f, 0.25f, 0.45f), Color.white, 20);
        var price1 = CreateTMPText("Price1", "2800", shipListPanel.transform, new Vector2(0, -45), 20, Color.white, FontStyles.Bold);
        var btnShip2 = CreateTMPButton("Btn_Ship2", "Stellar\nStriker", shipListPanel.transform,
            new Vector2(0, -115), new Vector2(300, 100), new Color(0.15f, 0.25f, 0.45f), Color.white, 20);
        var price2 = CreateTMPText("Price2", "3089", shipListPanel.transform, new Vector2(0, -190), 20, Color.white, FontStyles.Bold);

        // Center: Ship Image (Large)
        var invShipImgArea = CreatePanel("InvShipImg", invPanel.transform, new Vector2(0, 50), new Vector2(400, 400), new Color(0, 0, 0, 0));
        
        // Ship Stats (center bottom)
        var invShipPanel = CreatePanel("InvShipDisplay", invPanel.transform, Vector2.zero, new Vector2(400, 160), new Color(0.05f, 0.15f, 0.25f, 0.8f));
        SetAnchor(invShipPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        invShipPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 120);
        var statsOutline = invShipPanel.AddComponent<UnityEngine.UI.Outline>();
        statsOutline.effectColor = new Color(0.2f, 0.8f, 0.8f, 0.8f);
        statsOutline.effectDistance = new Vector2(2, -2);

        var invShipNameObj = CreateTMPText("InvShipName", "Nebula\nGhost", invShipPanel.transform, new Vector2(100, 20), 22, Color.white, FontStyles.Bold);
        var invShipHPObj = CreateTMPText("InvShipHP", "70 HP", invShipPanel.transform, new Vector2(-120, 40), 16, Color.white, FontStyles.Normal);
        var invShipATKObj = CreateTMPText("InvShipATK", "0.8", invShipPanel.transform, new Vector2(-20, 40), 16, Color.white, FontStyles.Normal);
        var invShipSPDObj = CreateTMPText("InvShipSPD", "7.5", invShipPanel.transform, new Vector2(-120, 0), 16, Color.white, FontStyles.Normal);

        var btnInventoryAction = CreateTMPButton("Btn_Action", "Equip", invShipPanel.transform,
            Vector2.zero, new Vector2(250, 60), new Color(0.2f, 0.6f, 0.3f), Color.white, 24);
        SetAnchor(btnInventoryAction, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        btnInventoryAction.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 40); // 40 units above bottom edge of invShipPanel

        // Skills Box (right)
        var skillPanel = CreatePanel("SkillPanel", invPanel.transform, Vector2.zero, new Vector2(400, 550), new Color(0.05f, 0.1f, 0.15f, 0.9f));
        SetAnchor(skillPanel, new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        skillPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(-250, -10);
        var skillOutline = skillPanel.AddComponent<UnityEngine.UI.Outline>();
        skillOutline.effectColor = new Color(0.4f, 0.9f, 0.9f, 0.8f);
        skillOutline.effectDistance = new Vector2(2, -2);

        // 4 Skill buttons
        var btnSkillStun = CreateTMPButton("Btn_SkillSTUN", "", skillPanel.transform,
            new Vector2(-90, 140), new Vector2(140, 140), new Color(0,0,0,0), Color.white, 16);
        SetImageSprite(btnSkillStun, "Images/icon_stun");
        btnSkillStun.GetComponent<Image>().preserveAspect = true;
        var labelStun = CreateTMPText("LabelSTUN", "STUN", skillPanel.transform, new Vector2(-90, 50), 16, new Color(0.6f, 0.8f, 0.2f), FontStyles.Bold);

        var btnSkillShield = CreateTMPButton("Btn_SkillSHIELD", "", skillPanel.transform,
            new Vector2(90, 140), new Vector2(140, 140), new Color(0,0,0,0), Color.white, 16);
        SetImageSprite(btnSkillShield, "Images/icon_shield");
        btnSkillShield.GetComponent<Image>().preserveAspect = true;
        var labelShield = CreateTMPText("LabelSHIELD", "SHIELD", skillPanel.transform, new Vector2(90, 50), 16, new Color(0.8f, 0.6f, 0.2f), FontStyles.Bold);

        var btnSkillNova = CreateTMPButton("Btn_SkillNOVA", "", skillPanel.transform,
            new Vector2(-90, -60), new Vector2(140, 140), new Color(0,0,0,0), Color.white, 16);
        SetImageSprite(btnSkillNova, "Images/icon_nova");
        btnSkillNova.GetComponent<Image>().preserveAspect = true;
        var labelNova = CreateTMPText("LabelNOVA", "NOVA", skillPanel.transform, new Vector2(-90, -150), 16, new Color(0.8f, 0.3f, 0.4f), FontStyles.Bold);

        var btnSkillSeeker = CreateTMPButton("Btn_SkillSEEKER", "", skillPanel.transform,
            new Vector2(90, -60), new Vector2(140, 140), new Color(0,0,0,0), Color.white, 16);
        SetImageSprite(btnSkillSeeker, "Images/icon_seeker");
        btnSkillSeeker.GetComponent<Image>().preserveAspect = true;
        var labelSeeker = CreateTMPText("LabelSEEKER", "SEEKER", skillPanel.transform, new Vector2(90, -150), 16, new Color(0.4f, 0.7f, 0.9f), FontStyles.Bold);

        // Divider Line
        var divider = CreatePanel("Divider", skillPanel.transform, new Vector2(0, -190), new Vector2(360, 2), new Color(0.4f, 0.8f, 1f, 0.5f));
        var glow = divider.AddComponent<UnityEngine.UI.Outline>();
        glow.effectColor = new Color(0.2f, 0.6f, 1f, 1f);
        glow.effectDistance = new Vector2(0, 2);

        // Skill Description & Install button (inside skillPanel)
        var invShipSkillObj = CreateTMPText("InvShipSkill", "STUN", skillPanel.transform, new Vector2(-150, -230), 22, Color.white, FontStyles.Bold);
        var skillDescText = CreateTMPText("SkillDescText", "Paralyze wave\nCooldown 12 sec", skillPanel.transform, new Vector2(-10, -230), 14, Color.white, FontStyles.Normal);
        skillDescText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        var btnInstallSkill = CreateTMPButton("Btn_InstallSkill", "Install", skillPanel.transform,
            new Vector2(140, -230), new Vector2(80, 40), new Color(0.1f, 0.6f, 0.3f), Color.white, 16);

        // =============================================
        //  ROOM PANEL
        // =============================================
        GameObject rmPanel = new GameObject("RoomPanel");
        rmPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform rpRT = rmPanel.AddComponent<RectTransform>();
        rpRT.anchorMin = Vector2.zero; rpRT.anchorMax = Vector2.one;
        rpRT.offsetMin = Vector2.zero; rpRT.offsetMax = Vector2.zero;
        rmPanel.SetActive(false);

        CreatePanel("RoomBG", rmPanel.transform, Vector2.zero, new Vector2(1920, 1080), new Color(0f, 0f, 0f, 0.7f));

        // Player Info (top-left)
        var rmPlayerPanel = CreatePanel("RmPlayerInfo", rmPanel.transform, Vector2.zero, new Vector2(250, 80), new Color(0, 0, 0, 0));
        SetAnchor(rmPlayerPanel, new Vector2(0, 1), new Vector2(0, 1));
        rmPlayerPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(150, -50);

        var rmAvatarObj = CreatePanel("Avatar", rmPlayerPanel.transform, new Vector2(-70, -10), new Vector2(60, 60), new Color(0.3f, 0.35f, 0.45f));
        SetImageSprite(rmAvatarObj, "Images/หน้าตัวละครเอก");

        CreateTMPText("RmPlayerName", "player 1", rmPlayerPanel.transform, new Vector2(10, 10), 22, Color.white, FontStyles.Bold);
        CreateTMPText("RmPlayerCoins", "Coins: 1800", rmPlayerPanel.transform, new Vector2(10, -20), 16, new Color(1f, 0.85f, 0.2f), FontStyles.Normal);

        // Back Button (top-right)
        var btnBackRoom = CreateTMPButton("Btn_BackRoom", "< Back", rmPanel.transform,
            Vector2.zero, new Vector2(130, 45), new Color(0.4f, 0.2f, 0.2f), Color.white, 18);
        SetAnchor(btnBackRoom, new Vector2(1, 1), new Vector2(1, 1));
        btnBackRoom.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90, -35);

        // LEFT: Create Room
        var createPanel = CreatePanel("CreateRoomPanel", rmPanel.transform, Vector2.zero, new Vector2(480, 480), new Color(0.08f, 0.15f, 0.3f, 0.8f));
        SetAnchor(createPanel, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        createPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(280, -20);

        CreateTMPText("CreateTitle", "Create Room", createPanel.transform, new Vector2(0, 200), 28, new Color(0.4f, 0.8f, 1f), FontStyles.Bold);
        CreateTMPText("CreateSubtitle", "Room Settings", createPanel.transform, new Vector2(0, 168), 16, Color.gray, FontStyles.Normal);

        var roomNumLabel = CreateTMPText("RoomNumLabel", "Room No. :", createPanel.transform, new Vector2(-100, 110), 20, Color.white, FontStyles.Normal);
        roomNumLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        var roomNumObj = CreateTMPText("RoomNumber", "653478", createPanel.transform, new Vector2(100, 110), 22, Color.white, FontStyles.Bold);

        var modeLabel = CreateTMPText("ModeLabel", "Game Mode :", createPanel.transform, new Vector2(-100, 60), 20, Color.white, FontStyles.Normal);
        modeLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        var roomModeObj = CreateTMPText("RoomMode", "1 VS 1\n(Quick Match)", createPanel.transform, new Vector2(100, 50), 18, Color.white, FontStyles.Normal);

        var mapLabel = CreateTMPText("MapLabel", "Map :", createPanel.transform, new Vector2(-100, -10), 20, Color.white, FontStyles.Normal);
        mapLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        
        var btnPrevMap = CreateTMPButton("Btn_PrevMap", "<", createPanel.transform, new Vector2(-20, -10), new Vector2(40, 40), new Color(0.1f, 0.2f, 0.4f), Color.white, 20);
        var mapNameText = CreateTMPText("MapName", "Thunder Jellyfish Core", createPanel.transform, new Vector2(100, -10), 16, Color.white, FontStyles.Bold);
        var btnNextMap = CreateTMPButton("Btn_NextMap", ">", createPanel.transform, new Vector2(220, -10), new Vector2(40, 40), new Color(0.1f, 0.2f, 0.4f), Color.white, 20);

        var btnCreateConfirm = CreateTMPButton("Btn_CreateConfirm", "Create Room", createPanel.transform,
            new Vector2(0, -170), new Vector2(350, 70), new Color(0.2f, 0.6f, 0.35f), Color.white, 26);

        // RIGHT: Join Room
        var joinPanel = CreatePanel("JoinRoomPanel", rmPanel.transform, Vector2.zero, new Vector2(480, 480), new Color(0.05f, 0.1f, 0.15f, 0.9f));
        SetAnchor(joinPanel, new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        joinPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(-280, -20);
        var joinOutline = joinPanel.AddComponent<UnityEngine.UI.Outline>();
        joinOutline.effectColor = new Color(0.4f, 0.9f, 0.9f, 0.8f);
        joinOutline.effectDistance = new Vector2(2, -2);

        CreateTMPText("JoinTitle", "Join Room", joinPanel.transform, new Vector2(0, 200), 28, new Color(0.4f, 0.8f, 1f), FontStyles.Bold);
        CreateTMPText("JoinSubtitle", "Search Room", joinPanel.transform, new Vector2(0, 168), 16, Color.gray, FontStyles.Normal);

        var searchInput = CreateLegacyInputField("SearchInput", "Enter Room No.", joinPanel.transform, new Vector2(-30, 120));
        var btnSearch = CreateTMPButton("Btn_Search", "Search", joinPanel.transform,
            new Vector2(190, 120), new Vector2(100, 40), new Color(0.3f, 0.5f, 0.7f), Color.white, 16);

        CreateTMPText("RoomListTitle", "Available Rooms", joinPanel.transform, new Vector2(0, 70), 20, new Color(1f, 0.85f, 0.3f), FontStyles.Bold);

        // Room List (ScrollView)
        var scrollRectObj = CreatePanel("RoomListScroll", joinPanel.transform, Vector2.zero, new Vector2(440, 320), new Color(0, 0, 0, 0));
        SetAnchor(scrollRectObj, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        scrollRectObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 180);
        
        var viewport = CreatePanel("Viewport", scrollRectObj.transform, Vector2.zero, new Vector2(440, 320), new Color(0, 0, 0, 0));
        viewport.AddComponent<RectMask2D>();
        
        var contentPanel = CreatePanel("Content", viewport.transform, Vector2.zero, new Vector2(440, 320), new Color(0, 0, 0, 0));
        SetAnchor(contentPanel, new Vector2(0, 1), new Vector2(1, 1));
        var vlg = contentPanel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 10;
        var csf = contentPanel.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = scrollRectObj.AddComponent<UnityEngine.UI.ScrollRect>();
        scrollRect.content = contentPanel.GetComponent<RectTransform>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 20f;

        // RoomItem Prefab (ซ่อนไว้สำหรับก๊อปปี้)
        var roomItem = CreatePanel("RoomItemPrefab", contentPanel.transform, Vector2.zero, new Vector2(440, 60), new Color(0.15f, 0.2f, 0.35f, 0.8f));
        var rLe = CreatePanel("LayoutElement", roomItem.transform, Vector2.zero, Vector2.zero, Color.clear);
        var le = roomItem.AddComponent<UnityEngine.UI.LayoutElement>();
        le.minHeight = 60;
        
        var roomNameObj = CreateTMPText("RoomNameText", "Room: 123456", roomItem.transform, new Vector2(-100, 28), 16, Color.white, FontStyles.Bold);
        roomNameObj.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        
        CreateTMPText("RoomModeMapText", "1 VS 1 | Obelisk Plains", roomItem.transform, new Vector2(-100, 8), 12, Color.gray, FontStyles.Normal).GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        
        var roomPlayersObj = CreateTMPText("RoomPlayersText", "1 / 2", roomItem.transform, new Vector2(50, 18), 14, Color.white, FontStyles.Normal);
        
        var btnJoinRoom = CreateTMPButton("Btn_Join", "Join", roomItem.transform, new Vector2(170, 20), new Vector2(80, 40), new Color(0.2f, 0.5f, 0.3f), Color.white, 16);
        
        roomItem.SetActive(false); // ซ่อนไว้เป็นต้นแบบ

        // =============================================
        //  SETTINGS PANEL
        // =============================================
        GameObject setPanel = new GameObject("SettingsPanel");
        setPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform setRT = setPanel.AddComponent<RectTransform>();
        setRT.anchorMin = Vector2.zero; setRT.anchorMax = Vector2.one;
        setRT.offsetMin = Vector2.zero; setRT.offsetMax = Vector2.zero;
        setPanel.SetActive(false); // ซ่อนตอนเริ่ม

        // พื้นหลังดำโปร่งแสง
        CreatePanel("SettingsBG", setPanel.transform, Vector2.zero, new Vector2(1920, 1080), new Color(0, 0, 0, 0.85f));

        // กรอบนอกสุด (สีขอบ Cyan)
        var setOuter = CreatePanel("SettingsOuter", setPanel.transform, Vector2.zero, new Vector2(800, 500), new Color(0.1f, 0.15f, 0.2f, 1f));
        // เพิ่ม Outline สี Cyan
        var outline = setOuter.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.2f, 0.8f, 0.8f, 1f);
        outline.effectDistance = new Vector2(4, -4);

        // Header Section
        var setHeader = CreatePanel("SettingsHeader", setOuter.transform, new Vector2(0, 200), new Vector2(760, 80), new Color(0.05f, 0.1f, 0.15f, 1f));
        var headerOutline = setHeader.AddComponent<UnityEngine.UI.Outline>();
        headerOutline.effectColor = new Color(0.2f, 0.8f, 0.8f, 1f);
        headerOutline.effectDistance = new Vector2(2, -2);
        
        CreateTMPText("SettingsTitle", "SETTINGS", setHeader.transform, Vector2.zero, 32, new Color(0.3f, 0.8f, 0.8f), FontStyles.Bold);

        var btnCloseSet = CreateTMPButton("Btn_Close", "X", setOuter.transform, new Vector2(350, 200), new Vector2(50, 50), new Color(0.05f, 0.1f, 0.15f, 1f), new Color(0.2f, 0.8f, 0.8f), 24);

        // Sliders Section
        var contentBox = CreatePanel("ContentBox", setOuter.transform, new Vector2(0, -20), new Vector2(760, 320), new Color(0.05f, 0.1f, 0.15f, 1f));
        var contentOutline = contentBox.AddComponent<UnityEngine.UI.Outline>();
        contentOutline.effectColor = new Color(0.2f, 0.8f, 0.8f, 1f);
        contentOutline.effectDistance = new Vector2(2, -2);

        // Master Volume
        var rowMaster = CreatePanel("RowMaster", contentBox.transform, new Vector2(0, 100), new Vector2(700, 70), new Color(0.1f, 0.15f, 0.2f, 0.5f));
        CreateTMPText("LblMaster", "Master Volume", rowMaster.transform, new Vector2(-150, 0), 24, Color.white, FontStyles.Bold).GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        var volMaster = CreateSlider("VolMaster", rowMaster.transform, new Vector2(120, 0), new Vector2(400, 20));

        // Music Volume
        var rowMusic = CreatePanel("RowMusic", contentBox.transform, new Vector2(0, 10), new Vector2(700, 70), new Color(0.1f, 0.15f, 0.2f, 0.5f));
        CreateTMPText("LblMusic", "Music Volume", rowMusic.transform, new Vector2(-150, 0), 24, Color.white, FontStyles.Bold).GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        var volMusic = CreateSlider("VolMusic", rowMusic.transform, new Vector2(120, 0), new Vector2(400, 20));

        // SFX Volume
        var rowSFX = CreatePanel("RowSFX", contentBox.transform, new Vector2(0, -80), new Vector2(700, 70), new Color(0.1f, 0.15f, 0.2f, 0.5f));
        CreateTMPText("LblSFX", "SFX Volume", rowSFX.transform, new Vector2(-150, 0), 24, Color.white, FontStyles.Bold).GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        var volSFX = CreateSlider("VolSFX", rowSFX.transform, new Vector2(120, 0), new Vector2(400, 20));

        // Logout
        var btnLogout = CreateTMPButton("Btn_Logout", "Logout", setOuter.transform, new Vector2(0, -210), new Vector2(200, 50), new Color(0.8f, 0.3f, 0.3f), Color.white, 20);

        // =============================================
        //  WAITING ROOM PANEL
        // =============================================
        GameObject waitPanel = new GameObject("WaitingRoomPanel");
        waitPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform waitRT = waitPanel.AddComponent<RectTransform>();
        waitRT.anchorMin = Vector2.zero; waitRT.anchorMax = Vector2.one;
        waitRT.offsetMin = Vector2.zero; waitRT.offsetMax = Vector2.zero;
        waitPanel.SetActive(false);

        // Background
        CreatePanel("WaitBG", waitPanel.transform, Vector2.zero, new Vector2(1920, 1080), new Color(0.02f, 0.05f, 0.1f, 0.95f));

        // Room Number Header (top center)
        var waitHeader = CreatePanel("WaitHeader", waitPanel.transform, Vector2.zero, new Vector2(350, 70), new Color(0.05f, 0.12f, 0.2f, 1f));
        SetAnchor(waitHeader, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        waitHeader.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -50);
        var waitHeaderOutline = waitHeader.AddComponent<UnityEngine.UI.Outline>();
        waitHeaderOutline.effectColor = new Color(0.2f, 0.8f, 0.8f, 1f);
        waitHeaderOutline.effectDistance = new Vector2(2, -2);
        var waitRoomNumObj = CreateTMPText("WaitRoomNumber", "Room: 653478", waitHeader.transform, Vector2.zero, 26, new Color(0.4f, 0.9f, 0.9f), FontStyles.Bold);

        // === PLAYER 1 CARD (Left) ===
        var p1Card = CreatePanel("P1Card", waitPanel.transform, Vector2.zero, new Vector2(400, 420), new Color(0.05f, 0.12f, 0.15f, 0.9f));
        SetAnchor(p1Card, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        p1Card.GetComponent<RectTransform>().anchoredPosition = new Vector2(250, 0);
        var p1Outline = p1Card.AddComponent<UnityEngine.UI.Outline>();
        p1Outline.effectColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        p1Outline.effectDistance = new Vector2(3, -3);

        var waitP1Name = CreateTMPText("WaitP1Name", "Player 1", p1Card.transform, new Vector2(0, 180), 26, Color.white, FontStyles.Bold);
        var waitP1Ready = CreateTMPText("WaitP1Ready", "NOT READY", p1Card.transform, new Vector2(0, 150), 16, new Color(0.8f, 0.4f, 0.2f), FontStyles.Normal);

        var waitP1ShipImg = CreatePanel("WaitP1ShipImg", p1Card.transform, new Vector2(0, 40), new Vector2(180, 130), new Color(1, 1, 1, 0.2f));

        var waitP1ShipName = CreateTMPText("WaitP1ShipName", "Nebula Ghost", p1Card.transform, new Vector2(0, -50), 22, new Color(0.4f, 0.9f, 0.9f), FontStyles.Bold);
        var waitP1Stats = CreateTMPText("WaitP1Stats", "70 HP  |  ATK 0.8  |  SPD 7.5", p1Card.transform, new Vector2(0, -85), 14, Color.white, FontStyles.Normal);
        var waitP1Skill = CreateTMPText("WaitP1Skill", "STUN", p1Card.transform, new Vector2(0, -120), 18, new Color(0.3f, 0.8f, 0.3f), FontStyles.Bold);

        // === PLAYER 2 CARD (Right) ===
        var p2Card = CreatePanel("P2Card", waitPanel.transform, Vector2.zero, new Vector2(400, 420), new Color(0.05f, 0.1f, 0.15f, 0.9f));
        SetAnchor(p2Card, new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        p2Card.GetComponent<RectTransform>().anchoredPosition = new Vector2(-250, 0);
        var p2Outline = p2Card.AddComponent<UnityEngine.UI.Outline>();
        p2Outline.effectColor = new Color(0.8f, 0.3f, 0.3f, 1f);
        p2Outline.effectDistance = new Vector2(3, -3);

        var waitP2Name = CreateTMPText("WaitP2Name", "Waiting...", p2Card.transform, new Vector2(0, 180), 26, Color.white, FontStyles.Bold);
        var waitP2Ready = CreateTMPText("WaitP2Ready", "", p2Card.transform, new Vector2(0, 150), 16, new Color(0.8f, 0.4f, 0.2f), FontStyles.Normal);

        var waitP2ShipImg = CreatePanel("WaitP2ShipImg", p2Card.transform, new Vector2(0, 40), new Vector2(180, 130), new Color(1, 1, 1, 0.2f));

        var waitP2ShipName = CreateTMPText("WaitP2ShipName", "", p2Card.transform, new Vector2(0, -50), 22, new Color(0.4f, 0.9f, 0.9f), FontStyles.Bold);
        var waitP2Stats = CreateTMPText("WaitP2Stats", "", p2Card.transform, new Vector2(0, -85), 14, Color.white, FontStyles.Normal);
        var waitP2Skill = CreateTMPText("WaitP2Skill", "", p2Card.transform, new Vector2(0, -120), 18, new Color(0.8f, 0.3f, 0.3f), FontStyles.Bold);

        // Center: Map Preview
        var waitMapArea = CreatePanel("WaitMapArea", waitPanel.transform, new Vector2(0, -20), new Vector2(200, 150), new Color(0.1f, 0.15f, 0.25f, 1f));
        var waitMapOutline = waitMapArea.AddComponent<UnityEngine.UI.Outline>();
        waitMapOutline.effectColor = new Color(0.2f, 0.8f, 0.8f, 0.5f);
        waitMapOutline.effectDistance = new Vector2(2, -2);
        CreateTMPText("WaitMapLabel", "Obelisk Plains\nof Prism", waitMapArea.transform, Vector2.zero, 14, Color.white, FontStyles.Normal);

        // VS Text
        CreateTMPText("VSText", "VS", waitPanel.transform, new Vector2(0, 130), 48, new Color(0.9f, 0.8f, 0.2f), FontStyles.Bold);

        // Bottom Buttons
        var btnWaitCancel = CreateTMPButton("Btn_WaitCancel", "Leave Room", waitPanel.transform,
            new Vector2(-150, 0), new Vector2(220, 60), new Color(0.7f, 0.25f, 0.25f), Color.white, 22);
        SetAnchor(btnWaitCancel, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        btnWaitCancel.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, 60);

        var btnWaitReady = CreateTMPButton("Btn_WaitReady", "Ready", waitPanel.transform,
            new Vector2(80, 0), new Vector2(220, 60), new Color(0.2f, 0.7f, 0.3f), Color.white, 22);
        SetAnchor(btnWaitReady, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        btnWaitReady.GetComponent<RectTransform>().anchoredPosition = new Vector2(80, 60);

        var btnWaitStart = CreateTMPButton("Btn_WaitStart", "Start Game", waitPanel.transform,
            new Vector2(300, 0), new Vector2(220, 60), new Color(0.1f, 0.5f, 0.8f), Color.white, 22);
        SetAnchor(btnWaitStart, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        btnWaitStart.GetComponent<RectTransform>().anchoredPosition = new Vector2(300, 60);
        btnWaitStart.SetActive(false); // เฉพาะ Master Client เท่านั้น

        // =============================================
        //  TUTORIAL PANEL
        // =============================================
        GameObject tutorialPanel = new GameObject("TutorialPanel");
        tutorialPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform tutRT = tutorialPanel.AddComponent<RectTransform>();
        tutRT.anchorMin = Vector2.zero; tutRT.anchorMax = Vector2.one;
        tutRT.offsetMin = Vector2.zero; tutRT.offsetMax = Vector2.zero;
        tutorialPanel.SetActive(false);

        CreatePanel("TutorialBG", tutorialPanel.transform, Vector2.zero, new Vector2(1920, 1080), new Color(0f, 0f, 0f, 0.85f));
        var tutContent = CreatePanel("Content", tutorialPanel.transform, Vector2.zero, new Vector2(1000, 700), new Color(0.1f, 0.15f, 0.25f, 1f));
        var tutOutline = tutContent.AddComponent<UnityEngine.UI.Outline>();
        tutOutline.effectColor = new Color(0.4f, 0.8f, 1f, 1f);
        tutOutline.effectDistance = new Vector2(2, -2);

        CreateTMPText("TutHeader", "HOW TO PLAY", tutContent.transform, new Vector2(0, 300), 48, new Color(0.4f, 0.8f, 1f), FontStyles.Bold);

        string tutorialString = "<color=#FFFF00>■ Controls (การควบคุม)</color>\n" +
            "Left Joystick : Move (เคลื่อนที่)\n" +
            "Right Button : Fire Laser & Use Skills (ยิงและใช้สกิล)\n\n" +
            "<color=#FFFF00>■ Skills (สกิลประจำยาน)</color>\n" +
            "<color=#00FF00>STUN:</color> ปล่อยคลื่นไฟฟ้าทำให้ศัตรูขยับไม่ได้ชั่วขณะ\n" +
            "<color=#FFAA00>SHIELD:</color> กางบาเรียอมตะป้องกันดาเมจทุกชนิด\n" +
            "<color=#FF5555>NOVA:</color> ระเบิดทำความเสียหายมหาศาลรอบทิศทาง\n" +
            "<color=#55AAFF>SEEKER:</color> ปล่อยจรวดติดตามเป้าหมายอัตโนมัติ\n\n" +
            "<color=#FFFF00>■ Map Hazards (อุปสรรคประจำแผนที่)</color>\n" +
            "🌋 <color=#FF8844>Abandoned Mech</color> : ระวังอุกกาบาตตกใส่\n" +
            "⬛ <color=#AAAAAA>Obelisk Plains</color> : หลบหลังเสาหินเพื่อกำบัง\n" +
            "⚡ <color=#44FFFF>Electric Jellyfish</color> : เข้าแกนพลังงานเพื่อบัฟยิงรัวสุดขีด (แลกกับเสียเลือด)";

        var tutDesc = CreateTMPText("TutDesc", tutorialString, tutContent.transform, new Vector2(0, -30), 24, Color.white, FontStyles.Normal);
        tutDesc.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 500);
        tutDesc.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.TopLeft;

        var btnCloseTut = CreateTMPButton("Btn_CloseTutorial", "X", tutContent.transform,
            Vector2.zero, new Vector2(60, 60), new Color(0.8f, 0.2f, 0.2f), Color.white, 32);
        SetAnchor(btnCloseTut, new Vector2(1, 1), new Vector2(1, 1));
        btnCloseTut.GetComponent<RectTransform>().anchoredPosition = new Vector2(-40, -40);


        // =============================================
        //  WIRE UP UI REFERENCES
        // =============================================
        lm.mainPanel = mainPanel;
        lm.inventoryPanel = invPanel;
        lm.roomPanel = rmPanel;
        lm.settingsPanel = setPanel;
        lm.statusText = statusObj.GetComponent<TMP_Text>();
        lm.playerNameText = playerNameObj.GetComponent<TMP_Text>();
        lm.coinText = coinObj.GetComponent<TMP_Text>();
        lm.winsText = winsObj.GetComponent<TMP_Text>();
        lm.playersOnlineText = playersOnlineObj.GetComponent<TMP_Text>();
        lm.shipNameText = shipNameObj.GetComponent<TMP_Text>();
        lm.shipHPText = shipHPObj.GetComponent<TMP_Text>();
        lm.shipATKText = shipATKObj.GetComponent<TMP_Text>();
        lm.shipSPDText = shipSPDObj.GetComponent<TMP_Text>();
        lm.shipSkillText = shipSkillObj.GetComponent<TMP_Text>();
        lm.shipImage = shipImgArea.GetComponent<Image>();
        lm.playButton = btnPlay.GetComponent<Button>();
        lm.createRoomButton = btnCreateRoom.GetComponent<Button>();
        lm.inventoryButton = btnInventory.GetComponent<Button>();
        lm.settingsButton = btnSettings.GetComponent<Button>();
        lm.roomNumberText = roomNumObj.GetComponent<TMP_Text>();
        lm.roomModeText = roomModeObj.GetComponent<TMP_Text>();
        lm.createRoomConfirmButton = btnCreateConfirm.GetComponent<Button>();
        lm.roomSearchInput = searchInput.GetComponent<UnityEngine.UI.InputField>();
        lm.searchRoomButton = btnSearch.GetComponent<Button>();
        lm.backFromInventoryButton = btnBackInv.GetComponent<Button>();
        lm.backFromRoomButton = btnBackRoom.GetComponent<Button>();
        
        lm.roomListContent = contentPanel.transform;
        lm.roomItemPrefab = roomItem;
        lm.inventoryShipName = invShipNameObj.GetComponent<TMP_Text>();
        lm.inventoryShipHP = invShipHPObj.GetComponent<TMP_Text>();
        lm.inventoryShipATK = invShipATKObj.GetComponent<TMP_Text>();
        lm.inventoryShipSPD = invShipSPDObj.GetComponent<TMP_Text>();
        lm.inventoryShipSkill = invShipSkillObj.GetComponent<TMP_Text>();
        lm.inventoryShipImage = invShipImgArea.GetComponent<Image>();
        lm.inventoryActionButton = btnInventoryAction.GetComponent<Button>();
        lm.skillDescText = skillDescText.GetComponent<TMP_Text>();
        lm.installSkillButton = btnInstallSkill.GetComponent<Button>();
        lm.logoutButton = btnLogout.GetComponent<Button>();
        lm.closeSettingsButton = btnCloseSet.GetComponent<Button>();
        lm.volumeSlider = volMaster.GetComponent<UnityEngine.UI.Slider>();
        lm.musicSlider = volMusic.GetComponent<UnityEngine.UI.Slider>();
        lm.sfxSlider = volSFX.GetComponent<UnityEngine.UI.Slider>();

        // Waiting Room references
        lm.waitingRoomPanel = waitPanel;
        lm.waitRoomNumberText = waitRoomNumObj.GetComponent<TMP_Text>();
        lm.waitP1NameText = waitP1Name.GetComponent<TMP_Text>();
        lm.waitP1ShipNameText = waitP1ShipName.GetComponent<TMP_Text>();
        lm.waitP1StatsText = waitP1Stats.GetComponent<TMP_Text>();
        lm.waitP1SkillText = waitP1Skill.GetComponent<TMP_Text>();
        lm.waitP1ShipImage = waitP1ShipImg.GetComponent<Image>();
        lm.waitP1ReadyText = waitP1Ready.GetComponent<TMP_Text>();
        lm.waitP2NameText = waitP2Name.GetComponent<TMP_Text>();
        lm.waitP2ShipNameText = waitP2ShipName.GetComponent<TMP_Text>();
        lm.waitP2StatsText = waitP2Stats.GetComponent<TMP_Text>();
        lm.waitP2SkillText = waitP2Skill.GetComponent<TMP_Text>();
        lm.waitP2ShipImage = waitP2ShipImg.GetComponent<Image>();
        lm.waitP2ReadyText = waitP2Ready.GetComponent<TMP_Text>();
        lm.waitReadyButton = btnWaitReady.GetComponent<Button>();
        lm.waitCancelButton = btnWaitCancel.GetComponent<Button>();
        lm.waitStartButton = btnWaitStart.GetComponent<Button>();

        lm.tutorialPanel = tutorialPanel;

        // Wire up button events
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnPlay.GetComponent<Button>().onClick, lm.OnPlayButtonClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnCreateRoom.GetComponent<Button>().onClick, lm.OnCreateRoomClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnInventory.GetComponent<Button>().onClick, lm.ShowInventoryPanel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnSettings.GetComponent<Button>().onClick, lm.OnSettingsClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnCreateConfirm.GetComponent<Button>().onClick, lm.OnCreateRoomConfirm);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnSearch.GetComponent<Button>().onClick, lm.OnSearchRoom);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnBackInv.GetComponent<Button>().onClick, lm.ShowMainPanel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnBackRoom.GetComponent<Button>().onClick, lm.ShowMainPanel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnInventoryAction.GetComponent<Button>().onClick, lm.OnInventoryActionClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnLogout.GetComponent<Button>().onClick, lm.OnLogoutButtonClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnCloseSet.GetComponent<Button>().onClick, lm.OnCloseSettingsClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnTutorial.GetComponent<Button>().onClick, lm.OnTutorialClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnCloseTut.GetComponent<Button>().onClick, lm.OnCloseTutorialClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnWaitReady.GetComponent<Button>().onClick, lm.OnReadyButtonClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnWaitCancel.GetComponent<Button>().onClick, lm.OnLeaveWaitingRoom);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnWaitStart.GetComponent<Button>().onClick, lm.OnStartGameClicked);

        // Ship selection
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(btnShip0.GetComponent<Button>().onClick, lm.SelectShip, 0);
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(btnShip1.GetComponent<Button>().onClick, lm.SelectShip, 1);
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(btnShip2.GetComponent<Button>().onClick, lm.SelectShip, 2);
        
        // Skill selection
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(btnSkillStun.GetComponent<Button>().onClick, lm.SelectSkill, 0);
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(btnSkillShield.GetComponent<Button>().onClick, lm.SelectSkill, 1);
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(btnSkillNova.GetComponent<Button>().onClick, lm.SelectSkill, 2);
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(btnSkillSeeker.GetComponent<Button>().onClick, lm.SelectSkill, 3);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnInstallSkill.GetComponent<Button>().onClick, lm.OnInstallSkillClicked);
        
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnPrevMap.GetComponent<Button>().onClick, lm.PrevMap);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnNextMap.GetComponent<Button>().onClick, lm.NextMap);
        lm.createRoomMapNameText = mapNameText.GetComponent<TMP_Text>();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LobbyScene.unity");
    }


    // ============================
    //  SAMPLE SCENE (GAMEPLAY)
    // ============================
    private static void BuildSampleScene()
    {
        string scenePath = "Assets/Scenes/SampleScene.unity";
        GameObject existingMap2Visuals = null;
        if (System.IO.File.Exists(scenePath))
        {
            Scene existingScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject found = GameObject.Find("Map2_Visuals");
            if (found != null)
            {
                // Save it to a temporary prefab to preserve user's layout
                string tempPath = "Assets/TempMap2Visuals.prefab";
                PrefabUtility.SaveAsPrefabAsset(found, tempPath);
                existingMap2Visuals = AssetDatabase.LoadAssetAtPath<GameObject>(tempPath);
            }
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera(new Color(0.05f, 0.05f, 0.15f));
        CreatePostProcessingVolume();

        // === GameplayManager (ตัวเดียว) ===
        GameObject gameplayManagerObj = new GameObject("GameplayManager");
        GameplayManager gm = gameplayManagerObj.AddComponent<GameplayManager>();
        gameplayManagerObj.AddComponent<MapHazardManager>(); // Attach Hazard Manager

        // === Canvas & EventSystem ===
        GameObject canvasObj = CreateCanvas();
        CreateEventSystem();

        // === Background Map ===
        GameObject bgObj = new GameObject("BackgroundMap");
        SpriteRenderer sr = bgObj.AddComponent<SpriteRenderer>();
        // Default sprite for Map 2
        sr.sprite = Resources.Load<Sprite>("Images/Map_AncientMech");
        bgObj.transform.localScale = new Vector3(15f, 15f, 1f); // Adjust scale to match GameplayManager
        bgObj.transform.position = new Vector3(0, 0, 10f); // Push behind everything
        sr.sortingOrder = -10; // Ensure it is behind everything

        // === Starfield Effect ===
        GameObject starfieldObj = new GameObject("Starfield");
        starfieldObj.transform.position = new Vector3(0, 12f, 9f);
        starfieldObj.transform.rotation = Quaternion.Euler(90f, 0, 0); // ยิงดาวลงข้างล่าง
        ParticleSystem starPs = starfieldObj.AddComponent<ParticleSystem>();
        
        var starMain = starPs.main;
        starMain.duration = 10f;
        starMain.loop = true;
        starMain.prewarm = true; // เล่นล่วงหน้า เพื่อให้มีดาวเต็มจอตั้งแต่เริ่ม
        starMain.startLifetime = 8f;
        starMain.startSpeed = 3f;
        starMain.startSize = 0.05f;
        starMain.startColor = new Color(1f, 1f, 1f, 0.7f); // ดาวสีขาวแบบโปร่งแสง
        starMain.simulationSpace = ParticleSystemSimulationSpace.World;
        starMain.maxParticles = 500;
        
        var starEmission = starPs.emission;
        starEmission.rateOverTime = 30f;
        
        var starShape = starPs.shape;
        starShape.shapeType = ParticleSystemShapeType.Box;
        starShape.scale = new Vector3(20f, 1f, 1f); // กว้างครอบคลุมจอ
        
        var starRenderer = starPs.GetComponent<ParticleSystemRenderer>();
        starRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // === Build Fixed Visuals for Map 2 in Scene ===
        if (existingMap2Visuals != null)
        {
            GameObject restored = (GameObject)PrefabUtility.InstantiatePrefab(existingMap2Visuals);
            restored.name = "Map2_Visuals";
            PrefabUtility.UnpackPrefabInstance(restored, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            AssetDatabase.DeleteAsset("Assets/TempMap2Visuals.prefab");
        }
        else
        {
            BuildMap2Visuals();
        }

        // 1. --- BOTTOM CENTER HUD (Player 1) ---
        var p1HUD = CreatePanel("Player1HUD", canvasObj.transform, Vector2.zero, new Vector2(350, 80), new Color(0, 0, 0, 0));
        SetAnchor(p1HUD, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        p1HUD.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 60); // Bottom Center

        var p1Name = CreateTMPText("Name", "Player 1", p1HUD.transform, new Vector2(20, 25), 20, new Color(1f, 0.4f, 0.4f), FontStyles.Bold);
        p1Name.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

        var p1Heart = CreatePanel("Heart", p1HUD.transform, new Vector2(-130, -5), new Vector2(50, 50), Color.white);
        SetImageSprite(p1Heart, "Images/icon_heart"); // Assuming icon_heart exists or it will be white circle
        var p1HPBG = CreatePanel("HP_BG", p1HUD.transform, new Vector2(20, -5), new Vector2(200, 20), new Color(0.2f, 0.05f, 0.05f));
        var p1HPFill = CreatePanel("HP_Fill", p1HPBG.transform, Vector2.zero, new Vector2(200, 20), new Color(0.8f, 0.1f, 0.1f));
        var p1HPText = CreateTMPText("HP_Text", "100 / 100", p1HPBG.transform, Vector2.zero, 14, Color.white, FontStyles.Bold);
        var hpOutline1 = p1HPBG.AddComponent<UnityEngine.UI.Outline>();
        hpOutline1.effectColor = Color.black;

        // 2. --- TOP RIGHT HUD (Player 2) ---
        var p2HUD = CreatePanel("Player2HUD", canvasObj.transform, Vector2.zero, new Vector2(350, 80), new Color(0, 0, 0, 0));
        SetAnchor(p2HUD, new Vector2(1, 1), new Vector2(1, 1));
        p2HUD.GetComponent<RectTransform>().anchoredPosition = new Vector2(-200, -60);
        
        var p2Heart = CreatePanel("Heart", p2HUD.transform, new Vector2(-130, 5), new Vector2(50, 50), Color.white);
        SetImageSprite(p2Heart, "Images/icon_heart");
        var p2HPBG = CreatePanel("HP_BG", p2HUD.transform, new Vector2(20, 5), new Vector2(200, 20), new Color(0.2f, 0.05f, 0.05f));
        var p2HPFill = CreatePanel("HP_Fill", p2HPBG.transform, Vector2.zero, new Vector2(200, 20), new Color(0.8f, 0.1f, 0.1f));
        var p2HPText = CreateTMPText("HP_Text", "100 / 100", p2HPBG.transform, Vector2.zero, 14, Color.white, FontStyles.Bold);
        var hpOutline2 = p2HPBG.AddComponent<UnityEngine.UI.Outline>();
        hpOutline2.effectColor = Color.black;

        var p2Name = CreateTMPText("Name", "Player 2", p2HUD.transform, new Vector2(20, -25), 20, new Color(0.6f, 0.4f, 1f), FontStyles.Bold);
        p2Name.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

        // 3. --- BOTTOM LEFT (Joystick) ---
        var joystickBase = CreatePanel("JoystickBase", canvasObj.transform, Vector2.zero, new Vector2(250, 250), new Color(1f, 1f, 1f, 0.3f));
        SetAnchor(joystickBase, new Vector2(0, 0), new Vector2(0, 0));
        joystickBase.GetComponent<RectTransform>().anchoredPosition = new Vector2(160, 180);
        SetImageSprite(joystickBase, "Images/icon_move");
        joystickBase.GetComponent<Image>().preserveAspect = true;
        CreateTMPText("MoveText", "MOVE", joystickBase.transform, new Vector2(0, -140), 20, new Color(0.3f, 0.8f, 1f), FontStyles.Bold);

        // Handle จอยสติ๊ก
        var joystickHandle = CreatePanel("JoystickHandle", joystickBase.transform, Vector2.zero, new Vector2(60, 60), new Color(1f, 1f, 1f, 0.15f));
        UIJoystick uiJoystick = joystickBase.AddComponent<UIJoystick>();

        // 4. --- BOTTOM RIGHT (Fire Button) ---
        var fireBtnObj = CreatePanel("FireButton", canvasObj.transform, Vector2.zero, new Vector2(160, 160), Color.white);
        SetAnchor(fireBtnObj, new Vector2(1, 0), new Vector2(1, 0));
        fireBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, 120);
        SetImageSprite(fireBtnObj, "Images/icon_fire");
        fireBtnObj.GetComponent<Image>().preserveAspect = true;
        CreateTMPText("FireText", "FIRE", fireBtnObj.transform, new Vector2(0, -90), 18, new Color(1f, 0.5f, 0.2f), FontStyles.Bold);
        UIButton fireBtn = fireBtnObj.AddComponent<UIButton>();
        fireBtn.buttonName = "Fire";

        // 5. --- Skill Button (Top Right of Fire) ---
        var skillBtnObj = CreatePanel("SkillButton", canvasObj.transform, Vector2.zero, new Vector2(110, 110), Color.white);
        SetAnchor(skillBtnObj, new Vector2(1, 0), new Vector2(1, 0));
        skillBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-80, 280);
        skillBtnObj.GetComponent<Image>().preserveAspect = true;
        SetImageSprite(skillBtnObj, "Images/icon_stun");
        CreateTMPText("SkillText", "SKILL", skillBtnObj.transform, new Vector2(0, -70), 16, new Color(0.4f, 1f, 0.4f), FontStyles.Bold);
        UIButton skillBtn = skillBtnObj.AddComponent<UIButton>();
        skillBtn.buttonName = "Skill";
        
        // Cooldown Overlay (วงกลมทับปุ่มสกิลเวลาคูลดาวน์)
        var cooldownOverlay = CreatePanel("CooldownOverlay", skillBtnObj.transform, Vector2.zero, new Vector2(110, 110), new Color(0, 0, 0, 0.6f));
        var cdImg = cooldownOverlay.GetComponent<Image>();
        cdImg.type = Image.Type.Filled;
        cdImg.fillMethod = Image.FillMethod.Radial360;
        cdImg.fillAmount = 0f;

        // === ผูก GameplayManager ===
        gm.joystick = uiJoystick;
        gm.fireButton = fireBtn;
        gm.skillButton = skillBtn;
        gm.skillCooldownImage = cdImg;
        gm.skillIconImage = skillBtnObj.GetComponent<Image>();

        // 6. --- TOP CENTER (Ping & Exit) ---
        var topCenter = CreatePanel("TopCenter", canvasObj.transform, Vector2.zero, new Vector2(300, 60), new Color(0, 0, 0, 0));
        SetAnchor(topCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        topCenter.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -40);
        
        var pingObj = CreateTMPText("PingText", "Ping: -- ms", topCenter.transform, new Vector2(0, 20), 18, Color.green, FontStyles.Normal);
        
        var btnExit = CreateTMPButton("Btn_Exit", "Exit", topCenter.transform, new Vector2(0, -20), new Vector2(100, 40), new Color(0.8f, 0.2f, 0.2f), Color.white, 16);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnExit.GetComponent<Button>().onClick, gm.LeaveRoom);

        gm.pingText = pingObj.GetComponent<TMP_Text>();
        gm.playerInfoText = p1Name.GetComponent<TMP_Text>();

        // 6.5 --- Low HP Warning (Red Vignette) ---
        var lowHPPanel = CreatePanel("LowHPWarning", canvasObj.transform, Vector2.zero, new Vector2(1920, 1080), new Color(1f, 0f, 0f, 0.3f));
        SetAnchor(lowHPPanel, Vector2.zero, Vector2.one);
        lowHPPanel.SetActive(false);
        var lowHPCg = lowHPPanel.AddComponent<CanvasGroup>();
        lowHPCg.blocksRaycasts = false; // ทะลุคลิกได้
        lowHPCg.interactable = false;
        gm.lowHPWarning = lowHPPanel;

        // 7. --- RESULT PANEL (Hidden by Default) ---
        var resultPanel = CreatePanel("ResultPanel", canvasObj.transform, Vector2.zero, new Vector2(1920, 1080), new Color(0, 0, 0, 0.8f));
        SetAnchor(resultPanel, Vector2.zero, Vector2.one);
        resultPanel.SetActive(false); // ซ่อนไว้ก่อน
        
        // Header (Room Number)
        var resultHeader = CreatePanel("Header", resultPanel.transform, new Vector2(0, 480), new Vector2(400, 80), new Color(0.05f, 0.1f, 0.3f, 0.9f));
        var resultRoomText = CreateTMPText("RoomText", "เลขห้อง\n--", resultHeader.transform, Vector2.zero, 24, Color.white, FontStyles.Bold);
        
        // Return to Menu Button
        var btnReturnToMenu = CreateTMPButton("BtnReturnMenu", "กลับหน้าเมนูหลัก", resultPanel.transform, new Vector2(0, -450), new Vector2(300, 60), new Color(0.1f, 0.2f, 0.4f), Color.white, 24);
        var returnOutline = btnReturnToMenu.AddComponent<UnityEngine.UI.Outline>();
        returnOutline.effectColor = new Color(0.2f, 0.8f, 0.8f);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnReturnToMenu.GetComponent<Button>().onClick, gm.LeaveRoom);

        // Center Map Image
        var resultMapImg = CreatePanel("MapImage", resultPanel.transform, Vector2.zero, new Vector2(300, 300), Color.white);
        SetImageSprite(resultMapImg, "Images/Obelisk_Plains_of_Prism");
        var mapOutline = resultMapImg.AddComponent<UnityEngine.UI.Outline>();
        mapOutline.effectColor = Color.white;

        // Player 1 Card (Left)
        var p1ResultCard = CreatePanel("P1ResultCard", resultPanel.transform, new Vector2(-450, 0), new Vector2(400, 500), new Color(0.1f, 0.15f, 0.2f, 0.9f));
        var p1ResOutline = p1ResultCard.AddComponent<UnityEngine.UI.Outline>();
        p1ResOutline.effectColor = new Color(0.2f, 0.8f, 0.4f); // Default Green (Win)
        p1ResOutline.effectDistance = new Vector2(3, -3);

        var p1ResAvatar = CreatePanel("Avatar", p1ResultCard.transform, new Vector2(-140, 190), new Vector2(60, 60), Color.gray);
        var p1ResName = CreateTMPText("Name", "Player 1", p1ResultCard.transform, new Vector2(40, 190), 32, Color.white, FontStyles.Bold);
        var p1ResLine = CreatePanel("Line", p1ResultCard.transform, new Vector2(0, 140), new Vector2(350, 5), new Color(0.2f, 0.6f, 1f));
        
        var p1ResShip = CreatePanel("ShipImage", p1ResultCard.transform, new Vector2(0, 0), new Vector2(300, 200), new Color(1, 1, 1, 0)); // Transparent initially
        SetImageSprite(p1ResShip, "Images/ship1"); // Mock image
        p1ResShip.GetComponent<Image>().preserveAspect = true;

        var p1ResStatus = CreateTMPText("StatusText", "ชัยชนะ +", p1ResultCard.transform, new Vector2(-80, -200), 32, new Color(1f, 0.8f, 0.2f), FontStyles.Bold);
        var p1ResCoinIcon = CreatePanel("CoinIcon", p1ResultCard.transform, new Vector2(30, -200), new Vector2(40, 40), Color.white);
        SetImageSprite(p1ResCoinIcon, "Images/icon_coin");
        var p1ResCoins = CreateTMPText("CoinsText", "190", p1ResultCard.transform, new Vector2(100, -200), 28, Color.white, FontStyles.Bold);

        // Player 2 Card (Right)
        var p2ResultCard = CreatePanel("P2ResultCard", resultPanel.transform, new Vector2(450, 0), new Vector2(400, 500), new Color(0.2f, 0.1f, 0.1f, 0.9f));
        var p2ResOutline = p2ResultCard.AddComponent<UnityEngine.UI.Outline>();
        p2ResOutline.effectColor = new Color(0.8f, 0.2f, 0.2f); // Default Red (Lose)
        p2ResOutline.effectDistance = new Vector2(3, -3);

        var p2ResAvatar = CreatePanel("Avatar", p2ResultCard.transform, new Vector2(-140, 190), new Vector2(60, 60), Color.gray);
        var p2ResName = CreateTMPText("Name", "Player 2", p2ResultCard.transform, new Vector2(40, 190), 32, Color.white, FontStyles.Bold);
        var p2ResLine = CreatePanel("Line", p2ResultCard.transform, new Vector2(0, 140), new Vector2(350, 5), new Color(0.2f, 0.6f, 1f));
        
        var p2ResShip = CreatePanel("ShipImage", p2ResultCard.transform, new Vector2(0, 0), new Vector2(300, 200), new Color(1, 1, 1, 0));
        SetImageSprite(p2ResShip, "Images/ship2"); // Mock image
        p2ResShip.GetComponent<Image>().preserveAspect = true;

        var p2ResStatus = CreateTMPText("StatusText", "พ่ายแพ้ +", p2ResultCard.transform, new Vector2(-80, -200), 32, new Color(1f, 0.4f, 0.4f), FontStyles.Bold);
        var p2ResCoinIcon = CreatePanel("CoinIcon", p2ResultCard.transform, new Vector2(30, -200), new Vector2(40, 40), Color.white);
        SetImageSprite(p2ResCoinIcon, "Images/icon_coin");
        var p2ResCoins = CreateTMPText("CoinsText", "10", p2ResultCard.transform, new Vector2(100, -200), 28, Color.white, FontStyles.Bold);

        // Map GameplayManager Result UI
        gm.resultPanel = resultPanel;
        gm.resultRoomNumber = resultRoomText.GetComponent<TMP_Text>();
        gm.btnReturnToMenu = btnReturnToMenu.GetComponent<Button>();
        
        gm.localResultOutline = p1ResOutline;
        gm.localResultName = p1ResName.GetComponent<TMP_Text>();
        gm.localResultShip = p1ResShip.GetComponent<Image>();
        gm.localResultStatus = p1ResStatus.GetComponent<TMP_Text>();
        gm.localResultCoins = p1ResCoins.GetComponent<TMP_Text>();

        gm.remoteResultOutline = p2ResOutline;
        gm.remoteResultName = p2ResName.GetComponent<TMP_Text>();
        gm.remoteResultShip = p2ResShip.GetComponent<Image>();
        gm.remoteResultStatus = p2ResStatus.GetComponent<TMP_Text>();
        gm.remoteResultCoins = p2ResCoins.GetComponent<TMP_Text>();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SampleScene.unity");
    }

    private static void BuildMap2Visuals()
    {
        GameObject visualsContainer = new GameObject("Map2_Visuals");
        
        Sprite[] asteroidSprites = Resources.LoadAll<Sprite>("Images/Obs_Asteroids");
        Sprite[] turretSprites = Resources.LoadAll<Sprite>("Images/Obs_Turrets");
        Sprite[] coreSprites = Resources.LoadAll<Sprite>("Images/Obs_RedCores");

        System.Collections.Generic.List<Sprite> validAsteroids = new System.Collections.Generic.List<Sprite>();
        if (asteroidSprites != null)
        {
            foreach (var sp in asteroidSprites)
            {
                if (!sp.name.EndsWith("_9") && !sp.name.EndsWith("_10") && !sp.name.EndsWith("_11"))
                {
                    validAsteroids.Add(sp);
                }
            }
            if (validAsteroids.Count == 0 && asteroidSprites != null) validAsteroids.AddRange(asteroidSprites);
        }

        var obstacles = new System.Collections.Generic.List<(Vector2 pos, Vector2 scale, string type)>();
        obstacles.Add((new Vector2(-18, 18), new Vector2(6, 2.5f), "large"));
        obstacles.Add((new Vector2(-19, 15), new Vector2(2.5f, 6), "large"));
        obstacles.Add((new Vector2(18, -18), new Vector2(6, 2.5f), "large"));
        obstacles.Add((new Vector2(19, -15), new Vector2(2.5f, 6), "large"));
        obstacles.Add((new Vector2(-10, 5), new Vector2(4, 2), "medium"));
        obstacles.Add((new Vector2(10, -5), new Vector2(4, 2), "medium"));
        obstacles.Add((new Vector2(0, 0), new Vector2(2.5f, 2.5f), "small"));

        int id = 1;
        // Seed so generated scene is deterministic every time the setup tool runs
        Random.InitState(12345);

        foreach (var obs in obstacles)
        {
            GameObject group = new GameObject("VisualGroup_" + id);
            group.transform.SetParent(visualsContainer.transform);
            group.transform.position = new Vector3(obs.pos.x, obs.pos.y, 0);
            
            // เพิ่ม Collider เพื่อให้ชนได้จริง และขยับตามเวลาปรับตำแหน่งใน Scene
            BoxCollider2D col = group.AddComponent<BoxCollider2D>();
            col.size = obs.scale;

            if (validAsteroids.Count > 0)
            {
                float area = obs.scale.x * obs.scale.y;
                int count = Mathf.Max(1, Mathf.RoundToInt(area / 3f)); 

                for (int i = 0; i < count; i++)
                {
                    GameObject rock = new GameObject("Asteroid_Vis");
                    rock.transform.SetParent(group.transform);
                    float rx = Random.Range(-obs.scale.x / 2.2f, obs.scale.x / 2.2f);
                    float ry = Random.Range(-obs.scale.y / 2.2f, obs.scale.y / 2.2f);
                    rock.transform.localPosition = new Vector3(rx, ry, 0);
                    
                    SpriteRenderer sr = rock.AddComponent<SpriteRenderer>();
                    sr.sprite = validAsteroids[Random.Range(0, validAsteroids.Count)];
                    sr.sortingOrder = -2;
                    
                    float rScale = Random.Range(0.8f, 1.5f);
                    rock.transform.localScale = new Vector3(rScale, rScale, 1f); 
                    rock.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
                }

                if (obs.type == "large" && turretSprites != null && turretSprites.Length > 0)
                {
                    GameObject turret = new GameObject("Turret_Vis");
                    turret.transform.SetParent(group.transform);
                    turret.transform.localPosition = new Vector3(0, 0, 0); 
                    SpriteRenderer tsr = turret.AddComponent<SpriteRenderer>();
                    tsr.sprite = turretSprites[Random.Range(0, turretSprites.Length)];
                    tsr.sortingOrder = -1;
                    float tScale = Random.Range(0.8f, 1.2f);
                    turret.transform.localScale = new Vector3(tScale, tScale, 1f);
                    turret.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
                }
            }
            id++;
        }

        if (validAsteroids.Count > 0)
        {
            GameObject bgDeco = new GameObject("BackgroundDecorations");
            bgDeco.transform.SetParent(visualsContainer.transform);

            for (int i = 0; i < 40; i++)
            {
                GameObject rock = new GameObject("Asteroid_BG");
                rock.transform.SetParent(bgDeco.transform);
                rock.transform.position = new Vector3(Random.Range(-20f, 20f), Random.Range(-11f, 11f), 0);
                SpriteRenderer sr = rock.AddComponent<SpriteRenderer>();
                sr.sprite = validAsteroids[Random.Range(0, validAsteroids.Count)];
                sr.sortingOrder = -4; 
                sr.color = new Color(0.7f, 0.7f, 0.7f, 1f); 
                float rScale = Random.Range(0.3f, 0.8f);
                rock.transform.localScale = new Vector3(rScale, rScale, 1f);
                rock.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
            }

            if (coreSprites != null && coreSprites.Length > 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    GameObject core = new GameObject("RedCore_BG");
                    core.transform.SetParent(bgDeco.transform);
                    core.transform.position = new Vector3(Random.Range(-18f, 18f), Random.Range(-9f, 9f), 0);
                    SpriteRenderer sr = core.AddComponent<SpriteRenderer>();
                    sr.sprite = coreSprites[Random.Range(0, coreSprites.Length)];
                    sr.sortingOrder = -3; 
                    float rScale = Random.Range(0.8f, 1.2f);
                    core.transform.localScale = new Vector3(rScale, rScale, 1f);
                    core.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
                }
            }
        }
    }

    private static void SetAnchor(GameObject obj, Vector2 min, Vector2 max)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
    }

    // ============================
    //  BUILD SETTINGS
    // ============================
    private static void SetupBuildSettings()
    {
        EditorBuildSettingsScene[] s = new EditorBuildSettingsScene[3];
        s[0] = new EditorBuildSettingsScene("Assets/Scenes/LoginScene.unity", true);
        s[1] = new EditorBuildSettingsScene("Assets/Scenes/LobbyScene.unity", true);
        s[2] = new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true);
        EditorBuildSettings.scenes = s;
    }

    // ============================
    //  UI HELPERS & POST-PROCESSING
    // ============================
    private static void CreatePostProcessingVolume()
    {
        GameObject volumeObj = new GameObject("Global PostProcessing");
        var volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "SciFi_PostProcessingProfile";

        // Add Bloom
        Bloom bloom;
        if (!profile.Has<Bloom>())
        {
            bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.intensity.Override(1.5f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(0.7f, 0.9f, 1f));
        }

        // Add Vignette
        Vignette vignette;
        if (!profile.Has<Vignette>())
        {
            vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.8f);
        }

        // Add Chromatic Aberration for dynamic feel
        ChromaticAberration chroma;
        if (!profile.Has<ChromaticAberration>())
        {
            chroma = profile.Add<ChromaticAberration>();
            chroma.active = true;
            chroma.intensity.Override(0.15f);
        }

        volume.profile = profile;
    }
    private static void CreateCamera(Color bgColor)
    {
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.backgroundColor = bgColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(0, 0, -10f); // ขยับกล้องออกมาให้มองเห็น Sprite
        
        camObj.AddComponent<CameraShake>(); // เพิ่มระบบสั่นหน้าจอ

        // Enable Post Processing
        var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
    }

    private static GameObject CreateCanvas()
    {
        GameObject obj = new GameObject("Canvas");
        Canvas c = obj.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler s = obj.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 1f; // Match Height เสมอเพื่อไม่ให้ปุ่มหลุดกรอบแนวนอน
        obj.AddComponent<GraphicRaycaster>();
        return obj;
    }

    private static void CreateEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject obj = new GameObject("EventSystem");
            obj.AddComponent<EventSystem>();
            obj.AddComponent<StandaloneInputModule>();
        }
    }

    private static void CreateBackground(Transform parent, string resourcePath)
    {
        GameObject obj = new GameObject("Background");
        obj.transform.SetParent(parent, false);
        RawImage img = obj.AddComponent<RawImage>();
        Texture2D tex = Resources.Load<Texture2D>(resourcePath);
        if (tex != null) img.texture = tex;
        img.color = Color.white;
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.color = color;
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return obj;
    }

    private static void SetImageSprite(GameObject obj, string resourcePath)
    {
        Image img = obj.GetComponent<Image>();
        if (img != null)
        {
            Sprite sp = Resources.Load<Sprite>(resourcePath);
            if (sp != null)
            {
                img.sprite = sp;
                img.color = Color.white;
                img.preserveAspect = true;
            }
        }
    }

    private static GameObject CreateTMPText(string name, string content, Transform parent, Vector2 pos, int fontSize, Color color, FontStyles style)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(500, 60);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }

    private static GameObject CreateTMPButton(string name, string text, Transform parent, Vector2 pos, Vector2 size, Color bgColor, Color textColor, int fontSize)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = obj.AddComponent<Image>();
        img.color = bgColor;
        obj.AddComponent<Button>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        RectTransform trt = textObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(5, 2); trt.offsetMax = new Vector2(-5, -2);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;

        return obj;
    }

    private static GameObject CreateLegacyInputField(string name, string placeholder, Transform parent, Vector2 pos)
    {
        GameObject obj = DefaultControls.CreateInputField(new DefaultControls.Resources());
        obj.name = name;
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(300, 40);
        obj.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.25f);
        
        Transform placeholderObj = obj.transform.Find("Placeholder");
        if (placeholderObj != null)
        {
            Text ph = placeholderObj.GetComponent<Text>();
            if (ph != null)
            {
                ph.text = placeholder;
                ph.color = new Color(0.5f, 0.5f, 0.6f);
            }
        }
        
        Transform textObj = obj.transform.Find("Text");
        if (textObj != null)
        {
            Text t = textObj.GetComponent<Text>();
            if (t != null)
            {
                t.color = Color.white;
            }
        }
        return obj;
    }

    private static UnityEngine.UI.Slider CreateSlider(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);
        RectTransform rt = sliderObj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.25f); bgRt.anchorMax = new Vector2(1, 0.75f);
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0, 0.25f); fillAreaRt.anchorMax = new Vector2(1, 0.75f);
        fillAreaRt.offsetMin = Vector2.zero; fillAreaRt.offsetMax = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.4f, 0.8f, 1f, 1f);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRt = handleArea.AddComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero; handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10, 0); handleAreaRt.offsetMax = new Vector2(-10, 0);

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleArea.transform, false);
        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.color = Color.white;
        RectTransform handleRt = handleObj.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0, 0); handleRt.anchorMax = new Vector2(0, 1);
        handleRt.sizeDelta = new Vector2(20, 0);

        UnityEngine.UI.Slider slider = sliderObj.AddComponent<UnityEngine.UI.Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.value = 1f;

        return slider;
    }
}

// Renders an isolated preview scene. It never saves over the user's open scenes.
public static class LobbyPreviewValidation
{
    [InitializeOnLoadMethod]
    private static void ScheduleRequestedValidation()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode
                && System.IO.File.Exists("Library/LobbyValidation.request"))
                ValidateAndRender();
        };
    }

    [MenuItem("Battlefield/Lobby/Validate and Render Preview")]
    public static void ValidateAndRender()
    {
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        string output = "Library/LobbyValidation";
        System.IO.Directory.CreateDirectory(output);
        try
        {
            LobbyManager manager = null;
            foreach (GameObject root in preview.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                var found = root.GetComponentInChildren<LobbyManager>(true);
                if (found != null) manager = found;
            }
            if (manager == null) throw new System.Exception("LobbyManager is missing from LobbyScene.");
            var ready = new ExitGames.Client.Photon.Hashtable {
                ["IsReady"] = true, ["LoadoutLoaded"] = true, ["ReadyMap"] = 2, ["ReadyRevision"] = 3 };
            if (!LobbyManager.ReadyPropertiesMatch(ready, 2, 3)) throw new System.Exception("Valid readiness was rejected.");
            if (LobbyManager.ReadyPropertiesMatch(ready, 1, 3)) throw new System.Exception("Readiness survived a map change.");
            if (LobbyManager.ReadyPropertiesMatch(ready, 2, 5)) throw new System.Exception("Readiness survived switching away and back.");
            ready["LoadoutLoaded"] = false;
            if (LobbyManager.ReadyPropertiesMatch(ready, 2, 3)) throw new System.Exception("Unloaded profile was accepted.");
            ready["LoadoutLoaded"] = true;
            ready["IsReady"] = false;
            if (LobbyManager.ReadyPropertiesMatch(ready, 2, 3)) throw new System.Exception("Cancelled readiness was accepted.");
            ready["IsReady"] = "true";
            if (LobbyManager.ReadyPropertiesMatch(ready, 2, 3)) throw new System.Exception("Malformed readiness was accepted.");
            if (LobbyManager.ReadyPropertiesMatch(null, 0, 0)) throw new System.Exception("Missing readiness was accepted.");
            var build = typeof(LobbyManager).GetMethod("BuildLobbyUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            build.Invoke(manager, null);
            if (manager.waitReadyButton == null || manager.waitStartButton == null
                || manager.waitCancelButton == null || manager.roomSearchInput == null)
                throw new System.Exception("Required lobby controls are missing.");
            foreach (var path in new[] { "Images/Map_ThunderJellyfish", "Images/Map_ObeliskPlains", "Images/Map_AncientMech",
                "Images/ship1", "Images/ship2", "Images/ship3" })
                if (Resources.Load<Sprite>(path) == null) throw new System.Exception("Missing sprite: " + path);
            manager.mainPanel.SetActive(false);
            manager.inventoryPanel.SetActive(false);
            manager.settingsPanel.SetActive(false);
            if (manager.tutorialPanel != null) manager.tutorialPanel.SetActive(false);
            manager.waitRoomNumberText.text = "ROOM  653478";
            manager.waitP1NameText.text = "[HOST] Pilot One (YOU)";
            manager.waitP1ShipNameText.text = "Nebula Ghost";
            manager.waitP1StatsText.text = "70 HP  /  ATK 0.8  /  SPD 7.5";
            manager.waitP1SkillText.text = "EQUIPPED SKILL / STUN";
            manager.waitP1ReadyText.text = "READY";
            manager.waitP1ShipImage.sprite = Resources.Load<Sprite>("Images/ship1");
            manager.waitP1ShipImage.color = Color.white;
            manager.waitP1ShipImage.preserveAspect = true;
            manager.waitP2NameText.text = "Pilot Two";
            manager.waitP2ShipNameText.text = "Stellar Striker";
            manager.waitP2StatsText.text = "55 HP  /  ATK 1.5  /  SPD 9";
            manager.waitP2SkillText.text = "EQUIPPED SKILL / NOVA";
            manager.waitP2ReadyText.text = "NOT READY";
            manager.waitP2ShipImage.sprite = Resources.Load<Sprite>("Images/ship3");
            manager.waitP2ShipImage.color = Color.white;
            manager.waitP2ShipImage.preserveAspect = true;
            var cameraObject = new GameObject("LobbyPreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera renderCamera = cameraObject.GetComponent<Camera>();
            renderCamera.scene = preview;
            renderCamera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            renderCamera.enabled = false;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(0.01f, 0.02f, 0.04f);
            renderCamera.orthographic = true;
            renderCamera.orthographicSize = 360;
            var canvas = manager.waitingRoomPanel.GetComponentInParent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = renderCamera;
            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.position = new Vector3(0, 0, 10);
            canvasRect.rotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
            string report = "Readiness regression checks: 7 PASS\nLobby references and sprites: PASS\n";
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                new Vector2Int(2340, 1080), new Vector2Int(1024, 768) })
            {
                foreach (bool waiting in new[] { true, false })
                {
                    manager.waitingRoomPanel.SetActive(waiting);
                    manager.roomPanel.SetActive(!waiting);
                    var target = new RenderTexture(size.x, size.y, 24);
                    var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    var previous = RenderTexture.active;
                    try
                    {
                        renderCamera.targetTexture = target;
                        renderCamera.orthographicSize = size.y / 2f;
                        renderCamera.aspect = (float)size.x / size.y;
                        canvasRect.sizeDelta = size;
                        target.Create();
                        Canvas.ForceUpdateCanvases();
                        typeof(LobbyManager).GetMethod("FitLobbyUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(manager, null);
                        Canvas.ForceUpdateCanvases();
                        GameObject visiblePanel = waiting ? manager.waitingRoomPanel : manager.roomPanel;
                        foreach (TMP_Text text in visiblePanel.GetComponentsInChildren<TMP_Text>())
                        {
                            text.ForceMeshUpdate();
                            if (text.isTextOverflowing) report += "Text overflow: " + text.name + " at " + size + "\n";
                        }
                        renderCamera.Render();
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                        pixels.Apply();
                        System.IO.File.WriteAllBytes(output + "/" + (waiting ? "waiting-" : "rooms-") + size.x + "x" + size.y + ".png", pixels.EncodeToPNG());
                    }
                    finally
                    {
                        renderCamera.targetTexture = null;
                        RenderTexture.active = previous;
                        Object.DestroyImmediate(pixels);
                        target.Release();
                        Object.DestroyImmediate(target);
                    }
                }
            }
            System.IO.File.WriteAllText(output + "/report.txt", report + "Preview render completed. Network play requires two clients.\n");
            Debug.Log("Lobby preview validation completed: " + output);
        }
        catch (System.Exception error)
        {
            System.IO.File.WriteAllText(output + "/report.txt", error.ToString());
            Debug.LogException(error);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
