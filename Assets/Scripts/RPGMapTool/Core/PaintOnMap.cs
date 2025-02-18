using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Enums;

namespace RPGMapTool.Core
{
    /// <summary>
    /// Manages drawing overlays that share the same width, height, position, and aspect ratio as the base map.
    /// The overlays are realigned each frame so that any zooming or transform changes to the base map are reflected.
    /// Call LoadBaseMap(newBaseMap) once the base map is fully loaded.
    /// </summary>
    public class PaintOnMap : MonoBehaviour
    {
        [Header("Overlays (Assign via Inspector if desired)")]
        [Tooltip("RawImage for the Traversable Draw Layer.")]
        [SerializeField] private RawImage traversableOverlay;
        [Tooltip("RawImage for the Non-Traversable Draw Layer.")]
        [SerializeField] private RawImage nonTraversableOverlay;

        private Texture2D traversableTexture;
        private Texture2D nonTraversableTexture;

        // Use base map texture resolution for the overlays.
        private int texWidth = 1024;
        private int texHeight = 1024;
        // Clear color set to fully transparent.
        private readonly Color clearColor = new Color(0, 0, 0, 0);

        private RawImage baseMap;

        /// <summary>
        /// Loads the new base map and creates/resizes/repositions the overlays so they exactly match.
        /// </summary>
        public void LoadBaseMap(RawImage newBaseMap)
        {
            if (newBaseMap == null || newBaseMap.texture == null)
            {
                Debug.LogError("PaintOnMap: Base map or its texture is null.");
                return;
            }

            baseMap = newBaseMap;
            texWidth = baseMap.texture.width;
            texHeight = baseMap.texture.height;
            Debug.Log($"PaintOnMap: Base map loaded with texture size {texWidth} x {texHeight}");

            // Create overlays if necessary.
            if (traversableOverlay == null)
                traversableOverlay = CreateOverlay("TraversableOverlay");
            if (nonTraversableOverlay == null)
                nonTraversableOverlay = CreateOverlay("NonTraversableOverlay");

            // Initialize overlay textures (fill with clearColor) while keeping the set color.
            InitializeOverlay(ref traversableOverlay, ref traversableTexture, "Traversable");
            InitializeOverlay(ref nonTraversableOverlay, ref nonTraversableTexture, "Non-Traversable");

            // Reparent overlays so they share the same parent as the base map.
            traversableOverlay.transform.SetParent(baseMap.transform.parent, false);
            nonTraversableOverlay.transform.SetParent(baseMap.transform.parent, false);

            // Force the overlays to exactly match the base map.
            AlignOverlay(traversableOverlay);
            AlignOverlay(nonTraversableOverlay);

            Debug.Log("PaintOnMap: Overlays aligned to base map.");
        }

