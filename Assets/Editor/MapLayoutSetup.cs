using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MapLayoutSetup : EditorWindow
{
    private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";
    private const float TargetBackgroundHeight = 75f;

    private struct RockPlacement
    {
        public Vector2 position;
        public Vector2 collisionSize;
        public float rotation;

        public RockPlacement(float x, float y, float width, float height, float angle)
        {
            position = new Vector2(x, y);
            collisionSize = new Vector2(width, height);
            rotation = angle;
        }
    }

    [MenuItem("Battlefield/Setup Map Layouts")]
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
            GameObject mapLayout = GameObject.Find(mapNames[i]);
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

    [MenuItem("Battlefield/Setup Map 2 Full Arena Obstacles")]
    public static void SetupMap2RockObstacles()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameplayScenePath)
        {
            scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        GameObject mapLayout = GameObject.Find("Map2_Layout");
        if (mapLayout == null)
        {
            Debug.LogError("Map2_Layout was not found in " + GameplayScenePath);
            return;
        }

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

        RockPlacement[] rocks =
        {
            new RockPlacement(-14f, 10f, 7.5f, 5.8f, -18f),
            new RockPlacement(14f, -10f, 7.5f, 5.8f, 162f),
            new RockPlacement(15f, 11f, 6.5f, 5f, 28f),
            new RockPlacement(-15f, -11f, 6.5f, 5f, 208f),
            new RockPlacement(-34f, 30f, 13f, 9f, 14f),
            new RockPlacement(34f, 30f, 13f, 9f, -14f),
            new RockPlacement(-34f, -30f, 13f, 9f, 24f),
            new RockPlacement(34f, -30f, 13f, 9f, -24f),
            new RockPlacement(-11f, 32f, 9f, 6f, 35f),
            new RockPlacement(11f, -32f, 9f, 6f, 215f),
            new RockPlacement(-36f, 6f, 8f, 6f, -12f),
            new RockPlacement(36f, -6f, 8f, 6f, 168f)
        };

        for (int i = 0; i < rocks.Length; i++)
        {
            RockPlacement placement = rocks[i];
            Sprite sprite = sprites[i % sprites.Count];
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
        }

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
            }
        }
        else
        {
            Debug.LogWarning("No turret sprites were found at Assets/Resources/Images/Obs_Turrets.png");
        }

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
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Map2 obstacle layout created: " + rocks.Length + " rocks, 2 turrets and 6 red cores.");
    }
}

