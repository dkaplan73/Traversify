using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Enums;

namespace RPGMapTool.Core
{
    public class PaintOnMap : MonoBehaviour
    {
        public enum ToolMode { Brush, MagicWand, Eraser, EyeDropper }

        [Header("Writable Overlays")]
        [Tooltip("RawImage for the Traversable Draw Layer.")]
        [SerializeField] private RawImage traversableOverlay;
        [Tooltip("RawImage for the Non-Traversable Draw Layer.")]
        [SerializeField] private RawImage nonTraversableOverlay;

        private Texture2D traversableTexture;
        private Texture2D nonTraversableTexture;

        // Default texture dimensions in case a blank texture is created.
        private const int defaultWidth = 1024;
        private const int defaultHeight = 1024;
        private readonly Color clearColor = new Color(0, 0, 0, 0);

        private ToolMode currentToolMode;

        private void Awake()
        {
            InitializeOverlay(ref traversableOverlay, ref traversableTexture, "Traversable");
            InitializeOverlay(ref nonTraversableOverlay, ref nonTraversableTexture, "Non-Traversable");
        }

        /// <summary>
        /// Toggles the active tool mode.
        /// </summary>
        public void ToggleToolMode(ToolMode mode)
        {
            currentToolMode = mode;
            Debug.Log($"PaintOnMap: Switched tool mode to {mode}");
        }

        private void InitializeOverlay(ref RawImage overlay, ref Texture2D texture, string overlayName)
        {
            if (overlay == null)
            {
                Debug.LogError($"PaintOnMap: {overlayName} overlay not assigned.");
                return;
            }
            if (overlay.texture == null)
            {
                Debug.LogWarning($"PaintOnMap: {overlayName} overlay texture missing. Creating new blank texture.");
                texture = CreateBlankTexture(defaultWidth, defaultHeight, clearColor);
                overlay.texture = texture;
            }
            else
            {
                Texture2D src = overlay.texture as Texture2D;
                if (src == null)
                {
                    Debug.LogWarning($"PaintOnMap: {overlayName} overlay texture is not a Texture2D. Creating new blank texture.");
                    texture = CreateBlankTexture(defaultWidth, defaultHeight, clearColor);
                    overlay.texture = texture;
                }
                else
                {
                    texture = Instantiate(src);
                    overlay.texture = texture;
                }
            }
        }

        private Texture2D CreateBlankTexture(int width, int height, Color fill)
        {
            Texture2D newTex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Color[] fillColors = new Color[width * height];
            for (int i = 0; i < fillColors.Length; i++)
                fillColors[i] = fill;
            newTex.SetPixels(fillColors);
            newTex.Apply();
            return newTex;
        }

        /// <summary>
        /// Applies a paint color to every pixel in the specified region.
        /// </summary>
        public void ApplyPaintRegion(List<Vector2Int> region, Color paintColor, AnnotationLayer layer)
        {
            Texture2D targetTexture = GetTargetTexture(layer);
            if (targetTexture == null)
            {
                Debug.LogError("PaintOnMap: Target texture is null.");
                return;
            }
            foreach (var pos in region)
            {
                if (pos.x >= 0 && pos.x < targetTexture.width && pos.y >= 0 && pos.y < targetTexture.height)
                    targetTexture.SetPixel(pos.x, pos.y, paintColor);
                else
                    Debug.LogWarning($"PaintOnMap: Pixel position {pos} out of bounds on {layer} layer.");
            }
            targetTexture.Apply();
            Debug.Log($"PaintOnMap: Painted {region.Count} pixels on {layer} layer with color {paintColor}.");
        }

        /// <summary>
        /// Brush tool: paints a circular region.
        /// </summary>
        public void BrushPaint(Vector2Int center, int brushSize, Color brushColor, AnnotationLayer layer)
        {
            Texture2D targetTexture = GetTargetTexture(layer);
            if (targetTexture == null)
            {
                Debug.LogError("PaintOnMap: Cannot perform BrushPaint. Target texture is null.");
                return;
            }
            List<Vector2Int> region = GetCircleRegion(center, brushSize, targetTexture.width, targetTexture.height);
            ApplyPaintRegion(region, brushColor, layer);
        }