        /// <summary>
        /// Creates a new RawImage overlay.
        /// </summary>
        private RawImage CreateOverlay(string overlayName)
        {
            GameObject go = new GameObject(overlayName, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(this.transform, false);
            return go.GetComponent<RawImage>();
        }

        /// <summary>
        /// Initializes the overlay's texture to the base map resolution and fills it with clearColor.
        /// Does not override the overlay’s set color.
        /// </summary>
        private void InitializeOverlay(ref RawImage overlay, ref Texture2D texture, string overlayName)
        {
            texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[texWidth * texHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clearColor;
            texture.SetPixels(pixels);
            texture.Apply();
            overlay.texture = texture;
            Debug.Log($"PaintOnMap: Initialized {overlayName} overlay with texture {texWidth} x {texHeight}");
        }

        /// <summary>
        /// Aligns the overlay’s RectTransform exactly to the base map’s RectTransform.
        /// </summary>
        private void AlignOverlay(RawImage overlay)
        {
            if (baseMap == null || overlay == null)
            {
                Debug.LogError("PaintOnMap.AlignOverlay: Base map or overlay is null.");
                return;
            }
            RectTransform baseRT = baseMap.rectTransform;
            RectTransform overlayRT = overlay.rectTransform;
            overlayRT.anchorMin = baseRT.anchorMin;
            overlayRT.anchorMax = baseRT.anchorMax;
            overlayRT.pivot = baseRT.pivot;
            overlayRT.anchoredPosition = baseRT.anchoredPosition;
            overlayRT.sizeDelta = baseRT.rect.size;
            overlayRT.localScale = baseRT.localScale;
            overlayRT.localRotation = baseRT.localRotation;
        }

        /// <summary>
        /// In Update, the overlay alignment is refreshed so that any zooming or transform changes are reflected.
        /// </summary>
        private void Update()
        {
            if (baseMap != null)
            {
                AlignOverlay(traversableOverlay);
                AlignOverlay(nonTraversableOverlay);
            }
        }

        /// <summary>
        /// Paints on the specified layer using a circular brush.
        /// The drawing color is taken from the overlay's RawImage color.
        /// </summary>
        public void BrushPaint(Vector2Int center, int brushSize, Color unusedColor, AnnotationLayer layer)
        {
            Texture2D target = GetTargetTexture(layer);
            if (target == null)
            {
                Debug.LogError("PaintOnMap.BrushPaint: Target texture is null.");
                return;
            }
            RawImage targetOverlay = (layer == AnnotationLayer.Traversable) ? traversableOverlay : nonTraversableOverlay;
            Color paintColor = targetOverlay != null ? targetOverlay.color : unusedColor;
            List<Vector2Int> region = GetCircleRegion(center, brushSize, target.width, target.height);
            ApplyPaintRegion(region, paintColor, layer);
        }

        /// <summary>
        /// Applies the provided paint color to all pixels in the region.
        /// </summary>
        public void ApplyPaintRegion(List<Vector2Int> region, Color paintColor, AnnotationLayer layer)
        {
            Texture2D target = GetTargetTexture(layer);
            if (target == null)
            {
                Debug.LogError("PaintOnMap.ApplyPaintRegion: Target texture is null.");
                return;
            }
            foreach (Vector2Int pos in region)
            {
                if (pos.x >= 0 && pos.x < target.width && pos.y >= 0 && pos.y < target.height)
                    target.SetPixel(pos.x, pos.y, paintColor);
            }
            target.Apply();
            UpdateRawImage(layer);
        }

        /// <summary>
        /// Erases on the specified layer by filling with clearColor.
        /// </summary>
        public void EraseBrush(Vector2Int center, int eraserSize, AnnotationLayer layer)
        {
            Texture2D target = GetTargetTexture(layer);
            if (target == null)
            {
                Debug.LogError("PaintOnMap.EraseBrush: Target texture is null.");
                return;
            }
            List<Vector2Int> region = GetCircleRegion(center, eraserSize, target.width, target.height);
            foreach (Vector2Int pos in region)
            {
                if (pos.x >= 0 && pos.x < target.width && pos.y >= 0 && pos.y < target.height)
                    target.SetPixel(pos.x, pos.y, clearColor);
            }
            target.Apply();
            UpdateRawImage(layer);
        }

        /// <summary>
        /// Returns a list of texture coordinates forming a filled circle.
        /// </summary>
        private List<Vector2Int> GetCircleRegion(Vector2Int center, int radius, int maxWidth, int maxHeight)
        {
            List<Vector2Int> region = new List<Vector2Int>();
            int rsq = radius * radius;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy <= rsq)
                    {
                        Vector2Int pos = new Vector2Int(center.x + dx, center.y + dy);
                        if (pos.x >= 0 && pos.x < maxWidth && pos.y >= 0 && pos.y < maxHeight)
                            region.Add(pos);
                    }
                }
            }
            return region;
        }

        /// <summary>
        /// Retrieves the texture for the specified annotation layer.
        /// </summary>
        private Texture2D GetTargetTexture(AnnotationLayer layer)
        {
            if (layer == AnnotationLayer.Traversable)
                return traversableTexture;
            else if (layer == AnnotationLayer.NonTraversable)
                return nonTraversableTexture;
            else
            {
                Debug.LogError("PaintOnMap.GetTargetTexture: Invalid layer specified.");
                return null;
            }
        }

        /// <summary>
        /// Marks the overlay as dirty so that it refreshes its display.
        /// </summary>
        public void UpdateRawImage(AnnotationLayer layer)
        {
            RawImage overlay = (layer == AnnotationLayer.Traversable) ? traversableOverlay : nonTraversableOverlay;
            if (overlay != null)
                overlay.SetAllDirty();
        }

        /// <summary>
        /// Clears the layer by reinitializing its texture to be completely transparent.
        /// </summary>
        public void ClearLayer(AnnotationLayer layer)
        {
            Texture2D target = GetTargetTexture(layer);
            if (target == null)
            {
                Debug.LogWarning("PaintOnMap.ClearLayer: Target texture is null. Reinitializing texture.");
                if (layer == AnnotationLayer.Traversable)
                    InitializeOverlay(ref traversableOverlay, ref traversableTexture, "Traversable");
                else if (layer == AnnotationLayer.NonTraversable)
                    InitializeOverlay(ref nonTraversableOverlay, ref nonTraversableTexture, "Non-Traversable");
                target = GetTargetTexture(layer);
                if (target == null)
                {
                    Debug.LogError("PaintOnMap.ClearLayer: Unable to reinitialize texture.");
                    return;
                }
            }
            Color[] pixels = new Color[target.width * target.height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clearColor;
            target.SetPixels(pixels);
            target.Apply();
            UpdateRawImage(layer);
        }

        /// <summary>
        /// Returns the overlay color for the given layer.
        /// </summary>
        public Color GetOverlayColor(AnnotationLayer layer)
        {
            RawImage overlay = (layer == AnnotationLayer.Traversable) ? traversableOverlay : nonTraversableOverlay;
            if (overlay != null)
                return overlay.color;
            Debug.LogWarning($"PaintOnMap: No overlay for layer {layer}; defaulting to white.");
            return Color.white;
        }
    }
}