// ไฟล์: PolishSprites.cs — static helper (ไม่ต้องติดกับ GameObject) โหลดภาพจาก Resources/Images
// LobbyManager.Views ใช้ AddCoinIcon ใส่ไอคอนเหรียญหน้าตัวเลขเหรียญ
// HazardController ใช้ Swamp ดึงภาพบึง (ภาพชุด PrismSwamps)
using UnityEngine;
using System;
using System.Collections.Generic;

// ตัวช่วยโหลดภาพเหรียญและบึง รวมถึงสร้างไอคอนใน UI
// คลาส static: เก็บ Sprite ที่โหลดแล้วไว้ใช้ซ้ำ (cache) ไม่ต้องโหลดใหม่ทุกครั้ง
public static class PolishSprites
{
    // cache ของ sprite เหรียญ และ sprite บึง 3 แบบ
    private static Sprite coin;
    private static Sprite[] swamps;
    // จำ handler ที่ผูกกับแต่ละ label ไว้ เพื่อถอดอันเก่าก่อนผูกใหม่ (กันผูกซ้ำ)
    private static readonly Dictionary<TMPro.TMP_Text, Action<TMPro.TMP_TextInfo>> coinIconHandlers =
        new Dictionary<TMPro.TMP_Text, Action<TMPro.TMP_TextInfo>>();
    // คืน sprite เหรียญ (โหลด Images/UI_AstroniumCoin ครั้งแรกแล้ว cache) คืน null ถ้าไม่พบไฟล์
    public static Sprite Coin()
    {
        if (coin != null) return coin;
        var texture = Resources.Load<Texture2D>("Images/UI_AstroniumCoin");
        if (texture != null) coin = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
        return coin;
    }
    // คืน sprite บึงแบบที่ index (บังคับให้อยู่ในช่วง 0–2) ครั้งแรกจะโหลดภาพแล้วตัดเป็น 3 ชิ้น
    // ตามสัดส่วนความกว้าง 0–28%, 28–60%, 60–100% คืน null ถ้าไม่พบไฟล์ภาพ
    public static Sprite Swamp(int index)
    {
        if (swamps == null)
        {
            // This copy has a real alpha channel.  The source sheet has an opaque
            // black backdrop, which becomes a large dark rectangle in game.
            var texture = Resources.Load<Texture2D>("Images/Obs_PrismSwamps_Transparent");
            if(texture == null) return null;
            swamps = new Sprite[3];
            // จุดตัดแนวนอน (สัดส่วนของความกว้างภาพ) ของบึงแต่ละแบบ
            float[] edges={0,.28f,.60f,1};
            for(int i=0;i<3;i++) swamps[i]=Sprite.Create(texture,
                new Rect(edges[i]*texture.width,0,(edges[i+1]-edges[i])*texture.width,texture.height),new Vector2(.5f,.5f),100);
        }
        return swamps[Mathf.Clamp(index,0,2)];
    }
    // ใส่ไอคอนเหรียญ (Image ชื่อ CoinIcon) เป็นลูกของ label ถ้ามีอยู่แล้วใช้ของเดิม
    // แล้วผูก OnPreRenderText ให้ไอคอนอยู่ทางซ้ายของตัวอักษรตัวแรกที่มองเห็นเสมอ
    public static void AddCoinIcon(TMPro.TMP_Text label)
    {
        if (label == null) return;
        // 1) หาไอคอนเดิม หรือสร้างใหม่ขนาด 32x32 (ใน Editor ลงทะเบียน Undo ไว้)
        var icon = label.transform.Find("CoinIcon");
        RectTransform rect;
        UnityEngine.UI.Image image;
        if (icon == null)
        {
            var obj = new GameObject("CoinIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            rect = obj.GetComponent<RectTransform>();
            rect.SetParent(label.transform, false);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(obj, "Create editable coin icon");
#endif
            rect.anchorMin = rect.anchorMax = label.rectTransform.pivot;
            rect.sizeDelta = new Vector2(32, 32);
            image = obj.GetComponent<UnityEngine.UI.Image>();
        }
        else
        {
            rect = icon as RectTransform;
            image = icon.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
        }
        // 2) ตั้งภาพเหรียญ (ซ่อนไอคอนถ้าโหลดภาพไม่ได้)
        image.sprite = Coin(); image.preserveAspect = true; image.raycastTarget = false;
        image.enabled=image.sprite != null;
        // 3) ถอด handler เก่า แล้วสร้าง handler ใหม่ที่หาขอบซ้าย/ล่าง/บนของตัวอักษร แล้ววางไอคอนห่างขอบซ้าย 24 หน่วย
        // Follow the actual letters, not the left edge of the wide centered label.
        // TMP calls this again when the balance, font size or layout changes.
        if (coinIconHandlers.TryGetValue(label, out var previous)) label.OnPreRenderText -= previous;
        Action<TMPro.TMP_TextInfo> handler = textInfo =>
        {
            if (rect == null) return;
            float left = float.PositiveInfinity;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var character = textInfo.characterInfo[i];
                if (!character.isVisible) continue;
                left = Mathf.Min(left, character.bottomLeft.x);
                bottom = Mathf.Min(bottom, character.bottomLeft.y);
                top = Mathf.Max(top, character.topRight.y);
            }
            if (!float.IsPositiveInfinity(left))
                rect.anchoredPosition = new Vector2(left - 24f, (bottom + top) * .5f);
        };
        // 4) เก็บ handler ผูกกับ label และสั่ง ForceMeshUpdate ให้คำนวณตำแหน่งทันที
        coinIconHandlers[label] = handler;
        label.OnPreRenderText += handler;
        label.ForceMeshUpdate();
    }
}
