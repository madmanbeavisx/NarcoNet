using UnityEngine;

namespace NarcoNet.UI;

public static class Utility
{
    private static readonly Dictionary<Color, Texture2D> Textures = [];
    private static readonly Dictionary<string, Texture2D> GradientTextures = [];
    private static readonly Dictionary<string, Texture2D> RoundedTextures = [];

    public static Texture2D GetTexture(Color color)
    {
        if (Textures.TryGetValue(color, out Texture2D? texture1))
        {
            return texture1;
        }

        Texture2D texture = new(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();

        Textures.Add(color, texture);
        return texture;
    }

    public static Texture2D GetGradientTexture(Color colorStart, Color colorEnd, int width = 1, int height = 256,
        bool horizontal = false)
    {
        var key = $"{colorStart}_{colorEnd}_{width}_{height}_{horizontal}";
        if (GradientTextures.TryGetValue(key, out Texture2D? cachedTexture))
        {
            return cachedTexture;
        }

        Texture2D texture = new(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                float t = horizontal ? (float)x / width : (float)y / height;
                Color color = Color.Lerp(colorStart, colorEnd, t);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        GradientTextures.Add(key, texture);
        return texture;
    }

    public static Texture2D GetRoundedTexture(int width, int height, int radius, Color color, Color? borderColor = null,
        int borderWidth = 0)
    {
        var key = $"{width}_{height}_{radius}_{color}_{borderColor}_{borderWidth}";
        if (RoundedTextures.TryGetValue(key, out Texture2D? cachedTexture))
        {
            return cachedTexture;
        }

        Texture2D texture = new(width, height);
        Color transparent = new(0, 0, 0, 0);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                float distanceFromEdge = GetDistanceFromRoundedRectEdge(x, y, width, height, radius);

                if (distanceFromEdge < 0)
                {
                    texture.SetPixel(x, y, transparent);
                }
                else if (borderColor.HasValue && borderWidth > 0 && distanceFromEdge < borderWidth)
                {
                    texture.SetPixel(x, y, borderColor.Value);
                }
                else
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }

        texture.Apply();
        RoundedTextures.Add(key, texture);
        return texture;
    }

    private static float GetDistanceFromRoundedRectEdge(int x, int y, int width, int height, int radius)
    {
        if (x < radius && y < radius)
        {
            // Top-left corner
            float dist = Mathf.Sqrt((x - radius) * (x - radius) + (y - radius) * (y - radius));
            return radius - dist;
        }

        if (x > width - radius && y < radius)
        {
            // Top-right corner
            float dist = Mathf.Sqrt((x - (width - radius)) * (x - (width - radius)) + (y - radius) * (y - radius));
            return radius - dist;
        }

        if (x < radius && y > height - radius)
        {
            // Bottom-left corner
            float dist = Mathf.Sqrt((x - radius) * (x - radius) + (y - (height - radius)) * (y - (height - radius)));
            return radius - dist;
        }

        if (x > width - radius && y > height - radius)
        {
            // Bottom-right corner
            float dist = Mathf.Sqrt((x - (width - radius)) * (x - (width - radius)) +
                                    (y - (height - radius)) * (y - (height - radius)));
            return radius - dist;
        }

        // Inside the rectangle (not in corners)
        return Mathf.Min(
            Mathf.Min(x, width - x),
            Mathf.Min(y, height - y)
        );
    }

    public static void DrawShadow(Rect rect, int offsetX = 0, int offsetY = 4, int blur = 8, Color? shadowColor = null)
    {
        Color shadow = shadowColor ?? Colors.Shadow;
        Rect shadowRect = new(rect.x + offsetX, rect.y + offsetY, rect.width, rect.height);

        // Simple shadow approximation using multiple layers
        for (int i = blur; i > 0; i--)
        {
            float alpha = (1f - (float)i / blur) * shadow.a;
            Color layerColor = new(shadow.r, shadow.g, shadow.b, alpha);
            Rect layerRect = new(
                shadowRect.x - i / 2f,
                shadowRect.y - i / 2f,
                shadowRect.width + i,
                shadowRect.height + i
            );
            GUI.DrawTexture(layerRect, GetTexture(layerColor), ScaleMode.StretchToFill, true);
        }
    }

    public static float SmoothStep(float t)
    {
        // Smooth interpolation curve
        return t * t * (3f - 2f * t);
    }

    public static Color LerpColor(Color from, Color to, float t, bool smooth = true)
    {
        if (smooth)
        {
            t = SmoothStep(t);
        }

        return Color.Lerp(from, to, t);
    }
}
