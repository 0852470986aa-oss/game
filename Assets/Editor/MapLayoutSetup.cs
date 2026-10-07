// ไฟล์ MapLayoutSetup.cs — เครื่องมือ Unity Editor (ไม่ได้รันในเกม / ไม่ถูก build ลงมือถือ)
// ใช้จัดเลย์เอาต์แม็พใน Scene เกมเพลย์ (Assets/Scenes/SampleScene.unity): ตั้งภาพพื้นหลัง 3 แม็พ,
// วางสิ่งกีดขวางแม็พ 1 (ปริซึม) และแม็พ 2 (หุ่นยนต์) แล้วเซฟ Scene, และเรนเดอร์ภาพพรีวิวไว้ตรวจงาน
// เมนู [MenuItem] ส่วนใหญ่ถูกคอมเมนต์ปิดไว้ เรียกใช้ DecoratePrism ของ GameplayManager และอ่านฟิลด์ของ PlayerController
// มีคลาส MechThrusterPreview (เรนเดอร์ภาพไอพ่นยาน 3 ลำ) อยู่ท้ายไฟล์
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// หน้าต่าง/ชุดเมธอด static ของ Editor สำหรับสร้างเลย์เอาต์แม็พลงใน Scene
public class MapLayoutSetup : EditorWindow
{
    // path ของ Scene เกมเพลย์ที่เครื่องมือนี้จะเปิดและแก้ไข
    private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";
    // ความสูงของภาพพื้นหลังแม็พในโลก (หน่วย Unity) ใช้คำนวณสเกลภาพพื้นหลัง
    private const float TargetBackgroundHeight = 75f;

