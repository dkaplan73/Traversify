// Folder: Traversify/Assets/Scripts/RPGMapTool/UI
// GameObject Parent: DropDowns under RPGMapTool/UI
using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Managers;

namespace RPGMapTool.UI
{
    public class DrawLayerOptions : MonoBehaviour
    {
        [SerializeField] private Dropdown layerDropdown;
        private LayerManager layerManager;

        private void Awake()
        {
            layerManager = FindObjectOfType<LayerManager>();
            if (layerDropdown != null)
            {
                layerDropdown.onValueChanged.AddListener(OnLayerChanged);
            }
        }

        private void OnLayerChanged(int index)
        {
            string layerName = layerDropdown.options[index].text;
            // Toggle layer visibility; for this example, we toggle on.
            layerManager.ToggleLayerVisibility(layerName, true);
        }
    }
}