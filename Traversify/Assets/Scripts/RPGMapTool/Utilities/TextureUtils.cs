// Folder: Traversify/Assets/Scripts/RPGMapTool/Utilities
using UnityEngine;

namespace RPGMapTool.Utilities
{
    public static class TextureUtils
    {
        /// <summary>
        /// Returns a blocked mask for a texture.
        /// For example, white pixels might be considered blocked.
        /// </summary>
        public static bool[,] GetBlockedMask(Texture2D tex)
        {
            int width = tex.width;
            int height = tex.height;
            bool[,] mask = new bool[width, height];

            Color[] pixels = tex.GetPixels();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color p = pixels[y * width + x];
                    // For this example, consider pixels near white as blocked.
                    mask[x, y] = (p.r > 0.95f && p.g > 0.95f && p.b > 0.95f);
                }
            }
            return mask;
        }
    }
}