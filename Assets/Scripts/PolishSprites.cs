using UnityEngine;

// ตัวช่วยโหลดภาพเหรียญและบึง รวมถึงสร้างไอคอนใน UI
public static class PolishSprites
{
    private static Sprite coin;
    private static Sprite[] swamps;
    public static Sprite Coin()
    {
        if (coin != null) return coin;
        var texture = Resources.Load<Texture2D>("Images/UI_AstroniumCoin");
        if (texture != null) coin = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
        return coin;
    }
    public static Sprite Swamp(int index)
    {
        if (swamps == null)
        {
            // This copy has a real alpha channel.  The source sheet has an opaque
            // black backdrop, which becomes a large dark rectangle in game.
            var texture = Resources.Load<Texture2D>("Images/Obs_PrismSwamps_Transparent");
            if(texture == null) return null;
            swamps = new Sprite[3];
            float[] edges={0,.28f,.60f,1};
            for(int i=0;i<3;i++) swamps[i]=Sprite.Create(texture,
                new Rect(edges[i]*texture.width,0,(edges[i+1]-edges[i])*texture.width,texture.height),new Vector2(.5f,.5f),100);
        }
        return swamps[Mathf.Clamp(index,0,2)];
    }
    public static void AddCoinIcon(TMPro.TMP_Text label)
    {
        if(label == null || label.transform.Find("CoinIcon") != null) return;
        var obj=new GameObject("CoinIcon",typeof(RectTransform),typeof(UnityEngine.UI.Image));
        var rect=obj.GetComponent<RectTransform>(); rect.SetParent(label.transform,false);
        rect.anchorMin=rect.anchorMax=label.rectTransform.pivot;
        rect.sizeDelta=new Vector2(32,32);
        var image=obj.GetComponent<UnityEngine.UI.Image>(); image.sprite=Coin(); image.preserveAspect=true; image.raycastTarget=false;
        image.enabled=image.sprite != null;
        // Follow the actual letters, not the left edge of the wide centered label.
        // TMP calls this again when the balance, font size or layout changes.
        label.OnPreRenderText += textInfo =>
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
        label.ForceMeshUpdate();
    }
}
