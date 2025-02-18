using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RPGMapTool.Core;
using RPGMapTool.Enums;
using RPGMapTool.Managers;

namespace RPGMapTool.Tools
{
    [RequireComponent(typeof(RectTransform))]
    public class PaintBrushTool : MonoBehaviour, IPointerClickHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Header("Brush Settings")]
        [SerializeField] private float brushSize = 10f;
        [SerializeField, Range(0.1f, 1f)] private float brushOpacity = 1f;
        [Header("UI References")]
        [SerializeField] private Dropdown areaTypeDropdown;
        [SerializeField] private Image colorBackground;

        private PaintManager paintManager;
        private PaintOnMap paintOnMap;
        private bool isDragging;
        private Vector2 lastPaintPosition;
        private const float MIN_PAINT_DISTANCE = 2f;

        private void Awake()
        {
            paintManager = FindObjectOfType<PaintManager>();
            paintOnMap = FindObjectOfType<PaintOnMap>();
            if (paintManager == null)
                Debug.LogError("PaintBrushTool: PaintManager not found.");
            if (paintOnMap == null)
                Debug.LogError("PaintBrushTool: PaintOnMap not found.");
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
            lastPaintPosition = eventData.position;
            UseBrush(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            if (Vector2.Distance(lastPaintPosition, eventData.position) >= MIN_PAINT_DISTANCE)
            {
                UseBrush(eventData.position);
                lastPaintPosition = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isDragging)
                UseBrush(eventData.position);
        }

        /// <summary>
        /// Uses the PaintManager to convert the screen position into texture coordinates, then calls BrushPaint.
        /// </summary>
        private void UseBrush(Vector2 screenPosition)
        {
            if (paintManager == null || paintOnMap == null) return;
            AnnotationLayer targetLayer = GetTargetLayer();
            Color overlayColor = paintOnMap.GetOverlayColor(targetLayer);
            overlayColor.a *= brushOpacity;

            // Convert screen point into texture coordinates using PaintManager.
            Vector2Int texPos = paintManager.ScreenPointToTextureCoords(screenPosition);
            paintOnMap.BrushPaint(texPos, Mathf.CeilToInt(brushSize), overlayColor, targetLayer);
            Debug.Log($"PaintBrushTool: Painted at {texPos} with brushSize={brushSize} on layer {targetLayer}.");
        }

        /// <summary>
        /// Retrieves the target annotation layer from the areaTypeDropdown.
        /// </summary>
        private AnnotationLayer GetTargetLayer()
        {
            int index = (areaTypeDropdown != null) ? areaTypeDropdown.value : 1;
            if (index == 0)
            {
                Debug.LogWarning("PaintBrushTool: Base layer selected; defaulting to Traversable.");
                index = (int)AnnotationLayer.Traversable;
            }
            return (AnnotationLayer)index;
        }

        public void SetBrushSize(float size)
        {
            brushSize = Mathf.Max(1f, size);
            Debug.Log($"PaintBrushTool: Brush size set to {brushSize}");
        }

        public void SetBrushOpacity(float opacity)
        {
            brushOpacity = Mathf.Clamp01(opacity);
            Debug.Log($"PaintBrushTool: Brush opacity set to {brushOpacity}");
        }

        /// <summary>
        /// Sets the paint color (for UI display) used by the overlay.
        /// </summary>
        public void SetPaintColor(Color color)
        {
            if (colorBackground != null)
            {
                colorBackground.color = color;
                Debug.Log($"PaintBrushTool: Paint color set to {color}");
            }
            else
                Debug.LogError("PaintBrushTool: Color background not assigned.");
        }
    }
}