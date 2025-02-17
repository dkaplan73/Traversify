// File: Assets/Scripts/RPGMapTool/Tools/MagicWandTool.cs
// GameObject Parent: PaintManagerObject (under RPGMapTool)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RPGMapTool.Core;
using RPGMapTool.Utilities;
using RPGMapTool.Enums;
using RPGMapTool.Managers;

namespace RPGMapTool.Managers
{
    public class MagicWandTool : MonoBehaviour
    {
        [SerializeField] private GameObject baseMap;
        [SerializeField] private float colorThreshold = 0.1f;
        [SerializeField] private int minRegionSize = 10;
        [SerializeField] private Color fillColor = Color.red;
        [Tooltip("Target layer to paint: Traversable or NonTraversable.")]
        [SerializeField] private AnnotationLayer targetLayer = AnnotationLayer.Traversable;

        // New parameters for border tolerance.
        [SerializeField] private float borderThreshold = 0.1f;
        [SerializeField] private Color borderColor = Color.black;

        /// <summary>
        /// Executes the magic wand flood fill from the provided screen position.
        /// Picks the color beneath the click from the base map and paints the region
        /// while respecting the pixel tolerance and border color settings.
        /// </summary>
        public void ExecuteMagicWand(Vector2 screenPos, PaintManager paintManager)
        {
            // Ensure base map reference is valid.
            if (baseMap == null)
            {
                RawImage bm = paintManager.GetBaseMap();
                if (bm != null)
                    baseMap = bm.gameObject;
                else
                {
                    Debug.LogError("MagicWandTool: Base map not found in PaintManager.");
                    return;
                }
            }

            // Delay fill if UI interaction is active.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Debug.LogWarning("MagicWandTool: UI interaction detected; delaying fill.");
                StartCoroutine(RetryMagicWand(screenPos, paintManager));
                return;
            }

            if (!ValidateBaseMap())
                return;

            RawImage baseRaw = baseMap.GetComponent<RawImage>();
            RectTransform rt = baseRaw.rectTransform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos))
            {
                Debug.LogWarning("MagicWandTool: Click outside base map bounds.");
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out Vector2 localPoint))
            {
                Debug.LogError("MagicWandTool: Could not convert screen point.");
                return;
            }

            Texture2D baseTex = baseRaw.texture as Texture2D;
            if (baseTex == null)
            {
                Debug.LogError("MagicWandTool: Base texture is not a Texture2D.");
                return;
            }

            // Pick the target color from the pixel beneath the click.
            Vector2Int texPos = ScreenPointToTextureCoords(localPoint, rt, baseTex);
            Color targetColor = baseTex.GetPixel(texPos.x, texPos.y);

            // Perform flood fill while avoiding pixels that are similar to the defined border color.
            bool[,] blocked = TextureUtils.GetBlockedMask(baseTex);
            List<Vector2Int> region = FloodFill(baseTex, texPos.x, texPos.y, targetColor, colorThreshold, blocked);
            if (region.Count < minRegionSize)
            {
                Debug.LogWarning("MagicWandTool: Region too small. Aborting fill.");
                return;
            }

            // Paint on the correct layer using the appropriate fill color.
            PaintOnMap painter = paintManager.GetComponent<PaintOnMap>();
            if (painter != null)
                painter.ApplyPaintRegion(region, fillColor, targetLayer);
            else
                Debug.LogError("MagicWandTool: PaintOnMap component not found.");

            Debug.Log($"MagicWandTool: Filled region with {region.Count} pixels.");
        }

        private IEnumerator RetryMagicWand(Vector2 screenPos, PaintManager paintManager)
        {
            float timer = 0f;
            while (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() && timer < 5f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Debug.LogWarning("MagicWandTool: UI still active; fill aborted.");
                yield break;
            }
            ExecuteMagicWand(screenPos, paintManager);
        }

        /// <summary>
        /// Performs a flood fill on the texture starting at (startX, startY). 
        /// It adds pixels similar to the target color within colorThreshold and that are not similar
        /// to the borderColor (using borderThreshold), thereby preventing fill from crossing defined borders.
        /// </summary>
        private List<Vector2Int> FloodFill(Texture2D tex, int startX, int startY, Color target, float tolerance, bool[,] blocked)
        {
            int width = tex.width;
            int height = tex.height;
            bool[,] visited = new bool[width, height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            List<Vector2Int> region = new List<Vector2Int>();

            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                region.Add(current);

                foreach (var neighbor in GetFourConnected(current, width, height))
                {
                    if (!visited[neighbor.x, neighbor.y] && !blocked[neighbor.x, neighbor.y])
                    {
                        Color sample = tex.GetPixel(neighbor.x, neighbor.y);
                        // Only add pixel if it matches the target color within tolerance and is not similar to the border color.
                        if (ColorsSimilar(sample, target, tolerance) && !ColorsSimilar(sample, borderColor, borderThreshold))
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
        /// Returns 4-connected neighbors of a given pixel coordinate.
        /// </summary>
        private IEnumerable<Vector2Int> GetFourConnected(Vector2Int coord, int width, int height)
        {
            if (coord.x + 1 < width) yield return new Vector2Int(coord.x + 1, coord.y);
            if (coord.x - 1 >= 0) yield return new Vector2Int(coord.x - 1, coord.y);
            if (coord.y + 1 < height) yield return new Vector2Int(coord.x, coord.y + 1);
            if (coord.y - 1 >= 0) yield return new Vector2Int(coord.x, coord.y - 1);
        }

        /// <summary>
        /// Converts a local point (from a RectTransform) to texture coordinates.
        /// </summary>
        private Vector2Int ScreenPointToTextureCoords(Vector2 localPoint, RectTransform rt, Texture2D tex)
        {
            Vector2 size = rt.rect.size;
            int x = Mathf.Clamp((int)(((localPoint.x + size.x * 0.5f) / size.x) * tex.width), 0, tex.width - 1);
            int y = Mathf.Clamp((int)(((localPoint.y + size.y * 0.5f) / size.y) * tex.height), 0, tex.height - 1);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Determines if two colors are similar within a specified threshold.
        /// </summary>
        private bool ColorsSimilar(Color a, Color b, float thresh)
        {
            float diff = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
            return diff <= thresh;
        }

        /// <summary>
        /// Validates that the base map is correctly assigned.
        /// </summary>
        private bool ValidateBaseMap()
        {
            if (baseMap == null)
            {
                Debug.LogError("MagicWandTool: Base map is null.");
                return false;
            }
            return true;
        }

        // Public setters to allow updates to thresholds and border settings from other tools or the UI.
        public void SetThreshold(float newThreshold)
        {
            colorThreshold = newThreshold;
            Debug.Log($"MagicWandTool: Color threshold set to {newThreshold}");
        }

        public void SetBorderColor(Color newColor)
        {
            borderColor = newColor;
            Debug.Log($"MagicWandTool: Border color set to {newColor}");
        }

        public void SetBorderThreshold(float newThreshold)
        {
            borderThreshold = newThreshold;
            Debug.Log($"MagicWandTool: Border threshold set to {newThreshold}");
        }
    }
}