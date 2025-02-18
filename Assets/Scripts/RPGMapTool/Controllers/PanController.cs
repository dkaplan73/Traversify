using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGMapTool.Controllers
{
    public class PanController : MonoBehaviour, IDragHandler
    {
        [Tooltip("The RawImage displaying the Base Map.")]
        [SerializeField] private RawImage baseMapImage;

        private RectTransform baseMapRect;
        private Vector2 originalPosition;

        private void Awake()
        {
            if (baseMapImage == null)
            {
                Debug.LogError("PanController: Base Map Image not assigned.");
                return;
            }
            baseMapRect = baseMapImage.GetComponent<RectTransform>();
            originalPosition = baseMapRect.anchoredPosition;
            Debug.Log("PanController: Initialized.");
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (baseMapRect != null)
            {
                baseMapRect.anchoredPosition += eventData.delta;
                Debug.Log("PanController: Panning at new anchored position " + baseMapRect.anchoredPosition);
            }
        }
    }
}