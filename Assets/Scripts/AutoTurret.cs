using UnityEngine;
using Photon.Pun;

public class AutoTurret : MonoBehaviour
{
    public float detectionRadius = 24f;
    public float fireRate = BattleBalance.TurretShotInterval;
    public float damage = BattleBalance.TurretDamage;
    public string bulletPrefabName = "BulletPrefab";
    public Transform firePoint;

    private const string RotationSheetResource = "Images/MechTurret_RotationSheet";
    private const float TurretArtWidth = 1.78f;
    private const float TurretArtHeight = 1.88f;
    private const float DesiredArtWidth = 3.8f;
    private const float DesiredArtHeight = 3.35f;
    private static Sprite cachedBaseSprite;
    private static Sprite cachedHeadSprite;
    private static bool attemptedSpriteLoad;

    private float nextFireTime;
    private float nextScan;
    private PlayerController target;
    private Transform turretHead;

    void Start()
    {
        SetupSplitTurretVisual();
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || GameplayManager.Instance == null || !GameplayManager.Instance.MatchInputAllowed) return;
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .2f;
            target = null;
            float nearest = detectionRadius;
            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                float distance = Vector2.Distance(transform.position, player.transform.position);
                if (!player.isDead && distance < nearest && ClearSight(player))
                { target = player; nearest = distance; }
            }
        }
        if (target == null || target.isDead) return;

        Vector2 direction = target.transform.position - transform.position;
        float bearing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion bulletAim = Quaternion.Euler(0, 0, bearing - 90f);
        Quaternion headAim = Quaternion.Euler(0, 0, bearing);

        if (turretHead != null)
            turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, headAim, 120 * Time.deltaTime);
        else
            transform.rotation = Quaternion.RotateTowards(transform.rotation, bulletAim, 120 * Time.deltaTime);

        bool linedUp = turretHead != null
            ? Quaternion.Angle(turretHead.rotation, headAim) <= 8f
            : Quaternion.Angle(transform.rotation, bulletAim) <= 8f;
        if (!PhotonNetwork.IsMasterClient || Time.time < nextFireTime || !linedUp || !ClearSight(target)) return;

        nextFireTime = Time.time + Mathf.Max(.3f, fireRate);
        Vector3 muzzle = firePoint != null ? firePoint.position : transform.position + transform.up * MuzzleDistance();
        PhotonNetwork.InstantiateRoomObject(bulletPrefabName, muzzle, bulletAim, 0,
            new object[] { damage, true, true });
    }

    void SetupSplitTurretVisual()
    {
        if (!LoadTurretSprites()) return;

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer == null) return;

        float parentScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), .001f);
        float visualScale = Mathf.Min(DesiredArtWidth / TurretArtWidth, DesiredArtHeight / TurretArtHeight) / parentScale;
        int sortingLayer = rootRenderer.sortingLayerID;
        int sortingOrder = rootRenderer.sortingOrder;

        // The source sheet faces right. Keep the platform fixed and rotate only its gun assembly.
        transform.rotation = Quaternion.identity;
        CreatePart("TurretBase", cachedBaseSprite, new Vector2(-.38f, -.52f), visualScale,
            sortingLayer, sortingOrder);

        GameObject headObject = CreatePart("RotatingGunHead", cachedHeadSprite, new Vector2(-.38f, -.16f), visualScale,
            sortingLayer, sortingOrder + 1);
        turretHead = headObject.transform;

        GameObject muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.SetParent(turretHead, false);
        muzzle.transform.localPosition = new Vector3(.84f, .49f, 0f);
        firePoint = muzzle.transform;

        rootRenderer.enabled = false;
        Transform warningGlow = transform.Find("WarningGlow");
        if (warningGlow != null) warningGlow.gameObject.SetActive(false);
    }

    GameObject CreatePart(string partName, Sprite sprite, Vector2 localPosition, float visualScale,
        int sortingLayer, int sortingOrder)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(transform, false);
        part.transform.localPosition = new Vector3(localPosition.x * visualScale, localPosition.y * visualScale, 0f);
        part.transform.localScale = Vector3.one * visualScale;

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        return part;
    }

    static bool LoadTurretSprites()
    {
        if (attemptedSpriteLoad) return cachedBaseSprite != null && cachedHeadSprite != null;
        attemptedSpriteLoad = true;

        Texture2D sheet = Resources.Load<Texture2D>(RotationSheetResource);
        if (sheet == null || sheet.width < 4 || sheet.height < 4)
        {
            Debug.LogWarning("AutoTurret: rotation sheet not found; retaining the original turret artwork.");
            return false;
        }

        // Use the top-left frame: the lower circular platform and upper gun are cropped separately.
        const int cellX = 0;
        const int cellYFromTop = 0;
        int cellLeft = Mathf.RoundToInt(cellX * sheet.width / 4f);
        int cellRight = Mathf.RoundToInt((cellX + 1) * sheet.width / 4f);
        int cellTop = sheet.height - Mathf.RoundToInt(cellYFromTop * sheet.height / 4f);
        int cellBottom = sheet.height - Mathf.RoundToInt((cellYFromTop + 1) * sheet.height / 4f);
        int cellWidth = cellRight - cellLeft;
        int cellHeight = cellTop - cellBottom;

        int cropLeft = cellLeft + Mathf.RoundToInt(cellWidth * .18f);
        int cropRight = cellLeft + Mathf.RoundToInt(cellWidth * .68f);
        int baseTopFromImageTop = Mathf.RoundToInt(cellHeight * .53f);
        int headBottomFromImageTop = Mathf.RoundToInt(cellHeight * .60f);
        int baseBottom = cellBottom;
        int baseTop = cellTop - baseTopFromImageTop;
        int headBottom = cellTop - headBottomFromImageTop;
        int headTop = cellTop;

        float axisX = cellLeft + cellWidth * .423f;
        float baseCenterY = cellBottom + cellHeight * .24f;
        float axisY = cellTop - cellHeight * .58f;
        Rect baseRect = new Rect(cropLeft, baseBottom, cropRight - cropLeft, baseTop - baseBottom);
        Rect headRect = new Rect(cropLeft, headBottom, cropRight - cropLeft, headTop - headBottom);
        Vector2 basePivot = new Vector2((axisX - cropLeft) / baseRect.width,
            (baseCenterY - baseBottom) / baseRect.height);
        Vector2 headPivot = new Vector2((axisX - cropLeft) / headRect.width,
            (axisY - headBottom) / headRect.height);

        cachedBaseSprite = Sprite.Create(sheet, baseRect, basePivot, 100f, 0, SpriteMeshType.FullRect);
        cachedBaseSprite.name = "MechTurret_Base_Runtime";
        cachedHeadSprite = Sprite.Create(sheet, headRect, headPivot, 100f, 0, SpriteMeshType.FullRect);
        cachedHeadSprite.name = "MechTurret_RotatingHead_Runtime";
        return cachedBaseSprite != null && cachedHeadSprite != null;
    }

    float MuzzleDistance()
    {
        var shape = GetComponent<Collider2D>();
        return shape != null ? shape.bounds.extents.magnitude + .5f : 2.5f;
    }

    bool ClearSight(PlayerController player)
    {
        Vector2 delta = player.transform.position - transform.position;
        foreach (var hit in Physics2D.RaycastAll(transform.position, delta.normalized, delta.magnitude))
        {
            if (hit.collider.isTrigger || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (hit.collider.GetComponentInParent<BulletController>() != null || hit.collider.GetComponentInParent<SkillController>() != null) continue;
            return false;
        }
        return delta.magnitude > MuzzleDistance();
    }
}
