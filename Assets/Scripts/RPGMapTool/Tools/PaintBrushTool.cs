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
        
        [Header("Base Map Options")]
        [Tooltip("Parent transform holding BaseMapContainer children. The child at the dropdown index provides the brush color.")]
        [SerializeField] private Transform baseMapContainer;

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
        /// Retrieves the brush color from the selected BaseMapContainer option and uses it for painting.
        /// </summary>
        private void UseBrush(Vector2 screenPosition)
        {
            if (paintManager == null || paintOnMap == null)
                return;

            // Get brush color from the selected option.
            Color optionColor = GetColorFromOption();
            // Update UI color background (if desired).
            if (colorBackground != null)
                colorBackground.color = optionColor;

            // Multiply with brush opacity.
            Color finalColor = optionColor;
            finalColor.a *= brushOpacity;

            // Convert screen point into texture coordinate.
            Vector2Int texPos = paintManager.ScreenPointToTextureCoords(screenPosition);
            paintOnMap.BrushPaint(texPos, Mathf.CeilToInt(brushSize), finalColor, GetTargetLayer());
            Debug.Log($"PaintBrushTool: Painted at {texPos} with brushSize={brushSize} and color {finalColor}.");
        }

        /// <summary>
        /// Returns the option color based on the dropdown selection from the BaseMapContainer child.
        /// </summary>
        private Color GetColorFromOption()
        {
            if (baseMapContainer == null)
            {
                Debug.LogWarning("PaintBrushTool: BaseMapContainer reference is missing.");
                return Color.white;
            }

            int index = (areaTypeDropdown != null) ? areaTypeDropdown.value : 0;
            if (index < 0 || index >= baseMapContainer.childCount)
            {
                Debug.LogWarning("PaintBrushTool: Dropdown index out of range. Defaulting to white.");
                return Color.white;
            }
            Image optionImage = baseMapContainer.GetChild(index).GetComponent<Image>();
            if (optionImage == null)
            {
                Debug.LogWarning("PaintBrushTool: Option child missing an Image component. Defaulting to white.");
                return Color.white;
            }
            return optionImage.color;
        }

        /// <summary>
        /// Returns the annotation layer based on the dropdown value.
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
    }
}