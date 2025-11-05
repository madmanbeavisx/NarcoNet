using UnityEngine;

namespace NarcoNet.UI;

public static class Colors
{
    // Primary Colors - Modern Blue/Indigo Palette (more contemporary)
    public static readonly Color Primary = new Color32(79, 70, 229, 255);      // Vibrant indigo
    public static readonly Color PrimaryDark = new Color32(67, 56, 202, 255);  // Deeper indigo
    public static readonly Color PrimaryLight = new Color32(99, 102, 241, 255); // Lighter indigo
    public static readonly Color PrimaryVeryLight = new Color32(129, 140, 248, 255); // Very light indigo
    public static readonly Color PrimaryGlow = new Color32(79, 70, 229, 100);

    // Secondary Colors - Modern muted red/rose
    public static readonly Color Secondary = new Color32(225, 29, 72, 255);    // Modern rose
    public static readonly Color SecondaryDark = new Color32(190, 18, 60, 255);
    public static readonly Color SecondaryLight = new Color32(244, 63, 94, 255);

    // Neutral Colors - Modern dark theme
    public static readonly Color White = new Color32(248, 250, 252, 255);      // Near white
    public static readonly Color OffWhite = new Color32(226, 232, 240, 255);   // Light slate
    public static readonly Color Dark = new Color32(15, 23, 42, 255);          // Deep slate
    public static readonly Color DarkMedium = new Color32(30, 41, 59, 255);    // Medium slate
    public static readonly Color Grey = new Color32(148, 163, 184, 255);       // Slate grey
    public static readonly Color GreyLight = new Color32(203, 213, 225, 255);  // Light slate grey
    public static readonly Color GreyDark = new Color32(71, 85, 105, 255);     // Dark slate grey

    // Semantic Colors - Modern vibrant
    public static readonly Color Success = new Color32(34, 197, 94, 255);      // Modern green
    public static readonly Color SuccessDark = new Color32(22, 163, 74, 255);
    public static readonly Color Warning = new Color32(251, 146, 60, 255);     // Modern orange
    public static readonly Color WarningDark = new Color32(249, 115, 22, 255);
    public static readonly Color Error = new Color32(239, 68, 68, 255);        // Modern red
    public static readonly Color ErrorDark = new Color32(220, 38, 38, 255);
    public static readonly Color Info = new Color32(59, 130, 246, 255);        // Modern blue
    public static readonly Color InfoDark = new Color32(37, 99, 235, 255);

    // Accent & Effect Colors
    public static readonly Color Accent = new Color32(56, 189, 248, 255);      // Bright cyan
    public static readonly Color Shadow = new Color32(0, 0, 0, 100);           // Slightly stronger
    public static readonly Color ShadowStrong = new Color32(0, 0, 0, 150);
    public static readonly Color Overlay = new Color32(0, 0, 0, 180);          // Darker overlay
    public static readonly Color Highlight = new Color32(255, 255, 255, 50);   // Brighter highlight

    // Gradient Colors (for modern effects)
    public static readonly Color GradientStart = new Color32(79, 70, 229, 255);
    public static readonly Color GradientEnd = new Color32(99, 102, 241, 255);
}
