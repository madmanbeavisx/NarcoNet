using UnityEngine;

namespace NarcoNet.Utilities;

public static class ColorExtensions
{
    public static Color SetAlpha(this Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }
}
