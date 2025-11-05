using UnityEngine;

namespace NarcoNet.UI;

public static class Colors
{
    // Primary Colors - Dark mode friendly purple/violet
    public static readonly Color Primary = new Color32(167, 139, 250, 255);      // Violet
    public static readonly Color PrimaryDark = new Color32(139, 92, 246, 255);  // Deep violet
    public static readonly Color PrimaryLight = new Color32(196, 181, 253, 255); // Light violet
    public static readonly Color PrimaryVeryLight = new Color32(221, 214, 254, 255); // Very light violet
    public static readonly Color PrimaryGlow = new Color32(167, 139, 250, 100);

    // Secondary Colors - Magenta/fuchsia accent
    public static readonly Color Secondary = new Color32(232, 121, 249, 255);    // Fuchsia
    public static readonly Color SecondaryDark = new Color32(217, 70, 239, 255);
    public static readonly Color SecondaryLight = new Color32(245, 158, 255, 255);

    // Neutral Colors - Modern dark theme
    public static readonly Color White = new Color32(248, 250, 252, 255);      // Near white
    public static readonly Color OffWhite = new Color32(226, 232, 240, 255);   // Light slate
    public static readonly Color Dark = new Color32(15, 23, 42, 255);          // Deep slate
    public static readonly Color DarkMedium = new Color32(30, 41, 59, 255);    // Medium slate
    public static readonly Color Grey = new Color32(148, 163, 184, 255);       // Slate grey
    public static readonly Color GreyLight = new Color32(203, 213, 225, 255);  // Light slate grey
    public static readonly Color GreyDark = new Color32(71, 85, 105, 255);     // Dark slate grey

    // Semantic Colors - Modern vibrant with purple tints
    public static readonly Color Success = new Color32(134, 239, 172, 255);      // Light green
    public static readonly Color SuccessDark = new Color32(74, 222, 128, 255);
    public static readonly Color Warning = new Color32(251, 191, 36, 255);     // Amber
    public static readonly Color WarningDark = new Color32(245, 158, 11, 255);
    public static readonly Color Error = new Color32(248, 113, 113, 255);        // Light red
    public static readonly Color ErrorDark = new Color32(239, 68, 68, 255);
    public static readonly Color Info = new Color32(196, 181, 253, 255);        // Light violet
    public static readonly Color InfoDark = new Color32(167, 139, 250, 255);

    // Accent & Effect Colors
    public static readonly Color Accent = new Color32(192, 132, 252, 255);      // Purple accent
    public static readonly Color Shadow = new Color32(0, 0, 0, 100);           // Slightly stronger
    public static readonly Color ShadowStrong = new Color32(0, 0, 0, 150);
    public static readonly Color Overlay = new Color32(0, 0, 0, 180);          // Darker overlay
    public static readonly Color Highlight = new Color32(255, 255, 255, 50);   // Brighter highlight

    // Gradient Colors (for modern effects)
    public static readonly Color GradientStart = new Color32(139, 92, 246, 255); // Deep violet
    public static readonly Color GradientEnd = new Color32(196, 181, 253, 255);  // Light violet
}
