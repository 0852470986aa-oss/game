// PowerUpManager.cs — ไอเท็มเกิดในแม็พระหว่างแข่ง (เฟส 7) ติดกับ GameplayManager อัตโนมัติตอนเริ่มฉาก
// Master Client เป็นคนสุ่มเกิดไอเท็ม (ทุก 10 วินาที มีพร้อมกันไม่เกิน 4 ชิ้น หายเองใน 25 วินาที)
// ส่งข้อมูลด้วย PhotonNetwork.RaiseEvent (ไม่ต้องมี Prefab/PhotonView):
//   61 SPAWN (Master → ทุกคน): id, ชนิด, x, y
//   62 CLAIM (ผู้เล่น → Master): id, CombatantId ของคนเก็บ — ใครส่งถึง Master ก่อนได้ไป
//   63 TAKEN (Master → ทุกคน): id, CombatantId ของคนได้ (-1 = หมดเวลา), ชนิด
// คนที่ได้ไอเท็มใช้ผลบนเครื่องเจ้าของยาน (PlayerController.ApplyPowerUp) / บอทใช้ผลบนเครื่อง Master
// ชนิด: 0 REPAIR (+40% HP), 1 OVERDRIVE (ยิงเร็ว 2 เท่า 8 วิ), 2 BOOST (วิ่ง +35% 8 วิ), 3 SHIELD (โล่), 4 DAMAGE (แรง +50% 8 วิ)
// รูป: Resources/Images/PowerUps/powerup_{ชื่อ}.png (ยังไม่มี = วงกลมสี + ตัวอักษร)
// ปิดได้ด้วย FeatureFlags.PowerUps หรือปุ่ม POWER-UPS OFF ในห้องรอ
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

// คลาสจัดการไอเท็มในแม็พ: Master สุ่มเกิด/ตัดสินคนเก็บ ทุกเครื่องแสดงผลและใช้ผลไอเท็ม
public class PowerUpManager : MonoBehaviour, IOnEventCallback
{
    private const byte EvSpawn = 61, EvClaim = 62, EvTaken = 63; // รหัส Event ของ Photon: เกิดไอเท็ม / ขอเก็บ / มีคนได้ไป
    public static readonly string[] Names = { "REPAIR", "OVERDRIVE", "BOOST", "SHIELD", "DAMAGE" }; // ชื่อไอเท็มตามชนิด ใช้แสดงผลและหารูป
    // สีประจำไอเท็มแต่ละชนิด (ใช้ตอนไม่มีรูป)
    private static readonly Color[] Colors =
    {
        new Color(.35f, 1f, .5f), new Color(1f, .6f, .2f), new Color(.35f, .8f, 1f), new Color(.6f, .6f, 1f), new Color(1f, .3f, .35f)
    };
    private const int MaxActive = 4; // จำนวนไอเท็มในแม็พพร้อมกันสูงสุด
    private const float SpawnEvery = 10f, Lifetime = 25f, PickupRadius = 2.2f; // รอบเกิดไอเท็ม, อายุก่อนหาย (วินาที), ระยะที่ยานเก็บได้

    // ข้อมูลไอเท็ม 1 ชิ้นที่อยู่ในแม็พ: id, ชนิด, GameObject ที่แสดง, เวลาหมดอายุ และส่งขอเก็บไปแล้วหรือยัง
    private sealed class Pickup
    {
        public int id, type; // id ไอเท็ม และชนิดไอเท็ม (index ของ Names)
        public GameObject view; // GameObject ที่แสดงไอเท็มในแม็พ
        public float expireAt; // เวลาที่ไอเท็มหมดอายุหายไป
        public bool claimSent; // true = ส่งขอเก็บไปแล้ว กันส่งซ้ำ
    }

    private readonly Dictionary<int, Pickup> active = new Dictionary<int, Pickup>(); // ไอเท็มที่อยู่ในแม็พตอนนี้ (id -> ข้อมูล)
    private float nextSpawnAt = -1f; // เวลาที่ Master จะเกิดไอเท็มชิ้นถัดไป
    private int counter; // ตัวนับสร้าง id ไอเท็มไม่ซ้ำ
    private static Sprite circleSprite; // Sprite วงกลมที่สร้างด้วยโค้ด (แคชไว้)

    // ลงทะเบียนรับ Event ของ Photon (OnEvent) เมื่อเปิดคอมโพเนนต์
    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    // ยกเลิกการรับ Event ของ Photon เมื่อปิดคอมโพเนนต์
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    private static bool Enabled => PhotonNetwork.InRoom && MatchRules.PowerUpsEnabled(PhotonNetwork.CurrentRoom); // ห้องนี้เปิดไอเท็มหรือไม่

