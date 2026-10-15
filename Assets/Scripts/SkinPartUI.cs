using UnityEngine;
using UnityEngine.UI;

// UI uses the same rig as combat; no whole-picture floating and no extra raycast targets.
[RequireComponent(typeof(Image))]
public sealed class SkinPartUI : BaseMeshEffect
{
    public float previewTime = -1;
    private void LateUpdate() { if(IsActive()) graphic.SetVerticesDirty(); }
    public override void ModifyMesh(VertexHelper vh)
    {
        if(!IsActive()) return;
        var image=(Image)graphic;
        var sprite=image.overrideSprite!=null ? image.overrideSprite : image.sprite;
        int index=SkinPartMotion.Index(sprite);
        if(index<0) return;
        Rect rect=image.GetPixelAdjustedRect();
        float aspect=sprite.rect.width/sprite.rect.height;
        if(image.preserveAspect)
        {
            Vector2 center=rect.center;
            if(rect.width/Mathf.Max(.01f,rect.height)>aspect) rect.width=rect.height*aspect;
            else rect.height=rect.width/aspect;
            rect.center=center;
        }
        float time=previewTime>=0 ? previewTime : Time.unscaledTime;
        vh.Clear();
        int cols=SkinPartMotion.Columns,rows=SkinPartMotion.Rows;
        for(int y=0;y<=rows;y++) for(int x=0;x<=cols;x++)
        {
            Vector2 uvPoint=new Vector2(x/(float)cols,y/(float)rows);
            Vector2 p=SkinPartMotion.Deform(uvPoint,time,index,aspect);
            Vector2 uv=new Vector2((sprite.rect.x+uvPoint.x*sprite.rect.width)/sprite.texture.width,
                (sprite.rect.y+uvPoint.y*sprite.rect.height)/sprite.texture.height);
            vh.AddVert(new Vector3(rect.x+p.x*rect.width,rect.y+p.y*rect.height,0),image.color,uv);
            if(x==cols || y==rows) continue;
            int i=y*(cols+1)+x;
            vh.AddTriangle(i,i+cols+1,i+1); vh.AddTriangle(i+1,i+cols+1,i+cols+2);
        }
    }
}
