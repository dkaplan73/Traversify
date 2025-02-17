using System.Collections.Generic;
using UnityEngine;
using RPGMapTool.Core;
using RPGMapTool.Enums;
using RPGMapTool.Managers;

namespace RPGMapTool.Tools
{
    public class PaintBrushTool : MonoBehaviour
    {
        [Header("Brush Settings")]
        [SerializeField] private float brushSize = 10f;
        [SerializeField] private Color brushColor = Color.white;
        [Tooltip("Target layer to paint: Traversable or NonTraversable.")]
        [SerializeField] private AnnotationLayer targetLayer = AnnotationLayer.Traversable;

        /// <summary>
        /// Executes a brush stroke by painting a circular region.
        /// </summary>
        /// <param name="screenPosition">Screen position converted to texture coordinates.</param>
        public void UseBrush(Vector2 screenPosition, PaintManager paintManager)
        {
            PaintOnMap painter = paintManager.GetComponent<PaintOnMap>();
            if (painter == null)
            {
                Debug.LogError("PaintBrushTool: PaintOnMap component not found.");
                return;
            }
            // Convert screen position to texture position (assume PaintManager exposes conversion).
            Vector2Int texPos = paintManager.ScreenPointToTextureCoords(screenPosition);
            painter.BrushPaint(texPos, Mathf.CeilToInt(brushSize), brushColor, targetLayer);
        }
    }
}