    // Unity เรียกทุกครั้งที่ Editor โหลด/คอมไพล์สคริปต์เสร็จ: ถ้ามีไฟล์ Library/PrismPreview.request
    // และไม่ได้อยู่ใน Play Mode จะสั่ง RenderPrismPreview() หลังจาก Editor ว่าง (delayCall)
    [InitializeOnLoadMethod]
    private static void QueuePrismPreview()
    {
        EditorApplication.delayCall += () => {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && System.IO.File.Exists("Library/PrismPreview.request"))
                RenderPrismPreview();
        };
    }

    // เปิด SampleScene เป็น Preview Scene (ไม่กระทบ Scene ที่เปิดอยู่) แล้วทดสอบการตกแต่งแม็พปริซึม
    // ตรวจว่าไม่มี Collider เพิ่ม และเรียกซ้ำแล้วไม่สร้างซ้ำ จากนั้นเรนเดอร์ภาพ 1400x840 ไปที่ Library/PrismPreview.png
    // ผลตรวจ (PASS หรือข้อความ error) เขียนไว้ที่ Library/PrismPreview.txt
    // [MenuItem("Battlefield/Preview Prism Atmosphere")]
    public static void RenderPrismPreview()
    {
        Scene scene = EditorSceneManager.OpenPreviewScene(GameplayScenePath);
        RenderTexture target = null;
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            // 1) เปิดเฉพาะ Map1_Layout แล้วเรียก DecoratePrism สองรอบเพื่อตรวจว่าไม่สร้างของซ้ำ
            GameObject layout = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool selected = root.name == "Map1_Layout";
                root.SetActive(selected);
                if (selected) layout = root;
            }
            if (layout == null) throw new System.Exception("Missing Map1_Layout");
            GameplayManager.DecoratePrism(layout.transform);
            Transform decor = layout.transform.Find("PrismAtmosphere");
            if (decor == null || decor.GetComponentsInChildren<Collider2D>().Length != 0)
                throw new System.Exception("Decoration missing or unexpected blocking collider.");
            int count = decor.childCount;
            GameplayManager.DecoratePrism(layout.transform);
            if (decor.childCount != count) throw new System.Exception("Decoration duplicated.");
            // 2) สร้างกล้อง orthographic ชั่วคราวใน Preview Scene แล้วเรนเดอร์ลง RenderTexture
            var go = new GameObject("PrismPreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(go, scene);
            Camera camera = go.GetComponent<Camera>();
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 39;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.farClipPlane = 200; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.07f);
            Material unlit = decor.GetComponentInChildren<SpriteRenderer>().sharedMaterial;
            foreach (SpriteRenderer renderer in layout.GetComponentsInChildren<SpriteRenderer>()) renderer.sharedMaterial = unlit;
            target = new RenderTexture(1400, 840, 24);
            target.Create(); camera.targetTexture = target;
            camera.Render();
            // 3) อ่านพิกเซลจาก RenderTexture แล้วบันทึกเป็นไฟล์ PNG และไฟล์ผลตรวจ
            pixels = new Texture2D(1400, 840, TextureFormat.RGB24, false);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1400, 840), 0, 0); pixels.Apply();
            System.IO.File.WriteAllBytes("Library/PrismPreview.png", pixels.EncodeToPNG());
            System.IO.File.WriteAllText("Library/PrismPreview.txt", "PASS: " + count + " decorative elements, no added colliders, duplicate generation prevented.");
        }
        catch (System.Exception error) { System.IO.File.WriteAllText("Library/PrismPreview.txt", error.ToString()); Debug.LogException(error); }
        // คืนค่า RenderTexture เดิม ทำลายของชั่วคราว และปิด Preview Scene เสมอ (แม้เกิด error)
        finally
        {
            RenderTexture.active = previous;
            if (pixels != null) DestroyImmediate(pixels);
            if (target != null) { target.Release(); DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // ตกแต่งบรรยากาศแม็พปริซึม (Map1_Layout) ใน Scene ที่เปิดอยู่ ถ้ามี PrismAtmosphere อยู่แล้วจะไม่ทำซ้ำ
    // บันทึก Undo ไว้ และ mark Scene ว่ามีการแก้ไข (ต้องกดเซฟเอง)
    // [MenuItem("Battlefield/Decorate Prism Atmosphere")]
    public static void DecoratePrismAtmosphere()
    {
        GameObject layout = FindSceneRoot("Map1_Layout");
        if (layout == null) { Debug.LogWarning("Open SampleScene to decorate Map1_Layout."); return; }
        if (layout.transform.Find("PrismAtmosphere") != null) return;
        GameplayManager.DecoratePrism(layout.transform);
        Transform created = layout.transform.Find("PrismAtmosphere");
        if (created != null) Undo.RegisterCreatedObjectUndo(created.gameObject, "Decorate Prism Atmosphere");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ข้อมูลการวางสิ่งกีดขวาง 1 ชิ้น: ตำแหน่ง (x, y), ขนาดที่ต้องการ (กว้าง, สูง หน่วย Unity), มุมหมุน (องศา)
    private struct RockPlacement
    {
        public Vector2 position;
        public Vector2 collisionSize;
        public float rotation;

        // Constructor: รับ x, y, กว้าง, สูง, มุม
        public RockPlacement(float x, float y, float width, float height, float angle)
        {
            position = new Vector2(x, y);
            collisionSize = new Vector2(width, height);
            rotation = angle;
        }
    }

    // ดึงเลขท้ายชื่อ Sprite (เช่น "Obs_Pillars_14" -> 14) ถ้าไม่มีเลขคืน 0
    private static int SpriteNumber(Sprite sprite)
    {
        if (sprite == null) return 0;
        int underscore = sprite.name.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(sprite.name.Substring(underscore + 1), out int value) ? value : 0;
    }

    // หา GameObject ระดับบนสุด (root) ใน Scene ที่เปิดอยู่ตามชื่อ คืน null ถ้าไม่เจอ
    private static GameObject FindSceneRoot(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == objectName) return root;
        }
        return null;
    }

    // โหลด Sprite ย่อยทั้งหมดจากไฟล์ภาพ (Sprite Sheet) แล้วเก็บใน Dictionary โดยใช้เลขท้ายชื่อเป็น key
    private static Dictionary<int, Sprite> LoadNumberedSprites(string assetPath)
    {
        Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null)
            {
                sprites[SpriteNumber(sprite)] = sprite;
            }
        }
        return sprites;
    }

    // สร้าง GameObject เปล่าเป็นกลุ่มย่อยใต้ parent ไว้จัดหมวดสิ่งกีดขวาง
    private static Transform CreateMap1Group(Transform parent, string name)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    // สร้างสิ่งกีดขวาง 1 ชิ้นของแม็พ 1: วางตำแหน่ง/มุมตาม placement, ปรับสเกลให้ภาพครอบขนาด collisionSize
    // และใส่ภาพไว้ในลูกชื่อ "Artwork" ที่เลื่อนให้กึ่งกลางภาพตรงกับจุดหมุน (ภาพอย่างเดียว ไม่มี Collider)
    private static void CreateMap1Obstacle(Transform parent, string name, RockPlacement placement,
        Sprite sprite, int sortingOrder)
    {
        if (sprite == null)
        {
            Debug.LogError("Cannot create " + name + " because its sprite is missing.");
            return;
        }

        GameObject obstacle = new GameObject(name);
        obstacle.transform.SetParent(parent, false);
        obstacle.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
        obstacle.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);

        // Pillar sprites use a bottom-left pivot while crystal sprites use a centered pivot.
        // Centering the artwork under a holder keeps every obstacle aligned with its runtime collider.
        Vector2 spriteSize = sprite.bounds.size;
        float scale = Mathf.Max(
            placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
            placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
        obstacle.transform.localScale = new Vector3(scale, scale, 1f);

        GameObject artwork = new GameObject("Artwork");
        artwork.transform.SetParent(obstacle.transform, false);
        artwork.transform.localPosition = -sprite.bounds.center;

        SpriteRenderer renderer = artwork.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
    }

    // ตั้งค่าพื้นฐานของ 3 แม็พ: ลบ BackgroundMap เก่า, สร้าง/ใช้ Map0_Layout..Map2_Layout
    // ใส่ภาพพื้นหลังสูง TargetBackgroundHeight หน่วย และเปิดไว้เฉพาะแม็พ 2 (ที่เหลือปิด)
    // [MenuItem("Battlefield/Setup Map Layouts")]
    public static void SetupMaps()
    {
        // 1. ????? BackgroundMap ???????????
        GameObject oldBg = GameObject.Find("BackgroundMap");
        if (oldBg != null)
        {
            DestroyImmediate(oldBg);
        }

        // ?????????????? 3
        string[] mapNames = { "Map0_Layout", "Map1_Layout", "Map2_Layout" };
        string[] bgImages = { "Images/Map_ThunderJellyfish", "Images/Map_ObeliskPlains", "Images/Map_AncientMech" };

        for (int i = 0; i < 3; i++)
        {
            // ??????????????????????????????? ????????????????????
            GameObject mapLayout = FindSceneRoot(mapNames[i]);
            if (mapLayout == null)
            {
                mapLayout = new GameObject(mapNames[i]);
            }
            
            // ???????????????????????????
            mapLayout.transform.position = Vector3.zero;
            mapLayout.transform.rotation = Quaternion.identity;
            mapLayout.transform.localScale = Vector3.one;

            // ?????????????????????????????
            Transform bgTransform = mapLayout.transform.Find("Background");
            GameObject bgObj;
            if (bgTransform == null)
            {
                bgObj = new GameObject("Background");
                bgObj.transform.SetParent(mapLayout.transform);
            }
            else
            {
                bgObj = bgTransform.gameObject;
            }

            // ?????? SpriteRenderer ?????????????
            SpriteRenderer sr = bgObj.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = bgObj.AddComponent<SpriteRenderer>();
            }

            // ?????????????????
            Sprite bgSprite = Resources.Load<Sprite>(bgImages[i]);
            if (bgSprite != null)
            {
                sr.sprite = bgSprite;
            }
            
            sr.sortingOrder = -10; // ??????????????
            
            // ????????????????????
            bgObj.transform.position = Vector3.zero;
            if (bgSprite != null)
            {
                float uniformScale = TargetBackgroundHeight / Mathf.Max(bgSprite.bounds.size.y, 0.01f);
                bgObj.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            }

            // ???????????? ?????????? 2 ??????????????????????
            mapLayout.SetActive(i == 2);
        }

        Debug.Log("?? ???????????????????????????????????! ???????????????????????????????!");
    }

    // สร้างสิ่งกีดขวางแม็พ 1 (ปริซึม) ใหม่ทั้งหมดใต้ Map1_Layout/Map1Obstacles แล้วเซฟ Scene
    // ประกอบด้วย เสาโอเบลิสก์กลาง 1, คริสตัล 8, โดม 4, แผงพลังงาน 12 (วางสมมาตรรอบจุดกลาง)
    // [MenuItem("Battlefield/Setup Map 1 Prism Obstacles")]
    public static void SetupMap1Obstacles()
    {
        // 1) เปิด SampleScene ถ้ายังไม่ได้เปิดอยู่ และหา Map1_Layout
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameplayScenePath)
        {
            scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        GameObject mapLayout = FindSceneRoot("Map1_Layout");
        if (mapLayout == null)
        {
            Debug.LogError("Map1_Layout was not found in " + GameplayScenePath);
            return;
        }

        // 2) โหลด Sprite คริสตัล/เสา และตรวจว่ามีเลขที่ต้องใช้ครบ ถ้าขาดจะยกเลิกโดยไม่แก้ Scene
        Dictionary<int, Sprite> crystals = LoadNumberedSprites("Assets/Resources/Images/Obs_Crystals.png");
        Dictionary<int, Sprite> pillars = LoadNumberedSprites("Assets/Resources/Images/Obs_Pillars.png");
        int[] requiredCrystals = { 0, 2, 3, 4 };
        int[] requiredPillars = { 0, 2, 4, 12, 13, 14 };
        foreach (int spriteNumber in requiredCrystals)
        {
            if (!crystals.ContainsKey(spriteNumber))
            {
                Debug.LogError("Obs_Crystals_" + spriteNumber + " is missing; Map1 was not changed.");
                return;
            }
        }
        foreach (int spriteNumber in requiredPillars)
        {
            if (!pillars.ContainsKey(spriteNumber))
            {
                Debug.LogError("Obs_Pillars_" + spriteNumber + " is missing; Map1 was not changed.");
                return;
            }
        }

        // 3) ลบกลุ่ม Map1Obstacles เดิม แล้วสร้างใหม่
        // This method intentionally replaces only the generated Map1 obstacle artwork.
        Transform oldContainer = mapLayout.transform.Find("Map1Obstacles");
        if (oldContainer != null)
        {
            DestroyImmediate(oldContainer.gameObject);
        }

        GameObject container = new GameObject("Map1Obstacles");
        container.transform.SetParent(mapLayout.transform, false);

        Transform obeliskGroup = CreateMap1Group(container.transform, "CentralObelisk");
        CreateMap1Obstacle(obeliskGroup, "CentralObelisk", new RockPlacement(0f, 0f, 5.5f, 8f, 0f),
            pillars[14], 2);

        // 4) คริสตัล 8 ชิ้น: (x, y, กว้าง, สูง, มุม) และเลข Sprite ที่ใช้ตามลำดับ
        Transform crystalGroup = CreateMap1Group(container.transform, "CrystalObstacles");
        RockPlacement[] crystalPlacements =
        {
            new RockPlacement(-20f, 12f, 5f, 4f, -8f),
            new RockPlacement(20f, -12f, 5f, 4f, -8f),
            new RockPlacement(20f, 12f, 5f, 4f, 8f),
            new RockPlacement(-20f, -12f, 5f, 4f, 8f),
            new RockPlacement(-32f, 7f, 4.5f, 4f, -12f),
            new RockPlacement(32f, -7f, 4.5f, 4f, -12f),
            new RockPlacement(32f, 7f, 4.5f, 4f, 12f),
            new RockPlacement(-32f, -7f, 4.5f, 4f, 12f)
        };
        int[] crystalSpriteNumbers = { 2, 2, 3, 3, 0, 0, 4, 4 };
        for (int i = 0; i < crystalPlacements.Length; i++)
        {
            CreateMap1Obstacle(crystalGroup, string.Format("CrystalObstacle_{0:00}", i + 1),
                crystalPlacements[i], crystals[crystalSpriteNumbers[i]], 1);
        }

        // 5) โดม 4 มุมแม็พ สลับใช้ Sprite เสาเลข 12 และ 13
        Transform domeGroup = CreateMap1Group(container.transform, "DomeObstacles");
        RockPlacement[] domePlacements =
        {
            new RockPlacement(-40f, 23f, 7f, 5f, 0f),
            new RockPlacement(40f, -23f, 7f, 5f, 0f),
            new RockPlacement(40f, 23f, 7f, 5f, 0f),
            new RockPlacement(-40f, -23f, 7f, 5f, 0f)
        };
        for (int i = 0; i < domePlacements.Length; i++)
        {
            CreateMap1Obstacle(domeGroup, string.Format("DomeObstacle_{0:00}", i + 1),
                domePlacements[i], pillars[i % 2 == 0 ? 12 : 13], 1);
        }

        // 6) แผงพลังงาน/กำแพง 12 ชิ้น และเลข Sprite เสาที่ใช้ตามลำดับ
        Transform barrierGroup = CreateMap1Group(container.transform, "EnergyBarriers");
        RockPlacement[] barrierPlacements =
        {
            new RockPlacement(-16f, 0f, 10f, 4.5f, 0f),
            new RockPlacement(16f, 0f, 10f, 4.5f, 0f),
            new RockPlacement(0f, 29f, 8f, 4f, 0f),
            new RockPlacement(0f, -29f, 8f, 4f, 0f),
            new RockPlacement(-52f, 25f, 8f, 4.5f, -8f),
            new RockPlacement(52f, -25f, 8f, 4.5f, -8f),
            new RockPlacement(52f, 25f, 8f, 4.5f, 8f),
            new RockPlacement(-52f, -25f, 8f, 4.5f, 8f),
            new RockPlacement(-31f, 16f, 4.5f, 5.5f, 0f),
            new RockPlacement(31f, -16f, 4.5f, 5.5f, 0f),
            new RockPlacement(31f, 16f, 4.5f, 5.5f, 0f),
            new RockPlacement(-31f, -16f, 4.5f, 5.5f, 0f)
        };
        int[] barrierSpriteNumbers = { 0, 0, 2, 2, 4, 4, 0, 0, 14, 14, 14, 14 };
        for (int i = 0; i < barrierPlacements.Length; i++)
        {
            CreateMap1Obstacle(barrierGroup, string.Format("EnergyBarrier_{0:00}", i + 1),
                barrierPlacements[i], pillars[barrierSpriteNumbers[i]], 0);
        }

        // 7) mark ว่าแก้แล้วและเซฟ Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Map1 composition created: 1 central obelisk, 8 crystal covers, 4 domes and 12 energy barriers.");
    }

    // สร้างองค์ประกอบแม็พ 2 (หุ่นยนต์) ใหม่ทั้งหมดใต้ Map2_Layout แล้วเซฟ Scene:
    // หินกีดขวาง 12 ก้อน (RockObstacles), เศษหินประดับ 24 ก้อน, ป้อมปืน 2, แกนแดง 6
    // กลุ่ม RockObstacles จะถูก GameplayManager.Maps.cs ใส่ MoltenContactSurface (ชนแล้วโดนดาเมจลาวา) ตอนรันเกม
    // [MenuItem("Battlefield/Setup Map 2 Full Arena Obstacles")]
    public static void SetupMap2RockObstacles()
    {
        // 1) เปิด SampleScene ถ้ายังไม่ได้เปิดอยู่ และหา Map2_Layout
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameplayScenePath)
        {
            scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        GameObject mapLayout = FindSceneRoot("Map2_Layout");
        if (mapLayout == null)
        {
            Debug.LogError("Map2_Layout was not found in " + GameplayScenePath);
            return;
        }

        // 2) ลบกลุ่มเดิมทั้งหมด (RockObstacles, TurretObstacles, RedCoreObstacles, RockDecorations) แล้วสร้างกลุ่มหินใหม่
        Transform oldContainer = mapLayout.transform.Find("RockObstacles");
        if (oldContainer != null)
        {
            DestroyImmediate(oldContainer.gameObject);
        }

        GameObject container = new GameObject("RockObstacles");
        container.transform.SetParent(mapLayout.transform, false);

        Transform oldTurretContainer = mapLayout.transform.Find("TurretObstacles");
        if (oldTurretContainer != null)
        {
            DestroyImmediate(oldTurretContainer.gameObject);
        }

        Transform oldRedCoreContainer = mapLayout.transform.Find("RedCoreObstacles");
        if (oldRedCoreContainer != null)
        {
            DestroyImmediate(oldRedCoreContainer.gameObject);
        }

        Transform oldDecorationContainer = mapLayout.transform.Find("RockDecorations");
        if (oldDecorationContainer != null)
        {
            DestroyImmediate(oldDecorationContainer.gameObject);
        }

        // 3) โหลด Sprite อุกกาบาต ยกเว้นเลข 9–11 (เป็นเฟรมระเบิดที่ HazardController ใช้) แล้วเรียงตามเลข
        Object[] loadedAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Images/Obs_Asteroids.png");
        List<Sprite> sprites = new List<Sprite>();
        foreach (Object asset in loadedAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null && !sprite.name.EndsWith("_9") && !sprite.name.EndsWith("_10") && !sprite.name.EndsWith("_11"))
            {
                sprites.Add(sprite);
            }
        }

        if (sprites.Count == 0)
        {
            Debug.LogError("No asteroid sprites were found at Assets/Resources/Images/Obs_Asteroids.png");
            DestroyImmediate(container);
            return;
        }
        sprites.Sort((left, right) => SpriteNumber(left).CompareTo(SpriteNumber(right)));

        // 4) ตำแหน่ง/ขนาด/มุมของหินกีดขวาง 12 ก้อน (วางเป็นคู่สมมาตร)
        RockPlacement[] rocks =
        {
            new RockPlacement(-14f, 10f, 9f, 7f, -18f),
            new RockPlacement(14f, -10f, 9f, 7f, 162f),
            new RockPlacement(15f, 11f, 8f, 6f, 28f),
            new RockPlacement(-15f, -11f, 8f, 6f, 208f),
            new RockPlacement(-34f, 30f, 16f, 12f, 14f),
            new RockPlacement(34f, 30f, 16f, 12f, -14f),
            new RockPlacement(-34f, -30f, 16f, 12f, 24f),
            new RockPlacement(34f, -30f, 16f, 12f, -24f),
            new RockPlacement(-11f, 32f, 11f, 8f, 35f),
            new RockPlacement(11f, -32f, 11f, 8f, 215f),
            new RockPlacement(-36f, 6f, 10f, 8f, -12f),
            new RockPlacement(36f, -6f, 10f, 8f, 168f)
        };

        // สร้างหินแต่ละก้อน: ปรับสเกลให้พอดีกรอบ collisionSize และเพิ่มขอบเรืองแสงสีลาวา (LavaRim) ขยาย 1.1 เท่าไว้ด้านหลัง
        for (int i = 0; i < rocks.Length; i++)
        {
            RockPlacement placement = rocks[i];
            // Prefer the cracked/lava asteroid cuts so Map 2 matches the orange mech background.
            Sprite sprite = sprites[(i + 6) % sprites.Count];
            GameObject rock = new GameObject(string.Format("RockObstacle_{0:00}", i + 1));
            rock.transform.SetParent(container.transform, false);
            rock.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
            rock.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);

            Vector2 spriteSize = sprite.bounds.size;
            float scale = Mathf.Min(
                placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
                placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
            rock.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = rock.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -1;

            GameObject rim = new GameObject("LavaRim");
            rim.transform.SetParent(rock.transform, false);
            rim.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            SpriteRenderer rimRenderer = rim.AddComponent<SpriteRenderer>();
            rimRenderer.sprite = sprite;
            rimRenderer.color = new Color(1f, 0.25f, 0.04f, 0.22f);
            rimRenderer.sortingOrder = -2;
        }

        // Small visual-only debris fills the full composition without making flight frustrating.
        GameObject decorationContainer = new GameObject("RockDecorations");
        decorationContainer.transform.SetParent(mapLayout.transform, false);
        // 5) เศษหินประดับ 24 ก้อน (ภาพอย่างเดียว ไม่มีผลต่อการบิน)
        RockPlacement[] decorations =
        {
            new RockPlacement(-27f, 23f, 3.8f, 3f, 18f),
            new RockPlacement(-18f, 27f, 2.4f, 2f, -24f),
            new RockPlacement(4f, 29f, 2.2f, 1.8f, 12f),
            new RockPlacement(18f, 25f, 3.5f, 2.8f, -32f),
            new RockPlacement(28f, 20f, 2.4f, 2f, 35f),
            new RockPlacement(-31f, 15f, 2.2f, 1.8f, -18f),
            new RockPlacement(-21f, 15f, 3.2f, 2.6f, 42f),
            new RockPlacement(-5f, 17f, 2f, 1.7f, -12f),
            new RockPlacement(8f, 18f, 2.5f, 2f, 28f),
            new RockPlacement(25f, 13f, 3.2f, 2.5f, -16f),
            new RockPlacement(-29f, 5f, 2.2f, 1.8f, 24f),
            new RockPlacement(-19f, 3f, 1.8f, 1.5f, -30f),
            new RockPlacement(-4f, 7f, 1.7f, 1.4f, 15f),
            new RockPlacement(18f, 5f, 2.2f, 1.8f, 38f),
            new RockPlacement(30f, 3f, 2.7f, 2.1f, -20f),
            new RockPlacement(-30f, -7f, 3f, 2.4f, -36f),
            new RockPlacement(-19f, -18f, 2.5f, 2f, 22f),
            new RockPlacement(-5f, -17f, 2f, 1.7f, -18f),
            new RockPlacement(19f, -17f, 3.2f, 2.5f, 32f),
            new RockPlacement(29f, -14f, 2.2f, 1.8f, -25f),
            new RockPlacement(-26f, -25f, 2.6f, 2.1f, 18f),
            new RockPlacement(-15f, -28f, 3.5f, 2.8f, -28f),
            new RockPlacement(1f, -28f, 2.2f, 1.8f, 12f),
            new RockPlacement(25f, -26f, 3.5f, 2.8f, 28f)
        };

        // สร้างเศษหินแต่ละก้อน: ขยายใหญ่ขึ้น 1.45 เท่า วางชั้นหลัง และทำให้จางเพื่อให้ดูอยู่ลึก
        for (int i = 0; i < decorations.Length; i++)
        {
            RockPlacement placement = decorations[i];
            Sprite sprite = sprites[(i * 3 + 2) % sprites.Count];
            GameObject rock = new GameObject(string.Format("RockDecoration_{0:00}", i + 1));
            rock.transform.SetParent(decorationContainer.transform, false);
            rock.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
            rock.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);
            Vector2 spriteSize = sprite.bounds.size;
            float scale = Mathf.Min(
                placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
                placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
            scale *= 1.45f;
            rock.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer renderer = rock.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -3;
            float depthAlpha = i % 3 == 0 ? 0.68f : 0.86f;
            renderer.color = new Color(0.72f, 0.78f, 0.9f, depthAlpha);
        }

        // 6) ป้อมปืน 2 ตัวที่มุมบน พร้อมแสงเรืองสีแดง (ถ้าไม่มี Sprite จะเตือนแล้วข้าม)
        Object[] loadedTurretAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Images/Obs_Turrets.png");
        List<Sprite> turretSprites = new List<Sprite>();
        foreach (Object asset in loadedTurretAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null) turretSprites.Add(sprite);
        }

        if (turretSprites.Count > 0)
        {
            GameObject turretContainer = new GameObject("TurretObstacles");
            turretContainer.transform.SetParent(mapLayout.transform, false);

            RockPlacement[] turrets =
            {
                new RockPlacement(-31f, 29f, 6f, 4.8f, -18f),
                new RockPlacement(31f, 29f, 6f, 4.8f, 198f)
            };

            for (int i = 0; i < turrets.Length; i++)
            {
                RockPlacement placement = turrets[i];
                Sprite sprite = turretSprites[(i + 2) % turretSprites.Count];
                GameObject turret = new GameObject(string.Format("TurretObstacle_{0:00}", i + 1));
                turret.transform.SetParent(turretContainer.transform, false);
                turret.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
                turret.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);

                Vector2 spriteSize = sprite.bounds.size;
                float scale = Mathf.Min(
                    placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
                    placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
                turret.transform.localScale = new Vector3(scale, scale, 1f);

                SpriteRenderer renderer = turret.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 0;

                GameObject warningGlow = new GameObject("WarningGlow");
                warningGlow.transform.SetParent(turret.transform, false);
                warningGlow.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
                SpriteRenderer glowRenderer = warningGlow.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = sprite;
                glowRenderer.color = new Color(1f, 0.08f, 0.02f, 0.24f);
                glowRenderer.sortingOrder = -1;
            }
        }
        else
        {
            Debug.LogWarning("No turret sprites were found at Assets/Resources/Images/Obs_Turrets.png");
        }

        // 7) แกนสีแดง 6 ชิ้น (ไม่ใช้ Sprite เลข 8–11) พร้อมวงเรืองแสงรอบ
        Object[] loadedCoreAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Images/Obs_RedCores.png");
        List<Sprite> coreSprites = new List<Sprite>();
        foreach (Object asset in loadedCoreAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null && !sprite.name.EndsWith("_8") && !sprite.name.EndsWith("_9") &&
                !sprite.name.EndsWith("_10") && !sprite.name.EndsWith("_11"))
            {
                coreSprites.Add(sprite);
            }
        }

        if (coreSprites.Count > 0)
        {
            GameObject coreContainer = new GameObject("RedCoreObstacles");
            coreContainer.transform.SetParent(mapLayout.transform, false);
            RockPlacement[] cores =
            {
                new RockPlacement(-9f, -3f, 3.8f, 3.8f, -10f),
                new RockPlacement(9f, 3f, 3.8f, 3.8f, 10f),
                new RockPlacement(0f, 20f, 3.5f, 3.5f, 0f),
                new RockPlacement(0f, -20f, 3.5f, 3.5f, 180f),
                new RockPlacement(-24f, 0f, 3.2f, 3.2f, -20f),
                new RockPlacement(24f, 0f, 3.2f, 3.2f, 20f)
            };

            for (int i = 0; i < cores.Length; i++)
            {
                RockPlacement placement = cores[i];
                Sprite sprite = coreSprites[i % coreSprites.Count];
                GameObject core = new GameObject(string.Format("RedCoreObstacle_{0:00}", i + 1));
                core.transform.SetParent(coreContainer.transform, false);
                core.transform.localPosition = new Vector3(placement.position.x, placement.position.y, 0f);
                core.transform.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);
                Vector2 spriteSize = sprite.bounds.size;
                float scale = Mathf.Min(
                    placement.collisionSize.x / Mathf.Max(spriteSize.x, 0.01f),
                    placement.collisionSize.y / Mathf.Max(spriteSize.y, 0.01f));
                core.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer renderer = core.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 1;

                GameObject halo = new GameObject("CoreHalo");
                halo.transform.SetParent(core.transform, false);
                halo.transform.localScale = new Vector3(1.55f, 1.55f, 1f);
                SpriteRenderer haloRenderer = halo.AddComponent<SpriteRenderer>();
                haloRenderer.sprite = sprite;
                haloRenderer.color = new Color(1f, 0.04f, 0.02f, 0.2f);
                haloRenderer.sortingOrder = 0;
            }
        }

        // 8) mark ว่าแก้แล้ว เซฟ Scene และพิมพ์สรุปจำนวนที่สร้าง
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Map2 full composition created: " + rocks.Length + " obstacle rocks, " + decorations.Length + " debris rocks, 2 turrets and 6 red cores.");
    }
}

