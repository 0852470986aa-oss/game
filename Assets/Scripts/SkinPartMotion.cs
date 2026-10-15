using UnityEngine;

// Continuous weighted rig: stable fuselage, rotating wings and independent engine travel.
// Shared by UI, combat mesh and exhaust anchors; never moves the gameplay root or collider.
public static class SkinPartMotion
{
    public const int Columns = 20, Rows = 24;
    public static int Index(Sprite sprite) => sprite != null && sprite.name.StartsWith("Skin_")
        ? SkinCatalog.IndexOf(sprite.name.Substring(5)) : -1;

    public static Vector2 Deform(Vector2 point, float time, int skin, float aspect)
    {
        if (skin < 0 || skin >= SkinCatalog.All.Length) return point;
        float phase = time * (1.5f + skin % 4 * .16f) + skin * .71f;
        bool organic = skin == 2 || skin == 5 || skin == 8 || skin == 9;
        float side = point.x < .5f ? -1 : 1;
        float wingWeight = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.32f,Mathf.Abs(point.x-.5f)));
        Vector2 hinge = new Vector2(.5f + side*.16f,.48f);
        Vector2 delta = point-hinge;
        delta.x *= aspect;
        float angle = side * Mathf.Sin(phase) * (organic ? 7f : 3.5f) * Mathf.Deg2Rad;
        Vector2 rotated = new Vector2(delta.x*Mathf.Cos(angle)-delta.y*Mathf.Sin(angle),
            delta.x*Mathf.Sin(angle)+delta.y*Mathf.Cos(angle));
        rotated.x /= Mathf.Max(.01f,aspect);
        Vector2 result = Vector2.Lerp(point,hinge+rotated,wingWeight);
        float engineWeight = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.42f,point.y));
        engineWeight *= 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.4f,Mathf.Abs(point.x-.5f)));
        result.y += Mathf.Sin(phase*1.6f+side*.5f) * .018f * engineWeight;
        return result;
    }
}
