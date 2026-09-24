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
            var texture = Resources.Load<Texture2D>("Images/Obs_PrismSwamps");
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
        rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);
        rect.anchoredPosition=new Vector2(-25,0); rect.sizeDelta=new Vector2(36,36);
        var image=obj.GetComponent<UnityEngine.UI.Image>(); image.sprite=Coin(); image.preserveAspect=true; image.raycastTarget=false;
        image.enabled=image.sprite != null;
    }
}