        /// <summary>
        /// Magic wand tool: flood fills an area based on tolerance.
        /// </summary>
        public void MagicWandFill(Vector2Int start, Color fillColor, float tolerance, AnnotationLayer layer)
        {
            Texture2D targetTexture = GetTargetTexture(layer);
            if (targetTexture == null)
            {
                Debug.LogError("PaintOnMap: Cannot perform MagicWandFill. Target texture is null.");
                return;
            }
            if (start.x < 0 || start.x >= targetTexture.width || start.y < 0 || start.y >= targetTexture.height)
            {
                Debug.LogError("PaintOnMap: MagicWandFill start position out of bounds.");
                return;
            }
            Color targetColor = targetTexture.GetPixel(start.x, start.y);
            List<Vector2Int> region = FloodFill(targetTexture, start.x, start.y, targetColor, tolerance);
            if (region.Count == 0)
            {
                Debug.LogWarning("PaintOnMap: MagicWandFill found no region to fill.");
                return;
            }
            ApplyPaintRegion(region, fillColor, layer);
        }

        /// <summary>
        /// Eraser tool: clears a circular region.
        /// </summary>
        public void EraseRegion(List<Vector2Int> region, AnnotationLayer layer)
        {
            ApplyPaintRegion(region, clearColor, layer);
        }

        /// <summary>
        /// Samples a color from the specified layer at given pixel coordinates.
        /// </summary>
        public Color SampleColor(Vector2Int pixelPos, AnnotationLayer layer)
        {
            Texture2D targetTexture = GetTargetTexture(layer);
            if (targetTexture == null)
            {
                Debug.LogError("PaintOnMap: Cannot sample color. Target texture is null.");
                return Color.clear;
            }
            if (pixelPos.x < 0 || pixelPos.x >= targetTexture.width || pixelPos.y < 0 || pixelPos.y >= targetTexture.height)
            {
                Debug.LogError("PaintOnMap: SampleColor position out of bounds.");
                return Color.clear;
            }
            return targetTexture.GetPixel(pixelPos.x, pixelPos.y);
        }

        private Texture2D GetTargetTexture(AnnotationLayer layer)
        {
            if (layer == AnnotationLayer.Traversable)
                return traversableTexture;
            else if (layer == AnnotationLayer.NonTraversable)
                return nonTraversableTexture;
            else
            {
                Debug.LogError("PaintOnMap: Invalid layer specified.");
                return null;
            }
        }

        /// <summary>
        /// Returns a list of pixel coordinates forming a circle.
        /// </summary>
        private List<Vector2Int> GetCircleRegion(Vector2Int center, int radius, int maxWidth, int maxHeight)
        {
            List<Vector2Int> region = new List<Vector2Int>();
            int sqrRadius = radius * radius;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= sqrRadius)
                    {
                        Vector2Int pos = new Vector2Int(center.x + x, center.y + y);
                        if (pos.x >= 0 && pos.x < maxWidth && pos.y >= 0 && pos.y < maxHeight)
                            region.Add(pos);
                    }
                }
            }
            return region;
        }

        /// <summary>
        /// Flood fill algorithm based on a tolerance.
        /// </summary>
        private List<Vector2Int> FloodFill(Texture2D tex, int startX, int startY, Color targetColor, float tolerance)
        {
            List<Vector2Int> region = new List<Vector2Int>();
            bool[,] visited = new bool[tex.width, tex.height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                region.Add(current);

                foreach (Vector2Int neighbor in GetFourConnected(current, tex.width, tex.height))
                {
                    if (!visited[neighbor.x, neighbor.y])
                    {
                        Color sample = tex.GetPixel(neighbor.x, neighbor.y);
                        if (ColorsSimilar(sample, targetColor, tolerance))
                        {
                            visited[neighbor.x, neighbor.y] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            return region;
        }

        /// <summary>
        /// Returns 4-connected neighboring pixel coordinates.
        /// </summary>
        private IEnumerable<Vector2Int> GetFourConnected(Vector2Int coord, int width, int height)
        {
            if (coord.x + 1 < width) yield return new Vector2Int(coord.x + 1, coord.y);
            if (coord.x - 1 >= 0) yield return new Vector2Int(coord.x - 1, coord.y);
            if (coord.y + 1 < height) yield return new Vector2Int(coord.x, coord.y + 1);
            if (coord.y - 1 >= 0) yield return new Vector2Int(coord.x, coord.y - 1);
        }

        /// <summary>
        /// Determines whether two colors are similar within a tolerance.
        /// </summary>
        private bool ColorsSimilar(Color a, Color b, float tolerance)
        {
            float diff = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
            return diff <= tolerance;
        }
    }
}