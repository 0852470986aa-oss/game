using UnityEngine;
using UnityEditor;
using System.IO;

public class PrefabSetupTool : EditorWindow
{
    [MenuItem("Battlefield/Setup Ships and Bullets")]
    public static void SetupPrefabs()
    {
        CreateLaserSprite();
        CreateVFXPrefabs();
        SetupShips();
        SetupBullet();
        Debug.Log("✅ Ships, VFX, and Bullets setup completed successfully!");
    }

    private static void CreateLaserSprite()
    {
        string path = "Assets/Resources/Images/laser_bullet.png";
        if (File.Exists(path)) return; // Already exists

        // Create a simple 4x16 glowing rect texture
        Texture2D tex = new Texture2D(4, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                // Glow effect (brighter in center)
                float alpha = (x == 0 || x == 3) ? 0.5f : 1f;
                tex.SetPixel(x, y, new Color(0.4f, 0.8f, 1f, alpha));
            }
        }
        tex.Apply();
        
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();

        // Ensure it's imported as a Sprite
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.SaveAndReimport();
        }
    }

    private static void CreateVFXPrefabs()
    {
        // 1. Muzzle Flash
        string mfPath = "Assets/Resources/MuzzleFlash.prefab";
        if (!File.Exists(mfPath))
        {
            GameObject mfObj = new GameObject("MuzzleFlash");
            ParticleSystem mfPs = mfObj.AddComponent<ParticleSystem>();
            var mfMain = mfPs.main;
            mfMain.duration = 0.1f;
            mfMain.loop = false;
            mfMain.startLifetime = 0.1f;
            mfMain.startSpeed = 10f;
            mfMain.startSize = 0.2f;
            mfMain.startColor = new Color(1f, 0.8f, 0f, 1f); // Yellow
            
            var mfEmission = mfPs.emission;
            mfEmission.rateOverTime = 0;
            mfEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 10) });
            
            var mfShape = mfPs.shape;
            mfShape.shapeType = ParticleSystemShapeType.Sphere;
            mfShape.radius = 0.1f;
            
            mfObj.AddComponent<DestroyAfterSeconds>().lifetime = 0.2f;
            
            PrefabUtility.SaveAsPrefabAsset(mfObj, mfPath);
            DestroyImmediate(mfObj);
        }

        // 2. Impact Effect
        string iePath = "Assets/Resources/ImpactEffect.prefab";
        if (!File.Exists(iePath))
        {
            GameObject ieObj = new GameObject("ImpactEffect");
            ParticleSystem iePs = ieObj.AddComponent<ParticleSystem>();
            var ieMain = iePs.main;
            ieMain.duration = 0.2f;
            ieMain.loop = false;
            ieMain.startLifetime = 0.3f;
            ieMain.startSpeed = 5f;
            ieMain.startSize = 0.15f;
            ieMain.startColor = new Color(1f, 0.4f, 0f, 1f); // Orange-Red
            
            var ieEmission = iePs.emission;
            ieEmission.rateOverTime = 0;
            ieEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });
            
            var ieShape = iePs.shape;
            ieShape.shapeType = ParticleSystemShapeType.Circle;
            ieShape.radius = 0.2f;

            ieObj.AddComponent<DestroyAfterSeconds>().lifetime = 0.5f;
            
            PrefabUtility.SaveAsPrefabAsset(ieObj, iePath);
            DestroyImmediate(ieObj);
        }
    }

    private static void SetupShips()
    {
        for (int i = 1; i <= 3; i++)
        {
            string prefabPath = $"Assets/Resources/ShipPrefabs/Ship{i}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            // Instantiate for modification
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            
            // ปรับสเกลยานให้เล็กลง (0.6x)
            instance.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

            // 1. Sprite
            SpriteRenderer sr = instance.GetComponent<SpriteRenderer>();
            if (sr == null) sr = instance.AddComponent<SpriteRenderer>();
            
            Sprite shipSprite = Resources.Load<Sprite>($"Images/ship{i}");
            if (shipSprite != null)
            {
                sr.sprite = shipSprite;
            }

            // 2. Collider
            // Remove old colliders
            var oldColliders = instance.GetComponents<Collider2D>();
            foreach (var col in oldColliders) DestroyImmediate(col);

            // Add PolygonCollider to fit the ship sprite
            instance.AddComponent<PolygonCollider2D>();

            // 3. FirePoint
            Transform firePoint = instance.transform.Find("FirePoint");
            if (firePoint == null)
            {
                GameObject fpObj = new GameObject("FirePoint");
                fpObj.transform.SetParent(instance.transform);
                firePoint = fpObj.transform;
            }
            firePoint.localPosition = new Vector3(0, 1.2f, 0); // At the nose of the ship

            // Link FirePoint to PlayerController
            PlayerController pc = instance.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.firePoint = firePoint;
            }

            // 4. Engine Thrusters (ไอพ่น)
            Transform thruster = instance.transform.Find("Thruster");
            if (thruster == null)
            {
                GameObject tObj = new GameObject("Thruster");
                tObj.transform.SetParent(instance.transform);
                thruster = tObj.transform;
                thruster.localPosition = new Vector3(0, -1f, 0); // ท้ายยาน
                thruster.localRotation = Quaternion.Euler(90f, 0, 0); // ยิงอนุภาคลงล่าง (แกน Z)
                
                ParticleSystem ps = tObj.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 1f;
                main.loop = true;
                main.startLifetime = 0.3f;
                main.startSpeed = 3f;
                main.startSize = 0.2f;
                main.startColor = new Color(0.2f, 0.8f, 1f, 1f); // Blue Engine
                main.simulationSpace = ParticleSystemSimulationSpace.World; // ให้เศษไฟพ่นทิ้งไว้ที่เดิมตอนขยับยาน
                
                var emission = ps.emission;
                emission.rateOverTime = 20f;
                
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 15f;
                shape.radius = 0.1f;

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));

                if (pc != null) pc.thrusterEffect = ps;
            }

            // Save and destroy
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            DestroyImmediate(instance);
        }
    }

    private static void SetupBullet()
    {
        string prefabPath = "Assets/Resources/BulletPrefab.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        // 1. Sprite
        SpriteRenderer sr = instance.GetComponent<SpriteRenderer>();
        if (sr == null) sr = instance.AddComponent<SpriteRenderer>();
        
        Sprite laserSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Images/laser_bullet.png");
        if (laserSprite != null) sr.sprite = laserSprite;
        
        // 2. Adjust Collider
        var col = instance.GetComponent<BoxCollider2D>();
        if (col == null) col = instance.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.2f, 0.8f);
        col.isTrigger = true;

        // 3. Trail Renderer
        TrailRenderer tr = instance.GetComponent<TrailRenderer>();
        if (tr == null) tr = instance.AddComponent<TrailRenderer>();
        
        tr.time = 0.15f;
        tr.startWidth = 0.1f;
        tr.endWidth = 0f;
        tr.material = new Material(Shader.Find("Sprites/Default"));
        
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.8f, 1f), 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        tr.colorGradient = gradient;

        // Ensure BulletController has high speed
        var bc = instance.GetComponent<BulletController>();
        if (bc != null)
        {
            bc.speed = 15f; // Fast bullet
            bc.lifeTime = 2f;
        }

        // Save and destroy
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        DestroyImmediate(instance);
    }
}
