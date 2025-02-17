// Folder: Traversify/Assets/Scripts/RPGMapTool/Controllers
// GameObject Parent: UIManager (child of RPGMapTool/UI)
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGMapTool.Controllers
{
    public class PanController : MonoBehaviour, IDragHandler
    {
        [SerializeField] private RectTransform content; // The map content to pan
        [SerializeField] private float panSpeed = 1f;

        public void OnDrag(PointerEventData eventData)
        {
            if (content != null)
            {
                content.anchoredPosition += eventData.delta * panSpeed;
            }
        }
    }
}