// คลาส Editor สำหรับเรนเดอร์ภาพไอพ่นของยาน 3 ลำตอนเร่งเต็มที่ ไว้ตรวจงานภาพ (ไม่เริ่มเกม/ไม่ต่อ Photon)
// Render-only inspection: never starts gameplay, saves the active scene, or joins Photon.
public static class MechThrusterPreview
{
    // สร้าง Preview Scene แยก, วางยาน Ship1..Ship3 แบบปิดคอมโพเนนต์ทั้งหมด แล้วใช้ Reflection
    // ตั้งค่าฟิลด์ private ของ PlayerController ให้แสดงไอพ่นเต็มกำลัง จากนั้นเรนเดอร์ 2000x1000 ไปที่ Library/MechValidation/thrusters.png
    // ต้องไม่อยู่ใน Play Mode ไม่งั้นจะ throw error
    // [MenuItem("Battlefield/Mech/Render Thrusters")]
    public static void Render()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Stop Play Mode before rendering the isolated thruster preview.");

        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        const string output = "Library/MechValidation/thrusters.png";
        try
        {
            // 1) สร้าง container ที่ปิดไว้ แล้วโหลดยานทั้ง 3 ลำมาไว้ข้างใน (ปิดทุก Behaviour/ฟิสิกส์/Particle/Renderer)
            var container = new GameObject("DisabledGameplayPreview");
            container.SetActive(false);
            SceneManager.MoveGameObjectToScene(container, preview);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(PlayerController);
            var ships = new List<PlayerController>();
            var hulls = new List<SpriteRenderer>();
            float largestHeight = 0f;
            float largestWidth = 0f;
            for (int i = 1; i <= 3; i++)
            {
                GameObject prefab = Resources.Load<GameObject>("ShipPrefabs/Ship" + i);
                if (prefab == null) throw new System.Exception("Missing preview prefab Ship" + i);
                GameObject ship = Object.Instantiate(prefab, container.transform);
                ship.name = "Ship" + i + "_FullThrustPreview";
                // Inactive ancestry prevents Photon OnEnable; disabling components also prevents Start.
                foreach (Behaviour behaviour in ship.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (Rigidbody2D body in ship.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
                foreach (ParticleSystem particle in ship.GetComponentsInChildren<ParticleSystem>(true))
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (Renderer child in ship.GetComponentsInChildren<Renderer>(true)) child.enabled = false;
                var hull = ship.GetComponent<SpriteRenderer>();
                var controller = ship.GetComponent<PlayerController>();
                if (hull == null || hull.sprite == null || controller == null)
                    throw new System.Exception("Missing hull/controller on Ship" + i);
                // เปิดเฉพาะตัวยาน และใช้ Reflection ตั้ง spriteRenderer, movementInput = ขึ้น, displayedThrust = 1 (เร่งเต็มที่)
                hull.enabled = true;
                hull.color = Color.white;
                type.GetField("spriteRenderer", flags).SetValue(controller, hull);
                type.GetField("movementInput", flags).SetValue(controller, Vector2.up);
                type.GetField("displayedThrust", flags).SetValue(controller, 1f);
                largestHeight = Mathf.Max(largestHeight, hull.sprite.bounds.size.y * Mathf.Abs(ship.transform.lossyScale.y));
                largestWidth = Mathf.Max(largestWidth, hull.sprite.bounds.size.x * Mathf.Abs(ship.transform.lossyScale.x));
                ships.Add(controller);
                hulls.Add(hull);
            }

            // 2) จัดเรียงยานเป็นแถวแนวนอน เรียก LateUpdate ของ PlayerController ให้สร้างภาพไอพ่น แล้วคำนวณกรอบรวมของภาพ
            float spacing = Mathf.Max(largestWidth * 1.1f, largestHeight * .74f);
            container.SetActive(true);
            Bounds frame = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            for (int i = 0; i < ships.Count; i++)
            {
                ships[i].transform.position = new Vector3((i - 1) * spacing, 0, 0) - hulls[i].bounds.center;
                // Keep remote/offline preview at full thrust without running Update or network code.
                type.GetField("previousVfxPosition", flags).SetValue(ships[i], ships[i].transform.position - Vector3.up * 1000f);
                type.GetMethod("LateUpdate", flags).Invoke(ships[i], null);
                foreach (SpriteRenderer renderer in ships[i].GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!renderer.enabled) continue;
                    if (first) { frame = renderer.bounds; first = false; }
                    else frame.Encapsulate(renderer.bounds);
                }
            }

            // 3) สร้างกล้อง orthographic อัตราส่วน 2:1 ครอบกรอบภาพ แล้วเรนเดอร์และบันทึกเป็น PNG
            var cameraObject = new GameObject("IsolatedThrusterCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.018f, .033f, .07f, 1);
            camera.transform.position = new Vector3(frame.center.x, frame.center.y, -20);
            camera.aspect = 2f;
            camera.orthographicSize = Mathf.Max(frame.extents.y, frame.extents.x / camera.aspect) * 1.08f;
            target = new RenderTexture(2000, 1000, 24);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(2000, 1000, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 2000, 1000), 0, 0);
            pixels.Apply();
            System.IO.Directory.CreateDirectory("Library/MechValidation");
            System.IO.File.WriteAllBytes(output, pixels.EncodeToPNG());
            Debug.Log("Thruster preview rendered (Ship1 / Ship2 / Ship3, full thrust): " + output);
        }
        finally
        {
            // คืนค่า RenderTexture เดิม ทำลายของชั่วคราว และปิด Preview Scene เสมอ
            RenderTexture.active = previous;
            if (pixels != null) Object.DestroyImmediate(pixels);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}


