// Folder: Traversify/Assets/Scripts/RPGMapTool/Utilities
namespace RPGMapTool.Utilities
{
    public static class ColorUtils
    {
        /// <summary>
        /// Returns a contrasting color (black or white) for legibility.
        /// </summary>
        public static UnityEngine.Color GetContrastingColor(UnityEngine.Color color)
        {
            float yiq = ((color.r * 255 * 299) + (color.g * 255 * 587) + (color.b * 255 * 114)) / 1000;
            return (yiq >= 128) ? UnityEngine.Color.black : UnityEngine.Color.white;
        }
    }
}