    // ทุกเฟรม: หมุนไอเท็ม, Master เกิดไอเท็มตามรอบ/ลบที่หมดเวลา/ให้บอทเก็บ
    // และถ้ายานเราแตะไอเท็มให้ส่งขอเก็บไปที่ Master
    private void Update()
    {
        var game = GameplayManager.Instance;
        if (!Enabled || game == null) return;
        bool playing = game.MatchInputAllowed;
        // หมุน/ลอยไอเท็ม
        foreach (var pickup in active.Values)
            if (pickup.view != null)
            {
                pickup.view.transform.Rotate(0, 0, 90f * Time.deltaTime);
                pickup.view.SetActive(playing);
            }
        if (!playing) return;

        if (PhotonNetwork.IsMasterClient)
        {
            if (nextSpawnAt < 0) nextSpawnAt = Time.time + 8f;
            if (Time.time >= nextSpawnAt)
            {
                nextSpawnAt = Time.time + SpawnEvery;
                if (active.Count < MaxActive) TrySpawn();
            }
            // หมดเวลา / บอทเก็บ
            var expired = new List<int>();
            foreach (var pickup in active.Values) if (Time.time >= pickup.expireAt && !pickup.claimSent) expired.Add(pickup.id);
            foreach (int id in expired)
            {
                if (!active.TryGetValue(id, out var pickup)) continue;
                pickup.claimSent = true; // ส่งครั้งเดียว (รอเหตุการณ์ย้อนกลับมา)
                Send(EvTaken, new object[] { id, -1, pickup.type }, ReceiverGroup.All);
            }
            foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!ship.IsBot || !ship.photonView.IsMine || ship.isDead) continue;
                int id = Touching(ship);
                if (id < 0 || !active.TryGetValue(id, out var pickup) || pickup.claimSent) continue;
                pickup.claimSent = true;
                Send(EvTaken, new object[] { id, ship.CombatantId, pickup.type }, ReceiverGroup.All);
            }
        }

        // ยานของเราแตะไอเท็ม → ขอ Master
        var me = game.localPlayer;
        if (me != null && !me.isDead && !me.HasMatchEnded)
        {
            int id = Touching(me);
            if (id >= 0 && !active[id].claimSent)
            {
                active[id].claimSent = true;
                Send(EvClaim, new object[] { id, me.CombatantId }, ReceiverGroup.MasterClient);
            }
        }
    }

    // คืน id ของไอเท็มที่ยานนี้อยู่ในระยะเก็บ (PickupRadius) ไม่แตะอะไรคืน -1
    private int Touching(PlayerController ship)
    {
        foreach (var pickup in active.Values)
            if (pickup.view != null && Vector2.Distance(ship.transform.position, pickup.view.transform.position) < PickupRadius) return pickup.id;
        return -1;
    }

    // Master: สุ่มจุดที่ว่าง (ไม่ทับสิ่งกีดขวาง/อันตราย และห่างยานพอสมควร)
    private void TrySpawn()
    {
        int map = GameplayManager.GetCurrentMapIndex();
        Vector2 min = GameplayManager.GetArenaMin(map) + Vector2.one * 5f, max = GameplayManager.GetArenaMax(map) - Vector2.one * 5f;
        for (int attempt = 0; attempt < 25; attempt++)
        {
            Vector2 point = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
            if (GameplayManager.IsSpawnAreaBlocked(point, 1.6f, null)) continue;
            bool nearShip = false;
            foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (!ship.isDead && Vector2.Distance(ship.transform.position, point) < 5f) { nearShip = true; break; }
            if (nearShip) continue;
            int id = PhotonNetwork.LocalPlayer.ActorNumber * 10000 + (++counter);
            Send(EvSpawn, new object[] { id, Random.Range(0, Names.Length), point.x, point.y }, ReceiverGroup.All);
            return;
        }
    }

    // ส่งเหตุการณ์ (โหมดออฟไลน์เรียกตัวรับในเครื่องตรงๆ)
    private void Send(byte code, object[] data, ReceiverGroup receivers)
    {
        if (PhotonNetwork.OfflineMode) { Handle(code, data); return; }
        PhotonNetwork.RaiseEvent(code, data, new RaiseEventOptions { Receivers = receivers }, SendOptions.SendReliable);
    }

    // Photon เรียกเมื่อได้รับ RaiseEvent: ส่งต่อเฉพาะรหัส 61-63 ของไอเท็มให้ Handle
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == EvSpawn || photonEvent.Code == EvClaim || photonEvent.Code == EvTaken)
            Handle(photonEvent.Code, photonEvent.CustomData as object[]);
    }

    // จัดการ Event ไอเท็ม: SPAWN = สร้างไอเท็ม, CLAIM = Master ยืนยันคนเก็บ,
    // TAKEN = ลบไอเท็ม แล้วเจ้าของยานใช้ผล + ขึ้นป้ายชื่อไอเท็มถ้าเป็นเรา
    private void Handle(byte code, object[] data)
    {
        if (data == null) return;
        if (code == EvSpawn && data.Length >= 4)
        {
            int id = (int)data[0];
            if (active.ContainsKey(id)) return;
            int type = Mathf.Clamp((int)data[1], 0, Names.Length - 1);
            active[id] = new Pickup { id = id, type = type, expireAt = Time.time + Lifetime, view = CreateView(type, new Vector2((float)data[2], (float)data[3])) };
        }
        else if (code == EvClaim && data.Length >= 2 && PhotonNetwork.IsMasterClient)
        {
            int id = (int)data[0];
            if (active.TryGetValue(id, out var pickup)) Send(EvTaken, new object[] { id, (int)data[1], pickup.type }, ReceiverGroup.All);
        }
        else if (code == EvTaken && data.Length >= 3)
        {
            int id = (int)data[0], taker = (int)data[1], type = (int)data[2];
            if (!active.TryGetValue(id, out var pickup)) return;
            if (pickup.view != null) Destroy(pickup.view);
            active.Remove(id);
            if (taker < 0) return;
            var ship = PlayerController.FindCombatant(taker);
            if (ship == null) return;
            if (ship.photonView.IsMine) ship.ApplyPowerUp(type);
            if (AudioManager.Instance != null && ship.IsLocalHuman) AudioManager.Instance.PlaySFX("SFX_PowerUp"); // ปิด NewSounds = เสียงโดนโล่แบบเดิม
            var game = GameplayManager.Instance;
            if (game != null && ship.IsLocalHuman) game.ShowBanner(Names[type] + "!", Colors[type]);
        }
    }

    // ภาพไอเท็ม: รูปจาก Resources ถ้ามี ไม่มีใช้วงกลมสี + ตัวอักษรแรก
    private GameObject CreateView(int type, Vector2 position)
    {
        var go = new GameObject("PowerUp_" + Names[type]);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        var art = Resources.Load<Sprite>("Images/PowerUps/powerup_" + Names[type].ToLowerInvariant());
        renderer.sortingOrder = 5;
        if (art != null)
        {
            renderer.sprite = art;
            go.transform.localScale = Vector3.one * (2.2f / Mathf.Max(.01f, art.bounds.size.x));
        }
        else
        {
            renderer.sprite = CircleSprite();
            renderer.color = Colors[type];
            go.transform.localScale = Vector3.one * 2.2f;
            var label = new GameObject("Letter");
            label.transform.SetParent(go.transform, false);
            var text = label.AddComponent<TMPro.TextMeshPro>();
            text.text = Names[type].Substring(0, 1);
            text.fontSize = 5;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.black;
            text.sortingOrder = 6;
            text.rectTransform.sizeDelta = new Vector2(1, 1);
        }
        return go;
    }

    // สร้าง Sprite วงกลมขอบนุ่ม 64x64 ด้วยโค้ด (สร้างครั้งเดียวแล้วแคช)
    // ใช้แทนรูปไอเท็มที่ยังไม่มี และใช้เป็นพื้นวงของโหมดเกม
    internal static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 64; // ขนาดภาพวงกลม (พิกเซล)
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f - .5f, size / 2f - .5f)) / (size / 2f);
                byte a = d < .82f ? (byte)255 : d < 1f ? (byte)(255 * (1f - (d - .82f) / .18f)) : (byte)0;
                byte rim = d > .7f && d < .82f ? (byte)200 : (byte)255;
                pixels[y * size + x] = new Color32(rim, rim, rim, a);
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        return circleSprite;
    }

    // ลบ GameObject ของไอเท็มที่ยังค้างในแม็พทั้งหมดเมื่อ Manager ถูกทำลาย
    private void OnDestroy()
    {
        foreach (var pickup in active.Values) if (pickup.view != null) Destroy(pickup.view);
    }
}
