using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // Add this line
using RPGMapTool.Core;
using RPGMapTool.Enums;
using RPGMapTool.Managers;

namespace RPGMapTool.Tools
{
    public class EraserTool : MonoBehaviour, IPointerClickHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Header("Eraser Settings")]
        [SerializeField] private float eraserSize = 10f;

        [Header("UI References")]
        [SerializeField] private Dropdown areaTypeDropdown;

        private PaintManager paintManager;
        private PaintOnMap paintOnMap;
        private bool isDragging;
        private Vector2 lastErasePosition;
        private const float MIN_ERASE_DISTANCE = 2f;

        private void Awake()
        {
            FindRequiredComponents();
        }

        private void FindRequiredComponents()
        {
            paintManager = FindObjectOfType<PaintManager>();
            if (paintManager == null)
            {
                Debug.LogError("EraserTool: PaintManager not found in scene.");
                return;
            }

            paintOnMap = GetComponentInParent<PaintOnMap>();
            if (paintOnMap == null)
            {
                paintOnMap = FindObjectOfType<PaintOnMap>();
                if (paintOnMap == null)
                {
                    Debug.LogError("EraserTool: PaintOnMap component not found.");
                    return;
                }
            }
            Debug.Log("EraserTool: Required components found successfully.");
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!ValidateBeforeErasing()) return;
            isDragging = true;
            lastErasePosition = eventData.position;
            UseEraser(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || !ValidateBeforeErasing()) return;

            if (Vector2.Distance(lastErasePosition, eventData.position) >= MIN_ERASE_DISTANCE)
            {
                UseEraser(eventData.position);
                lastErasePosition = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isDragging && ValidateBeforeErasing())
            {
                UseEraser(eventData.position);
            }
        }

        private bool ValidateBeforeErasing()
        {
            return paintManager != null && paintOnMap != null && areaTypeDropdown != null;
        }

        private void UseEraser(Vector2 screenPosition)
        {
            if (!ValidateBeforeErasing()) return;

            AnnotationLayer targetLayer = GetTargetLayer();
            Vector2Int texPos = paintManager.ScreenPointToTextureCoords(screenPosition);
            paintOnMap.EraseBrush(texPos, Mathf.CeilToInt(eraserSize), targetLayer);
        }

        private AnnotationLayer GetTargetLayer()
        {
            int selectedIndex = areaTypeDropdown != null ? areaTypeDropdown.value : 1;
            if (selectedIndex == 0)
            {
                Debug.LogWarning("EraserTool: Base layer selected; defaulting to Traversable.");
                selectedIndex = (int)AnnotationLayer.Traversable;
            }
            return (AnnotationLayer)selectedIndex;
        }

        public void SetEraserSize(float size)
        {
            eraserSize = Mathf.Max(1f, size);
        }
    }
}