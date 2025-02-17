using System.Collections.Generic;
using UnityEngine;
using RPGMapTool.Core;
using RPGMapTool.Enums;
using RPGMapTool.Managers;

namespace RPGMapTool.Tools
{
    public class EraserTool : MonoBehaviour
    {
        [Header("Eraser Settings")]
        [SerializeField] private int eraserRadius = 10;
        [Tooltip("Target layer to erase: Traversable or NonTraversable.")]
        [SerializeField] private AnnotationLayer targetLayer = AnnotationLayer.Traversable;

        /// <summary>
        /// Erases (clears) a circular region at the given screen position.
        /// </summary>
        public void UseEraser(Vector2 screenPosition, PaintManager paintManager)
        {
            PaintOnMap painter = paintManager.GetComponent<PaintOnMap>();
            if (painter == null)
            {
                Debug.LogError("EraserTool: PaintOnMap component not found.");
                return;
            }
            Vector2Int texPos = paintManager.ScreenPointToTextureCoords(screenPosition);
            List<Vector2Int> region = GetCircleRegion(texPos, eraserRadius);
            painter.EraseRegion(region, targetLayer);
        }

        private List<Vector2Int> GetCircleRegion(Vector2Int center, int radius)
        {
            List<Vector2Int> region = new List<Vector2Int>();
            int sqrRadius = radius * radius;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= sqrRadius)
                        region.Add(new Vector2Int(center.x + x, center.y + y));
                }
            }
            return region;
        }
    }
}