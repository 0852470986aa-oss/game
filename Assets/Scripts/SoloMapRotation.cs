using UnityEngine;

public static class SoloMapRotation
{
    private static int lastMap = -1;
    public static int Next(int count, int previous = -1)
    {
        if (count <= 1) return 0;
        int skip = previous >= 0 && previous < count ? previous : lastMap;
        int map = Random.Range(0, skip >= 0 && skip < count ? count - 1 : count);
        if (skip >= 0 && skip < count && map >= skip) map++;
        lastMap = map;
        return map;
    }
}
