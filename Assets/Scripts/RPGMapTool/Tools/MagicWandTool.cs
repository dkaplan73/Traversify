using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RPGMapTool.Core;
using RPGMapTool.Enums;
using RPGMapTool.Managers;

namespace RPGMapTool.Tools
{
    public class MagicWandTool : MonoBehaviour
    {
        [Header("Magic Wand Settings")]
        [SerializeField] private float colorThreshold = 0.1f;
        [SerializeField] private float borderColorThreshold = 0.1f;
        [SerializeField] private Color borderColor = Color.black;
        [SerializeField] private Color fillColor = Color.red;
        [Tooltip("Target annotation layer.")]
        [SerializeField] private AnnotationLayer targetLayer = AnnotationLayer.Traversable;

        [Header("References")]
        [SerializeField] private PaintManager paintManager;
        [SerializeField] private RawImage baseMapImage;

        private void Awake()
        {
            if (paintManager == null)
                paintManager = FindObjectOfType<PaintManager>();
            if (baseMapImage == null && paintManager != null)
                baseMapImage = paintManager.GetBaseMap();
            if (baseMapImage == null)
                Debug.LogError("MagicWandTool: Base map image not assigned or found.");
        }

        public void UseMagicWand(Vector2 screenPosition)
        {
            if (paintManager == null || baseMapImage == null)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Debug.LogWarning("MagicWandTool: UI interaction detected; aborting fill.");
                return;
            }
            RectTransform rt = baseMapImage.rectTransform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, screenPosition))
            {
                Debug.LogWarning("MagicWandTool: Click outside base map bounds.");
                return;
            }
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPosition, null, out localPoint))
            {
                Debug.LogError("MagicWandTool: Could not convert screen point.");
                return;
            }
            Texture2D baseTex = baseMapImage.texture as Texture2D;
            if (baseTex == null)
            {
                Debug.LogError("MagicWandTool: Base texture is not a Texture2D.");
                return;
            }
            // Convert local point to texture coordinates.
            Vector2Int texPos = ScreenPointToTextureCoords(localPoint, rt, baseTex);
            Color targetColor = baseTex.GetPixel(texPos.x, texPos.y);
            Debug.Log($"MagicWandTool: Picked color {targetColor} at texture coordinates {texPos}");

            // Get a blocked mask (implement in your utilities if needed; otherwise assume none).
            bool[,] blocked = new bool[baseTex.width, baseTex.height];
            List<Vector2Int> region = FloodFill(baseTex, texPos.x, texPos.y, targetColor, colorThreshold, blocked);
            if (region.Count < 1)
            {
                Debug.LogWarning("MagicWandTool: No region found.");
                return;
            }
            // Remove border pixels that are similar to the border color.
            region.RemoveAll(p => ColorsSimilar(baseTex.GetPixel(p.x, p.y), borderColor, borderColorThreshold));

            // Fill the region.
            PaintOnMap painter = paintManager.GetComponent<PaintOnMap>();
            if (painter != null)
                painter.ApplyPaintRegion(region, fillColor, targetLayer);
            else
                Debug.LogError("MagicWandTool: PaintOnMap component not found.");
            Debug.Log($"MagicWandTool: Filled region with {region.Count} pixels.");
        }

        /// <summary>
        /// Converts a local point from base map to texture coordinates.
        /// </summary>
        private Vector2Int ScreenPointToTextureCoords(Vector2 localPoint, RectTransform rt, Texture2D tex)
        {
            Rect rect = rt.rect;
            float normalizedX = (localPoint.x + rect.width * 0.5f) / rect.width;
            float normalizedY = (localPoint.y + rect.height * 0.5f) / rect.height;
            int x = Mathf.Clamp((int)(normalizedX * tex.width), 0, tex.width - 1);
            int y = Mathf.Clamp((int)(normalizedY * tex.height), 0, tex.height - 1);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Flood fills the texture starting from (startX, startY), and returns the region of similar colors.
        /// </summary>
        private List<Vector2Int> FloodFill(Texture2D tex, int startX, int startY, Color target, float tolerance, bool[,] blocked)
        {
            int width = tex.width, height = tex.height;
            bool[,] visited = new bool[width, height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            List<Vector2Int> region = new List<Vector2Int>();

            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                region.Add(current);
                foreach (Vector2Int neighbor in GetFourConnected(current, width, height))
                {
                    if (!visited[neighbor.x, neighbor.y] && !blocked[neighbor.x, neighbor.y])
                    {
                        Color sample = tex.GetPixel(neighbor.x, neighbor.y);
                        if (ColorsSimilar(sample, target, tolerance))
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
        /// Returns neighbors using four-connected rule.
        /// </summary>
        private IEnumerable<Vector2Int> GetFourConnected(Vector2Int coord, int width, int height)
        {
            if (coord.x + 1 < width) yield return new Vector2Int(coord.x + 1, coord.y);
            if (coord.x - 1 >= 0) yield return new Vector2Int(coord.x - 1, coord.y);
            if (coord.y + 1 < height) yield return new Vector2Int(coord.x, coord.y + 1);
            if (coord.y - 1 >= 0) yield return new Vector2Int(coord.x, coord.y - 1);
        }

        /// <summary>
        /// Returns true if colors a and b are similar within threshold.
        /// </summary>
        private bool ColorsSimilar(Color a, Color b, float thresh)
        {
            float diff = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
            return diff <= thresh;
        }
    }
}