using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class SkinPartMesh : MonoBehaviour
{
    private SpriteRenderer source;
    private PlayerController owner;
    private Sprite sprite;
    private Mesh mesh;
    private MeshRenderer view;
    private Material material;
    private Vector3[] vertices;
    private MaterialPropertyBlock properties;
    public void Configure(SpriteRenderer renderer)
    {
        source = renderer;
        owner = GetComponent<PlayerController>();
        if (SkinPartMotion.Index(source.sprite)<0) { Deactivate(); return; }
        enabled=true;
        Build();
        Sample(Time.time);
    }
    private void Build()
    {
        sprite=source.sprite;
        if(view==null)
        {
            var child=new GameObject("AnimatedSkinParts",typeof(MeshFilter),typeof(MeshRenderer));
            child.transform.SetParent(transform,false);
            view=child.GetComponent<MeshRenderer>();
            material=new Material(Shader.Find("Sprites/Default"));
            view.sharedMaterial=material;
            mesh=new Mesh { name="SkinPartRig" }; mesh.MarkDynamic();
            child.GetComponent<MeshFilter>().sharedMesh=mesh;
            properties=new MaterialPropertyBlock();
        }
        int cols=SkinPartMotion.Columns, rows=SkinPartMotion.Rows;
        vertices=new Vector3[(cols+1)*(rows+1)];
        var uv=new Vector2[vertices.Length];
        var colors=new Color[vertices.Length];
        var triangles=new int[cols*rows*6];
        for(int y=0;y<=rows;y++) for(int x=0;x<=cols;x++)
        {
            int i=y*(cols+1)+x;
            uv[i]=new Vector2((sprite.rect.x+sprite.rect.width*x/cols)/sprite.texture.width,
                (sprite.rect.y+sprite.rect.height*y/rows)/sprite.texture.height);
            colors[i]=Color.white;
            if(x==cols || y==rows) continue;
            int t=(y*cols+x)*6;
            triangles[t]=i; triangles[t+1]=i+cols+1; triangles[t+2]=i+1;
            triangles[t+3]=i+1; triangles[t+4]=i+cols+1; triangles[t+5]=i+cols+2;
        }
        mesh.Clear(); mesh.vertices=vertices; mesh.uv=uv; mesh.colors=colors; mesh.triangles=triangles;
        material.mainTexture=sprite.texture;
    }
    public void Sample(float time)
    {
        if(source==null || source.sprite==null) return;
        int index=SkinPartMotion.Index(source.sprite);
        if(index<0) { Deactivate(); return; }
        if(sprite!=source.sprite) Build();
        bool visible=source.enabled && (owner==null || (!owner.isDead && !owner.HiddenFromLocalViewer));
        source.forceRenderingOff=true; // Preserve sprite bounds and enabled/color state for all existing systems.
        view.enabled=visible;
        if(!visible) return;
        Bounds bounds=sprite.bounds;
        for(int y=0;y<=SkinPartMotion.Rows;y++) for(int x=0;x<=SkinPartMotion.Columns;x++)
        {
            Vector2 p=SkinPartMotion.Deform(new Vector2(x/(float)SkinPartMotion.Columns,y/(float)SkinPartMotion.Rows),time,index,bounds.size.x/bounds.size.y);
            if(source.flipX) p.x=1-p.x;
            if(source.flipY) p.y=1-p.y;
            vertices[y*(SkinPartMotion.Columns+1)+x]=bounds.min+Vector3.Scale(bounds.size,new Vector3(p.x,p.y,0));
        }
        mesh.vertices=vertices; mesh.RecalculateBounds();
        view.sortingLayerID=source.sortingLayerID; view.sortingOrder=source.sortingOrder;
        properties.SetColor("_Color",source.color); view.SetPropertyBlock(properties);
    }
    private void LateUpdate() => Sample(Time.time);
    public void Deactivate() { enabled=false; OnDisable(); }
    private void OnDisable() { if(source!=null) source.forceRenderingOff=false; if(view!=null) view.enabled=false; }
    private void OnDestroy()
    {
        OnDisable();
        Release(mesh); Release(material);
        if(view!=null) Release(view.gameObject);
    }
    private static void Release(Object value) { if(value==null)return; if(Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
}
